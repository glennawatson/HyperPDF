// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Platform.Linux.Spelling;

namespace HyperPdf.Core.Tests.Platform;

/// <summary>Tests the correction index's edit distance and lookups.</summary>
public sealed class CorrectionIndexTests
{
    /// <summary>The most suggestions asked for.</summary>
    private const int Limit = 5;

    /// <summary>A limit smaller than the words found.</summary>
    private const int SmallLimit = 2;

    /// <summary>Two letters back, for swapped neighbours in the reference table.</summary>
    private const int TwoBack = 2;

    /// <summary>The generator's multiplier (Numerical Recipes).</summary>
    private const uint Multiplier = 1_664_525;

    /// <summary>The generator's increment (Numerical Recipes).</summary>
    private const uint Increment = 1_013_904_223;

    /// <summary>The generator's low bits, which repeat quickly, are dropped.</summary>
    private const int DroppedBits = 8;

    /// <summary>A small word list, in alphabetical order as Linux word lists are.</summary>
    private static readonly string[] Words = ["an", "and", "ant", "Britain", "form", "from", "hello", "internationalisation", "receive", "relieve", "world"];

    /// <summary>Verifies each kind of change counts once, including two neighbouring letters swapped.</summary>
    /// <param name="first">The first word.</param>
    /// <param name="second">The second word.</param>
    /// <param name="expected">The changes.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("receive", "receive", 0)]
    [Arguments("recieve", "receive", 1)]
    [Arguments("helo", "hello", 1)]
    [Arguments("helllo", "hello", 1)]
    [Arguments("hallo", "hello", 1)]
    [Arguments("hlelo", "hello", 1)]
    [Arguments("wrold", "world", 1)]
    [Arguments("frm", "from", 1)]
    [Arguments("abcd", "badc", 2)]
    [Arguments("", "an", 2)]
    [Arguments("ca", "abc", 3)]
    public async Task CountsChanges(string first, string second, int expected) =>
        await Assert.That(CorrectionIndex.Distance(first, second, CorrectionIndex.MaxEditDistance + 1)).IsEqualTo(expected);

    /// <summary>Verifies the bit-parallel count matches a plain table on made-up words, including other alphabets and full-length words.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MatchesThePlainTableCount()
    {
        const int pairs = 5000;
        const string letters = "abcé日";
        var state = 7U;
        var mismatches = 0;
        for (var i = 0; i < pairs; i++)
        {
            var first = MakeWord(ref state, letters);
            var second = Next(ref state, Limit) == 0 ? MakeWord(ref state, letters) : Change(ref state, first, letters);
            var expected = TableDistance(first, second);
            var unbounded = CorrectionIndex.Distance(first, second, CorrectionIndex.MaxWordLength * TwoBack);
            var bounded = CorrectionIndex.Distance(first, second, CorrectionIndex.MaxEditDistance);
            var boundedRight = expected <= CorrectionIndex.MaxEditDistance ? bounded == expected : bounded > CorrectionIndex.MaxEditDistance;
            if (unbounded != expected || !boundedRight)
            {
                mismatches++;
            }
        }

        var full = new string('a', CorrectionIndex.MaxWordLength);
        var fullSwapped = string.Concat(full.AsSpan(0, CorrectionIndex.MaxWordLength - TwoBack), "ba");

        await Assert.That(mismatches).IsEqualTo(0);
        await Assert.That(CorrectionIndex.Distance(full, fullSwapped, CorrectionIndex.MaxEditDistance)).IsEqualTo(1);
    }

    /// <summary>Verifies the count stops just past the limit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StopsPastTheLimit()
    {
        await Assert.That(CorrectionIndex.Distance("abcdef", "uvwxyz", 1)).IsEqualTo(1 + 1);
        await Assert.That(CorrectionIndex.Distance("a", "abcdef", CorrectionIndex.MaxEditDistance)).IsEqualTo(CorrectionIndex.MaxEditDistance + 1);
    }

