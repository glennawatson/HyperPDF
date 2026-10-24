// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;

namespace HyperPdfLibrary.Tests.PageObjects;

/// <summary>Reads page content into objects.</summary>
public sealed class PageObjectParseTests
{
    /// <summary>The number of objects on the mixed page.</summary>
    private const int MixedCount = 6;

    /// <summary>The tolerance for positions in points.</summary>
    private const float Tolerance = 0.01F;

    /// <summary>The first glyph's x and the rectangle's edges on the mixed page.</summary>
    private const float Left = 10;

    /// <summary>The text baseline on the mixed page.</summary>
    private const float Baseline = 150;

    /// <summary>The characters in "Hello World".</summary>
    private const int HelloWorldLength = 11;

    /// <summary>The font size of the text on the mixed page.</summary>
    private const float TextSize = 18;

    /// <summary>The right edge of the red rectangle.</summary>
    private const float RectangleRight = 70;

    /// <summary>The top edge of the red rectangle.</summary>
    private const float RectangleTop = 50;

    /// <summary>The left edge of the image.</summary>
    private const float ImageLeft = 120;

    /// <summary>The right edge of the image.</summary>
    private const float ImageRight = 170;

    /// <summary>The top edge of the image.</summary>
    private const float ImageTop = 70;

    /// <summary>The bottom edge of the image and of the shading's clip.</summary>
    private const float ImageBottom = 20;

    /// <summary>The bytes of the 2 by 2 RGB image.</summary>
    private const int ImageBytes = 12;

    /// <summary>The bottom of the shading's clip.</summary>
    private const float ClipBottom = 60;

    /// <summary>The top of the shading's clip.</summary>
    private const float ClipTop = 100;

    /// <summary>The right edge of the shading's clip.</summary>
    private const float ClipRight = 140;

    /// <summary>The left edge of the form and of the shading's clip.</summary>
    private const float FormLeft = 30;

    /// <summary>The bottom edge of the form.</summary>
    private const float FormBottom = 100;

    /// <summary>The form's size.</summary>
    private const float FormSize = 40;

    /// <summary>The left edge of the inline image.</summary>
    private const float InlineLeft = 170;

    /// <summary>The inline image's size.</summary>
    private const float InlineSize = 10;

    /// <summary>The width and height of the image in pixels.</summary>
    private const int ImageSide = 2;

    /// <summary>The right edge of the clip on the clip sample.</summary>
    private const float ClipEdge = 60;

    /// <summary>The top of the stroked curve: its peak plus half the default line width.</summary>
    private const float CurvePeak = 85.5F;

    /// <summary>The red component of the rectangle's fill.</summary>
    private const float RectangleRed = 0.9F;

    /// <summary>Every kind of object is found, in painting order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsEveryKindOfObjectInPaintingOrder()
    {
        using var document = HyperPdfLibrary.Document.PdfDocumentReader.Open(PageObjectSamples.Mixed(), null);
        var content = HyperPdfLibrary.Document.PdfDocumentPageContent.GetPageContent(document, 0);

        await Assert.That(content.Objects.Count).IsEqualTo(MixedCount);
        await Assert.That(content.Objects[0].Kind).IsEqualTo(PdfPageObjectKind.Path);
        await Assert.That(content.Objects[1].Kind).IsEqualTo(PdfPageObjectKind.Text);
        await Assert.That(content.Objects[2].Kind).IsEqualTo(PdfPageObjectKind.Image);
        await Assert.That(content.Objects[3].Kind).IsEqualTo(PdfPageObjectKind.Shading);
        await Assert.That(content.Objects[4].Kind).IsEqualTo(PdfPageObjectKind.Form);
        await Assert.That(content.Objects[5].Kind).IsEqualTo(PdfPageObjectKind.Image);
    }

    /// <summary>A filled path has its colour, mode and bounds.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PathCarriesColourModeAndBounds()
    {
        using var document = HyperPdfLibrary.Document.PdfDocumentReader.Open(PageObjectSamples.Mixed(), null);
        var path = (PdfPathObject)HyperPdfLibrary.Document.PdfDocumentPageContent.GetPageContent(document, 0).Objects[0];

        await Assert.That(path.PaintMode).IsEqualTo(PdfPathPaintMode.Fill);
        await Assert.That(path.FillPaint.Components[0]).IsEqualTo(RectangleRed).Within(Tolerance);
        await Assert.That(path.Bounds.Left).IsEqualTo(Left).Within(Tolerance);
        await Assert.That(path.Bounds.Right).IsEqualTo(RectangleRight).Within(Tolerance);
        await Assert.That(path.Bounds.Top).IsEqualTo(RectangleTop).Within(Tolerance);
        await Assert.That(path.Segments.Length).IsEqualTo(1);
        await Assert.That(path.Segments[0].Kind).IsEqualTo(PdfPathSegmentKind.Rectangle);
    }

