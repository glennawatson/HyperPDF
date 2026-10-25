// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jbig2;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>Tests for the JBIG2 decoder against real pages, PDFium's test vectors and synthetic streams.</summary>
public sealed class Jbig2DecoderTests
{
    /// <summary>The bytes of a JBIG2 file header with a page count.</summary>
    private const int LongHeader = 13;

    /// <summary>The offset of the flags in a JBIG2 file header.</summary>
    private const int HeaderFlags = 8;

    /// <summary>The width of the small images built by the tests.</summary>
    private const int SmallSide = 16;

    /// <summary>The bytes in one row of a small image.</summary>
    private const int SmallStride = 2;

    /// <summary>A side length whose square passes the decoder's pixel limit.</summary>
    private const int HugeSide = 20_000;

    /// <summary>A byte with every pixel white in the output.</summary>
    private const byte White = 0xFF;

    /// <summary>Gets the JBIG2 file signature.</summary>
    private static ReadOnlySpan<byte> FileSignature => [0x97, 0x4A, 0x42, 0x32, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Gets a page information segment for a 16 by 16 page, then an end-of-page segment.</summary>
    private static ReadOnlySpan<byte> PageOnly =>
    [
        0x00, 0x00, 0x00, 0x00, 0x30, 0x00, 0x01, 0x00, 0x00, 0x00, 0x13,
        0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x01, 0x31, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
    ];

    /// <summary>Gets a page information segment for a 16 by 16 page whose default pixel is black.</summary>
    private static ReadOnlySpan<byte> BlackPage =>
    [
        0x00, 0x00, 0x00, 0x00, 0x30, 0x00, 0x01, 0x00, 0x00, 0x00, 0x13,
        0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00,
    ];

    /// <summary>Each sample decodes to the image PDFium renders, or to the encoder's image where PDFium departs from T.88.</summary>
    /// <param name="name">The sample's name.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(nameof(Jbig2Samples.RealLibraryOfCongress))]
    [Arguments(nameof(Jbig2Samples.RealGoogleBooks))]
    [Arguments(nameof(Jbig2Samples.RealInternetArchiveText))]
    [Arguments(nameof(Jbig2Samples.RealInternetArchiveGeneric))]
    [Arguments(nameof(Jbig2Samples.PdfiumComposeOrXorReplace))]
    [Arguments(nameof(Jbig2Samples.PdfiumComposeAndXnor))]
    [Arguments(nameof(Jbig2Samples.GenericTemplate0Typical))]
    [Arguments(nameof(Jbig2Samples.GenericTemplate0Adaptive))]
    [Arguments(nameof(Jbig2Samples.GenericTemplate1Typical))]
    [Arguments(nameof(Jbig2Samples.GenericTemplate1Adaptive))]
    [Arguments(nameof(Jbig2Samples.GenericTemplate2))]
    [Arguments(nameof(Jbig2Samples.GenericTemplate2Adaptive))]
    [Arguments(nameof(Jbig2Samples.GenericTemplate3Typical))]
    [Arguments(nameof(Jbig2Samples.GenericTemplate3Adaptive))]
    [Arguments(nameof(Jbig2Samples.GenericMmr))]
    [Arguments(nameof(Jbig2Samples.ComposeOperators))]
    [Arguments(nameof(Jbig2Samples.RefinePage))]
    [Arguments(nameof(Jbig2Samples.RefineIntermediate))]
    [Arguments(nameof(Jbig2Samples.RefineChain))]
    [Arguments(nameof(Jbig2Samples.TextHuffman))]
    [Arguments(nameof(Jbig2Samples.TextHuffmanTransposed))]
    [Arguments(nameof(Jbig2Samples.TextHuffmanBottomRight))]
    [Arguments(nameof(Jbig2Samples.TextHuffmanCustomTable))]
    [Arguments(nameof(Jbig2Samples.TextHuffmanMmrEndOfBlock))]
    [Arguments(nameof(Jbig2Samples.TextHuffmanRefineCustom))]
    [Arguments(nameof(Jbig2Samples.TextArith))]
    [Arguments(nameof(Jbig2Samples.TextArithRefine))]
    [Arguments(nameof(Jbig2Samples.TextArithTransposed))]
    [Arguments(nameof(Jbig2Samples.TextArithTopRight))]
    [Arguments(nameof(Jbig2Samples.TextArithRefineClipped))]
    [Arguments(nameof(Jbig2Samples.SymbolsGlobalsRefinementAggregate))]
    [Arguments(nameof(Jbig2Samples.SymbolsHuffmanAggregate))]
    [Arguments(nameof(Jbig2Samples.SymbolsHuffmanRefinementAggregate))]
    [Arguments(nameof(Jbig2Samples.HalftoneArith))]
    [Arguments(nameof(Jbig2Samples.HalftoneArithSkip))]
    [Arguments(nameof(Jbig2Samples.HalftoneMmr))]
    public async Task SampleDecodesToReference(string name)
    {
        var sample = Jbig2Samples.Get(name);
        var rows = new byte[Jbig2Decoder.GetRowBytes(sample.Width) * sample.Height];

        var decoded = Jbig2Decoder.TryDecode(sample.Data, sample.Globals, sample.Width, sample.Height, rows);

        await Assert.That(decoded).IsTrue();
        await Assert.That(Convert.ToHexStringLower(SHA256.HashData(rows))).IsEqualTo(sample.Sha256);
    }

    /// <summary>A scratch cache can decode different segment types after a cancelled operation.</summary>
    /// <param name="cancellationToken">Cancels the healthy decodes.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScratchCacheRecoversAfterCancellation(CancellationToken cancellationToken)
    {
        var scratch = new ScratchPools();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var first = Jbig2Samples.Get(nameof(Jbig2Samples.SymbolsGlobalsRefinementAggregate));
        var firstRows = new byte[Jbig2Decoder.GetRowBytes(first.Width) * first.Height];
        await Assert.That(() => Jbig2Decoder.TryDecode(first.Data, first.Globals, first.Width, first.Height, firstRows, scratch, cancelled.Token)).Throws<OperationCanceledException>();
        string[] names =
        [
            nameof(Jbig2Samples.SymbolsGlobalsRefinementAggregate),
            nameof(Jbig2Samples.TextHuffmanCustomTable),
            nameof(Jbig2Samples.HalftoneArith),
            nameof(Jbig2Samples.TextHuffmanRefineCustom),
            nameof(Jbig2Samples.HalftoneArithSkip),
            nameof(Jbig2Samples.TextHuffmanCustomTable),
            nameof(Jbig2Samples.SymbolsHuffmanRefinementAggregate),
            nameof(Jbig2Samples.GenericMmr),
            nameof(Jbig2Samples.SymbolsGlobalsRefinementAggregate),
        ];
        foreach (var name in names)
        {
            var sample = Jbig2Samples.Get(name);
            var rows = new byte[Jbig2Decoder.GetRowBytes(sample.Width) * sample.Height];
            var decoded = Jbig2Decoder.TryDecode(sample.Data, sample.Globals, sample.Width, sample.Height, rows, scratch, cancellationToken);
            await Assert.That(decoded).IsTrue();
            await Assert.That(Convert.ToHexStringLower(SHA256.HashData(rows))).IsEqualTo(sample.Sha256);
        }
    }

    /// <summary>Truncated custom tables leave reusable decoder storage valid for the next complete page.</summary>
    /// <param name="name">The sample with custom Huffman tables.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(nameof(Jbig2Samples.TextHuffmanCustomTable))]
    [Arguments(nameof(Jbig2Samples.TextHuffmanRefineCustom))]
    public async Task ScratchCacheRecoversAfterTruncatedHuffmanTables(string name, CancellationToken cancellationToken)
    {
        var sample = Jbig2Samples.Get(name);
        var scratch = new ScratchPools();
        var rows = new byte[Jbig2Decoder.GetRowBytes(sample.Width) * sample.Height];
        for (var length = 0; length < sample.Data.Length; length++)
        {
            _ = Jbig2Decoder.TryDecode(sample.Data.AsSpan(0, length), sample.Globals, sample.Width, sample.Height, rows, scratch, cancellationToken);
            var decoded = Jbig2Decoder.TryDecode(sample.Data, sample.Globals, sample.Width, sample.Height, rows, scratch, cancellationToken);
            await Assert.That(decoded).IsTrue();
            await Assert.That(Convert.ToHexStringLower(SHA256.HashData(rows))).IsEqualTo(sample.Sha256);
        }
    }

    /// <summary>A JBIG2 file header, which PDF forbids, is skipped as pdf.js does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FileHeaderIsSkipped()
    {
        var sample = Jbig2Samples.TextArith;
        var withHeader = new byte[LongHeader + sample.Data.Length];
        FileSignature.CopyTo(withHeader);
        withHeader[HeaderFlags] = 1;
        sample.Data.CopyTo(withHeader, LongHeader);
        var rows = new byte[Jbig2Decoder.GetRowBytes(sample.Width) * sample.Height];

        var decoded = Jbig2Decoder.TryDecode(withHeader, sample.Width, sample.Height, rows);

        await Assert.That(decoded).IsTrue();
        await Assert.That(Convert.ToHexStringLower(SHA256.HashData(rows))).IsEqualTo(sample.Sha256);
    }

    /// <summary>Text regions that use global symbols decode only when the globals are given.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingGlobalsFail()
    {
        var sample = Jbig2Samples.SymbolsGlobalsRefinementAggregate;
        var rows = new byte[Jbig2Decoder.GetRowBytes(sample.Width) * sample.Height];

        var decoded = Jbig2Decoder.TryDecode(sample.Data, sample.Width, sample.Height, rows);

        // The page information is read, so the white page counts as decoded; the text region fails.
        await Assert.That(decoded).IsTrue();
        await Assert.That(rows.AsSpan().ContainsAnyExcept(White)).IsFalse();
    }

    /// <summary>A page with no regions is white, or black when its default pixel says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageInformationSetsDefaultPixel()
    {
        var white = new byte[SmallStride * SmallSide];
        var black = new byte[SmallStride * SmallSide];

        var whiteDecoded = Jbig2Decoder.TryDecode(PageOnly, SmallSide, SmallSide, white);
        var blackDecoded = Jbig2Decoder.TryDecode(BlackPage, SmallSide, SmallSide, black);

        await Assert.That(whiteDecoded).IsTrue();
        await Assert.That(blackDecoded).IsTrue();
        await Assert.That(white.AsSpan().ContainsAnyExcept(White)).IsFalse();
        await Assert.That(black.AsSpan().ContainsAnyExcept((byte)0)).IsFalse();
    }

    /// <summary>Empty data and data with no valid segment decode nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyOrGarbageDataFails()
    {
        var rows = new byte[SmallStride * SmallSide];
        byte[] garbage = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

        await Assert.That(Jbig2Decoder.TryDecode([], SmallSide, SmallSide, rows)).IsFalse();
        await Assert.That(Jbig2Decoder.TryDecode(garbage, SmallSide, SmallSide, rows)).IsFalse();
        await Assert.That(rows.AsSpan().ContainsAnyExcept(White)).IsFalse();
    }

    /// <summary>An image over the pixel limit is not decoded, before the destination is even looked at.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OversizedImageFails()
    {
        var rows = new byte[SmallStride];

        var decoded = Jbig2Decoder.TryDecode(PageOnly, HugeSide, HugeSide, rows);

        await Assert.That(decoded).IsFalse();
        await Assert.That(rows.AsSpan().ContainsAnyExcept((byte)0)).IsFalse();
    }

    /// <summary>Invalid sizes and short destinations throw.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidArgumentsThrow()
    {
        await Assert.That(static () => Jbig2Decoder.TryDecode(PageOnly.ToArray(), 0, SmallSide, new byte[SmallStride * SmallSide])).Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => Jbig2Decoder.TryDecode(PageOnly.ToArray(), SmallSide, -1, new byte[SmallStride * SmallSide])).Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => Jbig2Decoder.TryDecode(PageOnly.ToArray(), SmallSide, SmallSide, new byte[SmallSide])).Throws<ArgumentException>();
    }
}
