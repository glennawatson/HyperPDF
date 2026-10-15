// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdf.Speech.Melo;

/// <summary>
/// MeloTTS's English front end, ported from its Python: the text is cut into BERT word pieces, the pieces are grouped
/// into words, each word becomes ARPAbet phones from the CMU dictionary or the spelling-to-sound network, and the
/// phones are shared out over the word's pieces so each BERT feature lines up with the phones it describes.
/// Reuses its buffers between calls, so it is not thread safe; <see cref="MeloEngine"/> holds a lock.
/// </summary>
[DebuggerDisplay("MeloFrontEnd: {_lexicon.Count} words")]
internal sealed class MeloFrontEnd
{
    /// <summary>The longest word handled; longer words are cut short.</summary>
    private const int MaxWordLength = 128;

    /// <summary>The slots a phone takes with its blank.</summary>
    private const int PhoneWithBlank = 2;

    /// <summary>The slots the start token covers: the leading blank, the padding phone and its blank.</summary>
    private const int FirstTokenPhones = 3;

    /// <summary>The marker that starts a continuing word piece.</summary>
    private const char Continuation = '#';

    /// <summary>The characters g2p_en reads; it drops the rest.</summary>
    private static readonly SearchValues<char> Readable = SearchValues.Create("abcdefghijklmnopqrstuvwxyz'.,?!-");

    /// <summary>The symbols.</summary>
    private readonly MeloSymbols _symbols;

    /// <summary>The CMU dictionary.</summary>
    private readonly MeloLexicon _lexicon;

    /// <summary>The spelling-to-sound network.</summary>
    private readonly SpellingToSound _spelling;

    /// <summary>The word piece tokenizer.</summary>
    private readonly WordPieceTokenizer _tokenizer;

    /// <summary>The word piece ids, reused.</summary>
    private readonly List<int> _pieces = [];

    /// <summary>The packed phones of the text, reused.</summary>
    private readonly List<int> _phones = [];

    /// <summary>The phones per piece, reused.</summary>
    private readonly List<int> _perPiece = [];

    /// <summary>The predicted phones of one word, reused.</summary>
    private readonly List<string> _predicted = [];

    /// <summary>Initializes a new instance of the <see cref="MeloFrontEnd"/> class.</summary>
    /// <param name="symbols">The symbols.</param>
    /// <param name="lexicon">The CMU dictionary.</param>
    /// <param name="spelling">The spelling-to-sound network.</param>
    /// <param name="tokenizer">The word piece tokenizer.</param>
    internal MeloFrontEnd(MeloSymbols symbols, MeloLexicon lexicon, SpellingToSound spelling, WordPieceTokenizer tokenizer)
    {
        _symbols = symbols;
        _lexicon = lexicon;
        _spelling = spelling;
        _tokenizer = tokenizer;
    }

    /// <summary>Gets the symbols.</summary>
    internal MeloSymbols Symbols => _symbols;

    /// <summary>Gets the words read with the spelling-to-sound network so far, for diagnostics.</summary>
    internal int GuessedWords { get; private set; }

    /// <summary>Loads the front end's files from the voice folder.</summary>
    /// <param name="directory">The voice folder.</param>
    /// <returns>The front end.</returns>
    internal static MeloFrontEnd Load(string directory)
    {
        var symbols = MeloSymbols.Load(File.ReadAllBytes(Path.Combine(directory, MeloModel.SymbolsFile)));
        var lexicon = MeloLexicon.Load(File.ReadAllText(Path.Combine(directory, MeloModel.LexiconFile)), symbols);
        var spelling = new SpellingToSound(File.ReadAllBytes(Path.Combine(directory, MeloModel.SpellingFile)));
        var tokenizer = new WordPieceTokenizer(File.ReadAllText(Path.Combine(directory, MeloModel.VocabularyFile)));
        return new(symbols, lexicon, spelling, tokenizer);
    }

    /// <summary>Prepares normalized, lower-case text for the model.</summary>
    /// <param name="text">The text.</param>
    /// <param name="input">Receives the model's input; cleared first.</param>
    internal void Prepare(ReadOnlySpan<char> text, MeloInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        input.Clear();
        _phones.Clear();
        _perPiece.Clear();
        _tokenizer.Tokenize(text, _pieces);
        Span<char> word = stackalloc char[MaxWordLength];
        var start = 0;
        while (start < _pieces.Count)
        {
            // A word is a piece and the continuing pieces after it.
            var end = WordEnd(start);
            var before = _phones.Count;
            AddWord(word[..ReadWord(start, end, word)]);
            Share(_phones.Count - before, end - start);
            start = end;
        }

        Fill(input);
    }

    /// <summary>Adds a phone and the blank after it.</summary>
    /// <param name="input">The input.</param>
    /// <param name="phone">The packed phone.</param>
    private static void AddPhone(MeloInput input, int phone)
    {
        input.Phones.Add(MeloSymbols.IdOf(phone));
        input.Tones.Add(MeloSymbols.ToneOf(phone) + MeloSymbols.EnglishToneStart);
        input.Languages.Add(MeloSymbols.English);
        input.Phones.Add(MeloSymbols.Blank);
        input.Tones.Add(0);
        input.Languages.Add(0);
    }