    /// <summary>A text object has its font, size, text and glyph positions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextCarriesFontSizeAndGlyphs()
    {
        using var document = HyperPdfLibrary.Document.PdfDocumentReader.Open(PageObjectSamples.Mixed(), null);
        var text = (PdfTextObject)HyperPdfLibrary.Document.PdfDocumentPageContent.GetPageContent(document, 0).Objects[1];

        await Assert.That(text.Text).IsEqualTo("Hello World");
        await Assert.That(text.GlyphCount).IsEqualTo(HelloWorldLength);
        await Assert.That(text.FontSize).IsEqualTo(TextSize).Within(Tolerance);
        await Assert.That(text.Font).IsNotNull();
        await Assert.That(text.RenderMode).IsEqualTo(0);
        await Assert.That(text.Glyphs[0].Origin.X).IsEqualTo(Left).Within(Tolerance);
        await Assert.That(text.Glyphs[0].Origin.Y).IsEqualTo(Baseline).Within(Tolerance);
        await Assert.That(text.Glyphs[1].Origin.X).IsGreaterThan(text.Glyphs[0].Origin.X);
        await Assert.That(text.Glyphs[0].Box.Top).IsGreaterThan(Baseline);
        await Assert.That(text.GetGlyphText(0).ToString()).IsEqualTo("H");
    }

    /// <summary>An image XObject exposes its size, filters and data and sits in the unit square under the matrix.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImageCarriesSizeAndData()
    {
        using var document = HyperPdfLibrary.Document.PdfDocumentReader.Open(PageObjectSamples.Mixed(), null);
        var image = (PdfImageObject)HyperPdfLibrary.Document.PdfDocumentPageContent.GetPageContent(document, 0).Objects[2];

        await Assert.That(image.IsInline).IsFalse();
        await Assert.That(image.Width).IsEqualTo(ImageSide);
        await Assert.That(image.Height).IsEqualTo(ImageSide);
        await Assert.That(image.Filters.Count).IsEqualTo(0);
        await Assert.That(image.GetRawData().Length).IsEqualTo(ImageBytes);
        await Assert.That(image.GetDecodedData().Length).IsEqualTo(ImageBytes);
        await Assert.That(image.Bounds.Left).IsEqualTo(ImageLeft).Within(Tolerance);
        await Assert.That(image.Bounds.Right).IsEqualTo(ImageRight).Within(Tolerance);
        await Assert.That(image.Bounds.Bottom).IsEqualTo(ImageBottom).Within(Tolerance);
        await Assert.That(image.Bounds.Top).IsEqualTo(ImageTop).Within(Tolerance);
        await Assert.That(image.DecodePixels(null)).IsNotNull();
    }

    /// <summary>A shading is bounded by the clip it is painted under, and a clip-only path is not an object.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShadingTakesTheClipAsBounds()
    {
        using var document = HyperPdfLibrary.Document.PdfDocumentReader.Open(PageObjectSamples.Mixed(), null);
        var shading = (PdfShadingObject)HyperPdfLibrary.Document.PdfDocumentPageContent.GetPageContent(document, 0).Objects[3];

        await Assert.That(shading.IsClipped).IsTrue();
        await Assert.That(shading.ClipPaths.Count).IsEqualTo(1);
        await Assert.That(shading.Bounds.Left).IsEqualTo(ClipRight - FormSize).Within(Tolerance);
        await Assert.That(shading.Bounds.Right).IsEqualTo(ClipRight).Within(Tolerance);
        await Assert.That(shading.Bounds.Bottom).IsEqualTo(ClipBottom).Within(Tolerance);
        await Assert.That(shading.Bounds.Top).IsEqualTo(ClipTop).Within(Tolerance);
    }

