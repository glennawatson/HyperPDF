// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Guesses the language of a run of text, for pages that state none. A script decides the languages that have their own
/// (Japanese, Korean, Chinese, Greek, Hebrew, Thai, Hindi, Arabic and Cyrillic); the Latin languages are told apart by
/// how many of their common function words appear. A guess is a hint, so it is made only when the text gives enough to
/// go on.
/// </summary>
internal static class LanguageGuesser
{
    /// <summary>The fewest letters a text needs before it is guessed.</summary>
    private const int MinLetters = 12;

    /// <summary>The fewest function words that must match for a Latin language.</summary>
    private const int MinHits = 3;

    /// <summary>The longest word compared, in characters.</summary>
    private const int MaxWord = 12;

    /// <summary>The share of letters in one non-Latin script that decides the language.</summary>
    private const float ScriptShare = 0.6F;

    /// <summary>How many times better than the next language the best Latin language must score, as a ratio.</summary>
    private const float Margin = 1.3F;

    /// <summary>The number of Latin languages.</summary>
    private const int LatinLanguages = 7;

    /// <summary>The slot of the Latin letters, and of any letter outside the listed scripts.</summary>
    private const int Latin = 0;

    /// <summary>The slot of the Hiragana and Katakana letters.</summary>
    private const int Kana = 1;

    /// <summary>The slot of the Han ideographs.</summary>
    private const int Han = 2;

    /// <summary>The slot of the Hangul syllables.</summary>
    private const int Hangul = 3;

    /// <summary>The slot of the Greek letters.</summary>
    private const int Greek = 4;

    /// <summary>The slot of the Cyrillic letters.</summary>
    private const int Cyrillic = 5;

    /// <summary>The slot of the Hebrew letters.</summary>
    private const int Hebrew = 6;

    /// <summary>The slot of the Arabic letters.</summary>
    private const int Arabic = 7;

    /// <summary>The slot of the Devanagari letters.</summary>
    private const int Devanagari = 8;

    /// <summary>The slot of the Thai letters.</summary>
    private const int Thai = 9;

    /// <summary>The number of script slots.</summary>
    private const int ScriptSlots = 10;

    /// <summary>The Latin languages by bit: English, French, German, Spanish, Italian, Portuguese, Dutch.</summary>
    private static readonly string[] LatinCodes = ["en", "fr", "de", "es", "it", "pt", "nl"];

    /// <summary>The language of each script slot after Kana and Han; Latin, Kana and Han have none of their own here.</summary>
    private static readonly string?[] ScriptCodes = [null, "ja", "zh", "ko", "el", "ru", "he", "ar", "hi", "th"];

    /// <summary>The Unicode blocks of the scripts, as code point ranges.</summary>
    private static readonly ScriptRange[] Blocks =
    [
        new(0x3040, 0x30FF, Kana),
        new(0x4E00, 0x9FFF, Han),
        new(0xAC00, 0xD7AF, Hangul),
        new(0x0370, 0x03FF, Greek),
        new(0x0400, 0x04FF, Cyrillic),
        new(0x0590, 0x05FF, Hebrew),
        new(0x0600, 0x06FF, Arabic),
        new(0x0900, 0x097F, Devanagari),
        new(0x0E00, 0x0E7F, Thai),
    ];

    /// <summary>Common function words, each with a bit for every Latin language it is common in.</summary>
    private static readonly Dictionary<string, byte> FunctionWords = BuildFunctionWords();

    /// <summary>Guesses the language of a text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>A primary language subtag such as <c>en</c>, or <see langword="null"/> when the text does not give enough to go on.</returns>
    internal static string? Guess(ReadOnlySpan<char> text)
    {
        Span<int> counts = stackalloc int[ScriptSlots];
        var letters = 0;
        foreach (var character in text)
        {
            if (!char.IsLetter(character))
            {
                continue;
            }

            letters++;
            counts[SlotOf(character)]++;
        }

        return letters < MinLetters ? null : Dominant(counts, letters) ?? GuessLatin(text, counts[Latin], letters);
    }

    /// <summary>Finds the script slot of a letter.</summary>
    /// <param name="letter">The letter.</param>
    /// <returns>The slot; Latin for a letter outside the listed blocks.</returns>
    private static int SlotOf(char letter)
    {
        foreach (var block in Blocks)
        {
            if (letter >= block.Start && letter <= block.End)
            {
                return block.Slot;
            }
        }

        return Latin;
    }

