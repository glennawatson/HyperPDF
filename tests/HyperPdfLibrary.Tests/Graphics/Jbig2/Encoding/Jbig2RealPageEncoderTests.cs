// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2.Encoding;

/// <summary>Encodes real scanned pages, decoded from the JBIG2 samples, and checks the lossless modes give every pixel back.</summary>
public sealed class Jbig2RealPageEncoderTests
{
    /// <summary>The most pixels a lossy merge may change on a real page, out of 100,000.</summary>
    private const int LossyPerHundredThousand = 100;

    /// <summary>The pixels in a hundred thousand.</summary>
    private const long HundredThousand = 100_000;

    /// <summary>Each lossless mode reproduces a real page exactly.</summary>
    /// <param name="name">The sample's name.</param>
    /// <param name="mode">The encoding mode.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(nameof(Jbig2Samples.RealLibraryOfCongress), Jbig2EncodeMode.GenericRegion)]
    [Arguments(nameof(Jbig2Samples.RealLibraryOfCongress), Jbig2EncodeMode.GenericRegionMmr)]
    [Arguments(nameof(Jbig2Samples.RealLibraryOfCongress), Jbig2EncodeMode.SymbolText)]
    [Arguments(nameof(Jbig2Samples.RealLibraryOfCongress), Jbig2EncodeMode.SymbolTextRefined)]
    [Arguments(nameof(Jbig2Samples.RealGoogleBooks), Jbig2EncodeMode.SymbolTextRefined)]
    [Arguments(nameof(Jbig2Samples.RealLibraryOfCongress), Jbig2EncodeMode.Smallest)]
    [Arguments(nameof(Jbig2Samples.RealGoogleBooks), Jbig2EncodeMode.SymbolText)]
    [Arguments(nameof(Jbig2Samples.RealInternetArchiveText), Jbig2EncodeMode.Smallest)]
    public async Task LosslessModesReproduceRealPages(string name, Jbig2EncodeMode mode)
    {
        var (rows, size) = Original(name);
        var coded = Jbig2Encoder.Encode(rows, size.Width, size.Height, new() { Mode = mode });
        var decoded = Jbig2TestPages.Decode(coded.Data, coded.Globals, size);

        await Assert.That(decoded).IsNotNull();
        await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue();
    }

    /// <summary>Lossy merging changes only a tiny share of the pixels of a real page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LossyMergingChangesFewPixelsOfARealPage()
    {
        var (rows, size) = Original(nameof(Jbig2Samples.RealLibraryOfCongress));
        var coded = Jbig2Encoder.Encode(rows, size.Width, size.Height, new() { Mode = Jbig2EncodeMode.LossySymbolMerging });
        var decoded = Jbig2TestPages.Decode(coded.Data, coded.Globals, size);
        var differences = Jbig2TestPages.CountDifferences(rows, decoded!, size);

        await Assert.That(differences * HundredThousand / ((long)size.Width * size.Height)).IsLessThan(LossyPerHundredThousand);
    }

    /// <summary>Decodes a sample to the rows the encoder takes.</summary>
    /// <param name="name">The sample's name.</param>
    /// <returns>The rows and the page size.</returns>
    private static OriginalPage Original(string name)
    {
        var sample = Jbig2Samples.Get(name);
        var rows = new byte[Jbig2Decoder.GetRowBytes(sample.Width) * sample.Height];
        _ = Jbig2Decoder.TryDecode(sample.Data, sample.Globals, sample.Width, sample.Height, rows);
        return new(rows, new(sample.Width, sample.Height));
    }

    /// <summary>A decoded sample page.</summary>
    /// <param name="Rows">The packed rows.</param>
    /// <param name="Size">The page size.</param>
    private sealed record OriginalPage(byte[] Rows, Jbig2Size Size);
}