    /// <summary>A form's own content is read on demand with its matrix applied.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormContentIsReadWithItsMatrix()
    {
        using var document = HyperPdfLibrary.Document.PdfDocumentReader.Open(PageObjectSamples.Mixed(), null);
        var form = (PdfFormObject)HyperPdfLibrary.Document.PdfDocumentPageContent.GetPageContent(document, 0).Objects[4];
        var inner = form.GetContent();

        await Assert.That(form.Bounds.Left).IsEqualTo(FormLeft).Within(Tolerance);
        await Assert.That(form.Bounds.Bottom).IsEqualTo(FormBottom).Within(Tolerance);
        await Assert.That(inner.Objects.Count).IsEqualTo(1);
        await Assert.That(inner.Objects[0].Bounds.Left).IsEqualTo(FormLeft).Within(Tolerance);
        await Assert.That(inner.Objects[0].Bounds.Right).IsEqualTo(FormLeft + FormSize).Within(Tolerance);
        await Assert.That(inner.Objects[0].FillPaint.Components[2]).IsEqualTo(1);
    }

    /// <summary>An inline image has its dictionary, data and bounds.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InlineImageIsRead()
    {
        using var document = HyperPdfLibrary.Document.PdfDocumentReader.Open(PageObjectSamples.Mixed(), null);
        var image = (PdfImageObject)HyperPdfLibrary.Document.PdfDocumentPageContent.GetPageContent(document, 0).Objects[5];

        await Assert.That(image.IsInline).IsTrue();
        await Assert.That(image.Width).IsEqualTo(1);
        await Assert.That(image.GetRawData().Length).IsEqualTo(1);
        await Assert.That(image.Bounds.Left).IsEqualTo(InlineLeft).Within(Tolerance);
        await Assert.That(image.Bounds.Right).IsEqualTo(InlineLeft + InlineSize).Within(Tolerance);
    }

    /// <summary>Marked content marks, with their /MCID, are carried by the objects inside them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ObjectsCarryTheirMarkedContent()
    {
        const int Mcid = 7;
        var pdf = PageObjectSamples.Page($"/P << /MCID {Mcid} >> BDC BT /F1 12 Tf 10 100 Td (Tagged) Tj ET EMC BT /F1 12 Tf 10 50 Td (Plain) Tj ET");
        using var document = HyperPdfLibrary.Document.PdfDocumentReader.Open(pdf, null);
        var content = HyperPdfLibrary.Document.PdfDocumentPageContent.GetPageContent(document, 0);

        await Assert.That(content.Objects[0].Marks.Count).IsEqualTo(1);
        await Assert.That(content.Objects[0].Marks[0].MarkedContentId).IsEqualTo(Mcid);
        await Assert.That(content.Objects[0].Marks[0].Tag.Is(KnownName.P)).IsTrue();
        await Assert.That(content.Objects[1].Marks.Count).IsEqualTo(0);
    }

    /// <summary>A rectangle clip becomes the clip bounds of the objects painted after it, inside the saved state only.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClipBoundsFollowSaveAndRestore()
    {
        var pdf = PageObjectSamples.Page("q 10 10 50 50 re W n 0 0 100 100 re f Q 0 0 100 100 re f");
        using var document = HyperPdfLibrary.Document.PdfDocumentReader.Open(pdf, null);
        var content = HyperPdfLibrary.Document.PdfDocumentPageContent.GetPageContent(document, 0);

        await Assert.That(content.Objects[0].IsClipped).IsTrue();
        await Assert.That(content.Objects[0].ClipBounds.Right).IsEqualTo(ClipEdge).Within(Tolerance);
        await Assert.That(content.Objects[1].IsClipped).IsFalse();
    }

    /// <summary>Curves are measured exactly, not by their control points.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CurveBoundsFollowTheCurve()
    {
        var pdf = PageObjectSamples.Page("10 10 m 10 110 90 110 90 10 c S");
        using var document = HyperPdfLibrary.Document.PdfDocumentReader.Open(pdf, null);
        var path = (PdfPathObject)HyperPdfLibrary.Document.PdfDocumentPageContent.GetPageContent(document, 0).Objects[0];

        // The curve peaks at three quarters of the control height: 10 + 0.75 * 100 = 85, plus half the default line width.
        await Assert.That(path.Bounds.Top).IsEqualTo(CurvePeak).Within(Tolerance);
        await Assert.That(path.Bounds.Left).IsLessThan(Left);
    }
}