    /// <summary>Gets the language of a script that holds most of the letters.</summary>
    /// <param name="counts">The letters in each script slot.</param>
    /// <param name="letters">All the letters.</param>
    /// <returns>The language, or <see langword="null"/> when no script holds enough.</returns>
    private static string? Dominant(ReadOnlySpan<int> counts, int letters)
    {
        var limit = letters * ScriptShare;
        if (counts[Kana] + counts[Han] >= limit && counts[Kana] > 0)
        {
            return ScriptCodes[Kana];
        }

        for (var slot = Han; slot < counts.Length; slot++)
        {
            if (counts[slot] >= limit)
            {
                return ScriptCodes[slot];
            }
        }

        return null;
    }

    /// <summary>Tells the Latin languages apart by their function words.</summary>
    /// <param name="text">The text.</param>
    /// <param name="latin">The Latin letters.</param>
    /// <param name="letters">All the letters.</param>
    /// <returns>The language, or <see langword="null"/> when none stands out.</returns>
    private static string? GuessLatin(ReadOnlySpan<char> text, int latin, int letters)
    {
        if (latin < letters * ScriptShare)
        {
            return null;
        }

        var lookup = FunctionWords.GetAlternateLookup<ReadOnlySpan<char>>();
        Span<int> scores = stackalloc int[LatinLanguages];
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var inWord = i < text.Length && char.IsLetter(text[i]);
            if (inWord && start < 0)
            {
                start = i;
            }
            else if (!inWord && start >= 0)
            {
                Score(text[start..i], lookup, scores);
                start = -1;
            }
        }

        return Best(scores);
    }

    /// <summary>Scores one word for each language it is a common function word of.</summary>
    /// <param name="word">The word.</param>
    /// <param name="lookup">The function words.</param>
    /// <param name="scores">The score of each Latin language.</param>
    private static void Score(ReadOnlySpan<char> word, Dictionary<string, byte>.AlternateLookup<ReadOnlySpan<char>> lookup, Span<int> scores)
    {
        if (word.Length > MaxWord || !lookup.TryGetValue(word, out var languages))
        {
            return;
        }

        for (var language = 0; language < scores.Length; language++)
        {
            scores[language] += (languages >> language) & 1;
        }
    }

    /// <summary>Picks the language that scores clearly best.</summary>
    /// <param name="scores">The score of each Latin language.</param>
    /// <returns>The language, or <see langword="null"/> when the scores are too low or too close.</returns>
    private static string? Best(ReadOnlySpan<int> scores)
    {
        var best = 0;
        var second = 0;
        var bestLanguage = -1;
        for (var language = 0; language < scores.Length; language++)
        {
            if (scores[language] > best)
            {
                second = best;
                best = scores[language];
                bestLanguage = language;
            }
            else if (scores[language] > second)
            {
                second = scores[language];
            }
        }

        return best >= MinHits && best >= second * Margin ? LatinCodes[bestLanguage] : null;
    }

    /// <summary>Builds the table of function words.</summary>
    /// <returns>The words, compared without regard to case.</returns>
    private static Dictionary<string, byte> BuildFunctionWords()
    {
        var words = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        string[] lists =
        [
            "the and of to is in that it for with as was on are this be by at from or an have not but which",
            "le la les de des du et en un une est que qui dans pour pas sur au aux avec ce ne se sont plus par",
            "der die das und ist nicht ein eine zu den mit von auf für im dem des sich auch es als wie oder bei",
            "el la los las de y en que un una es por con para del se no al su como más pero lo",
            "il lo la i gli le di e che un una è per con non del della in da sono si come più anche",
            "o a os as de e que um uma é em para com não do da dos das por se mais como mas",
            "de het een en van in is dat op te niet met voor zijn er aan ook als maar bij door",
        ];
        for (var language = 0; language < lists.Length; language++)
        {
            Add(words, language, lists[language]);
        }

        return words;
    }

    /// <summary>Adds a language's function words.</summary>
    /// <param name="words">The table.</param>
    /// <param name="language">The language's bit position.</param>
    /// <param name="list">The words, separated by spaces.</param>
    private static void Add(Dictionary<string, byte> words, int language, string list)
    {
        foreach (var range in list.AsSpan().Split(' '))
        {
            var word = list[range];
            words[word] = (byte)(words.GetValueOrDefault(word) | (1 << language));
        }
    }

    /// <summary>A block of code points that belongs to a script.</summary>
    /// <param name="Start">The first code point.</param>
    /// <param name="End">The last code point.</param>
    /// <param name="Slot">The script's slot.</param>
    private readonly record struct ScriptRange(int Start, int End, int Slot);
}
