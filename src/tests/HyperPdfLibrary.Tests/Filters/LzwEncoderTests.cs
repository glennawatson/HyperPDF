// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Tests for <see cref="LzwEncoder"/>: the specification example, round trips through <see cref="LzwFilter"/> and long inputs that clear the table.</summary>
public sealed class LzwEncoderTests
{
    /// <summary>The size of the input that makes the encoder wrap its table epoch; each table reset takes about 8,000 random bytes and the epoch holds 4,095 resets.</summary>
    private const int EpochWrapLength = 40_000_000;

    /// <summary>The seed of the large random input.</summary>
    private const uint Seed = 424_242;

    /// <summary>The /EarlyChange value that widens codes one entry early (the default).</summary>
    private const int Early = 1;

    /// <summary>The /EarlyChange value that widens codes as late as possible.</summary>
    private const int Late = 0;

    /// <summary>A value Table 8 does not allow, which the decoder counts as 1.</summary>
    private const int OutOfRangeEarlyChange = 2;

    /// <summary>The compression ratio all-zero data must beat: far better than 100 to 1.</summary>
    private const int ZeroRatio = 100;

    /// <summary>The worst case of 7.4.4.1 note 2 in eighths: 12-bit codes for 8-bit bytes is a factor of 3 over 2.</summary>
    private const int WorstCaseNumerator = 3;

    /// <summary>The denominator of the worst-case factor.</summary>
    private const int WorstCaseDenominator = 2;

    /// <summary>The slack for clear codes and the end code, as a fraction of the input.</summary>
    private const int ClearSlackDivisor = 100;

    /// <summary>The input of ISO 32000-2 table 7: 45 45 45 45 45 65 45 45 45 66.</summary>
    private static readonly byte[] SpecificationInput = [45, 45, 45, 45, 45, 65, 45, 45, 45, 66];

    /// <summary>The bytes ISO 32000-2 7.4.4.2 example 2 gives for the table 7 input.</summary>
    private static readonly byte[] SpecificationOutput = [0x80, 0x0B, 0x60, 0x50, 0x22, 0x0C, 0x0C, 0x85, 0x01];

    /// <summary>The encoder gives the exact bytes of the specification's worked example.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_SpecificationExample_GivesSpecificationBytes()
    {
        var encoded = Encode(SpecificationInput, Early);

        await Assert.That(encoded).IsEquivalentTo(SpecificationOutput);
    }

    /// <summary>Every sample decodes back to itself, with both /EarlyChange values.</summary>
    /// <param name="earlyChange">The /EarlyChange value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Late)]
    [Arguments(Early)]
    public async Task Encode_Samples_RoundTrip(int earlyChange)
    {
        var parameters = new PdfDictionary(null);
        parameters.Add(KnownName.EarlyChange, PdfValue.FromInteger(earlyChange));
        foreach (var sample in FilterSamples.All())
        {
            var decoded = FilterRoundTrip.Decode(KnownName.LZWDecode, parameters, Encode(sample.Data, earlyChange));

            await Assert.That(decoded).IsEquivalentTo(sample.Data).Because(sample.Name);
        }
    }

    /// <summary>The two /EarlyChange values give different codes for data that widens the codes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_EarlyChangeZeroAndOne_ProduceDifferentCodes()
    {
        var sample = FilterSamples.Get("alphabet3");

        await Assert.That(Encode(sample.Data, Late)).IsNotEquivalentTo(Encode(sample.Data, Early));
    }

    /// <summary>A value other than 0 counts as 1, as the decoder treats it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_EarlyChangeTwo_MatchesEarlyChangeOne()
    {
        var sample = FilterSamples.Get("alphabet7");

        await Assert.That(Encode(sample.Data, OutOfRangeEarlyChange)).IsEquivalentTo(Encode(sample.Data, Early));
    }

    /// <summary>Compressible data gets much smaller, and incompressible data grows no more than the worst case in the specification.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_Sizes_StayWithinBounds()
    {
        var zeros = FilterSamples.Get("zeros");
        var noise = FilterSamples.Get("noise");

        await Assert.That(Encode(zeros.Data, Early).Length).IsLessThan(zeros.Data.Length / ZeroRatio);
        await Assert.That(Encode(noise.Data, Early).Length).IsLessThan((noise.Data.Length * WorstCaseNumerator / WorstCaseDenominator) + (noise.Data.Length / ClearSlackDivisor));
    }

    /// <summary>A very long input resets the table so often that the encoder wraps its epoch counter, and still decodes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_EpochWrap_StillRoundTrips()
    {
        var input = new SampleRandom(Seed).NextBytes(EpochWrapLength);
        var decoded = FilterRoundTrip.Decode(KnownName.LZWDecode, Encode(input, Early));

        await Assert.That(decoded.AsSpan().SequenceEqual(input)).IsTrue();
    }

    /// <summary>A cancelled token stops the encoder.</summary>
    /// <param name="cancellationToken">The test's token, which is not cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_CancelledToken_Throws(CancellationToken cancellationToken)
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await source.CancelAsync();
        var token = source.Token;

        await Assert.That(() => FilterRoundTrip.Encode((ReadOnlySpan<byte> input, ref PooledBuffer output) => LzwEncoder.Encode(input, Early, ref output, token), SpecificationInput))
            .Throws<OperationCanceledException>();
    }

    /// <summary>Encodes bytes as LZW.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="earlyChange">The /EarlyChange value.</param>
    /// <returns>The encoded bytes.</returns>
    private static byte[] Encode(byte[] data, int earlyChange) =>
        FilterRoundTrip.Encode((ReadOnlySpan<byte> input, ref PooledBuffer output) => LzwEncoder.Encode(input, earlyChange, ref output), data);
}