    /// <summary>Verifies only the closest words are offered, in word list order.</summary>
    /// <param name="misspelled">The misspelling.</param>
    /// <param name="expected">The suggestions.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("recieve", new[] { "receive", "relieve" })]
    [Arguments("releive", new[] { "receive", "relieve" })]
    [Arguments("britian", new[] { "britain" })]
    [Arguments("fomr", new[] { "form" })]
    [Arguments("anf", new[] { "an", "and", "ant" })]
    [Arguments("hellooo", new[] { "hello" })]
    [Arguments("hello", new[] { "hello" })]
    [Arguments("wrld", new[] { "world" })]
    [Arguments("internationalsiation", new[] { "internationalisation" })]
    [Arguments("internatoinalization", new[] { "internationalisation" })]
    [Arguments("zzzzzz", new string[0])]
    public async Task FindsTheClosestWords(string misspelled, string[] expected)
    {
        var index = new CorrectionIndex(Words);
        List<string> found = [];

        index.Lookup(misspelled, found, Limit);

        await Assert.That(found).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    /// <summary>Verifies no more words are offered than asked for, and a second lookup starts afresh.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsToTheLimitAndStartsAfresh()
    {
        var index = new CorrectionIndex(["bat", "cat", "eat", "fat", "hat", "mat", "rat"]);
        List<string> first = [];
        List<string> second = [];

        index.Lookup("xat", first, SmallLimit);
        index.Lookup("xat", second, Limit);

        await Assert.That(first).IsEquivalentTo(["bat", "cat"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(second).IsEquivalentTo(["bat", "cat", "eat", "fat", "hat"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    /// <summary>Verifies words longer than the index holds are left out, too long or short a word finds nothing, and an empty list finds nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LeavesOutOverlongWords()
    {
        var overlong = new string('a', CorrectionIndex.MaxWordLength + 1);
        var index = new CorrectionIndex([overlong, "apple"]);
        var empty = new CorrectionIndex([]);
        List<string> found = [];
        List<string> tooLong = [];
        List<string> tooShort = [];
        List<string> none = [];

        index.Lookup("aple", found, Limit);
        index.Lookup("applesauce", tooLong, Limit);
        index.Lookup("ap", tooShort, Limit);
        empty.Lookup("aple", none, Limit);

        await Assert.That(found).IsEquivalentTo(["apple"]);
        await Assert.That(tooLong).IsEmpty();
        await Assert.That(tooShort).IsEmpty();
        await Assert.That(none).IsEmpty();
    }

    /// <summary>Gets the next number below a bound from a fixed sequence, so every run checks the same words.</summary>
    /// <param name="state">The sequence's state.</param>
    /// <param name="bound">The bound.</param>
    /// <returns>The number.</returns>
    private static int Next(ref uint state, int bound)
    {
        state = (state * Multiplier) + Increment;
        return (int)((state >> DroppedBits) % (uint)bound);
    }

    /// <summary>Makes a word of up to a dozen letters.</summary>
    /// <param name="state">The sequence's state.</param>
    /// <param name="letters">The letters to use.</param>
    /// <returns>The word.</returns>
    private static string MakeWord(ref uint state, string letters)
    {
        const int longest = 12;
        var chars = new char[Next(ref state, longest + 1)];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = letters[Next(ref state, letters.Length)];
        }

        return new(chars);
    }

    /// <summary>Makes up to three changes to a word: adding, removing, replacing or swapping letters.</summary>
    /// <param name="state">The sequence's state.</param>
    /// <param name="word">The word.</param>
    /// <param name="letters">The letters to use.</param>
    /// <returns>The changed word.</returns>
    private static string Change(ref uint state, string word, string letters)
    {
        const int mostChanges = 3;
        const int kinds = 4;
        const int replace = 2;
        var chars = new List<char>(word);
        for (var change = Next(ref state, mostChanges + 1); change > 0; change--)
        {
            var at = Next(ref state, chars.Count + 1);
            var letter = letters[Next(ref state, letters.Length)];
            switch (Next(ref state, kinds))
            {
                case 0:
                {
                    chars.Insert(at, letter);
                    break;
                }

                case 1 when at < chars.Count:
                {
                    chars.RemoveAt(at);
                    break;
                }

                case replace when at < chars.Count:
                {
                    chars[at] = letter;
                    break;
                }

                default:
                {
                    if (at + 1 < chars.Count)
                    {
                        (chars[at], chars[at + 1]) = (chars[at + 1], chars[at]);
                    }

                    break;
                }
            }
        }

        return new([.. chars]);
    }

    /// <summary>Counts changes with the full table, as the reference the bit-parallel count is checked against.</summary>
    /// <param name="first">The first word.</param>
    /// <param name="second">The second word.</param>
    /// <returns>The changes.</returns>
    private static int TableDistance(string first, string second)
    {
        var width = second.Length + 1;
        var table = new int[(first.Length + 1) * width];
        for (var i = 0; i <= first.Length; i++)
        {
            table[i * width] = i;
        }

        for (var j = 0; j < width; j++)
        {
            table[j] = j;
        }

        for (var i = 1; i <= first.Length; i++)
        {
            for (var j = 1; j < width; j++)
            {
                var cost = first[i - 1] == second[j - 1] ? 0 : 1;
                var value = Math.Min(Math.Min(table[((i - 1) * width) + j] + 1, table[(i * width) + j - 1] + 1), table[((i - 1) * width) + j - 1] + cost);
                if (i > 1 && j > 1 && first[i - 1] == second[j - TwoBack] && first[i - TwoBack] == second[j - 1])
                {
                    value = Math.Min(value, table[((i - TwoBack) * width) + j - TwoBack] + 1);
                }

                table[(i * width) + j] = value;
            }
        }

        return table[^1];
    }
}