    /// <summary>Keeps the characters g2p_en reads: lower-case letters and . , ? ! ' -.</summary>
    /// <param name="word">The word.</param>
    /// <param name="kept">Receives the kept characters.</param>
    /// <param name="letters">Whether any letter was kept.</param>
    /// <returns>How many were kept.</returns>
    private static int Keep(ReadOnlySpan<char> word, Span<char> kept, out bool letters)
    {
        var length = 0;
        letters = false;
        foreach (var c in word)
        {
            if (!Readable.Contains(c) || length >= kept.Length)
            {
                continue;
            }

            kept[length] = c;
            length++;
            letters |= char.IsAsciiLetterLower(c);
        }

        return length;
    }

    /// <summary>Finds where the word starting at a piece ends: after its continuing pieces.</summary>
    /// <param name="start">The word's first piece.</param>
    /// <returns>The index after its last piece.</returns>
    private int WordEnd(int start)
    {
        var end = start + 1;
        while (end < _pieces.Count && _tokenizer.Piece(_pieces[end]).StartsWith(Continuation))
        {
            end++;
        }

        return end;
    }

    /// <summary>Joins a word's pieces without their continuation marks, as MeloTTS does.</summary>
    /// <param name="start">The first piece.</param>
    /// <param name="end">The index after the last piece.</param>
    /// <param name="word">Receives the word.</param>
    /// <returns>The word's length.</returns>
    private int ReadWord(int start, int end, Span<char> word)
    {
        var length = 0;
        for (var i = start; i < end; i++)
        {
            foreach (var c in _tokenizer.Piece(_pieces[i]))
            {
                if (c == Continuation || length >= word.Length)
                {
                    continue;
                }

                word[length] = c;
                length++;
            }
        }

        return length;
    }

    /// <summary>Adds phones from the dictionary.</summary>
    /// <param name="phones">The packed phones.</param>
    private void AddPhones(ReadOnlySpan<int> phones)
    {
        foreach (var phone in phones)
        {
            _phones.Add(phone);
        }
    }

    /// <summary>Adds a word's phones: from the dictionary, or as MeloTTS's g2p_en fallback reads it.</summary>
    /// <param name="word">The word.</param>
    private void AddWord(ReadOnlySpan<char> word)
    {
        if (_lexicon.TryGet(word, out var known))
        {
            AddPhones(known);
            return;
        }

        // g2p_en keeps only letters, spaces and . , ? ! ' -, then reads punctuation as itself.
        Span<char> kept = stackalloc char[MaxWordLength];
        var spelling = kept[..Keep(word, kept, out var letters)];
        if (spelling.IsEmpty)
        {
            return;
        }

        if (!letters)
        {
            _phones.Add(MeloSymbols.Pack(_symbols.Symbol(spelling), 0));
        }
        else if (_lexicon.TryGet(spelling, out known))
        {
            AddPhones(known);
        }
        else
        {
            Guess(spelling);
        }
    }

    /// <summary>Adds the phones the spelling-to-sound network predicts for a word the dictionary lacks.</summary>
    /// <param name="spelling">The word.</param>
    private void Guess(ReadOnlySpan<char> spelling)
    {
        GuessedWords++;
        _predicted.Clear();
        _spelling.Predict(spelling, _predicted);
        foreach (var phone in _predicted)
        {
            _phones.Add(_symbols.FromPrediction(phone));
        }
    }

    /// <summary>Shares a word's phones over its pieces as evenly as possible, earlier pieces taking any extra.</summary>
    /// <param name="phones">The word's phone count.</param>
    /// <param name="pieces">The word's piece count.</param>
    private void Share(int phones, int pieces)
    {
        var each = phones / pieces;
        var extra = phones % pieces;
        for (var i = 0; i < pieces; i++)
        {
            _perPiece.Add(each + (i < extra ? 1 : 0));
        }
    }

    /// <summary>Writes the model's input: padding, blanks between phones, English tones and language, and the token alignment.</summary>
    /// <param name="input">The input.</param>
    private void Fill(MeloInput input)
    {
        input.Phones.Add(MeloSymbols.Blank);
        input.Tones.Add(0);
        input.Languages.Add(0);
        AddPhone(input, MeloSymbols.Pack((int)MeloSymbols.Blank, 0));
        foreach (var phone in CollectionsMarshal.AsSpan(_phones))
        {
            AddPhone(input, phone);
        }

        AddPhone(input, MeloSymbols.Pack((int)MeloSymbols.Blank, 0));

        // Each piece covers its phones and their blanks; the first also covers the leading blank.
        input.Tokens.Add(WordPieceTokenizer.Start);
        input.PhonesPerToken.Add(FirstTokenPhones);
        foreach (var piece in CollectionsMarshal.AsSpan(_pieces))
        {
            input.Tokens.Add(piece);
        }

        foreach (var count in CollectionsMarshal.AsSpan(_perPiece))
        {
            input.PhonesPerToken.Add(count * PhoneWithBlank);
        }

        input.Tokens.Add(WordPieceTokenizer.End);
        input.PhonesPerToken.Add(PhoneWithBlank);
    }
}
