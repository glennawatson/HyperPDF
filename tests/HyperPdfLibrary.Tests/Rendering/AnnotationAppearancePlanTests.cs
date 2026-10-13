// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>
/// Tests the generated annotation appearances one subtype at a time by sampling pixels, the colour spaces an annotation's
/// colour array can use, and the cache that keeps each generated appearance until the document's objects change.
/// </summary>
public sealed class AnnotationAppearancePlanTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 12;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>A device row near the top edge, empty when the cloud's top difference is large.</summary>
    private const int EmptyTopRow = 12;

    /// <summary>The first device row searched for the cloud.</summary>
    private const int CloudTopRow = 38;

    /// <summary>The last device row searched for the cloud.</summary>
    private const int CloudBottomRow = 92;

    /// <summary>The first device column sampled along the dashed border.</summary>
    private const int DashFirst = 12;

    /// <summary>The device column after the last one sampled along the dashed border.</summary>
    private const int DashEnd = 88;

    /// <summary>The device row of the dashed border's top edge.</summary>
    private const int DashRow = 41;

    /// <summary>The annotations on the page that has two.</summary>
    private const int AnnotationCount = 2;

    /// <summary>The flags that print and show an annotation.</summary>
    private const string Shown = "/F 4";

    /// <summary>The device row of the diagonal's middle on a page 100 points high.</summary>
    private const int DiagonalRow = 50;

    /// <summary>A device column on the left stroke of the path.</summary>
    private const int LeftStroke = 20;

    /// <summary>The rectangle of the annotation the cache tests use.</summary>
    private const string CacheSquare = "/Subtype /Square /Rect [20 20 80 80] /C [0 0 1] /IC [1 0 0] /BS << /W 4 >> /F 4";

    /// <summary>Each subtype draws its mark at a known place in a known colour.</summary>
    /// <param name="entries">The annotation entries.</param>
    /// <param name="x">The device column to sample.</param>
    /// <param name="y">The device row to sample.</param>
    /// <param name="red">The expected red channel.</param>
    /// <param name="green">The expected green channel.</param>
    /// <param name="blue">The expected blue channel.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/Subtype /Square /Rect [20 20 80 80] /C [0 0 1] /IC [1 0 0] /BS << /W 4 >>", 50, 50, 255, 0, 0)]
    [Arguments("/Subtype /Square /Rect [20 20 80 80] /C [0 0 1] /IC [1 0 0] /BS << /W 4 >>", 22, 50, 0, 0, 255)]
    [Arguments("/Subtype /Circle /Rect [20 20 80 80] /IC [0 1 0]", 50, 50, 0, 255, 0)]
    [Arguments("/Subtype /Circle /Rect [20 20 80 80] /IC [0 1 0]", 21, 21, 255, 255, 255)]
    [Arguments("/Subtype /Highlight /Rect [0 0 100 100] /QuadPoints [20 60 80 60 20 40 80 40] /C [0 1 1]", 50, 50, 0, 255, 255)]
    [Arguments("/Subtype /Underline /Rect [0 0 100 100] /QuadPoints [20 60 80 60 20 36 80 36] /C [1 0 0]", 50, 62, 255, 0, 0)]
    [Arguments("/Subtype /StrikeOut /Rect [0 0 100 100] /QuadPoints [20 60 80 60 20 36 80 36] /C [0 0 1]", 50, 52, 0, 0, 255)]
    [Arguments("/Subtype /Ink /Rect [10 40 90 60] /InkList [[10 50 90 50]] /C [1 0 1] /BS << /W 6 >>", 50, 50, 255, 0, 255)]
    [Arguments("/Subtype /Line /Rect [0 0 100 100] /L [10 50 90 50] /C [0 0 1] /BS << /W 6 >>", 50, 50, 0, 0, 255)]
    [Arguments("/Subtype /Polygon /Rect [0 0 100 100] /Vertices [20 20 80 20 80 80 20 80] /IC [1 0 0] /C [0 0 1]", 50, 50, 255, 0, 0)]
    [Arguments("/Subtype /Caret /Rect [20 20 80 80] /C [0 0 1]", 50, 50, 0, 0, 255)]
    [Arguments("/Subtype /Link /Rect [20 20 80 80] /C [0 1 0] /Border [0 0 4]", 22, 50, 0, 255, 0)]
    [Arguments("/Subtype /Redact /Rect [20 20 80 80] /C [0 0 1]", 20, 50, 0, 0, 255)]
    [Arguments("/Subtype /Redact /Rect [20 20 80 80] /C [0 0 1]", 50, 50, 255, 255, 255)]
    [Arguments("/Subtype /FreeText /Rect [20 20 80 80] /C [0 1 0] /DA (/Helv 12 Tf 0 g) /Contents (Hi)", 75, 75, 0, 255, 0)]
    public async Task EachSubtypeDrawsItsMark(string entries, int x, int y, int red, int green, int blue)
    {
        var image = Render($"{entries} {Shown}", PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, x, y, new(red, green, blue), Tolerance, nameof(EachSubtypeDrawsItsMark));
    }

    /// <summary>
    /// A gray, RGB or CMYK colour array each set the matching device colour (ISO 32000-2 12.5.2). DeviceCMYK to RGB is device
    /// dependent (ISO 32000-2 10.3.5), so the CMYK rows expect the library's own CMYK conversion, not the naive subtraction.
    /// </summary>
    /// <param name="color">The colour array.</param>
    /// <param name="red">The expected red channel.</param>
    /// <param name="green">The expected green channel.</param>
    /// <param name="blue">The expected blue channel.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("[0.5]", 128, 128, 128)]
    [Arguments("[1 0 0]", 255, 0, 0)]
    [Arguments("[0 0 0 1]", 35, 31, 32)]
    [Arguments("[0 1 1 0]", 234, 27, 11)]
    public async Task ColorArraysSetDeviceColors(string color, int red, int green, int blue)
    {
        var image = Render($"/Subtype /Ink /Rect [10 40 90 60] /InkList [[10 50 90 50]] /C {color} /BS << /W 6 >> {Shown}", PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, Middle, new(red, green, blue), Tolerance, nameof(ColorArraysSetDeviceColors));
    }

    /// <summary>A colour array of another length sets no colour instead of guessing one, so the default black stays.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnusableColorArrayLeavesTheDefaultColor()
    {
        var image = Render($"/Subtype /Ink /Rect [10 40 90 60] /InkList [[10 50 90 50]] /C [1 0] /BS << /W 6 >> {Shown}", PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, Middle, Rgb.Black, Tolerance, nameof(UnusableColorArrayLeavesTheDefaultColor));
    }

    /// <summary>A polygon closes its path back to the first vertex and a polyline does not (ISO 32000-2 12.5.6.9).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PolygonClosesItsPathAndPolyLineStaysOpen()
    {
        const string path = "/Rect [0 0 100 100] /Vertices [20 20 20 80 80 80] /C [1 0 0] /BS << /W 4 >>";
        var polygon = Render($"/Subtype /Polygon {path} {Shown}", PdfRenderFlags.Annotations);
        var polyline = Render($"/Subtype /PolyLine {path} {Shown}", PdfRenderFlags.Annotations);

        await RenderCheck.Near(polygon, Middle, DiagonalRow, Rgb.Red255, Tolerance, nameof(PolygonClosesItsPathAndPolyLineStaysOpen));
        await RenderCheck.Near(polyline, Middle, DiagonalRow, Rgb.White, Tolerance, nameof(PolygonClosesItsPathAndPolyLineStaysOpen));
        await RenderCheck.Near(polyline, LeftStroke, Middle, Rgb.Red255, Tolerance, nameof(PolygonClosesItsPathAndPolyLineStaysOpen));
    }

    /// <summary>The differences of <c>/RD</c> run left, top, right, bottom (ISO 32000-2 table 180): a cloud with a large top difference leaves the top of the rectangle empty.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CloudDifferencesRunLeftTopRightBottom()
    {
        var image = Render($"/Subtype /Square /Rect [10 10 90 90] /RD [0 30 0 0] /BE << /S /C /I 1 >> /C [0 0 1] /BS << /W 2 >> {Shown}", PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, EmptyTopRow, Rgb.White, Tolerance, nameof(CloudDifferencesRunLeftTopRightBottom));
        await Assert.That(HasColorInRows(image, CloudTopRow, CloudBottomRow, Rgb.Blue255)).IsTrue();
    }

    /// <summary>A dashed border (<c>/BS /S /D</c>) leaves gaps along its line.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DashedBorderLeavesGaps()
    {
        var image = Render($"/Subtype /Square /Rect [10 40 90 60] /C [0 0 0] /BS << /W 4 /S /D /D [8 8] >> {Shown}", PdfRenderFlags.Annotations);
        var inked = 0;
        for (var column = DashFirst; column < DashEnd; column++)
        {
            inked += image.IsNear(column, DashRow, Rgb.Black, Tolerance) ? 1 : 0;
        }

        await Assert.That(inked).IsGreaterThan(0);
        await Assert.That(inked).IsLessThan(DashEnd - DashFirst);
    }

    /// <summary>The Hidden, NoView and Print flags decide whether a generated appearance is drawn on screen and on paper (ISO 32000-2 12.5.3).</summary>
    /// <param name="flags">The /F value.</param>
    /// <param name="printing">Whether the page is rendered for printing.</param>
    /// <param name="drawn">Whether the mark is expected.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0, false, true)]
    [Arguments(4, false, true)]
    [Arguments(2, false, false)]
    [Arguments(2, true, false)]
    [Arguments(32, false, false)]
    [Arguments(36, true, true)]
    [Arguments(0, true, false)]
    public async Task FlagsDecideWhereAGeneratedAppearanceIsDrawn(int flags, bool printing, bool drawn)
    {
        var render = printing ? PdfRenderFlags.Annotations | PdfRenderFlags.Printing : PdfRenderFlags.Annotations;
        var image = Render($"/Subtype /Square /Rect [20 20 80 80] /IC [1 0 0] /F {flags}", render);

        await RenderCheck.Near(image, Middle, Middle, drawn ? Rgb.Red255 : Rgb.White, Tolerance, nameof(FlagsDecideWhereAGeneratedAppearanceIsDrawn));
    }

    /// <summary>A generated appearance is made once per annotation and object revision, so drawing the page again reuses it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppearanceIsReusedUntilTheObjectsChange()
    {
        using var page = CreateCachePage(out var context);
        var first = AnnotationAppearances.Generate(context);
        var second = AnnotationAppearances.Generate(context);

        await Assert.That(first).IsNotNull();
        await Assert.That(ReferenceEquals(first?.Form, second?.Form)).IsTrue();
        await Assert.That(context.Cache.Appearances.Count).IsEqualTo(1);

        PdfDocumentEditing.InvalidateCaches(page.Document);
        var third = AnnotationAppearances.Generate(context);

        await Assert.That(ReferenceEquals(first?.Form, third?.Form)).IsFalse();
        await Assert.That(third).IsNotNull();
    }

    /// <summary>Two annotations with equal entries get appearances of their own, because the cache is keyed by the annotation itself.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EqualAnnotationsKeepSeparateAppearances()
    {
        var pdf = new RenderTestPdf(Size, Size);
        var one = pdf.AddObject($"<< /Type /Annot {CacheSquare} >>");
        var two = pdf.AddObject($"<< /Type /Annot {CacheSquare} >>");
        pdf.PageEntries = $"/Annots [{one} 0 R {two} 0 R]";
        using var page = new RenderTestPage(pdf.ToBytes());
        var cache = new PdfRenderCache(page.Document);
        var pageModel = PdfDocumentPages.GetPage(page.Document, 0);
        var annotations = pageModel.Dictionary.GetArray(KnownName.Annots)!;

        var first = AnnotationAppearances.Generate(new(cache, pageModel, annotations.GetDictionary(0)!));
        var second = AnnotationAppearances.Generate(new(cache, pageModel, annotations.GetDictionary(1)!));

        await Assert.That(ReferenceEquals(first?.Form, second?.Form)).IsFalse();
        await Assert.That(cache.Appearances.Count).IsEqualTo(AnnotationCount);
    }

    /// <summary>Editing an annotation in a transaction makes the next render show the edit, not the appearance drawn before it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditingAnAnnotationInATransactionRedrawsIt()
    {
        var pdf = new RenderTestPdf(Size, Size);
        var annotation = pdf.AddObject($"<< /Type /Annot {CacheSquare} >>");
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";
        using var page = new RenderTestPage(pdf.ToBytes());
        var before = page.RenderPage(1, 0, PdfRenderFlags.Annotations);

        var dictionary = PdfDocumentPages.GetPage(page.Document, 0).Dictionary.GetArray(KnownName.Annots)!.GetDictionary(0)!;
        using (var transaction = PdfDocumentEditing.BeginEdit(page.Document, "Recolour"))
        {
            dictionary.Set(KnownName.IC, PdfValue.FromArray(PdfArray.FromNumbers(page.Document.Objects, [0F, 1F, 0F])));
            transaction.Commit();
        }

        var after = page.RenderPage(1, 0, PdfRenderFlags.Annotations);

        await RenderCheck.Near(before, Middle, Middle, Rgb.Red255, Tolerance, nameof(EditingAnAnnotationInATransactionRedrawsIt));
        await RenderCheck.Near(after, Middle, Middle, Rgb.Green255, Tolerance, nameof(EditingAnAnnotationInATransactionRedrawsIt));
    }

    /// <summary>Undoing the edit brings the first appearance back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndoRedrawsTheEarlierAppearance()
    {
        var pdf = new RenderTestPdf(Size, Size);
        var annotation = pdf.AddObject($"<< /Type /Annot {CacheSquare} >>");
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";
        using var page = new RenderTestPage(pdf.ToBytes());
        _ = page.RenderPage(1, 0, PdfRenderFlags.Annotations);

        var dictionary = PdfDocumentPages.GetPage(page.Document, 0).Dictionary.GetArray(KnownName.Annots)!.GetDictionary(0)!;
        using (var transaction = PdfDocumentEditing.BeginEdit(page.Document, "Recolour"))
        {
            dictionary.Set(KnownName.IC, PdfValue.FromArray(PdfArray.FromNumbers(page.Document.Objects, [0F, 1F, 0F])));

            // Replacing the object records it, so the history can put the first version back.
            StoreEditing.Replace(page.Document.Objects, new(annotation, 0), PdfValue.FromDictionary(dictionary));
            transaction.Commit();
        }

        _ = page.RenderPage(1, 0, PdfRenderFlags.Annotations);
        await Assert.That(PdfDocumentEditing.Undo(page.Document)).IsTrue();
        var undone = page.RenderPage(1, 0, PdfRenderFlags.Annotations);

        await RenderCheck.Near(undone, Middle, Middle, Rgb.Red255, Tolerance, nameof(UndoRedrawsTheEarlierAppearance));
    }

    /// <summary>Renders a page with one annotation.</summary>
    /// <param name="entries">The annotation's entries other than /Type.</param>
    /// <param name="flags">The render flags.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(string entries, PdfRenderFlags flags)
    {
        var pdf = new RenderTestPdf(Size, Size);
        var annotation = pdf.AddObject($"<< /Type /Annot {entries} >>");
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";
        using var page = new RenderTestPage(pdf.ToBytes());
        return page.RenderPage(1, 0, flags);
    }

    /// <summary>Makes a page with one annotation and the context that generates its appearance.</summary>
    /// <param name="context">Receives the context.</param>
    /// <returns>The page, which the caller disposes.</returns>
    private static RenderTestPage CreateCachePage(out AnnotationContext context)
    {
        var pdf = new RenderTestPdf(Size, Size);
        var annotation = pdf.AddObject($"<< /Type /Annot {CacheSquare} >>");
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";
        var page = new RenderTestPage(pdf.ToBytes());
        var pageModel = PdfDocumentPages.GetPage(page.Document, 0);
        context = new(new PdfRenderCache(page.Document), pageModel, pageModel.Dictionary.GetArray(KnownName.Annots)!.GetDictionary(0)!);
        return page;
    }

    /// <summary>Determines whether any pixel between two rows of the middle column is near a colour.</summary>
    /// <param name="image">The image.</param>
    /// <param name="top">The first row.</param>
    /// <param name="bottom">The last row.</param>
    /// <param name="expected">The colour.</param>
    /// <returns><see langword="true"/> when one is.</returns>
    private static bool HasColorInRows(RenderedImage image, int top, int bottom, Rgb expected)
    {
        for (var row = top; row <= bottom; row++)
        {
            for (var column = 0; column < Size; column++)
            {
                if (image.IsNear(column, row, expected, Tolerance))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
