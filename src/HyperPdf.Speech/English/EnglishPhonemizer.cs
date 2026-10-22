// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Speech.English;

/// <summary>
/// Turns English text into the phonemes Kokoro reads, following misaki (Apache-2.0): words come from its pronunciation
/// lexicon, with its rules for -s, -ed and -ing endings and for "the", "a" and "to" before vowels; numbers are said as
/// words; compound words are split into known halves; acronyms and unknown short words are spelled out.
/// Words are looked up straight from the text, so a listed word allocates nothing. Reuses its buffers between
/// sentences, so it is not thread safe; <see cref="Kokoro.KokoroEngine"/> holds a lock.
/// </summary>
[DebuggerDisplay("EnglishPhonemizer: {_lexicon.Count} words, British={_british}")]
internal sealed class EnglishPhonemizer
{
    /// <summary>The primary stress mark.</summary>
    private const char Primary = 'ˈ';

    /// <summary>The secondary stress mark.</summary>
    private const char Secondary = 'ˌ';

    /// <summary>The primary stress mark as text.</summary>
    private const string PrimaryText = "ˈ";

    /// <summary>The secondary stress mark as text.</summary>
    private const string SecondaryText = "ˌ";

    /// <summary>Roughly how many phoneme characters each letter becomes, to size the output.</summary>
    private const int PhonemesPerLetter = 2;

    /// <summary>How many times larger the output grows when full.</summary>
    private const int OutputGrowth = 2;

    /// <summary>The shortest half of a compound word.</summary>
    private const int MinCompoundPart = 3;

    /// <summary>The longest unknown word spelled out letter by letter.</summary>
    private const int MaxSpelled = 6;

    /// <summary>The shortest word given an -s, -ed or -ing ending.</summary>
    private const int MinStemmed = 3;

    /// <summary>The length of the -ing ending.</summary>
    private const int IngLength = 3;

    /// <summary>The length of the -es and -ed endings.</summary>
    private const int TwoLetterEnding = 2;

    /// <summary>The shortest word stemmed by removing -ies or -ing.</summary>
    private const int MinLongStem = 5;

    /// <summary>The longest word handled on the stack; a longer word gets an array.</summary>
    private const int StackWord = 128;

    /// <summary>The characters a spelled out word builds on the stack before it spills to the pool.</summary>
    private const int StackSpelled = 64;

    /// <summary>Sounds before which American English flaps a t, as in "writing".</summary>
    private static readonly SearchValues<char> Taus = SearchValues.Create("AIOWYiuæɑəɛɪɹʊʌ");

    /// <summary>Sounds after which -s is said as s.</summary>
    private static readonly SearchValues<char> VoicelessEndings = SearchValues.Create("ptkfθ");

    /// <summary>Sounds after which -s is said as iz.</summary>
    private static readonly SearchValues<char> Sibilants = SearchValues.Create("szʃʒʧʤ");

    /// <summary>Sounds after which -ed is said as t.</summary>
    private static readonly SearchValues<char> VoicelessPast = SearchValues.Create("pkfθʃsʧ");

    /// <summary>Symbols said as words.</summary>
    private static readonly Dictionary<char, string> Symbols =
        new() { ['%'] = "percent", ['&'] = "and", ['+'] = "plus", ['@'] = "at", ['='] = "equals" };

    /// <summary>Punctuation the voice uses for pauses and tone, as strings so tokens share them.</summary>
    private static readonly Dictionary<char, string> PauseMarks = BuildPauseMarks();

    /// <summary>The pronunciations.</summary>
    private readonly PronunciationLexicon _lexicon;

    /// <summary>The tokens of the text being read, reused between sentences.</summary>
    private readonly List<Token> _tokens = [];

    /// <summary>Whether to use British endings.</summary>
    private readonly bool _british;

    /// <summary>The phonemes being built, reused between sentences and grown when a sentence needs more.</summary>
    private char[] _output = [];

    /// <summary>The number of phoneme characters in <see cref="_output"/>.</summary>
    private int _outputLength;

