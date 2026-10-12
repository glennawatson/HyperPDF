// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Text;

namespace HyperPdf.Speech.English;

/// <summary>
/// Turns English text into the phonemes Kokoro reads, following misaki (Apache-2.0): words come from its pronunciation
/// lexicon, with its rules for -s, -ed and -ing endings and for "the", "a" and "to" before vowels; numbers are said as
/// words; compound words are split into known halves; acronyms and unknown short words are spelled out.
/// Reuses its buffers between sentences, so it is not thread safe; <see cref="Kokoro.KokoroEngine"/> holds a lock.
/// </summary>
[DebuggerDisplay("EnglishPhonemizer: {_lexicon.Count} words, British={_british}")]
internal sealed class EnglishPhonemizer
{
    /// <summary>The primary stress mark.</summary>
    private const char Primary = 'ˈ';

    /// <summary>The secondary stress mark.</summary>
    private const char Secondary = 'ˌ';

    /// <summary>Roughly how many phoneme characters each letter becomes, to size the output.</summary>
    private const int PhonemesPerLetter = 2;

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

    /// <summary>Sounds before which American English flaps a t, as in "writing".</summary>
    private static readonly SearchValues<char> Taus = SearchValues.Create("AIOWYiuæɑəɛɪɹʊʌ");

    /// <summary>Sounds after which -s is said as s.</summary>
    private static readonly SearchValues<char> VoicelessEndings = SearchValues.Create("ptkfθ");

    /// <summary>Sounds after which -s is said as iz.</summary>
    private static readonly SearchValues<char> Sibilants = SearchValues.Create("szʃʒʧʤ");

    /// <summary>Sounds after which -ed is said as t.</summary>
    private static readonly SearchValues<char> VoicelessPast = SearchValues.Create("pkfθʃsʧ");

    /// <summary>Symbols said as words.</summary>
    private static readonly FrozenDictionary<char, string> Symbols =
        new Dictionary<char, string> { ['%'] = "percent", ['&'] = "and", ['+'] = "plus", ['@'] = "at", ['='] = "equals" }.ToFrozenDictionary();

    /// <summary>Punctuation the voice uses for pauses and tone, as strings so tokens share them.</summary>
    private static readonly FrozenDictionary<char, string> PauseMarks = BuildPauseMarks();

    /// <summary>The pronunciations.</summary>
    private readonly PronunciationLexicon _lexicon;

    /// <summary>The tokens of the text being read, reused between sentences.</summary>
    private readonly List<Token> _tokens = [];

    /// <summary>The phonemes being built, reused between sentences.</summary>
    private readonly StringBuilder _output = new();

