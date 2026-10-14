// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Engine.Tests.RenderingComparison;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;
using HyperPdfLibrary.Tests.Graphics.Jbig2.Encoding;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// JBIG2 data from <see cref="Jbig2Encoder"/> embedded in a PDF and drawn one sample per pixel by PDFium, the independent
/// decoder: every sample must come out as the page the encoder was given.
/// </summary>
public sealed class Jbig2EncoderPdfiumTests
{
    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The blue channel level below which a rendered pixel counts as black.</summary>
    private const int BlackBelow = 128;

    /// <summary>The number of images drawn side by side in the shared test.</summary>
    private const int SideBySide = 2;

    /// <summary>The page size of the test pages.</summary>
    private static readonly Jbig2Size Page = new(160, 96);

    /// <summary>The text styles of the test pages.</summary>
    private static readonly Jbig2TestTextStyle Style = new(2, 11, 12);

    /// <summary>The text style of the second shared image.</summary>
    private static readonly Jbig2TestTextStyle SecondStyle = new(2, 23, 12);

    /// <summary>The spacing of the flipped pixels.</summary>
    private static readonly Jbig2Size FlipSpacing = new(13, 5);

    /// <summary>Every encoding mode draws the same pixels in PDFium as the input page.</summary>
    /// <param name="mode">The encoding mode.</param>
    /// <param name="template">The generic template.</param>
    /// <param name="separateGlobals">Whether the dictionary goes in /JBIG2Globals.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Jbig2EncodeMode.GenericRegion, 0, false)]
    [Arguments(Jbig2EncodeMode.GenericRegion, 1, false)]
    [Arguments(Jbig2EncodeMode.GenericRegion, 2, false)]
    [Arguments(Jbig2EncodeMode.GenericRegion, 3, false)]
    [Arguments(Jbig2EncodeMode.GenericRegionMmr, 0, false)]
    [Arguments(Jbig2EncodeMode.SymbolText, 0, false)]
    [Arguments(Jbig2EncodeMode.SymbolText, 2, false)]
    [Arguments(Jbig2EncodeMode.SymbolText, 0, true)]
    [Arguments(Jbig2EncodeMode.SymbolTextRefined, 0, false)]
    [Arguments(Jbig2EncodeMode.SymbolTextRefined, 1, true)]
    [Arguments(Jbig2EncodeMode.Smallest, 0, false)]
    public async Task PdfiumDecodesEncoderOutput(Jbig2EncodeMode mode, int template, bool separateGlobals)
    {
        var rows = Jbig2TestPages.Text(Page, Style);

        // Flipped pixels make repeated glyphs differ, so the refined mode has near copies to refine.
        for (var y = 0; y < Page.Height; y += FlipSpacing.Height)
        {
            for (var x = 1; x < Page.Width; x += FlipSpacing.Width)
            {
                Jbig2TestPages.Flip(rows, Page.Width, x, y);
            }
        }

        var options = new Jbig2EncodeOptions { Mode = mode, GenericTemplate = template, SeparateGlobals = separateGlobals };
        var coded = Jbig2Encoder.Encode(rows, Page.Width, Page.Height, options);

        var differences = CountPdfiumDifferences(rows, coded);

        await Assert.That(differences).IsEqualTo(0).Because($"{mode} template {template} globals {separateGlobals}");
    }

    /// <summary>Two images that share one globals stream, drawn side by side, both match the input in PDFium.</summary>
    /// <param name="mode">The symbol mode.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Jbig2EncodeMode.SymbolText)]
    [Arguments(Jbig2EncodeMode.SymbolTextRefined)]
    public async Task PdfiumDecodesSharedGlobals(Jbig2EncodeMode mode)
    {
        var left = Jbig2TestPages.Text(Page, Style);
        var right = Jbig2TestPages.Text(Page, SecondStyle);
        var coded = Jbig2Encoder.EncodeShared([new(left, Page.Width, Page.Height), new(right, Page.Width, Page.Height)], new() { Mode = mode });
        var content = string.Create(
            CultureInfo.InvariantCulture,
            $"q {Page.Width} 0 0 {Page.Height} 0 0 cm /Im1 Do Q q {Page.Width} 0 0 {Page.Height} {Page.Width} 0 cm /Im2 Do Q");
        var pdf = new RenderTestPdf(Page.Width * SideBySide, Page.Height) { Content = content };
        var globals = pdf.AddStream(string.Empty, coded.Globals);
        var entries = string.Create(
            CultureInfo.InvariantCulture,
            $"/Type /XObject /Subtype /Image /Width {Page.Width} /Height {Page.Height} /ColorSpace /DeviceGray /BitsPerComponent 1 "
            + $"/Filter /JBIG2Decode /DecodeParms << /JBIG2Globals {globals} 0 R >>");
        var first = pdf.AddStream(entries, coded.Pages[0]);
        var second = pdf.AddStream(entries, coded.Pages[1]);
        pdf.Resources = $"/XObject << /Im1 {first} 0 R /Im2 {second} 0 R >>";
        var raster = PdfiumReferenceRendering.RenderPage(pdf.ToBytes(), 0, 1);

        var differences = 0;
        var pixels = raster.Pixels.Span;
        for (var y = 0; y < Page.Height; y++)
        {
            for (var x = 0; x < Page.Width; x++)
            {
                differences += (pixels[((y * raster.Width) + x) * BytesPerPixel] < BlackBelow) == Jbig2TestPages.IsBlack(left, Page.Width, x, y) ? 0 : 1;
                differences += (pixels[((y * raster.Width) + Page.Width + x) * BytesPerPixel] < BlackBelow) == Jbig2TestPages.IsBlack(right, Page.Width, x, y) ? 0 : 1;
            }
        }

        await Assert.That(differences).IsEqualTo(0);
    }

    /// <summary>Counts the pixels where PDFium's drawing of the coded image differs from the input.</summary>
    /// <param name="rows">The input samples.</param>
    /// <param name="coded">The coded image.</param>
    /// <returns>The number of differing pixels.</returns>
    private static int CountPdfiumDifferences(byte[] rows, Jbig2EncodedImage coded)
    {
        var width = Page.Width;
        var height = Page.Height;
        var pdf = new RenderTestPdf(width, height) { Content = string.Create(CultureInfo.InvariantCulture, $"q {width} 0 0 {height} 0 0 cm /Im Do Q") };
        var parameters = string.Empty;
        if (coded.Globals is not null)
        {
            var globals = pdf.AddStream(string.Empty, coded.Globals);
            parameters = string.Create(CultureInfo.InvariantCulture, $" /DecodeParms << /JBIG2Globals {globals} 0 R >>");
        }

        var entries = string.Create(
            CultureInfo.InvariantCulture,
            $"/Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceGray /BitsPerComponent 1 /Filter /JBIG2Decode{parameters}");
        var image = pdf.AddStream(entries, coded.Data);
        pdf.Resources = $"/XObject << /Im {image} 0 R >>";
        var raster = PdfiumReferenceRendering.RenderPage(pdf.ToBytes(), 0, 1);

        var differences = 0;
        var pixels = raster.Pixels.Span;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var rendered = pixels[(((y * raster.Width) + x) * BytesPerPixel)] < BlackBelow;
                differences += rendered == Jbig2TestPages.IsBlack(rows, width, x, y) ? 0 : 1;
            }
        }

        return differences;
    }
}