    /// <summary>Initializes a new instance of the <see cref="EnglishPhonemizer"/> class.</summary>
    /// <param name="lexicon">The pronunciations.</param>
    /// <param name="british">Whether to use British endings.</param>
    internal EnglishPhonemizer(PronunciationLexicon lexicon, bool british)
    {
        _lexicon = lexicon;
        _british = british;
    }

    /// <summary>
    /// Gets or sets a value indicating whether words the lexicon does not know, and that are spelled out letter by
    /// letter instead, are added to <see cref="GuessedWords"/>, for the listening tests' pronunciation coverage.
    /// </summary>
    internal bool RecordsGuesses { get; set; }

    /// <summary>Gets the words spelled out letter by letter while <see cref="RecordsGuesses"/> was on.</summary>
    internal List<string> GuessedWords { get; } = [];

    /// <summary>Gets the unstressed vowel of endings like -es and -ed: ɪ in British English, ᵻ in American.</summary>
    private char UnstressedI => _british ? 'ɪ' : 'ᵻ';

    /// <summary>Turns text into phonemes.</summary>
    /// <param name="text">The text, normally one sentence.</param>
    /// <returns>The phonemes, words separated by spaces, with pause punctuation kept.</returns>
    internal string Phonemize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new(Phonemize(text.AsSpan()));
    }

    /// <summary>Turns text into phonemes, without making a string.</summary>
    /// <param name="text">The text, normally one sentence.</param>
    /// <returns>The phonemes, words separated by spaces, with pause punctuation kept; valid until the next call.</returns>
    internal ReadOnlySpan<char> Phonemize(ReadOnlySpan<char> text)
    {
        // A curly apostrophe is read as a straight one everywhere: inside a word, and as the quote mark it makes alone.
        if (!text.Contains('’'))
        {
            return PhonemizeStraight(text);
        }

        var straight = ArrayPool<char>.Shared.Rent(text.Length);
        try
        {
            text.Replace(straight, '’', '\'');
            return PhonemizeStraight(straight.AsSpan(0, text.Length));
        }
        finally
        {
            ArrayPool<char>.Shared.Return(straight);
        }
    }

    /// <summary>Gets the phonemes of one word.</summary>
    /// <param name="word">The word.</param>
    /// <param name="next">The next word, for "the" and "a" before vowels.</param>
    /// <param name="hasNext">Whether a word follows.</param>
    /// <returns>The phonemes, or an empty string.</returns>
    internal string Word(ReadOnlySpan<char> word, ReadOnlySpan<char> next, bool hasNext)
    {
        if (SpecialCase(word, next, hasNext) is { } special)
        {
            return special;
        }

        if (Lookup(word) is { } phonemes)
        {
            return phonemes;
        }

        return (IsAcronym(word) ? Spell(word) : null) ?? Unlisted(word);
    }

    /// <summary>Turns primary stress into secondary, for the second part of a compound or a spelled letter.</summary>
    /// <param name="phonemes">The phonemes.</param>
    /// <returns>The phonemes with only secondary stress.</returns>
    private static string Demote(string phonemes) =>
        phonemes.Contains(Primary, StringComparison.Ordinal) ? phonemes.Replace(SecondaryText, string.Empty, StringComparison.Ordinal).Replace(Primary, Secondary) : phonemes;

    /// <summary>Determines whether a word is an acronym to spell out, such as "PDF".</summary>
    /// <param name="word">The word.</param>
    /// <returns><see langword="true"/> for short all-capital words.</returns>
    private static bool IsAcronym(ReadOnlySpan<char> word) => word.Length is > 1 and < MaxSpelled && word.IndexOfAnyExceptInRange('A', 'Z') < 0;

    /// <summary>Determines whether the next word starts with a vowel sound, judged by its first letter.</summary>
    /// <param name="next">The next word.</param>
    /// <returns><see langword="true"/> for a vowel.</returns>
    private static bool StartsWithVowel(ReadOnlySpan<char> next) => !next.IsEmpty && "aeiouAEIOU".Contains(next[0], StringComparison.Ordinal);

    /// <summary>Determines whether a word follows a token, with no pause between.</summary>
    /// <param name="tokens">The tokens.</param>
    /// <param name="index">The current token.</param>
    /// <returns><see langword="true"/> when the next token is a word.</returns>
    private static bool HasNextWord(List<Token> tokens, int index) => index + 1 < tokens.Count && !tokens[index + 1].IsPause;

    /// <summary>Gets a token's characters, from the text it was read from or from the text it carries.</summary>
    /// <param name="token">The token.</param>
    /// <param name="text">The text the tokens were read from.</param>
    /// <returns>The characters.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<char> TextOf(in Token token, ReadOnlySpan<char> text) =>
        token.Text is null ? text.Slice(token.Start, token.Length) : token.Text.AsSpan(token.Start, token.Length);

    /// <summary>Splits text into words and pause marks, saying numbers and symbols as words.</summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">Receives the tokens.</param>
    private static void Tokenize(ReadOnlySpan<char> text, List<Token> tokens)
    {
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsLetter(c))
            {
                var end = WordEnd(text, i);
                tokens.Add(new(null, i, end - i, false));
                i = end;
            }
            else if (char.IsAsciiDigit(c))
            {
                var end = NumberEnd(text, i);
                var words = new TokenWords(tokens);
                NumberWords.Say(text.Slice(i, end - i), ref words);
                i = end;
            }
            else
            {
                AddMark(tokens, text, ref i);
            }
        }
    }

    /// <summary>Adds a pause mark or a symbol's word, skipping anything else.</summary>
    /// <param name="tokens">The tokens.</param>
    /// <param name="text">The text.</param>
    /// <param name="index">The character; moved past what was read.</param>
    private static void AddMark(List<Token> tokens, ReadOnlySpan<char> text, ref int index)
    {
        if (text[index..].StartsWith("..."))
        {
            tokens.Add(new("…", 0, 1, true));
            index += "...".Length;
            return;
        }

        var c = text[index];
        index++;
        if (Symbols.TryGetValue(c, out var word))
        {
            tokens.Add(new(word, 0, word.Length, false));
            return;
        }

        if (MarkFor(c, text, index) is { } mark)
        {
            tokens.Add(new(mark, 0, mark.Length, true));
        }
    }

    /// <summary>Gets the pause mark a character stands for.</summary>
    /// <param name="c">The character.</param>
    /// <param name="text">The text.</param>
    /// <param name="next">The index after the character.</param>
    /// <returns>The mark, or <see langword="null"/> for a character the voice ignores.</returns>
    private static string? MarkFor(char c, ReadOnlySpan<char> text, int next)
    {
        if (c is '–' or '—' || IsSpacedHyphen(c, text, next))
        {
            return "—";
        }

        return c is '\'' or '‘' or '’' ? "\"" : PauseMarks.GetValueOrDefault(c);
    }

    /// <summary>Builds the pause marks as strings.</summary>
    /// <returns>The marks.</returns>
    private static Dictionary<char, string> BuildPauseMarks()
    {
        var marks = new Dictionary<char, string>();
        foreach (var c in ";:,.!?—…\"()“”")
        {
            marks[c] = c.ToString();
        }

        return marks;
    }

    /// <summary>Determines whether a hyphen stands alone as a dash.</summary>
    /// <param name="c">The character.</param>
    /// <param name="text">The text.</param>
    /// <param name="next">The index after it.</param>
    /// <returns><see langword="true"/> for a hyphen followed by a space.</returns>
    private static bool IsSpacedHyphen(char c, ReadOnlySpan<char> text, int next) => c == '-' && next < text.Length && char.IsWhiteSpace(text[next]);

    /// <summary>Finds the end of a word: letters, with apostrophes inside it.</summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The first letter.</param>
    /// <returns>The index after the word.</returns>
    private static int WordEnd(ReadOnlySpan<char> text, int start)
    {
        var end = start;
        while (end < text.Length && (char.IsLetter(text[end]) || (text[end] is '\'' or '’' && end + 1 < text.Length && char.IsLetter(text[end + 1]))))
        {
            end++;
        }

        return end;
    }

    /// <summary>Finds the end of a number: digits, with commas and a decimal point between digits.</summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The first digit.</param>
    /// <returns>The index after the number.</returns>
    private static int NumberEnd(ReadOnlySpan<char> text, int start)
    {
        var end = start;
        while (end < text.Length && (char.IsAsciiDigit(text[end]) || (text[end] is ',' or '.' && end + 1 < text.Length && char.IsAsciiDigit(text[end + 1]))))
        {
            end++;
        }

        return end;
    }

    /// <summary>Says the small words whose sound depends on what follows.</summary>
    /// <param name="word">The word.</param>
    /// <param name="next">The next word.</param>
    /// <param name="hasNext">Whether a word follows.</param>
    /// <returns>The phonemes, or <see langword="null"/> when the word is not special.</returns>
    private static string? SpecialCase(ReadOnlySpan<char> word, ReadOnlySpan<char> next, bool hasNext) => word switch
    {
        "a" or "A" when hasNext => "ɐ",
        "an" or "An" => "ɐn",
        "I" => "ˌI",
        "the" or "The" => StartsWithVowel(next) ? "ði" : "ðə",
        "to" or "To" when hasNext => StartsWithVowel(next) ? "tʊ" : "tə",
        "in" or "In" => hasNext ? "ɪn" : "ˈɪn",
        _ => null,
    };

    /// <summary>Turns tokens already read from a text into phonemes.</summary>
    /// <param name="text">The text, with straight apostrophes.</param>
    /// <returns>The phonemes.</returns>
    private ReadOnlySpan<char> PhonemizeStraight(ReadOnlySpan<char> text)
    {
        var tokens = _tokens;
        tokens.Clear();
        Tokenize(text, tokens);
        _outputLength = 0;
        EnsureOutput(text.Length * PhonemesPerLetter);
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].IsPause)
            {
                AppendPause(tokens[i].Text![0]);
                continue;
            }

            var hasNext = HasNextWord(tokens, i);
            var next = hasNext ? TextOf(tokens[i + 1], text) : default;
            AppendWord(Word(TextOf(tokens[i], text), next, hasNext));
        }

        while (_outputLength > 0 && _output[_outputLength - 1] == ' ')
        {
            _outputLength--;
        }

        return _output.AsSpan(0, _outputLength);
    }

    /// <summary>Adds a word's phonemes to the output, after a space unless it opens the output or follows an opening mark.</summary>
    /// <param name="phonemes">The phonemes; nothing is added when empty.</param>
    private void AppendWord(string phonemes)
    {
        if (phonemes.Length == 0)
        {
            return;
        }

        if (_outputLength > 0 && _output[_outputLength - 1] is not (' ' or '(' or '“'))
        {
            Append(' ');
        }

        phonemes.CopyTo(Room(phonemes.Length));
        _outputLength += phonemes.Length;
    }

    /// <summary>Makes sure the output holds at least a number of characters, keeping what is in it.</summary>
    /// <param name="capacity">The characters needed.</param>
    private void EnsureOutput(int capacity)
    {
        if (_output.Length >= capacity)
        {
            return;
        }

        var bigger = new char[Math.Max(capacity, _output.Length * OutputGrowth)];
        _output.AsSpan(0, _outputLength).CopyTo(bigger);
        _output = bigger;
    }

    /// <summary>Gets room in the output to write characters at its end.</summary>
    /// <param name="length">The characters to be written.</param>
    /// <returns>The room.</returns>
    private Span<char> Room(int length)
    {
        EnsureOutput(_outputLength + length);
        return _output.AsSpan(_outputLength);
    }

    /// <summary>Adds a character to the output.</summary>
    /// <param name="c">The character.</param>
    private void Append(char c)
    {
        EnsureOutput(_outputLength + 1);
        _output[_outputLength] = c;
        _outputLength++;
    }

    /// <summary>Adds pause punctuation: opening marks get a space before, closing ones attach to the word.</summary>
    /// <param name="mark">The mark.</param>
    private void AppendPause(char mark)
    {
        if (mark is '(' or '“' && _outputLength > 0 && _output[_outputLength - 1] != ' ')
        {
            Append(' ');
        }

        Append(mark);
    }

    /// <summary>Says a word not listed as written: in lower case, with an ending, as a compound, or spelled out.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The phonemes, or an empty string.</returns>
    private string Unlisted(ReadOnlySpan<char> word)
    {
        Span<char> lower = word.Length <= StackWord ? stackalloc char[StackWord] : new char[word.Length];
        lower = lower[..word.ToLowerInvariant(lower)];
        if ((Lookup(lower) ?? Stemmed(lower) ?? Compound(lower)) is { } known)
        {
            return known;
        }

        if (RecordsGuesses)
        {
            GuessedWords.Add(word.ToString());
        }

        return Spell(word) ?? string.Empty;
    }

    /// <summary>Looks a word up as it is.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The phonemes, or <see langword="null"/>.</returns>
    private string? Lookup(ReadOnlySpan<char> word) => _lexicon.TryGet(word, out var phonemes) ? phonemes : null;

    /// <summary>Looks up a word made of a stem and one more letter.</summary>
    /// <param name="stem">The stem.</param>
    /// <param name="letter">The letter added.</param>
    /// <returns>The phonemes, or <see langword="null"/>.</returns>
    private string? LookupWith(ReadOnlySpan<char> stem, char letter)
    {
        Span<char> word = stem.Length < StackWord ? stackalloc char[StackWord] : new char[stem.Length + 1];
        stem.CopyTo(word);
        word[stem.Length] = letter;
        return Lookup(word[..(stem.Length + 1)]);
    }

    /// <summary>Says a word as a known stem with an -s, -ed or -ing ending.</summary>
    /// <param name="word">The lower case word.</param>
    /// <returns>The phonemes, or <see langword="null"/>.</returns>
    private string? Stemmed(ReadOnlySpan<char> word) => word.Length < MinStemmed ? null : StemS(word) ?? StemEd(word) ?? StemIng(word);

    /// <summary>Says a plural or possessive.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The phonemes, or <see langword="null"/>.</returns>
    private string? StemS(ReadOnlySpan<char> word) => word.EndsWith('s') && PluralStem(word) is { } stem ? AddS(stem) : null;

    /// <summary>Finds the known stem of a plural or possessive.</summary>
    /// <param name="word">The word ending in s.</param>
    /// <returns>The stem's phonemes, or <see langword="null"/>.</returns>
    private string? PluralStem(ReadOnlySpan<char> word)
    {
        if (!word.EndsWith("ss") && Lookup(word[..^1]) is { } single)
        {
            return single;
        }

        if (word.Length >= MinLongStem && word.EndsWith("ies"))
        {
            return LookupWith(word[..^IngLength], 'y');
        }

        var twoLetter = word.EndsWith("'s") || (word.Length >= MinLongStem && word.EndsWith("es"));
        return twoLetter ? Lookup(word[..^TwoLetterEnding]) : null;
    }

    /// <summary>Adds the -s ending's sound to a stem.</summary>
    /// <param name="stem">The stem's phonemes.</param>
    /// <returns>The phonemes.</returns>
    private string AddS(string stem)
    {
        if (VoicelessEndings.Contains(stem[^1]))
        {
            return $"{stem}s";
        }

        return Sibilants.Contains(stem[^1]) ? $"{stem}{UnstressedI}z" : $"{stem}z";
    }

    /// <summary>Says a past tense.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The phonemes, or <see langword="null"/>.</returns>
    private string? StemEd(ReadOnlySpan<char> word) => word.Length > MinStemmed && word.EndsWith('d') && PastStem(word) is { } stem ? AddEd(stem) : null;

    /// <summary>Finds the known stem of a past tense.</summary>
    /// <param name="word">The word ending in d.</param>
    /// <returns>The stem's phonemes, or <see langword="null"/>.</returns>
    private string? PastStem(ReadOnlySpan<char> word)
    {
        if (!word.EndsWith("dd") && Lookup(word[..^1]) is { } stem)
        {
            return stem;
        }

        var pastEnding = word.Length > MinStemmed + 1 && word.EndsWith("ed") && !word.EndsWith("eed");
        return pastEnding ? Lookup(word[..^TwoLetterEnding]) : null;
    }

    /// <summary>Adds the -ed ending's sound to a stem.</summary>
    /// <param name="stem">The stem's phonemes.</param>
    /// <returns>The phonemes.</returns>
    private string AddEd(string stem)
    {
        var last = stem[^1];
        if (VoicelessPast.Contains(last))
        {
            return $"{stem}t";
        }

        if (last is not ('d' or 't'))
        {
            return $"{stem}d";
        }

        return Flaps(stem) ? $"{stem[..^1]}ɾᵻd" : $"{stem}{UnstressedI}d";
    }

    /// <summary>Says a word ending in -ing.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The phonemes, or <see langword="null"/>.</returns>
    private string? StemIng(ReadOnlySpan<char> word)
    {
        if (word.Length < MinLongStem || !word.EndsWith("ing") || IngStem(word[..^IngLength]) is not { } stem)
        {
            return null;
        }

        return Flaps(stem) ? $"{stem[..^1]}ɾɪŋ" : $"{stem}ɪŋ";
    }

    /// <summary>Finds the known stem before -ing: as it is, with an e, or with a doubled consonant undone.</summary>
    /// <param name="root">The word without -ing.</param>
    /// <returns>The stem's phonemes, or <see langword="null"/>.</returns>
    private string? IngStem(ReadOnlySpan<char> root)
    {
        var stem = (root.Length > TwoLetterEnding ? Lookup(root) : null) ?? LookupWith(root, 'e');
        if (stem is not null)
        {
            return stem;
        }

        return root.Length > 1 && root[^1] == root[^TwoLetterEnding] ? Lookup(root[..^1]) : null;
    }

    /// <summary>Determines whether American English flaps a stem's final t before a vowel, as in "writing".</summary>
    /// <param name="stem">The stem's phonemes.</param>
    /// <returns><see langword="true"/> when it flaps.</returns>
    private bool Flaps(string stem) => !_british && stem.Length > 1 && stem[^1] == 't' && Taus.Contains(stem[^TwoLetterEnding]);

    /// <summary>Says a compound word made of two known words, such as "screenreader".</summary>
    /// <param name="word">The lower case word.</param>
    /// <returns>The phonemes, or <see langword="null"/>.</returns>
    private string? Compound(ReadOnlySpan<char> word)
    {
        for (var split = word.Length - MinCompoundPart; split >= MinCompoundPart; split--)
        {
            if (Lookup(word[..split]) is { } first && (Lookup(word[split..]) ?? Stemmed(word[split..])) is { } second)
            {
                return $"{first}{Demote(second)}";
            }
        }

        return null;
    }

    /// <summary>Spells a short word or acronym letter by letter, stressing the last letter.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The phonemes, or <see langword="null"/> when it is too long or a letter is unknown.</returns>
    private string? Spell(ReadOnlySpan<char> word)
    {
        if (word.Length > MaxSpelled)
        {
            return null;
        }

        var letters = new DefaultInterpolatedStringHandler(0, 0, null, stackalloc char[StackSpelled]);
        Span<char> upper = stackalloc char[1];
        foreach (var c in word)
        {
            upper[0] = char.ToUpperInvariant(c);
            if (!char.IsLetter(c) || !_lexicon.TryGet(upper, out var letter))
            {
                letters.Clear();
                return null;
            }

            letters.AppendLiteral(letter);
        }

        // As misaki does for initialisms: every letter gets secondary stress, the last primary.
        var spelled = Demote(letters.ToStringAndClear());
        var last = spelled.LastIndexOf(Secondary);
        return last < 0 ? spelled : string.Concat(spelled.AsSpan(0, last), PrimaryText, spelled.AsSpan(last + 1));
    }

    /// <summary>A word or a pause mark.</summary>
    /// <param name="Text">The text the characters are in, or <see langword="null"/> for the text being read.</param>
    /// <param name="Start">Where the characters start.</param>
    /// <param name="Length">The number of characters.</param>
    /// <param name="IsPause">Whether it is a pause mark.</param>
    private readonly record struct Token(string? Text, int Start, int Length, bool IsPause);

    /// <summary>Adds each word of a number said aloud as a token that points into the word's own string.</summary>
    /// <param name="Tokens">The tokens.</param>
    private readonly record struct TokenWords(List<Token> Tokens) : IWordSink
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(string word) => Tokens.Add(new(word, 0, word.Length, false));
    }
}
