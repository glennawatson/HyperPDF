// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Renders tiles: offsets, scale, rotation, layers, annotations and grayscale.</summary>
public sealed class PageRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>Content that paints a red square in the page's bottom-left corner.</summary>
    private const string SmallRedSquare = "1 0 0 rg 0 0 10 10 re f";

    /// <summary>The height of the wide page used for rotation.</summary>
    private const int ShortSide = 50;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 6;

    /// <summary>A coordinate near the start of an axis.</summary>
    private const int Near = 5;

    /// <summary>A pixel inside the red square after a tile offset.</summary>
    private const int TileInside = 20;

    /// <summary>The offset of the tile in the full page image.</summary>
    private const int TileOffset = 50;

    /// <summary>The size of the tile.</summary>
    private const int TileSize = 50;

    /// <summary>The scale of the scaled render.</summary>
    private const float DoubleScale = 2;

    /// <summary>The column of a pixel inside the square at double scale.</summary>
    private const int ScaledX = 140;

    /// <summary>The row of a pixel inside the square at double scale.</summary>
    private const int ScaledY = 160;

    /// <summary>The row of a pixel inside the red square after a tile offset.</summary>
    private const int TileInsideRow = 30;

    /// <summary>The device column and row of the annotation's centre.</summary>
    private const int AnnotationCentre = 20;

    /// <summary>The device row of the annotation's centre with y flipped.</summary>
    private const int AnnotationRow = 80;

    /// <summary>The luma of pure red scaled to a byte, as grayscale gives it.</summary>
    private const int RedLuma = 76;

    /// <summary>The largest difference accepted in a grayscale conversion.</summary>
    private const int GrayTolerance = 10;

    /// <summary>The annotation flag that hides it.</summary>
    private const int HiddenFlag = 2;

    /// <summary>The annotation flag that includes it when printing.</summary>
    private const int PrintFlag = 4;

    /// <summary>A tile is cut from the full page image at its offset.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TileOffsetSelectsPartOfThePage()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "1 0 0 rg 60 10 20 20 re f" };
        using var page = new RenderTestPage(pdf.ToBytes());

        var tile = page.RenderTile(new(0, 1, 0, TileOffset, TileOffset, PdfRenderFlags.None), TileSize, TileSize);

        await Assert.That(tile.IsNear(TileInside, TileInsideRow, Rgb.Red255, Tolerance)).IsTrue();
        await Assert.That(tile.IsNear(Near, Near, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>The scale sets the size of the full page image.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScaleEnlargesThePage()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "1 0 0 rg 60 10 20 20 re f" };
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage(DoubleScale, 0, PdfRenderFlags.None);

        await Assert.That(image.Width).IsEqualTo((int)(Size * DoubleScale));
        await Assert.That(image.IsNear(ScaledX, ScaledY, Rgb.Red255, Tolerance)).IsTrue();
    }

    /// <summary>The page's own /Rotate turns the page clockwise.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageRotationTurnsTheContent()
    {
        var pdf = new RenderTestPdf(Size, ShortSide) { Content = SmallRedSquare, PageEntries = "/Rotate 90" };
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.Width).IsEqualTo(ShortSide);
        await Assert.That(image.Height).IsEqualTo(Size);
        await Assert.That(image.IsNear(Near, Near, Rgb.Red255, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(ShortSide - Near, Size - Near, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>An extra rotation in the request turns the page again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtraRotationTurnsTheImage()
    {
        var pdf = new RenderTestPdf(Size, ShortSide) { Content = SmallRedSquare };
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage(1, 1, PdfRenderFlags.None);

        await Assert.That(image.Width).IsEqualTo(ShortSide);
        await Assert.That(image.Height).IsEqualTo(Size);
        await Assert.That(image.IsNear(Near, Near, Rgb.Red255, Tolerance)).IsTrue();
    }

    /// <summary>Content in a hidden optional content group is skipped until the group is shown.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HiddenLayerIsSkippedUntilShown()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/OC /MC0 BDC 1 0 0 rg 0 0 100 100 re f EMC" };
        var group = pdf.AddObject("<< /Type /OCG /Name (Layer) >>");
        pdf.Resources = $"/Properties << /MC0 {group} 0 R >>";
        pdf.CatalogEntries = $"/OCProperties << /OCGs [{group} 0 R] /D << /OFF [{group} 0 R] >> >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var hidden = page.RenderPage();
        var changed = HyperPdfLibrary.Document.PdfDocumentLayers.GetOptionalContent(page.Document).SetVisible(group, true);
        var shown = page.RenderPage();

        await Assert.That(changed).IsTrue();
        await Assert.That(hidden.IsNear(TileSize, TileSize, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(shown.IsNear(TileSize, TileSize, Rgb.Red255, Tolerance)).IsTrue();
    }

    /// <summary>An annotation's appearance is drawn into its rectangle when annotations are on.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationAppearanceIsDrawnWhenRequested()
    {
        using var page = new RenderTestPage(CreateAnnotationPdf(0));

        var without = page.RenderPage();
        var with = page.RenderPage(1, 0, PdfRenderFlags.Annotations);

        await Assert.That(without.IsNear(AnnotationCentre, AnnotationRow, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(with.IsNear(AnnotationCentre, AnnotationRow, Rgb.Red255, Tolerance)).IsTrue();
    }

    /// <summary>A hidden annotation is not drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HiddenAnnotationIsNotDrawn()
    {
        using var page = new RenderTestPage(CreateAnnotationPdf(HiddenFlag));

        var image = page.RenderPage(1, 0, PdfRenderFlags.Annotations);

        await Assert.That(image.IsNear(AnnotationCentre, AnnotationRow, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>When printing, an annotation without the Print flag is left out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PrintingNeedsThePrintFlag()
    {
        using var unflagged = new RenderTestPage(CreateAnnotationPdf(0));
        using var flagged = new RenderTestPage(CreateAnnotationPdf(PrintFlag));
        const PdfRenderFlags Printing = PdfRenderFlags.Annotations | PdfRenderFlags.Printing;

        var left = unflagged.RenderPage(1, 0, Printing);
        var kept = flagged.RenderPage(1, 0, Printing);

        await Assert.That(left.IsNear(AnnotationCentre, AnnotationRow, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(kept.IsNear(AnnotationCentre, AnnotationRow, Rgb.Red255, Tolerance)).IsTrue();
    }

    /// <summary>The grayscale flag converts colours to gray.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GrayscaleRemovesColor()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "1 0 0 rg 0 0 100 100 re f" };
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage(1, 0, PdfRenderFlags.Grayscale);

        await Assert.That(image.IsNear(TileSize, TileSize, new(RedLuma, RedLuma, RedLuma), GrayTolerance)).IsTrue();
    }

    /// <summary>Creates a page with one red annotation square at (10, 10) to (30, 30).</summary>
    /// <param name="flags">The annotation flags.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateAnnotationPdf(int flags)
    {
        var pdf = new RenderTestPdf(Size, Size);
        var appearance = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 10 10]", SmallRedSquare);
        var annotation = pdf.AddObject($"<< /Type /Annot /Subtype /Square /Rect [10 10 30 30] /F {flags} /AP << /N {appearance} 0 R >> >>");
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";
        return pdf.ToBytes();
    }
}
