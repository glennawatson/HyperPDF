// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2.Encoding;

/// <summary>Round-trip tests of the generic region encoder: arithmetic templates 0 to 3, typical prediction, adaptive pixels and MMR.</summary>
public sealed class Jbig2GenericEncoderTests
{
    /// <summary>A template number the format does not have.</summary>
    private const int InvalidTemplate = 4;

    /// <summary>A merge similarity below the lowest accepted.</summary>
    private const double TooLowSimilarity = 0.2;

    /// <summary>The page sizes that cover sub-byte widths, one pixel and several bytes.</summary>
    private static readonly Jbig2Size[] Sizes = [new(1, 1), new(7, 5), new(8, 8), new(33, 17), new(200, 120)];

    /// <summary>The page kinds the arithmetic tests code.</summary>
    private static readonly Jbig2TestPageKind[] Kinds = [Jbig2TestPageKind.Noise, Jbig2TestPageKind.Text, Jbig2TestPageKind.Blank];

    /// <summary>A page that has text, at a size where a moved pixel matters.</summary>
    private static readonly Jbig2Size MovedPixelPage = new(120, 80);

    /// <summary>A page the size compression tests code.</summary>
    private static readonly Jbig2Size CompressionPage = new(300, 200);

    /// <summary>A page the argument tests code.</summary>
    private static readonly Jbig2Size ArgumentPage = new(8, 1);

    /// <summary>Moved adaptive pixels for template 0: all four away from their nominal places, one on the current row.</summary>
    private static readonly Jbig2AtPixels MovedTemplate0 = new(-8, 0, -5, -1, 4, -2, -6, -3);

    /// <summary>A moved adaptive pixel for templates 1 to 3.</summary>
    private static readonly Jbig2AtPixels MovedSingle = new(-7, -2, 0, 0, 0, 0, 0, 0);

    /// <summary>Every template codes noise, text and blank pages back to the same pixels, with and without typical prediction.</summary>
    /// <param name="template">The generic template.</param>
    /// <param name="typical">Whether typical prediction is on.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0, true)]
    [Arguments(0, false)]
    [Arguments(1, true)]
    [Arguments(1, false)]
    [Arguments(2, true)]
    [Arguments(2, false)]
    [Arguments(3, true)]
    [Arguments(3, false)]
    public async Task ArithmeticCodingRoundTrips(int template, bool typical)
    {
        var options = new Jbig2EncodeOptions { Mode = Jbig2EncodeMode.GenericRegion, GenericTemplate = template, TypicalPrediction = typical };
        foreach (var size in Sizes)
        {
            foreach (var kind in Kinds)
            {
                var rows = Jbig2TestPages.Create(kind, size);
                var coded = Jbig2Encoder.Encode(rows, size.Width, size.Height, options);
                var decoded = Jbig2TestPages.Decode(coded.Data, coded.Globals, size);

                await Assert.That(coded.Mode).IsEqualTo(Jbig2EncodeMode.GenericRegion);
                await Assert.That(coded.IsLossless).IsTrue();
                await Assert.That(decoded).IsNotNull();
                await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue().Because($"{size} {kind} template {template}");
            }
        }
    }

    /// <summary>MMR codes noise and text pages back to the same pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MmrCodingRoundTrips()
    {
        var options = new Jbig2EncodeOptions { Mode = Jbig2EncodeMode.GenericRegionMmr };
        foreach (var size in Sizes)
        {
            foreach (var kind in Kinds)
            {
                var rows = Jbig2TestPages.Create(kind, size);
                var coded = Jbig2Encoder.Encode(rows, size.Width, size.Height, options);
                var decoded = Jbig2TestPages.Decode(coded.Data, coded.Globals, size);

                await Assert.That(coded.Mode).IsEqualTo(Jbig2EncodeMode.GenericRegionMmr);
                await Assert.That(decoded).IsNotNull();
                await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue().Because($"{size} {kind}");
            }
        }
    }

    /// <summary>Adaptive template pixels moved from their nominal places still round-trip, for every template.</summary>
    /// <param name="template">The generic template.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task MovedAdaptivePixelsRoundTrip(int template)
    {
        var rows = Jbig2TestPages.Create(Jbig2TestPageKind.Text, MovedPixelPage);
        var parameters = new Jbig2GenericParameters(template, true, template == 0 ? MovedTemplate0 : MovedSingle);
        var decoded = Jbig2TestPages.Decode(CodeWithAdaptivePixels(rows, parameters), null, MovedPixelPage);

        await Assert.That(decoded).IsNotNull();
        await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue();
    }

    /// <summary>The smallest mode is never larger than the generic region.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SmallestIsNoLargerThanGeneric()
    {
        var rows = Jbig2TestPages.Create(Jbig2TestPageKind.Text, CompressionPage);
        var generic = Jbig2Encoder.Encode(rows, CompressionPage.Width, CompressionPage.Height, new() { Mode = Jbig2EncodeMode.GenericRegion });
        var smallest = Jbig2Encoder.Encode(rows, CompressionPage.Width, CompressionPage.Height);

        await Assert.That(generic.Data.Length).IsLessThan(rows.Length);
        await Assert.That(smallest.Data.Length + (smallest.Globals?.Length ?? 0)).IsLessThanOrEqualTo(generic.Data.Length);
    }

    /// <summary>The encoder rejects short buffers, empty sizes and out-of-range options.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidArgumentsThrow()
    {
        var rows = new byte[ArgumentPage.Width];

        await Assert.That(() => Jbig2Encoder.Encode(rows, 0, 1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => Jbig2Encoder.Encode(rows, ArgumentPage.Width, rows.Length + 1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => Jbig2Encoder.Encode(rows, ArgumentPage.Width, 1, new() { GenericTemplate = InvalidTemplate })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => Jbig2Encoder.Encode(rows, ArgumentPage.Width, 1, new() { MergeSimilarity = TooLowSimilarity })).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>A cancelled token stops the encode.</summary>
    /// <param name="mode">The requested coding.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Jbig2EncodeMode.GenericRegion)]
    [Arguments(Jbig2EncodeMode.GenericRegionMmr)]
    [Arguments(Jbig2EncodeMode.Smallest)]
    public async Task CancelledTokenStopsEncoding(Jbig2EncodeMode mode)
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var rows = Jbig2TestPages.Create(Jbig2TestPageKind.Noise, MovedPixelPage);

        var options = new Jbig2EncodeOptions { Mode = mode };

        await Assert.That(() => Jbig2Encoder.Encode(rows, MovedPixelPage.Width, MovedPixelPage.Height, options, source.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Codes a page as one generic region with the given adaptive pixels.</summary>
    /// <param name="rows">The page samples.</param>
    /// <param name="parameters">The region parameters.</param>
    /// <returns>The page data.</returns>
    private static byte[] CodeWithAdaptivePixels(byte[] rows, in Jbig2GenericParameters parameters)
    {
        var page = default(PooledBuffer);
        var ink = default(PooledBuffer);
        try
        {
            Jbig2SegmentWriter.WritePageInformation(ref page, 0, MovedPixelPage.Width, MovedPixelPage.Height, true);
            Jbig2Ink.FromSamples(rows, MovedPixelPage.Width, MovedPixelPage.Height, ref ink);
            var bitmap = new Jbig2BitmapView(ink.WrittenSpan, MovedPixelPage.Width, MovedPixelPage.Height);
            Jbig2GenericRegionWriter.WriteArithmetic(ref page, 1, bitmap, default, parameters, CancellationToken.None);
            return page.ToArray();
        }
        finally
        {
            ink.Dispose();
            page.Dispose();
        }
    }
}