    /// <summary>Whether to use British endings.</summary>
    private readonly bool _british;

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
        var tokens = _tokens;
        tokens.Clear();
        Tokenize(text, tokens);
        var output = _output.Clear();
        _ = output.EnsureCapacity(text.Length * PhonemesPerLetter);
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.IsPause)
            {
                AppendPause(output, token.Text[0]);
                continue;
            }

            var phonemes = Word(token.Text, NextWord(tokens, i));
            if (phonemes.Length == 0)
            {
                continue;
            }

            if (output.Length > 0 && output[^1] is not (' ' or '(' or '“'))
            {
                _ = output.Append(' ');
            }

            _ = output.Append(phonemes);
        }

        while (output.Length > 0 && output[^1] == ' ')
        {
            output.Length--;
        }

        return output.ToString();
    }

    /// <summary>Gets the phonemes of one word.</summary>
    /// <param name="word">The word.</param>
    /// <param name="next">The next word, for "the" and "a" before vowels, or <see langword="null"/>.</param>
    /// <returns>The phonemes, or an empty string.</returns>
    internal string Word(string word, string? next)
    {
        if (SpecialCase(word, next) is { } special)
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
        phonemes.Contains(Primary, StringComparison.Ordinal) ? phonemes.Replace(Secondary.ToString(), string.Empty, StringComparison.Ordinal).Replace(Primary, Secondary) : phonemes;

    /// <summary>Determines whether a word is an acronym to spell out, such as "PDF".</summary>
    /// <param name="word">The word.</param>
    /// <returns><see langword="true"/> for short all-capital words.</returns>
    private static bool IsAcronym(string word) => word.Length is > 1 and < MaxSpelled && word.AsSpan().IndexOfAnyExceptInRange('A', 'Z') < 0;

    /// <summary>Determines whether the next word starts with a vowel sound, judged by its first letter.</summary>
    /// <param name="next">The next word.</param>
    /// <returns><see langword="true"/> for a vowel.</returns>
    private static bool StartsWithVowel(string? next) => next is { Length: > 0 } && "aeiouAEIOU".Contains(next[0], StringComparison.Ordinal);

    /// <summary>Gets the next word after a token, skipping pauses.</summary>
    /// <param name="tokens">The tokens.</param>
    /// <param name="index">The current token.</param>
    /// <returns>The next word, or <see langword="null"/>.</returns>
    private static string? NextWord(List<Token> tokens, int index) =>
        index + 1 < tokens.Count && !tokens[index + 1].IsPause ? tokens[index + 1].Text : null;

    /// <summary>Adds pause punctuation: opening marks get a space before, closing ones attach to the word.</summary>
    /// <param name="output">The phonemes so far.</param>
    /// <param name="mark">The mark.</param>
    private static void AppendPause(StringBuilder output, char mark)
    {
        if (mark is '(' or '“' && output.Length > 0 && output[^1] != ' ')
        {
            _ = output.Append(' ');
        }

        _ = output.Append(mark);
    }

    /// <summary>Splits text into words and pause marks, saying numbers and symbols as words.</summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">Receives the tokens.</param>
    private static void Tokenize(string text, List<Token> tokens)
    {
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsLetter(c))
            {
                var end = WordEnd(text, i);
                tokens.Add(new(text[i..end].Replace('’', '\''), false));
                i = end;
            }
            else if (char.IsAsciiDigit(c))
            {
                var end = NumberEnd(text, i);
                AddWords(tokens, NumberWords.Say(text.AsSpan(i, end - i)));
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
    private static void AddMark(List<Token> tokens, string text, ref int index)
    {
        if (text.AsSpan(index).StartsWith("..."))
        {
            tokens.Add(new("…", true));
            index += "...".Length;
            return;
        }

        var c = text[index];
        index++;
        if (Symbols.TryGetValue(c, out var word))
        {
            tokens.Add(new(word, false));
            return;
        }

        if (MarkFor(c, text, index) is { } mark)
        {
            tokens.Add(new(mark, true));
        }
    }

    /// <summary>Gets the pause mark a character stands for.</summary>
    /// <param name="c">The character.</param>
    /// <param name="text">The text.</param>
    /// <param name="next">The index after the character.</param>
    /// <returns>The mark, or <see langword="null"/> for a character the voice ignores.</returns>
    private static string? MarkFor(char c, string text, int next)
    {
        if (c is '–' or '—' || IsSpacedHyphen(c, text, next))
        {
            return "—";
        }

        return c is '\'' or '‘' or '’' ? "\"" : PauseMarks.GetValueOrDefault(c);
    }

    /// <summary>Builds the pause marks as strings.</summary>
    /// <returns>The marks.</returns>
    private static FrozenDictionary<char, string> BuildPauseMarks()
    {
        var marks = new Dictionary<char, string>();
        foreach (var c in ";:,.!?—…\"()“”")
        {
            marks[c] = c.ToString();
        }

        return marks.ToFrozenDictionary();
    }

    /// <summary>Determines whether a hyphen stands alone as a dash.</summary>
    /// <param name="c">The character.</param>
    /// <param name="text">The text.</param>
    /// <param name="next">The index after it.</param>
    /// <returns><see langword="true"/> for a hyphen followed by a space.</returns>
    private static bool IsSpacedHyphen(char c, string text, int next) => c == '-' && next < text.Length && char.IsWhiteSpace(text[next]);

    /// <summary>Adds the words of a phrase, such as a number said aloud.</summary>
    /// <param name="tokens">The tokens.</param>
    /// <param name="phrase">The phrase.</param>
    private static void AddWords(List<Token> tokens, string phrase)
    {
        foreach (var word in phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            tokens.Add(new(word, false));
        }
    }

    /// <summary>Finds the end of a word: letters, with apostrophes inside it.</summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The first letter.</param>
    /// <returns>The index after the word.</returns>
    private static int WordEnd(string text, int start)
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
    private static int NumberEnd(string text, int start)
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
    /// <returns>The phonemes, or <see langword="null"/> when the word is not special.</returns>
    private static string? SpecialCase(string word, string? next) => word switch
    {
        "a" or "A" when next is not null => "ɐ",
        "an" or "An" => "ɐn",
        "I" => $"{Secondary}I",
        "the" or "The" => StartsWithVowel(next) ? "ði" : "ðə",
        "to" or "To" when next is not null => StartsWithVowel(next) ? "tʊ" : "tə",
        "in" or "In" => next is null ? $"{Primary}ɪn" : "ɪn",
        _ => null,
    };

    /// <summary>Says a word not listed as written: in lower case, with an ending, as a compound, or spelled out.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The phonemes, or an empty string.</returns>
    private string Unlisted(string word)
    {
        var lower = word.ToLowerInvariant();
        if ((Lookup(lower) ?? Stemmed(lower) ?? Compound(lower)) is { } known)
        {
            return known;
        }

        if (RecordsGuesses)
        {
            GuessedWords.Add(word);
        }

        return Spell(word) ?? string.Empty;
    }

    /// <summary>Looks a word up as it is.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The phonemes, or <see langword="null"/>.</returns>
    private string? Lookup(string word) => _lexicon.TryGet(word, out var phonemes) ? phonemes : null;

    /// <summary>Says a word as a known stem with an -s, -ed or -ing ending.</summary>
    /// <param name="word">The lower case word.</param>
    /// <returns>The phonemes, or <see langword="null"/>.</returns>
    private string? Stemmed(string word) => word.Length < MinStemmed ? null : StemS(word) ?? StemEd(word) ?? StemIng(word);

    /// <summary>Says a plural or possessive.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The phonemes, or <see langword="null"/>.</returns>
    private string? StemS(string word) => word.EndsWith('s') && PluralStem(word) is { } stem ? AddS(stem) : null;

    /// <summary>Finds the known stem of a plural or possessive.</summary>
    /// <param name="word">The word ending in s.</param>
    /// <returns>The stem's phonemes, or <see langword="null"/>.</returns>
    private string? PluralStem(string word)
    {
        if (!word.EndsWith("ss", StringComparison.Ordinal) && Lookup(word[..^1]) is { } single)
        {
            return single;
        }

        if (word.Length >= MinLongStem && word.EndsWith("ies", StringComparison.Ordinal))
        {
            return Lookup($"{word[..^IngLength]}y");
        }

        var twoLetter = word.EndsWith("'s", StringComparison.Ordinal) || (word.Length >= MinLongStem && word.EndsWith("es", StringComparison.Ordinal));
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
    private string? StemEd(string word) => word.Length > MinStemmed && word.EndsWith('d') && PastStem(word) is { } stem ? AddEd(stem) : null;

    /// <summary>Finds the known stem of a past tense.</summary>
    /// <param name="word">The word ending in d.</param>
    /// <returns>The stem's phonemes, or <see langword="null"/>.</returns>
    private string? PastStem(string word)
    {
        if (!word.EndsWith("dd", StringComparison.Ordinal) && Lookup(word[..^1]) is { } stem)
        {
            return stem;
        }

        var pastEnding = word.Length > MinStemmed + 1 && word.EndsWith("ed", StringComparison.Ordinal) && !word.EndsWith("eed", StringComparison.Ordinal);
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
    private string? StemIng(string word)
    {
        if (word.Length < MinLongStem || !word.EndsWith("ing", StringComparison.Ordinal) || IngStem(word[..^IngLength]) is not { } stem)
        {
            return null;
        }

        return Flaps(stem) ? $"{stem[..^1]}ɾɪŋ" : $"{stem}ɪŋ";
    }

    /// <summary>Finds the known stem before -ing: as it is, with an e, or with a doubled consonant undone.</summary>
    /// <param name="root">The word without -ing.</param>
    /// <returns>The stem's phonemes, or <see langword="null"/>.</returns>
    private string? IngStem(string root)
    {
        var stem = (root.Length > TwoLetterEnding ? Lookup(root) : null) ?? Lookup($"{root}e");
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
    private string? Compound(string word)
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
    private string? Spell(string word)
    {
        if (word.Length > MaxSpelled)
        {
            return null;
        }

        var letters = new StringBuilder();
        foreach (var c in word)
        {
            if (!char.IsLetter(c) || !_lexicon.TryGet(char.ToUpperInvariant(c).ToString(), out var letter))
            {
                return null;
            }

            _ = letters.Append(letter);
        }

        // As misaki does for initialisms: every letter gets secondary stress, the last primary.
        var spelled = Demote(letters.ToString());
        var last = spelled.LastIndexOf(Secondary);
        return last < 0 ? spelled : string.Concat(spelled.AsSpan(0, last), Primary.ToString(), spelled.AsSpan(last + 1));
    }

    /// <summary>A word or a pause mark.</summary>
    /// <param name="Text">The word or mark.</param>
    /// <param name="IsPause">Whether it is a pause mark.</param>
    private readonly record struct Token(string Text, bool IsPause);
}
