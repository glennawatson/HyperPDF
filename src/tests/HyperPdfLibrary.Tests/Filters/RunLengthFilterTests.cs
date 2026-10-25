// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Tests for <see cref="RunLengthFilter"/> encoding and decoding.</summary>
public sealed class RunLengthFilterTests
{
    /// <summary>The length byte that ends the data.</summary>
    private const byte EndOfData = 128;

    /// <summary>The length of the all-equal and alternating samples.</summary>
    private const int LongLength = 100_000;

    /// <summary>The most bytes one run covers.</summary>
    private const int MaxRun = 128;

    /// <summary>The length byte of a repeat run of 128: 257 minus 128.</summary>
    private const byte RepeatOf128 = 129;

    /// <summary>The best case of 7.4.5: two bytes written for every 128, which is 64 to 1.</summary>
    private const int BestCaseRatio = 64;

    /// <summary>The slack for the end byte and a final short run.</summary>
    private const int Slack = 4;

    /// <summary>The worst case of 7.4.5: 129 bytes written for every 128.</summary>
    private const int WorstCaseNumerator = 129;

    /// <summary>The input of the specification-shaped example: a literal run of three, then three equal bytes.</summary>
    private static readonly byte[] LiteralThenRepeatOutput = [0x02, (byte)'a', (byte)'b', (byte)'c', 0xFE, (byte)'z', EndOfData];

    /// <summary>The output for empty data.</summary>
    private static readonly byte[] EmptyOutput = [EndOfData];

    /// <summary>The output for 129 equal bytes: a repeat of 128, then a literal run of one.</summary>
    private static readonly byte[] SplitRunOutput = [RepeatOf128, (byte)'q', 0, (byte)'q', EndOfData];

    /// <summary>The encoder gives the bytes of the decoder's own example: a literal run of three and a repeat of three.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_LiteralThenRepeat_GivesSpecificationShape()
    {
        var encoded = FilterRoundTrip.Encode(static (input, ref output) => RunLengthFilter.Encode(input, ref output), "abczzz"u8);

        await Assert.That(encoded).IsEquivalentTo(LiteralThenRepeatOutput);
    }

    /// <summary>Empty data is only the end-of-data byte.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_Empty_IsEndOfData()
    {
        var encoded = FilterRoundTrip.Encode(static (input, ref output) => RunLengthFilter.Encode(input, ref output), []);

        await Assert.That(encoded).IsEquivalentTo(EmptyOutput);
    }

    /// <summary>Every sample, including runs on the edges of the 128 limit, decodes back to itself.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_Samples_RoundTrip()
    {
        foreach (var sample in FilterSamples.All())
        {
            var encoded = FilterRoundTrip.Encode(static (input, ref output) => RunLengthFilter.Encode(input, ref output), sample.Data);
            var decoded = FilterRoundTrip.Decode(KnownName.RunLengthDecode, encoded);

            await Assert.That(decoded).IsEquivalentTo(sample.Data).Because(sample.Name);
        }
    }

    /// <summary>A run of exactly 129 bytes splits into 128 and 1, and the abbreviated name decodes it too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_RunOf129_SplitsAtTheLimit()
    {
        var input = new byte[MaxRun + 1];
        Array.Fill(input, (byte)'q');
        var encoded = FilterRoundTrip.Encode(static (data, ref output) => RunLengthFilter.Encode(data, ref output), input);

        await Assert.That(encoded).IsEquivalentTo(SplitRunOutput);
        await Assert.That(FilterRoundTrip.Decode(KnownName.RL, encoded)).IsEquivalentTo(input);
    }

    /// <summary>Long runs compress by about 64 to 1, and alternating data stays within the 129 to 128 worst case plus the end byte.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_BestAndWorstCases_MatchTheSpecification()
    {
        var zeros = new byte[LongLength];
        var alternating = new byte[LongLength];
        for (var i = 0; i < alternating.Length; i++)
        {
            alternating[i] = (i & 1) == 0 ? (byte)0 : byte.MaxValue;
        }

        var best = FilterRoundTrip.Encode(static (data, ref output) => RunLengthFilter.Encode(data, ref output), zeros);
        var worst = FilterRoundTrip.Encode(static (data, ref output) => RunLengthFilter.Encode(data, ref output), alternating);

        await Assert.That(best.Length).IsLessThanOrEqualTo((LongLength / BestCaseRatio) + Slack);
        await Assert.That(worst.Length).IsLessThanOrEqualTo((LongLength * WorstCaseNumerator / MaxRun) + Slack);
    }
}
