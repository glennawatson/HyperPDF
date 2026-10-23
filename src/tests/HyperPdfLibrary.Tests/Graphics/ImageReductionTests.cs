// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Checks that scan resolution follows the displayed size and mask rules.</summary>
public sealed class ImageReductionTests
{
    /// <summary>The scan width in pixels.</summary>
    private const int ScanWidth = 1879;

    /// <summary>The scan height in pixels.</summary>
    private const int ScanHeight = 3059;

    /// <summary>The displayed page width in points.</summary>
    private const int PageWidth = 612;

    /// <summary>The displayed page height in points.</summary>
    private const int PageHeight = 792;

    /// <summary>A 300 dpi JPX scan uses fewer levels when the page is zoomed in.</summary>
    /// <param name="scale">The upper device scale.</param>
    /// <param name="expected">The expected reduction levels.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0.5F, 2)]
    [Arguments(1F, 1)]
    [Arguments(2F, 0)]
    public async Task ZoomSelectsNativeResolution(float scale, int expected)
    {
        var dictionary = Scan(KnownName.JPXDecode);
        var matrix = Matrix3x2.CreateScale(PageWidth, PageHeight);

        await Assert.That(ImageReduction.Select(dictionary, matrix, scale)).IsEqualTo(expected);
    }

    /// <summary>A soft mask may be reduced with its JPX image; a colour key requires full samples.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SoftMaskCanReduceButColorKeyCannot()
    {
        var dictionary = Scan(KnownName.JPXDecode);
        dictionary.Add(KnownName.SMask, PdfValue.FromStream(new(Scan(KnownName.JBIG2Decode), [])));
        var matrix = Matrix3x2.CreateScale(PageWidth, PageHeight);
        await Assert.That(ImageReduction.Select(dictionary, matrix, 1)).IsEqualTo(1);

        dictionary.Add(KnownName.Mask, PdfValue.FromArray(PdfArray.FromNumbers(null, [0, 0])));
        await Assert.That(ImageReduction.Select(dictionary, matrix, 1)).IsEqualTo(0);
    }

    /// <summary>A JBIG2 stencil mask can keep the same reduced dimensions as its colour image.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StencilMaskCanReduceWithJpx()
    {
        var dictionary = Scan(KnownName.JPXDecode);
        dictionary.Add(KnownName.Mask, PdfValue.FromStream(new(Scan(KnownName.JBIG2Decode), [])));

        await Assert.That(ImageReduction.Select(dictionary, Matrix3x2.CreateScale(PageWidth, PageHeight), 1)).IsEqualTo(1);
    }

    /// <summary>A JBIG2 scan also selects display resolution; a JPEG stays at full decode size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlySupportedCodecsReduce()
    {
        var matrix = Matrix3x2.CreateScale(PageWidth, PageHeight);
        await Assert.That(ImageReduction.Select(Scan(KnownName.JBIG2Decode), matrix, 1)).IsEqualTo(1);
        await Assert.That(ImageReduction.Select(Scan(KnownName.DCTDecode), matrix, 1)).IsEqualTo(0);
    }

    /// <summary>Creates a scan image dictionary.</summary>
    /// <param name="filter">The image codec.</param>
    /// <returns>The image dictionary.</returns>
    private static PdfDictionary Scan(KnownName filter)
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.Width, PdfValue.FromInteger(ScanWidth));
        dictionary.Add(KnownName.Height, PdfValue.FromInteger(ScanHeight));
        dictionary.Add(KnownName.Filter, PdfValue.FromName(filter));
        return dictionary;
    }
}
