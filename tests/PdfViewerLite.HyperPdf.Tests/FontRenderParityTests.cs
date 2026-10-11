// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Tests.Fonts;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.HyperPdf.Tests.RenderingComparison;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Renders text pages with PDFium and with HyperPDF's managed fonts and compares the pixels.</summary>
[NotInParallel(nameof(FontRenderParityTests))]
public sealed class FontRenderParityTests
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>
    /// The largest mean absolute difference per channel accepted for embedded fonts, which both engines draw from the
    /// same outlines. PDFium snaps glyph origins to whole pixels and rasterises glyphs itself, so edges differ slightly.
    /// </summary>
    private const double EmbeddedThreshold = 1.5;

    /// <summary>
    /// The largest mean difference accepted for a standard font. Both engines draw it from the same bundled Foxit
    /// faces, so only rasterisation differs (measured 0.085).
    /// </summary>
    private const double SubstituteThreshold = 0.5;

    /// <summary>The descriptor flags of a non-symbolic font.</summary>
    private const int Nonsymbolic = 32;

    /// <summary>The descriptor flags of a symbolic font.</summary>
    private const int Symbolic = 4;

    /// <summary>The text page content: two lines of mixed glyphs, spacing and a TJ adjustment.</summary>
    private const string SimpleText = "BT /F1 24 Tf 10 60 Td (Hello AVW mi) Tj 0 -30 Td 3 Tw [(a b) -500 (cd)] TJ ET";

    /// <summary>The composite text page content: CIDs of A, B and m.</summary>
    private const string CompositeText = "BT /F1 24 Tf 10 60 Td <00220023004E> Tj 0 -30 Td <0022> Tj ET";

    /// <summary>An embedded TrueType font matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task EmbeddedTrueTypeMatchesPdfium() => AssertParity(
        nameof(EmbeddedTrueTypeMatchesPdfium),
        FontTestDocument.Build(new() { Subtype = "TrueType", Program = TestFont.Create(), Flags = Nonsymbolic, Entries = "/Encoding /WinAnsiEncoding", Content = SimpleText }),
        EmbeddedThreshold);

    /// <summary>An embedded Type 1 font matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task EmbeddedType1MatchesPdfium()
    {
        var program = TestFontPrograms.Type1(out var length1);
        var spec = new FontSpec
        {
            Subtype = "Type1",
            Program = program,
            FileKey = "FontFile",
            FileEntries = string.Create(CultureInfo.InvariantCulture, $"/Length1 {length1}"),
            Flags = Symbolic,
            Content = SimpleText,
        };
        return AssertParity(nameof(EmbeddedType1MatchesPdfium), FontTestDocument.Build(spec), EmbeddedThreshold);
    }

    /// <summary>An embedded CFF font matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task EmbeddedCffMatchesPdfium() => AssertParity(
        nameof(EmbeddedCffMatchesPdfium),
        FontTestDocument.Build(new() { Subtype = "Type1", Program = TestFontPrograms.Cff(), FileKey = "FontFile3", FileEntries = "/Subtype /Type1C", Flags = Nonsymbolic, Content = SimpleText }),
        EmbeddedThreshold);

    /// <summary>An Identity-H CIDFontType2 font matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task EmbeddedCompositeMatchesPdfium() => AssertParity(
        nameof(EmbeddedCompositeMatchesPdfium),
        FontTestDocument.Build(new() { Subtype = "Type0", Program = TestFont.Create(), Flags = Symbolic, CidEntries = "/W [34 [500 500]]", Content = CompositeText }),
        EmbeddedThreshold);

    /// <summary>A Helvetica page, drawn with the bundled Foxit face, stays close to PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task StandardFontIsCloseToPdfium() =>
        AssertParity(nameof(StandardFontIsCloseToPdfium), TestPdf.Create(1), SubstituteThreshold);

    /// <summary>Renders a page with both engines and asserts the images are close.</summary>
    /// <param name="name">The test name.</param>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="threshold">The largest mean difference accepted.</param>
    /// <returns>A task.</returns>
    private static async Task AssertParity(string name, byte[] pdf, double threshold)
    {
        using var input = new HyperPdfTestDocument(pdf);
        await input.Document.PreparePageAsync(0, CancellationToken.None);
        var difference = Compare(input, new(0, 1, PageRotation.None, 0, 0, RenderFlags.FixedDeviceColors), out var images);
        TestContext.Current?.Output.WriteLine($"{name}: mean difference {difference:F3}");
        if (difference >= threshold)
        {
            images.Save(name);
        }

        await Assert.That(difference).IsLessThan(threshold);
    }

    /// <summary>Renders a page with both engines.</summary>
    /// <param name="input">The comparison PDF and HyperPDF document.</param>
    /// <param name="info">The page request.</param>
    /// <param name="images">The two renders.</param>
    /// <returns>The mean absolute difference per channel.</returns>
    /// <exception cref="InvalidOperationException">An engine could not render the page.</exception>
    private static double Compare(HyperPdfTestDocument input, in PageRenderInfo info, out ParityImages images)
    {
        var reference = PdfiumReferenceRendering.Render(input.Pdf, info);
        var width = reference.Width;
        var height = reference.Height;
        var expected = reference.Pixels.ToArray();
        var actual = new byte[expected.Length];
        if (!input.Document.Render(info, new(actual, width, height, width * BytesPerPixel)))
        {
            throw new InvalidOperationException("The page could not be rendered.");
        }

        images = new(expected, actual, width, height);
        long total = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            total += Math.Abs(expected[i] - actual[i]);
        }

        return (double)total / expected.Length;
    }
}
