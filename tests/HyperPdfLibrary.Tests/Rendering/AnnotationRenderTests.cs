// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Renders annotations without appearance streams, the annotation flags, optional content and printing.</summary>
public sealed class AnnotationRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 10;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>A channel at half strength over white.</summary>
    private const int Half = 128;

    /// <summary>The device column on a 4 point border drawn inside x 20.</summary>
    private const int BorderColumn = 22;

    /// <summary>A device coordinate just inside the corner of the 20 to 80 rectangle.</summary>
    private const int Corner = 21;

    /// <summary>The scale used for one point lines, so they cover whole pixels.</summary>
    private const float LineScale = 2;

    /// <summary>The device row, at two times scale, of an underline one point above y 40.</summary>
    private const int UnderlineRow = 117;

    /// <summary>The device row, at two times scale, of a line through y 50.</summary>
    private const int StrikeRow = 99;

    /// <summary>The device column, at two times scale, of the page middle.</summary>
    private const int ScaledMiddle = 100;

    /// <summary>The first device row, at two times scale, searched for the squiggle.</summary>
    private const int SquiggleTop = 112;

    /// <summary>The last device row, at two times scale, searched for the squiggle.</summary>
    private const int SquiggleBottom = 122;

    /// <summary>Content that paints a green square.</summary>
    private const string GreenSquare = "0 1 0 rg 0 0 10 10 re f";

    /// <summary>The device column inside the note icon's body and clear of its lines.</summary>
    private const int NoteColumn = 20;

    /// <summary>The device row inside the note icon's body, above its first line; the icon sits at the upper left of /Rect.</summary>
    private const int NoteRow = 42;

    /// <summary>The device row inside the polygon.</summary>
    private const int PolygonRow = 60;

    /// <summary>The device coordinate in the bottom-right of a free text box, clear of its text.</summary>
    private const int FreeTextCorner = 75;

    /// <summary>The device row inside the 20 to 40 high widget.</summary>
    private const int WidgetRow = 70;

    /// <summary>The device column at the right of the widget, clear of its text.</summary>
    private const int WidgetColumn = 75;

    /// <summary>The first device column searched for a cloudy border.</summary>
    private const int CloudLeft = 18;

    /// <summary>The last device column searched for a cloudy border.</summary>
    private const int CloudRight = 30;

    /// <summary>The entries of a ten point appearance stream.</summary>
    private const string SmallForm = "/Type /XObject /Subtype /Form /BBox [0 0 10 10]";

    /// <summary>A square with interior and border colours.</summary>
    private const string Square = "/Subtype /Square /Rect [20 20 80 80] /C [0 0 1] /IC [1 0 0] /BS << /W 4 >>";

    /// <summary>A Square annotation without an appearance draws its interior and border.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SquareWithoutAppearanceIsGenerated()
    {
        var image = RenderAnnotation($"{Square} /F 4", 1, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, Middle, Rgb.Red255, Tolerance, nameof(SquareWithoutAppearanceIsGenerated));
        await RenderCheck.Near(image, BorderColumn, Middle, Rgb.Blue255, Tolerance, nameof(SquareWithoutAppearanceIsGenerated));
    }

    /// <summary>The /CA opacity applies to a generated appearance.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpacityAppliesToGeneratedAppearances()
    {
        var image = RenderAnnotation($"{Square} /CA 0.5", 1, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, Middle, new(Rgb.Red255.Red, Half, Half), Tolerance, nameof(OpacityAppliesToGeneratedAppearances));
    }

    /// <summary>A Hidden annotation is never drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HiddenAnnotationsAreNotDrawn()
    {
        var image = RenderAnnotation($"{Square} /F 2", 1, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, Middle, Rgb.White, Tolerance, nameof(HiddenAnnotationsAreNotDrawn));
    }

    /// <summary>A NoView annotation is left off the screen but printed when it has the Print flag.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoViewAnnotationsOnlyPrint()
    {
        var onScreen = RenderAnnotation($"{Square} /F 36", 1, PdfRenderFlags.Annotations);
        var printed = RenderAnnotation($"{Square} /F 36", 1, PdfRenderFlags.Annotations | PdfRenderFlags.Printing);

        await RenderCheck.Near(onScreen, Middle, Middle, Rgb.White, Tolerance, nameof(NoViewAnnotationsOnlyPrint));
        await RenderCheck.Near(printed, Middle, Middle, Rgb.Red255, Tolerance, nameof(NoViewAnnotationsOnlyPrint));
    }

    /// <summary>An annotation without the Print flag is left out when printing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationsWithoutThePrintFlagDoNotPrint()
    {
        var image = RenderAnnotation($"{Square} /F 0", 1, PdfRenderFlags.Annotations | PdfRenderFlags.Printing);

        await RenderCheck.Near(image, Middle, Middle, Rgb.White, Tolerance, nameof(AnnotationsWithoutThePrintFlagDoNotPrint));
    }

    /// <summary>A Circle annotation fills an ellipse that leaves the rectangle's corners empty.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CircleWithoutAppearanceIsGenerated()
    {
        var image = RenderAnnotation("/Subtype /Circle /Rect [20 20 80 80] /IC [0 1 0]", 1, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, Middle, Rgb.Green255, Tolerance, nameof(CircleWithoutAppearanceIsGenerated));
        await RenderCheck.Near(image, Corner, Corner, Rgb.White, Tolerance, nameof(CircleWithoutAppearanceIsGenerated));
    }

    /// <summary>A Highlight annotation fills its quadrilaterals in yellow by default.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HighlightFillsItsQuadrilaterals()
    {
        var image = RenderAnnotation("/Subtype /Highlight /Rect [0 0 100 100] /QuadPoints [20 60 80 60 20 40 80 40]", 1, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, Middle, Rgb.Yellow, Tolerance, nameof(HighlightFillsItsQuadrilaterals));
    }

    /// <summary>An Underline annotation draws a line one point above the bottom of each quadrilateral.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnderlineDrawsALine()
    {
        var image = RenderAnnotation("/Subtype /Underline /Rect [0 0 100 100] /QuadPoints [20 60 80 60 20 40 80 40]", LineScale, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, ScaledMiddle, UnderlineRow, Rgb.Black, Tolerance, nameof(UnderlineDrawsALine));
    }

    /// <summary>A StrikeOut annotation draws a line through the middle of each quadrilateral.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrikeOutDrawsALineThroughTheMiddle()
    {
        var image = RenderAnnotation("/Subtype /StrikeOut /Rect [0 0 100 100] /QuadPoints [20 60 80 60 20 40 80 40] /C [1 0 0]", LineScale, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, ScaledMiddle, StrikeRow, Rgb.Red255, Tolerance, nameof(StrikeOutDrawsALineThroughTheMiddle));
    }

    /// <summary>A Squiggly annotation draws a zigzag along the bottom of each quadrilateral.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SquigglyDrawsAZigzag()
    {
        var image = RenderAnnotation("/Subtype /Squiggly /Rect [0 0 100 100] /QuadPoints [20 60 80 60 20 40 80 40]", LineScale, PdfRenderFlags.Annotations);

        await Assert.That(AnyNear(image, ScaledMiddle, SquiggleTop, SquiggleBottom, Rgb.Black)).IsTrue();
    }

    /// <summary>An Ink annotation strokes each path of its ink list.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InkStrokesItsPaths()
    {
        var image = RenderAnnotation("/Subtype /Ink /Rect [10 40 90 60] /InkList [[10 50 90 50]] /C [1 0 0] /BS << /W 4 >>", 1, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, Middle, Rgb.Red255, Tolerance, nameof(InkStrokesItsPaths));
    }

    /// <summary>A Text annotation draws a yellow note icon at the upper-left of its rectangle (ISO 32000-2 12.5.3, 12.5.6.4).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextAnnotationDrawsANoteIcon()
    {
        var image = RenderAnnotation("/Subtype /Text /Rect [10 10 60 60]", 1, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, NoteColumn, NoteRow, Rgb.Yellow, Tolerance, nameof(TextAnnotationDrawsANoteIcon));
    }

    /// <summary>A Line annotation strokes from one end of /L to the other.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LineStrokesItsEnds()
    {
        var image = RenderAnnotation("/Subtype /Line /Rect [0 0 100 100] /L [10 50 90 50] /LE [/OpenArrow /ClosedArrow] /C [0 0 1] /BS << /W 4 >>", 1, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, Middle, Rgb.Blue255, Tolerance, nameof(LineStrokesItsEnds));
    }

    /// <summary>A Polygon annotation fills its vertices with the interior colour.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PolygonFillsItsInterior()
    {
        var image = RenderAnnotation("/Subtype /Polygon /Rect [0 0 100 100] /Vertices [20 20 80 20 50 80] /IC [0 1 0] /C [0 0 1]", 1, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, PolygonRow, Rgb.Green255, Tolerance, nameof(PolygonFillsItsInterior));
    }

    /// <summary>A PolyLine annotation is open, so its interior colour is not filled.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PolyLineIsNotFilled()
    {
        var image = RenderAnnotation("/Subtype /PolyLine /Rect [0 0 100 100] /Vertices [20 20 80 20 50 80] /IC [0 1 0] /C [0 0 1]", 1, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, PolygonRow, Rgb.White, Tolerance, nameof(PolyLineIsNotFilled));
    }

    /// <summary>A FreeText annotation fills its box with /C.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FreeTextFillsItsBackground()
    {
        var image = RenderAnnotation("/Subtype /FreeText /Rect [20 20 80 80] /C [0 1 0] /DA (/Helv 12 Tf 0 g) /Contents (Hi)", 1, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, FreeTextCorner, FreeTextCorner, Rgb.Green255, Tolerance, nameof(FreeTextFillsItsBackground));
    }

    /// <summary>A cloudy border draws scallops round the shape.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CloudyBorderDrawsScallops()
    {
        var image = RenderAnnotation("/Subtype /Square /Rect [15 15 85 85] /RD [7 7 7 7] /BE << /S /C /I 1 >> /C [0 0 1] /BS << /W 2 >>", 1, PdfRenderFlags.Annotations);

        await Assert.That(AnyNearInRow(image, Middle, CloudLeft, CloudRight, Rgb.Blue255)).IsTrue();
    }

    /// <summary>An annotation in a hidden optional content group is not drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationsInHiddenLayersAreNotDrawn()
    {
        var pdf = new RenderTestPdf(Size, Size);
        var layer = pdf.AddObject("<< /Type /OCG /Name (Off) >>");
        var annotation = pdf.AddObject($"<< /Type /Annot {Square} /OC {layer} 0 R >>");
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";
        pdf.CatalogEntries = $"/OCProperties << /OCGs [{layer} 0 R] /D << /OFF [{layer} 0 R] >> >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage(1, 0, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, Middle, Rgb.White, Tolerance, nameof(AnnotationsInHiddenLayersAreNotDrawn));
    }

    /// <summary>Content in a layer whose print usage is off shows on screen but not when printing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PrintUsageHidesContentWhenPrinting()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/OC /L1 BDC 1 0 0 rg 0 0 100 100 re f EMC" };
        var layer = pdf.AddObject("<< /Type /OCG /Name (Screen only) /Usage << /Print << /PrintState /OFF >> >> >>");
        pdf.Resources = $"/Properties << /L1 {layer} 0 R >>";
        pdf.CatalogEntries = $"/OCProperties << /OCGs [{layer} 0 R] /D << /AS [<< /Event /Print /Category [/Print] /OCGs [{layer} 0 R] >>] >> >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var screen = page.RenderPage(1, 0, PdfRenderFlags.None);
        var printed = page.RenderPage(1, 0, PdfRenderFlags.Printing);

        await RenderCheck.Near(screen, Middle, Middle, Rgb.Red255, Tolerance, nameof(PrintUsageHidesContentWhenPrinting));
        await RenderCheck.Near(printed, Middle, Middle, Rgb.White, Tolerance, nameof(PrintUsageHidesContentWhenPrinting));
    }

    /// <summary>Without /AS, the appearance state named by the field value is drawn, else /Off, as in PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingAppearanceStateFallsBackToTheValue()
    {
        var pdf = new RenderTestPdf(Size, Size);
        var on = pdf.AddStream(SmallForm, GreenSquare);
        var off = pdf.AddStream(SmallForm, "1 0 0 rg 0 0 10 10 re f");
        var annotation = pdf.AddObject($"<< /Type /Annot /Subtype /Widget /FT /Btn /T (c) /V /Yes /Rect [20 20 80 80] /AP << /N << /Yes {on} 0 R /Off {off} 0 R >> >> >>");
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage(1, 0, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, Middle, Middle, Rgb.Green255, Tolerance, nameof(MissingAppearanceStateFallsBackToTheValue));
    }

    /// <summary>A text widget without an appearance is drawn from its value when the form asks for appearances.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WidgetsAreDrawnWhenTheFormNeedsAppearances()
    {
        var pdf = new RenderTestPdf(Size, Size);
        var widget = pdf.AddObject("<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /V (x) /Rect [20 20 80 40] /MK << /BG [0 1 0] >> /F 4 >>");
        pdf.PageEntries = $"/Annots [{widget} 0 R]";
        pdf.CatalogEntries = $"/AcroForm << /Fields [{widget} 0 R] /NeedAppearances true /DA (/Helv 0 Tf 0 g) >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage(1, 0, PdfRenderFlags.Annotations);

        await RenderCheck.Near(image, WidgetColumn, WidgetRow, Rgb.Green255, Tolerance, nameof(WidgetsAreDrawnWhenTheFormNeedsAppearances));
    }

    /// <summary>Two renderers of one form keep their own widget tint when rendering in turns.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConflictingRenderersKeepTheirFormHighlights()
    {
        const uint redTint = 0xFF0000;
        const uint blueTint = 0x0000FF;
        const int passes = 2;
        var pdf = new RenderTestPdf(Size, Size);
        var widget = pdf.AddObject("<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /Rect [20 20 80 40] /F 4 >>");
        pdf.PageEntries = $"/Annots [{widget} 0 R]";
        pdf.CatalogEntries = $"/AcroForm << /Fields [{widget} 0 R] /NeedAppearances true /DA (/Helv 0 Tf 0 g) >>";
        using var page = new RenderTestPage(pdf.ToBytes());
        using var red = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { FormHighlight = new(redTint, byte.MaxValue) });
        using var blue = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { FormHighlight = new(blueTint, byte.MaxValue) });

        for (var i = 0; i < passes; i++)
        {
            await RenderCheck.Near(RenderWidget(red), WidgetColumn, WidgetRow, Rgb.Red255, Tolerance, nameof(ConflictingRenderersKeepTheirFormHighlights));
            await RenderCheck.Near(RenderWidget(blue), WidgetColumn, WidgetRow, Rgb.Blue255, Tolerance, nameof(ConflictingRenderersKeepTheirFormHighlights));
        }
    }

    /// <summary>Annotations are drawn in /Annots order, widgets included, so the last one listed is on top (ISO 32000-2 11.2).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationsAreDrawnInAnnotsOrder()
    {
        var pdf = new RenderTestPdf(Size, Size);
        var green = pdf.AddStream(SmallForm, GreenSquare);
        var widget = pdf.AddObject($"<< /Type /Annot /Subtype /Widget /Rect [20 20 80 80] /AP << /N {green} 0 R >> >>");
        var square = pdf.AddObject($"<< /Type /Annot {Square} >>");
        pdf.PageEntries = $"/Annots [{widget} 0 R {square} 0 R]";
        using var page = new RenderTestPage(pdf.ToBytes());
        var squareOnTop = page.RenderPage(1, 0, PdfRenderFlags.Annotations);

        var reversed = new RenderTestPdf(Size, Size);
        var greenAgain = reversed.AddStream(SmallForm, GreenSquare);
        var widgetAgain = reversed.AddObject($"<< /Type /Annot /Subtype /Widget /Rect [20 20 80 80] /AP << /N {greenAgain} 0 R >> >>");
        var squareAgain = reversed.AddObject($"<< /Type /Annot {Square} >>");
        reversed.PageEntries = $"/Annots [{squareAgain} 0 R {widgetAgain} 0 R]";
        using var reversedPage = new RenderTestPage(reversed.ToBytes());
        var widgetOnTop = reversedPage.RenderPage(1, 0, PdfRenderFlags.Annotations);

        await RenderCheck.Near(squareOnTop, Middle, Middle, Rgb.Red255, Tolerance, nameof(AnnotationsAreDrawnInAnnotsOrder));
        await RenderCheck.Near(widgetOnTop, Middle, Middle, Rgb.Green255, Tolerance, nameof(AnnotationsAreDrawnInAnnotsOrder));
    }

    /// <summary>Renders a page with one annotation.</summary>
    /// <param name="entries">The annotation's entries other than /Type.</param>
    /// <param name="scale">The render scale.</param>
    /// <param name="flags">The render flags.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderAnnotation(string entries, float scale, PdfRenderFlags flags)
    {
        var pdf = new RenderTestPdf(Size, Size);
        var annotation = pdf.AddObject($"<< /Type /Annot {entries} >>");
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";
        using var page = new RenderTestPage(pdf.ToBytes());
        return page.RenderPage(scale, 0, flags);
    }

    /// <summary>Renders annotations through a configured renderer.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <returns>The page pixels.</returns>
    /// <exception cref="InvalidOperationException">The page could not be rendered.</exception>
    private static RenderedImage RenderWidget(PdfPageRenderer renderer)
    {
        var pixels = new byte[Size * Size * RenderedImage.BytesPerPixel];
        if (!renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.Annotations), new(pixels, Size, Size, Size * RenderedImage.BytesPerPixel)))
        {
            throw new InvalidOperationException("The widget page could not be rendered.");
        }

        return new(pixels, Size, Size);
    }

    /// <summary>Determines whether any pixel in a column range of rows is near a colour.</summary>
    /// <param name="image">The image.</param>
    /// <param name="column">The column.</param>
    /// <param name="top">The first row.</param>
    /// <param name="bottom">The last row.</param>
    /// <param name="expected">The colour.</param>
    /// <returns><see langword="true"/> when one is.</returns>
    private static bool AnyNear(RenderedImage image, int column, int top, int bottom, Rgb expected)
    {
        for (var row = top; row <= bottom; row++)
        {
            if (image.IsNear(column, row, expected, Tolerance))
            {
                return true;
            }
        }

        RenderCheck.Save(image, nameof(AnyNear));
        return false;
    }

    /// <summary>Determines whether any pixel in a row between two columns is near a colour.</summary>
    /// <param name="image">The image.</param>
    /// <param name="row">The row.</param>
    /// <param name="left">The first column.</param>
    /// <param name="right">The last column.</param>
    /// <param name="expected">The colour.</param>
    /// <returns><see langword="true"/> when one is.</returns>
    private static bool AnyNearInRow(RenderedImage image, int row, int left, int right, Rgb expected)
    {
        for (var column = left; column <= right; column++)
        {
            if (image.IsNear(column, row, expected, Tolerance))
            {
                return true;
            }
        }

        RenderCheck.Save(image, nameof(AnyNearInRow));
        return false;
    }
}
