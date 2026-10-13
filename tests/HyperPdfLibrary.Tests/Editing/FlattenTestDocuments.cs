// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Builds documents with annotations and form fields for the flatten tests, and saves and renders them.</summary>
internal static class FlattenTestDocuments
{
    /// <summary>The page edge in points.</summary>
    internal const int Edge = 200;

    /// <summary>The Print flag.</summary>
    internal const int Print = 4;

    /// <summary>The width of the wide test page.</summary>
    internal const int WideWidth = 300;

    /// <summary>The Hidden flag.</summary>
    internal const int Hidden = 2;

    /// <summary>The NoView flag.</summary>
    internal const int NoView = 32;

    /// <summary>The pixels of a test annotation's blue appearance: 40 by 20 points at one pixel per point.</summary>
    internal const int BluePixels = 800;

    /// <summary>The widest channel difference two renders may have at a pixel and still count as the same.</summary>
    internal const int PixelTolerance = 4;

    /// <summary>The share of pixels two renders may differ in, as parts per ten thousand.</summary>
    internal const int PixelShareTolerance = 10;

    /// <summary>The width and height of the forms drawn for most test annotations.</summary>
    private const string BoxSize = "40 20";

    /// <summary>The dictionary entries of the forms drawn for most test annotations.</summary>
    private const string BoxEntries = "/Type /XObject /Subtype /Form /BBox [0 0 40 20]";

    /// <summary>The buttons in the radio group of the field kinds page.</summary>
    private const int RadioButtons = 2;

    /// <summary>The share of ten thousand parts used for the pixel share.</summary>
    private const int ShareBase = 10_000;

    /// <summary>Gets the colour of the blue appearance every test annotation draws.</summary>
    internal static Rgb Blue => Rgb.Blue255;

    /// <summary>Gets the dictionary of a Helvetica font.</summary>
    internal static string Helvetica => "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>";

    /// <summary>Builds a page with one annotation whose appearance is a blue box.</summary>
    /// <param name="annotationEntries">The annotation's entries after its type, rectangle and appearance, such as <c>/F 4</c>.</param>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] WithAnnotation(string annotationEntries)
    {
        var pdf = RedPage();
        var appearance = BlueBox(pdf);
        pdf.PageEntries = $"/Annots [<< /Type /Annot /Subtype /Square /Rect [10 10 50 30] /AP << /N {appearance} 0 R >> {annotationEntries} >>]";
        return pdf.ToBytes();
    }

