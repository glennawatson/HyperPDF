// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Checks <see cref="CodeBitSet"/> against a <see cref="HashSet{T}"/> holding the same values.</summary>
public sealed class CodeBitSetTests
{
    /// <summary>The rounds that fill and return pooled words.</summary>
    private const int Rounds = 8;

    /// <summary>The values a round fills.</summary>
    private const int RoundValues = 2000;

    /// <summary>The one value of the reuse test.</summary>
    private const int Single = 1500;

    /// <summary>The values added: codes in the first words, a gap, a large glyph number, and values that overflow the bits.</summary>
    private static readonly int[] Values = [0, 1, 63, 64, 65, 255, 256, 4000, 65_535, CodeBitSet.BitLimit - 1, CodeBitSet.BitLimit, int.MaxValue, -1, 1, 64];

    /// <summary>The values of the first set of the union test.</summary>
    private static readonly int[] FirstValues = [3, 70, 71, -5];

    /// <summary>The values of the second set of the union test.</summary>
    private static readonly int[] SecondValues = [71, 1000, 5000, -5, -6];

    /// <summary>The values of the union.</summary>
    private static readonly int[] UnionValues = [3, 70, 71, 1000, 5000, -5, -6];

    /// <summary>Values that no test set holds.</summary>
    private static readonly int[] AbsentValues = [2, 1_000_000, -2];

    /// <summary>Adds the same values to a set and a hash set, then compares count, membership and walk order.</summary>
    /// <param name="pooled">Whether the set rents its words.</param>
    /// <returns>A task that completes when the checks finish.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MatchesHashSet(bool pooled)
    {
        using var set = new CodeBitSet(pooled);
        var expected = new HashSet<int>();
        foreach (var value in Values)
        {
            await Assert.That(set.Add(value)).IsEqualTo(expected.Add(value));
        }

        var walked = new HashSet<int>();
        for (var value = set.NextBit(0); value != CodeBitSet.NoBit; value = set.NextBit(value + 1))
        {
            _ = walked.Add(value);
        }

        if (set.Overflow is { } overflow)
        {
            walked.UnionWith(overflow);
        }

        await Assert.That(set.Count).IsEqualTo(expected.Count);
        await Assert.That(walked.SetEquals(expected)).IsTrue();
        foreach (var value in expected)
        {
            await Assert.That(set.Contains(value)).IsTrue();
        }

        foreach (var value in AbsentValues)
        {
            await Assert.That(set.Contains(value)).IsFalse();
        }
    }

    /// <summary>A union holds the values of both sets and counts each once.</summary>
    /// <returns>A task that completes when the checks finish.</returns>
    [Test]
    public async Task UnionHoldsBoth()
    {
        using var first = new CodeBitSet(true);
        using var second = new CodeBitSet(true);
        foreach (var value in FirstValues)
        {
            _ = first.Add(value);
        }

        foreach (var value in SecondValues)
        {
            _ = second.Add(value);
        }

        first.UnionWith(second);
        await Assert.That(first.Count).IsEqualTo(UnionValues.Length);
        foreach (var value in UnionValues)
        {
            await Assert.That(first.Contains(value)).IsTrue();
        }
    }

    /// <summary>A pooled array that was used before does not leak its old bits into a new set.</summary>
    /// <returns>A task that completes when the checks finish.</returns>
    [Test]
    public async Task ReusedPoolWordsStartEmpty()
    {
        for (var round = 0; round < Rounds; round++)
        {
            using var dirty = new CodeBitSet(true);
            for (var value = 0; value < RoundValues; value++)
            {
                _ = dirty.Add(value);
            }
        }

        using var set = new CodeBitSet(true);
        _ = set.Add(Single);
        await Assert.That(set.Count).IsEqualTo(1);
        await Assert.That(set.NextBit(0)).IsEqualTo(Single);
        await Assert.That(set.NextBit(Single + 1)).IsEqualTo(CodeBitSet.NoBit);
    }
}