    /// <summary>Builds a page with a text field, a check box, a radio button group, a combo box, a list box and a push button, each with an appearance of its own colour.</summary>
    /// <param name="widgetFlags">The annotation flags of every widget.</param>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] WithFieldKinds(int widgetFlags)
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Content = "0.9 g 0 0 200 200 re f", CatalogEntries = string.Empty };
        var text = Box(pdf, "1 0 0", "80 20");
        var on = Box(pdf, "0 0.8 0", "20 20");
        var off = Box(pdf, "0.5 0.5 0.5", "20 20");
        var combo = Box(pdf, "1 1 0", "80 20");
        var list = Box(pdf, "0 1 1", "80 40");
        var push = Box(pdf, "1 0 1", "60 20");
        var f = widgetFlags;
        var textWidget = pdf.AddObject($"<< /Type /Annot /Subtype /Widget /FT /Tx /T (text) /V (hello) /Rect [10 170 90 190] /F {f} /AP << /N {text} 0 R >> >>");
        var check = pdf.AddObject($"<< /Type /Annot /Subtype /Widget /FT /Btn /T (check) /V /Yes /AS /Yes /Rect [100 170 120 190] /F {f} /AP << /N << /Yes {on} 0 R /Off {off} 0 R >> >> >>");

        // The group's object follows its two buttons.
        var radioParent = pdf.NextNumber + RadioButtons;
        var radioOne = pdf.AddObject($"<< /Type /Annot /Subtype /Widget /Parent {radioParent} 0 R /AS /A /Rect [130 170 150 190] /F {f} /AP << /N << /A {on} 0 R /Off {off} 0 R >> >> >>");
        var radioTwo = pdf.AddObject($"<< /Type /Annot /Subtype /Widget /Parent {radioParent} 0 R /AS /Off /Rect [160 170 180 190] /F {f} /AP << /N << /B {on} 0 R /Off {off} 0 R >> >> >>");
        _ = pdf.AddObject($"<< /FT /Btn /Ff 49152 /T (radio) /V /A /Kids [{radioOne} 0 R {radioTwo} 0 R] >>");
        var comboWidget = pdf.AddObject($"<< /Type /Annot /Subtype /Widget /FT /Ch /Ff 131072 /T (combo) /V (one) /Opt [(one) (two)] /Rect [10 130 90 150] /F {f} /AP << /N {combo} 0 R >> >>");
        var listWidget = pdf.AddObject($"<< /Type /Annot /Subtype /Widget /FT /Ch /T (list) /V (b) /Opt [(a) (b) (c)] /Rect [10 70 90 110] /F {f} /AP << /N {list} 0 R >> >>");
        var pushWidget = pdf.AddObject($"<< /Type /Annot /Subtype /Widget /FT /Btn /Ff 65536 /T (push) /Rect [110 70 170 90] /F {f} /AP << /N {push} 0 R >> >>");
        pdf.CatalogEntries = $"/AcroForm << /Fields [{textWidget} 0 R {check} 0 R {radioParent} 0 R {comboWidget} 0 R {listWidget} 0 R {pushWidget} 0 R] >>";
        pdf.PageEntries = $"/Annots [{textWidget} 0 R {check} 0 R {radioOne} 0 R {radioTwo} 0 R {comboWidget} 0 R {listWidget} 0 R {pushWidget} 0 R]";
        return pdf.ToBytes();
    }

    /// <summary>Builds a page with a square annotation, a link and a text field, each with its own appearance, and a form for the field.</summary>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] WithMixedAnnotations()
    {
        var pdf = RedPage();
        var square = BlueBox(pdf);
        var link = Box(pdf, "1 1 0", BoxSize);
        var field = Box(pdf, "0 1 0", BoxSize);
        var squareAnnotation = pdf.AddObject($"<< /Type /Annot /Subtype /Square /Rect [10 10 50 30] /F {Print} /AP << /N {square} 0 R >> >>");
        var linkAnnotation = pdf.AddObject($"<< /Type /Annot /Subtype /Link /Rect [60 10 100 30] /F {Print} /Border [0 0 0] /AP << /N {link} 0 R >> >>");
        var widget = pdf.AddObject($"<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /V (x) /Rect [110 10 150 30] /F {Print} /AP << /N {field} 0 R >> >>");
        pdf.CatalogEntries = $"/AcroForm << /Fields [{widget} 0 R] >>";
        pdf.PageEntries = $"/Annots [{squareAnnotation} 0 R {linkAnnotation} 0 R {widget} 0 R]";
        return pdf.ToBytes();
    }

    /// <summary>Builds a page with a square annotation that has a popup, and a hidden reply to the square.</summary>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] WithPopupAndReply()
    {
        var pdf = RedPage();
        var square = BlueBox(pdf);
        var parent = pdf.NextNumber;
        _ = pdf.AddObject($"<< /Type /Annot /Subtype /Square /Rect [10 10 50 30] /F {Print} /AP << /N {square} 0 R >> /Popup {parent + 1} 0 R >>");
        var popup = pdf.AddObject($"<< /Type /Annot /Subtype /Popup /Rect [100 100 180 180] /Parent {parent} 0 R /Open true >>");
        var reply = pdf.AddObject($"<< /Type /Annot /Subtype /Text /Rect [60 10 80 30] /F {Hidden + Print} /IRT {parent} 0 R /RT /R /Contents (reply) >>");
        pdf.PageEntries = $"/Annots [{parent} 0 R {popup} 0 R {reply} 0 R]";
        return pdf.ToBytes();
    }

    /// <summary>Builds a page with a highlight annotation that has no appearance.</summary>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] WithHighlightWithoutAppearance()
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Content = "1 1 1 rg 0 0 200 200 re f" };
        pdf.PageEntries = $"/Annots [<< /Type /Annot /Subtype /Highlight /Rect [20 100 120 120] /QuadPoints [20 120 120 120 20 100 120 100] /C [1 1 0] /F {Print} >>]";
        return pdf.ToBytes();
    }

    /// <summary>Builds a wide page, optionally turned, with a square annotation and a link at the lower left.</summary>
    /// <param name="rotation">The page's /Rotate.</param>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] Rotated(int rotation)
    {
        var pdf = new RenderTestPdf(WideWidth, Edge) { Content = "1 0 0 rg 0 0 300 100 re f 0 1 0 rg 0 100 300 100 re f" };
        var square = BlueBox(pdf);
        const string link = "<< /Type /Annot /Subtype /Link /Rect [60 10 100 30] /Border [0 0 0] >>";
        pdf.PageEntries = $"/Rotate {rotation} /Annots [<< /Type /Annot /Subtype /Square /Rect [10 10 50 30] /F {Print} /AP << /N {square} 0 R >> >> {link}]";
        return pdf.ToBytes();
    }

    /// <summary>Builds a page with a square annotation in an optional content group.</summary>
    /// <param name="visible">Whether the group is on by default.</param>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] WithLayer(bool visible)
    {
        var pdf = RedPage();
        var square = BlueBox(pdf);
        var group = pdf.AddObject("<< /Type /OCG /Name (Notes) >>");
        pdf.CatalogEntries = $"/OCProperties << /OCGs [{group} 0 R] /D << /{(visible ? "ON" : "OFF")} [{group} 0 R] >> >>";
        pdf.PageEntries = $"/Annots [<< /Type /Annot /Subtype /Square /Rect [10 10 50 30] /F {Print} /OC {group} 0 R /AP << /N {square} 0 R >> >>]";
        return pdf.ToBytes();
    }

    /// <summary>
    /// Builds a tagged page with one paragraph, a text field whose form element refers to it by an object reference, and a
    /// square annotation that no element owns.
    /// </summary>
    /// <param name="pageHasStructParents">Whether the page has a parent tree key for its marked content.</param>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] Tagged(bool pageHasStructParents) => Tagged(pageHasStructParents, Print);

    /// <summary>Builds the tagged page with the text field's annotation flags chosen.</summary>
    /// <param name="pageHasStructParents">Whether the page has a parent tree key for its marked content.</param>
    /// <param name="widgetFlags">The text field's annotation flags.</param>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] Tagged(bool pageHasStructParents, int widgetFlags)
    {
        var pageKey = pageHasStructParents ? "/StructParents 0" : string.Empty;
        var entries = pageHasStructParents ? "/Nums [0 [7 0 R] 1 10 0 R]" : "/Nums [1 10 0 R]";
        return MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /MarkInfo << /Marked true >> /StructTreeRoot 6 0 R /AcroForm << /Fields [8 0 R] >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Edge} {Edge}] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> /Annots [8 0 R 9 0 R] {pageKey} >>",
            MiniPdf.Stream(string.Empty, "/P <</MCID 0>> BDC BT /F1 12 Tf 10 180 Td (Hello) Tj ET EMC"),
            Helvetica,
            $"<< /Type /StructTreeRoot /K [7 0 R 10 0 R] /ParentTree << {entries} >> /ParentTreeNextKey 2 >>",
            "<< /Type /StructElem /S /P /P 6 0 R /Pg 3 0 R /K 0 >>",
            $"<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /V (x) /Rect [10 10 50 30] /F {widgetFlags} /P 3 0 R /AP << /N 11 0 R >> /StructParent 1 >>",
            $"<< /Type /Annot /Subtype /Square /Rect [60 10 100 30] /F {Print} /P 3 0 R /AP << /N 12 0 R >> >>",
            "<< /Type /StructElem /S /Form /P 6 0 R /Pg 3 0 R /K << /Type /OBJR /Obj 8 0 R /Pg 3 0 R >> >>",
            MiniPdf.Stream(BoxEntries, $"0 1 0 rg 0 0 {BoxSize} re f"),
            MiniPdf.Stream(BoxEntries, $"0 0 1 rg 0 0 {BoxSize} re f"));
    }

    /// <summary>
    /// Builds two pages with a form. Each page has a field of its own, and a third field has a widget on each page, so
    /// flattening one page leaves the form with the other page's fields.
    /// </summary>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] TwoPageForm() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R 6 0 R 7 0 R] >> >>",
        "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
        $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Edge} {Edge}] /Annots [5 0 R 9 0 R] >>",
        $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Edge} {Edge}] /Annots [6 0 R 10 0 R] >>",
        $"<< /Type /Annot /Subtype /Widget /FT /Tx /T (first) /V (a) /Rect [10 10 50 30] /F {Print} /AP << /N 8 0 R >> >>",
        $"<< /Type /Annot /Subtype /Widget /FT /Tx /T (second) /V (b) /Rect [10 10 50 30] /F {Print} /AP << /N 8 0 R >> >>",
        "<< /FT /Tx /T (shared) /V (c) /Kids [9 0 R 10 0 R] >>",
        MiniPdf.Stream(BoxEntries, $"0 0 1 rg 0 0 {BoxSize} re f"),
        $"<< /Type /Annot /Subtype /Widget /Parent 7 0 R /Rect [60 10 100 30] /F {Print} /AP << /N 8 0 R >> >>",
        $"<< /Type /Annot /Subtype /Widget /Parent 7 0 R /Rect [60 10 100 30] /F {Print} /AP << /N 8 0 R >> >>");

    /// <summary>Saves a document with the compact writer.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The file.</returns>
    internal static byte[] SaveCompact(PdfDocument document) => PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);

    /// <summary>Renders the first page, with annotations, at one pixel per point.</summary>
    /// <param name="file">The PDF bytes.</param>
    /// <param name="flags">More render options.</param>
    /// <returns>The pixels.</returns>
    internal static RenderedImage Render(byte[] file, PdfRenderFlags flags)
    {
        using var page = new RenderTestPage(file);
        return page.RenderPage(1, 0, PdfRenderFlags.Annotations | flags);
    }

    /// <summary>Counts the pixels two renders differ at by more than the tolerance.</summary>
    /// <param name="first">The first render.</param>
    /// <param name="second">The second render.</param>
    /// <returns>The number of pixels that differ, or the pixel count of the larger render when the sizes differ.</returns>
    internal static int CountDifferences(RenderedImage first, RenderedImage second)
    {
        if (first.Width != second.Width || first.Height != second.Height)
        {
            return Math.Max(first.Pixels.Length, second.Pixels.Length) / RenderedImage.BytesPerPixel;
        }

        var differing = 0;
        for (var i = 0; i < first.Pixels.Length; i += RenderedImage.BytesPerPixel)
        {
            differing += PixelDiffers(first.Pixels, second.Pixels, i) ? 1 : 0;
        }

        return differing;
    }

    /// <summary>Counts the pixels of a render that are a colour.</summary>
    /// <param name="image">The render.</param>
    /// <param name="colour">The colour.</param>
    /// <returns>The number of pixels within the tolerance of the colour.</returns>
    internal static int CountColour(RenderedImage image, Rgb colour)
    {
        var count = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                count += image.IsNear(x, y, colour, PixelTolerance) ? 1 : 0;
            }
        }

        return count;
    }

    /// <summary>Gets the number of pixels two renders of the same size may differ in.</summary>
    /// <param name="image">A render.</param>
    /// <returns>The allowance.</returns>
    internal static int AllowedDifferences(RenderedImage image) => image.Width * image.Height * PixelShareTolerance / ShareBase;

    /// <summary>Lists the decoded content of a page's streams.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The content of each stream, one character per byte.</returns>
    internal static string[] Contents(PdfDocument document, int pageIndex)
    {
        var contents = PdfDocumentPages.GetPage(document, pageIndex).Dictionary.Get(KnownName.Contents);
        if (contents.AsStream() is { } single)
        {
            return [Encoding.Latin1.GetString(single.DecodeToArray())];
        }

        var array = contents.AsArray();
        var result = new string[array?.Count ?? 0];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = Encoding.Latin1.GetString(array!.Get(i).AsStream()!.DecodeToArray());
        }

        return result;
    }

    /// <summary>Joins a page's content streams.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The content.</returns>
    internal static string Content(PdfDocument document, int pageIndex) => string.Concat(Contents(document, pageIndex));

    /// <summary>Gets the number of annotations on a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The count.</returns>
    internal static int AnnotationCount(PdfDocument document, int pageIndex) =>
        PdfDocumentPages.GetPage(document, pageIndex).Dictionary.GetArray(KnownName.Annots)?.Count ?? 0;

    /// <summary>Walks every object the document reaches and counts the dictionaries that satisfy a test.</summary>
    /// <param name="document">The document.</param>
    /// <param name="test">The test.</param>
    /// <returns>The number of dictionaries that satisfy it.</returns>
    internal static int CountReachable(PdfDocument document, Func<PdfDictionary, bool> test)
    {
        var store = document.Objects;
        var reached = HyperPdfLibrary.Editing.PdfObjectReachability.FromTrailer(store, CancellationToken.None);
        var count = 0;
        foreach (var number in reached)
        {
            if (StoreReading.GetObject(store, new(number, 0)).AsDictionary() is { } dictionary && test(dictionary))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Determines whether a pixel of two renders differs in any channel by more than the tolerance.</summary>
    /// <param name="first">The first render's pixels.</param>
    /// <param name="second">The second render's pixels.</param>
    /// <param name="offset">The offset of the pixel's first byte.</param>
    /// <returns><see langword="true"/> when the pixels differ.</returns>
    private static bool PixelDiffers(byte[] first, byte[] second, int offset)
    {
        for (var channel = 0; channel < RenderedImage.BytesPerPixel; channel++)
        {
            if (Math.Abs(first[offset + channel] - second[offset + channel]) > PixelTolerance)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Starts a page that is all red.</summary>
    /// <returns>The page.</returns>
    private static RenderTestPdf RedPage() => new(Edge, Edge) { Content = "1 0 0 rg 0 0 200 200 re f" };

    /// <summary>Adds a blue form the size of a test annotation.</summary>
    /// <param name="pdf">The document being built.</param>
    /// <returns>The object number.</returns>
    private static int BlueBox(RenderTestPdf pdf) => Box(pdf, "0 0 1", BoxSize);

    /// <summary>Adds a form that fills its box with a colour.</summary>
    /// <param name="pdf">The document being built.</param>
    /// <param name="rgb">The fill colour as three numbers.</param>
    /// <param name="size">The box width and height as two numbers.</param>
    /// <returns>The object number.</returns>
    private static int Box(RenderTestPdf pdf, string rgb, string size) =>
        pdf.AddStream($"/Type /XObject /Subtype /Form /BBox [0 0 {size}]", $"{rgb} rg 0 0 {size} re f");
}
