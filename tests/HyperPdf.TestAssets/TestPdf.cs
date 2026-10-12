// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdf.TestAssets;

/// <summary>
/// Writes small, deterministic PDF files for tests and benchmarks. Page 1 links to page 3 and to a web address, page 2
/// is landscape, every page carries searchable text, and the outline has one entry per page.
/// </summary>
public static class TestPdf
{
    /// <summary>The portrait page width in points.</summary>
    public static readonly int PortraitWidth = 612;

    /// <summary>The portrait page height in points.</summary>
    public static readonly int PortraitHeight = 792;

    /// <summary>The sentence repeated on each page.</summary>
    public static readonly string Sentence = "The quick brown fox jumps over the lazy dog.";

    /// <summary>The URI linked from page 1.</summary>
    public static readonly string LinkUri = "https://example.com/hyperpdf";

    /// <summary>The document title.</summary>
    public static readonly string Title = "HyperPDF Test Document";

    /// <summary>The document author.</summary>
    public static readonly string Author = "Glenn Watson";

    /// <summary>The text of the typewriter annotation in <see cref="CreateWithFreeText"/>.</summary>
    public static readonly string ForeignText = "Typed elsewhere";

    /// <summary>The file name of the embedded file in <see cref="CreateWithAttachment"/>.</summary>
    public static readonly string AttachmentName = "notes.txt";

    /// <summary>The contents of the embedded file in <see cref="CreateWithAttachment"/>.</summary>
    public static readonly string AttachmentText = "Meeting notes: bring the signed form.";

    /// <summary>The PDF beside a <see cref="CreateWithFileLinks"/> document that its first link opens at page 3.</summary>
    public static readonly string LinkedDocumentName = "other.pdf";

    /// <summary>The file a <see cref="CreateWithFileLinks"/> document's second link opens with another app.</summary>
    public static readonly string LaunchedFileName = "notes.txt";

    /// <summary>The scale <see cref="CreateWithViewport"/> declares.</summary>
    public static readonly string ViewportScale = "1 in = 10 ft";

    /// <summary>The running header of <see cref="CreateTagged"/>, marked as an artifact.</summary>
    public static readonly string TaggedHeader = "Running header 7";

    /// <summary>The heading of <see cref="CreateTagged"/>.</summary>
    public static readonly string TaggedHeading = "Tagged Heading";

    /// <summary>The first paragraph of <see cref="CreateTagged"/>, drawn below the second.</summary>
    public static readonly string TaggedFirst = "First paragraph comes first.";

    /// <summary>The second paragraph of <see cref="CreateTagged"/>, drawn above the first.</summary>
    public static readonly string TaggedSecond = "Second paragraph comes later.";

    /// <summary>The figure description of <see cref="CreateTagged"/>.</summary>
    public static readonly string TaggedFigure = "A black bar";

    /// <summary>The name of the visible layer in <see cref="CreateWithLayers"/>.</summary>
    public static readonly string DrawingLayer = "Drawing";

    /// <summary>The name of the hidden layer in <see cref="CreateWithLayers"/>.</summary>
    public static readonly string NotesLayer = "Notes";

    /// <summary>The left edge of the visible layer's box, in points.</summary>
    public static readonly int LayerBoxLeft = 72;

    /// <summary>The left edge of the hidden layer's box, in points.</summary>
    public static readonly int LayerBoxRight = 340;

    /// <summary>The bottom of both boxes, in PDF points from the bottom.</summary>
    public static readonly int LayerBoxBottom = 600;

    /// <summary>The size of both boxes, in points.</summary>
    public static readonly int LayerBoxSize = 100;

    /// <summary>The top of the first calculated form field.</summary>
    private const int FirstFieldTop = 700;

    /// <summary>The distance between calculated form fields.</summary>
    private const int FieldSpacing = 40;

    /// <summary>The height of a calculated form field.</summary>
    private const int FieldHeight = 24;

    /// <summary>The Total field's place among the calculated form's fields.</summary>
    private const int TotalField = 2;

    /// <summary>The Tax field's place among the calculated form's fields.</summary>
    private const int TaxField = 3;

    /// <summary>A number field's keystroke and format actions.</summary>
    private const string NumberActions = "/K << /S /JavaScript /JS (AFNumber_Keystroke(2, 0, 0, 0, \"\", true);) >> /F << /S /JavaScript /JS (AFNumber_Format(2, 0, 0, 0, \"\", true);) >>";

    /// <summary>A currency format action.</summary>
    private const string CurrencyFormat = "/F << /S /JavaScript /JS (AFNumber_Format(2, 0, 0, 0, \"$\", true);) >>";

    /// <summary>The standard Helvetica font dictionary.</summary>
    private const string HelveticaFont = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>";

    /// <summary>A free cross-reference entry.</summary>
    private const byte XrefFree = 0;

    /// <summary>A cross-reference entry at a byte offset.</summary>
    private const byte XrefOffset = 1;

    /// <summary>A cross-reference entry inside an object stream.</summary>
    private const byte XrefPacked = 2;

    /// <summary>The object stream's number in the compressed document.</summary>
    private const int ObjectStreamNumber = 6;

    /// <summary>The bytes in a cross-reference stream row: type, four byte offset, index.</summary>
    private const int XrefRowLength = 6;

    /// <summary>The PNG Up filter.</summary>
    private const byte PngUp = 2;

    /// <summary>The page that is landscape.</summary>
    private const int LandscapePage = 2;

    /// <summary>The page the internal link on page 1 points at.</summary>
    private const int LinkedPage = 3;

    /// <summary>The left margin and the distance of the heading baseline from the top.</summary>
    private const int Margin = 72;

    /// <summary>The heading font size.</summary>
    private const int HeadingSize = 24;

    /// <summary>The body font size.</summary>
    private const int BodySize = 12;

    /// <summary>How far below the top the printed form's label sits, in points.</summary>
    private const int FlatLabelDepth = 200;

    /// <summary>The distance between body lines.</summary>
    private const int LineHeight = 20;

    /// <summary>The gap before a new paragraph.</summary>
    private const int ParagraphGap = 40;

    /// <summary>How far below the baseline a link rectangle starts.</summary>
    private const int LinkDescent = 5;

    /// <summary>The width of link rectangles.</summary>
    private const int LinkWidth = 260;

    /// <summary>The Total field's actions: the product of Price and Quantity, shown as currency.</summary>
    private const string TotalActions = $"/C << /S /JavaScript /JS (AFSimple_Calculate(\"PRD\", new Array (\"Price\", \"Quantity\"));) >> {CurrencyFormat}";

    /// <summary>The Tax field's actions: a tenth of Total in simplified field notation, shown as currency.</summary>
    private const string TaxActions = $"/C << /S /JavaScript /JS (/** BVCALC Total * 0.1 EVCALC **/ event.value = 0.1 * this.getField\\(\"Total\"\\).value;) >> {CurrencyFormat}";

    /// <summary>The calculated form's fields and their actions.</summary>
    private static readonly (string Name, string Actions)[] CalculatedFields =
    [
        ("Price", NumberActions),
        ("Quantity", NumberActions),
        ("Total", TotalActions),
        ("Tax", TaxActions),
        ("Percent", "/V << /S /JavaScript /JS (AFRange_Validate(true, 0, true, 100);) >>"),
        ("Date", "/K << /S /JavaScript /JS (AFDate_KeystrokeEx(\"dd/mm/yyyy\");) >> /F << /S /JavaScript /JS (AFDate_FormatEx(\"dd/mm/yyyy\");) >>"),
    ];

    /// <summary>Creates a PDF document.</summary>
    /// <param name="pageCount">The number of pages.</param>
    /// <returns>The PDF bytes.</returns>
    public static byte[] Create(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var font = Add(objects, HelveticaFont);
        var outlines = Reserve(objects);
        var pageIds = new int[pageCount];
        for (var i = 0; i < pageCount; i++)
        {
            pageIds[i] = Reserve(objects);
        }

        for (var i = 0; i < pageCount; i++)
        {
            WritePage(objects, pageIds, i, pages, font);
        }

        var outlineIds = WriteOutline(objects, pageIds, outlines);
        objects[outlines - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Outlines /First {outlineIds[0]} 0 R /Last {outlineIds[^1]} 0 R /Count {outlineIds.Length} >>");

        var kids = new StringBuilder();
        foreach (var id in pageIds)
        {
            _ = kids.Append(CultureInfo.InvariantCulture, $"{id} 0 R ");
        }

        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{kids.ToString().TrimEnd()}] /Count {pageCount} >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {pages} 0 R /Outlines {outlines} 0 R /PageMode /UseOutlines >>");
        var info = Add(objects, $"<< /Title ({Title}) /Author ({Author}) /Creator (HyperPdf.TestAssets) /CreationDate (D:20260102030405+10'00') >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>
    /// Creates a one page form: a text field "Name", a check box "Agree" (with checked and unchecked appearances) and a
    /// combo box "Colour" offering Red, Green and Blue.
    /// </summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateForm()
    {
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var page = Reserve(objects);
        var font = Add(objects, HelveticaFont);
        const string tick = "q 0 g BT /ZaDb 12 Tf 2 3 Td (4) Tj ET Q";
        var zapf = Add(objects, "<< /Type /Font /Subtype /Type1 /BaseFont /ZapfDingbats >>");
        var on = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /XObject /Subtype /Form /BBox [0 0 16 16]
               /Resources << /Font << /ZaDb {{zapf}} 0 R >> >> /Length {{tick.Length}} >>
            stream
            {{tick}}
            endstream
            """));
        var off = Add(objects, "<< /Type /XObject /Subtype /Form /BBox [0 0 16 16] /Length 0 >>\nstream\n\nendstream");
        var name = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /Rect [72 650 300 674] /P {{page}} 0 R /F 4
               /DA (/Helv 12 Tf 0 g) /MK << /BC [0.5 0.5 0.5] >> >>
            """));
        var agree = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Annot /Subtype /Widget /FT /Btn /T (Agree) /Rect [72 610 88 626] /P {{page}} 0 R /F 4 /V /Off /AS /Off
               /MK << /BC [0.5 0.5 0.5] >> /AP << /N << /Yes {{on}} 0 R /Off {{off}} 0 R >> >> >>
            """));
        var colour = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Annot /Subtype /Widget /FT /Ch /Ff 131072 /T (Colour) /Rect [72 570 200 594] /P {{page}} 0 R /F 4
               /Opt [(Red) (Green) (Blue)] /V (Red) /DA (/Helv 12 Tf 0 g) /MK << /BC [0.5 0.5 0.5] >> >>
            """));
        var content = new StringBuilder();
        AppendText(content, PortraitHeight - Margin, HeadingSize, "Form");
        var stream = content.ToString();
        var contentId = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream"));
        objects[page - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Page /Parent {{pages}} 0 R /MediaBox [0 0 {{PortraitWidth}} {{PortraitHeight}}]
               /Resources << /Font << /F1 {{font}} 0 R >> >> /Contents {{contentId}} 0 R /Annots [{{name}} 0 R {{agree}} 0 R {{colour}} 0 R] >>
            """);
        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{page} 0 R] /Count 1 >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Catalog /Pages {{pages}} 0 R
               /AcroForm << /Fields [{{name}} 0 R {{agree}} 0 R {{colour}} 0 R] /NeedAppearances true /DA (/Helv 12 Tf 0 g)
                            /DR << /Font << /Helv {{font}} 0 R /ZaDb {{zapf}} 0 R >> >> >> >>
            """);
        var info = Add(objects, $"<< /Title (Form) /Author ({Author}) >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>
    /// Creates a one page form whose fields carry Recognised PDF form scripts: Price and Quantity are numbers, Total
    /// is their product shown as currency, Tax is Total times 0.1 in simplified field notation, Percent must be between
    /// 0 and 100, and Date is shown as day/month/year.
    /// </summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateCalculatedForm()
    {
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var page = Reserve(objects);
        var font = Add(objects, HelveticaFont);
        var ids = new int[CalculatedFields.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            var top = FirstFieldTop - (i * FieldSpacing);
            ids[i] = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
                << /Type /Annot /Subtype /Widget /FT /Tx /T ({{CalculatedFields[i].Name}}) /Rect [72 {{top - FieldHeight}} 300 {{top}}] /P {{page}} 0 R /F 4
                   /DA (/Helv 12 Tf 0 g) /MK << /BC [0.5 0.5 0.5] >> /AA << {{CalculatedFields[i].Actions}} >> >>
                """));
        }

        var content = new StringBuilder();
        AppendText(content, PortraitHeight - Margin, HeadingSize, "Order");
        var stream = content.ToString();
        var contentId = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream"));
        var fields = string.Join(' ', ids.Select(static id => string.Create(CultureInfo.InvariantCulture, $"{id} 0 R")));
        objects[page - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Page /Parent {{pages}} 0 R /MediaBox [0 0 {{PortraitWidth}} {{PortraitHeight}}]
               /Resources << /Font << /F1 {{font}} 0 R >> >> /Contents {{contentId}} 0 R /Annots [{{fields}}] >>
            """);
        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{page} 0 R] /Count 1 >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Catalog /Pages {{pages}} 0 R
               /AcroForm << /Fields [{{fields}}] /CO [{{ids[TotalField]}} 0 R {{ids[TaxField]}} 0 R] /NeedAppearances true /DA (/Helv 12 Tf 0 g)
                            /DR << /Font << /Helv {{font}} 0 R >> >> >> >>
            """);
        var info = Add(objects, $"<< /Title (Order) /Author ({Author}) >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>
    /// Creates a one page, portrait, image-only PDF, like a scan: the page shows an 8 bit greyscale image filling it
    /// and has no text.
    /// </summary>
    /// <param name="grey">The image, one byte per pixel.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateScan(ReadOnlySpan<byte> grey, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(grey.Length, width * height);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, true))
        {
            zlib.Write(grey[..(width * height)]);
        }

        var hex = $"{Convert.ToHexString(compressed.GetBuffer(), 0, (int)compressed.Length)}>";
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var page = Reserve(objects);
        var image = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /XObject /Subtype /Image /Width {{width}} /Height {{height}} /ColorSpace /DeviceGray /BitsPerComponent 8
               /Filter [/ASCIIHexDecode /FlateDecode] /Length {{hex.Length}} >>
            stream
            {{hex}}
            endstream
            """));
        var stream = string.Create(CultureInfo.InvariantCulture, $"q {PortraitWidth} 0 0 {PortraitHeight} 0 0 cm /Im1 Do Q\n");
        var content = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Length {stream.Length} >>\nstream\n{stream}endstream"));
        objects[page - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Page /Parent {{pages}} 0 R /MediaBox [0 0 {{PortraitWidth}} {{PortraitHeight}}]
               /Resources << /XObject << /Im1 {{image}} 0 R >> >> /Contents {{content}} 0 R >>
            """);
        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{page} 0 R] /Count 1 >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {pages} 0 R >>");
        var info = Add(objects, $"<< /Title (Scan) /Author ({Author}) >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>
    /// Creates a one page document with three links stacked down the page: to page 3 of <see cref="LinkedDocumentName"/>
    /// beside it, to open <see cref="LaunchedFileName"/> with another app, and to a document embedded in this one.
    /// </summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateWithFileLinks()
    {
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var page = Reserve(objects);
        var font = Add(objects, HelveticaFont);
        var content = new StringBuilder();
        var baseline = PortraitHeight - Margin;
        AppendText(content, baseline, HeadingSize, "Links to files");
        string[] actions =
        [
            $"<< /S /GoToR /F ({LinkedDocumentName}) /D [2 /Fit] >>",
            $"<< /S /Launch /F ({LaunchedFileName}) >>",
            "<< /S /GoToE /T << /R /C /N (inner) >> /D [0 /Fit] >>",
        ];
        var annotations = new StringBuilder();
        foreach (var action in actions)
        {
            baseline -= ParagraphGap;
            AppendText(content, baseline, BodySize, "Open the linked file");
            var link = Add(objects, $"<< /Type /Annot /Subtype /Link /Rect [{LinkRect(baseline)}] /Border [0 0 0] /A {action} >>");
            _ = annotations.Append(CultureInfo.InvariantCulture, $"{link} 0 R ");
        }

        var stream = content.ToString();
        var contentId = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream"));
        objects[page - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Page /Parent {{pages}} 0 R /MediaBox [0 0 {{PortraitWidth}} {{PortraitHeight}}]
               /Resources << /Font << /F1 {{font}} 0 R >> >> /Contents {{contentId}} 0 R /Annots [{{annotations.ToString().TrimEnd()}}] >>
            """);
        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{page} 0 R] /Count 1 >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {pages} 0 R >>");
        var info = Add(objects, $"<< /Title (Links to files) /Author ({Author}) >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>
    /// Creates a one page document using everything HyperPDF cannot show or run: an XFA form, document JavaScript,
    /// a field script it does not understand, a media player, a 3D model, and a portfolio cover page with an attached file.
    /// </summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateWithUnsupportedContent()
    {
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var page = Reserve(objects);
        var font = Add(objects, HelveticaFont);
        var file = Add(objects, string.Create(CultureInfo.InvariantCulture, $"""
            << /Type /EmbeddedFile /Length {AttachmentText.Length} >>
            stream
            {AttachmentText}
            endstream
            """));
        var spec = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Type /Filespec /F ({AttachmentName}) /UF ({AttachmentName}) /EF << /F {file} 0 R >> >>"));
        var script = Add(objects, "<< /S /JavaScript /JS (app.alert\\(\"Hello\"\\);) >>");
        var widget = Add(objects, $"<< /Type /Annot /Subtype /Widget /FT /Tx /T (Custom) /Rect [72 600 300 624] /P {page} 0 R /AA << /K << /S /JavaScript /JS (checkCustomRules\\(\\);) >> >> >>");
        var screen = Add(objects, $"<< /Type /Annot /Subtype /Screen /Rect [72 400 300 560] /P {page} 0 R >>");
        var model = Add(objects, $"<< /Type /Annot /Subtype /3D /Rect [320 400 540 560] /P {page} 0 R >>");
        var xfa = Add(objects, "<< /Length 9 >>\nstream\n<xdp:xdp>endstream");
        var content = new StringBuilder();
        AppendText(content, PortraitHeight - Margin, HeadingSize, "PDF Portfolio");
        var stream = content.ToString();
        var contentId = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream"));
        objects[page - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Page /Parent {{pages}} 0 R /MediaBox [0 0 {{PortraitWidth}} {{PortraitHeight}}]
               /Resources << /Font << /F1 {{font}} 0 R >> >> /Contents {{contentId}} 0 R /Annots [{{widget}} 0 R {{screen}} 0 R {{model}} 0 R] >>
            """);
        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{page} 0 R] /Count 1 >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Catalog /Pages {{pages}} 0 R /AcroForm << /Fields [{{widget}} 0 R] /XFA {{xfa}} 0 R >> /Collection << /View /D >>
               /Names << /EmbeddedFiles << /Names [({{AttachmentName}}) {{spec}} 0 R] >> /JavaScript << /Names [(init) {{script}} 0 R] >> >> >>
            """);
        var info = Add(objects, $"<< /Title (Unsupported content) /Author ({Author}) >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>
    /// Creates a one page document holding typewriter text written by another program: red, bold, centred 14 point
    /// Helvetica reading <see cref="ForeignText"/>, described only by its default appearance and style.
    /// </summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateWithFreeText()
    {
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var page = Reserve(objects);
        var font = Add(objects, HelveticaFont);
        var note = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Annot /Subtype /FreeText /IT /FreeTextTypeWriter /Rect [72 600 300 640] /P {{page}} 0 R /F 4
               /Contents ({{ForeignText}}) /DA (/Helv 14 Tf 1 0 0 rg) /DS (font: bold 14pt Helvetica; text-align:center) >>
            """));
        var content = new StringBuilder();
        AppendText(content, PortraitHeight - Margin, HeadingSize, "Typed");
        var stream = content.ToString();
        var contentId = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream"));
        objects[page - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Page /Parent {{pages}} 0 R /MediaBox [0 0 {{PortraitWidth}} {{PortraitHeight}}]
               /Resources << /Font << /F1 {{font}} 0 R >> >> /Contents {{contentId}} 0 R /Annots [{{note}} 0 R] >>
            """);
        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{page} 0 R] /Count 1 >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {pages} 0 R >>");
        var info = Add(objects, $"<< /Title (Typed) /Author ({Author}) >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>
    /// Creates a one page form for keyboard filling: a text field "Name", a six box comb field "Code" limited to six
    /// characters, a check box "Agree", and a radio group "Size" with the choices Small and Large, in that order.
    /// </summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateCombForm()
    {
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var page = Reserve(objects);
        var font = Add(objects, HelveticaFont);
        var zapf = Add(objects, "<< /Type /Font /Subtype /Type1 /BaseFont /ZapfDingbats >>");
        const string tick = "q 0 g BT /ZaDb 12 Tf 2 3 Td (4) Tj ET Q";
        var on = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /XObject /Subtype /Form /BBox [0 0 16 16]
               /Resources << /Font << /ZaDb {{zapf}} 0 R >> >> /Length {{tick.Length}} >>
            stream
            {{tick}}
            endstream
            """));
        var off = Add(objects, "<< /Type /XObject /Subtype /Form /BBox [0 0 16 16] /Length 0 >>\nstream\n\nendstream");
        var name = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /Rect [72 650 300 674] /P {{page}} 0 R /F 4 /DA (/Helv 12 Tf 0 g) >>
            """));
        var code = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Annot /Subtype /Widget /FT /Tx /Ff 16777216 /MaxLen 6 /T (Code) /Rect [72 610 192 634] /P {{page}} 0 R /F 4 /DA (/Helv 12 Tf 0 g) >>
            """));
        var agree = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Annot /Subtype /Widget /FT /Btn /T (Agree) /Rect [72 570 88 586] /P {{page}} 0 R /F 4 /V /Off /AS /Off
               /AP << /N << /Yes {{on}} 0 R /Off {{off}} 0 R >> >> >>
            """));
        var size = Reserve(objects);
        var small = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Annot /Subtype /Widget /Parent {{size}} 0 R /Rect [72 530 88 546] /P {{page}} 0 R /F 4 /AS /Off
               /AP << /N << /Small {{on}} 0 R /Off {{off}} 0 R >> >> >>
            """));
        var large = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Annot /Subtype /Widget /Parent {{size}} 0 R /Rect [120 530 136 546] /P {{page}} 0 R /F 4 /AS /Off
               /AP << /N << /Large {{on}} 0 R /Off {{off}} 0 R >> >> >>
            """));
        objects[size - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /FT /Btn /Ff 49152 /T (Size) /V /Off /Kids [{small} 0 R {large} 0 R] >>");
        var content = new StringBuilder();
        AppendText(content, PortraitHeight - Margin, HeadingSize, "Keyboard form");
        var stream = content.ToString();
        var contentId = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream"));
        objects[page - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Page /Parent {{pages}} 0 R /MediaBox [0 0 {{PortraitWidth}} {{PortraitHeight}}]
               /Resources << /Font << /F1 {{font}} 0 R >> >> /Contents {{contentId}} 0 R
               /Annots [{{name}} 0 R {{code}} 0 R {{agree}} 0 R {{small}} 0 R {{large}} 0 R] >>
            """);
        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{page} 0 R] /Count 1 >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Catalog /Pages {{pages}} 0 R
               /AcroForm << /Fields [{{name}} 0 R {{code}} 0 R {{agree}} 0 R {{size}} 0 R] /NeedAppearances true /DA (/Helv 12 Tf 0 g)
                            /DR << /Font << /Helv {{font}} 0 R /ZaDb {{zapf}} 0 R >> >> >> >>
            """);
        var info = Add(objects, $"<< /Title (Keyboard form) /Author ({Author}) >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>
    /// Creates a one page printed form with no fillable fields: "Name" with a line to write on from (100, 192) to
    /// (300, 192), a box from (100, 240) to (300, 265), and a row of six character boxes from (100, 320) to
    /// (220, 340), in page points from the top-left.
    /// </summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateFlatForm()
    {
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var page = Reserve(objects);
        var font = Add(objects, HelveticaFont);
        var content = new StringBuilder();
        AppendText(content, PortraitHeight - FlatLabelDepth, BodySize, "Name");
        _ = content.Append("0 G 1 w 100 600 m 300 600 l S\n");
        _ = content.Append("100 527 200 25 re S\n");
        _ = content.Append("100 452 120 20 re S\n");
        _ = content.Append("120 452 m 120 472 l S 140 452 m 140 472 l S 160 452 m 160 472 l S 180 452 m 180 472 l S 200 452 m 200 472 l S\n");

        var stream = content.ToString();
        var contentId = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream"));
        objects[page - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Page /Parent {{pages}} 0 R /MediaBox [0 0 {{PortraitWidth}} {{PortraitHeight}}]
               /Resources << /Font << /F1 {{font}} 0 R >> >> /Contents {{contentId}} 0 R >>
            """);
        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{page} 0 R] /Count 1 >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {pages} 0 R >>");
        var info = Add(objects, $"<< /Title (Printed form) /Author ({Author}) >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>Creates a one page document with one embedded text file, <see cref="AttachmentName"/>.</summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateWithAttachment()
    {
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var page = Reserve(objects);
        var file = Add(objects, string.Create(CultureInfo.InvariantCulture, $"""
            << /Type /EmbeddedFile /Subtype /text#2Fplain /Length {AttachmentText.Length} /Params << /Size {AttachmentText.Length} >> >>
            stream
            {AttachmentText}
            endstream
            """));
        var spec = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Type /Filespec /F ({AttachmentName}) /UF ({AttachmentName}) /EF << /F {file} 0 R >> >>"));
        var font = Add(objects, HelveticaFont);
        var content = new StringBuilder();
        AppendText(content, PortraitHeight - Margin, HeadingSize, "Attachments");
        var stream = content.ToString();
        var contentId = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream"));
        objects[page - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Page /Parent {{pages}} 0 R /MediaBox [0 0 {{PortraitWidth}} {{PortraitHeight}}]
               /Resources << /Font << /F1 {{font}} 0 R >> >> /Contents {{contentId}} 0 R >>
            """);
        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{page} 0 R] /Count 1 >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {pages} 0 R /Names << /EmbeddedFiles << /Names [({AttachmentName}) {spec} 0 R] >> >> >>");
        var info = Add(objects, $"<< /Title (Attachments) /Author ({Author}) >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>
    /// Creates a one page document in the compressed PDF 1.5 layout: the catalog, page tree, page and font are packed in
    /// an object stream, and the cross-reference information is a Flate encoded stream with the PNG Up predictor.
    /// </summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateCompressed()
    {
        const string content = "BT /F1 24 Tf 72 720 Td (Compressed) Tj ET";
        string[] packed =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PortraitWidth} {PortraitHeight}] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>"),
            HelveticaFont,
        ];
        var header = new StringBuilder();
        var body = new StringBuilder();
        for (var i = 0; i < packed.Length; i++)
        {
            _ = header.Append(CultureInfo.InvariantCulture, $"{i + 1} {body.Length} ");
            _ = body.Append(packed[i]).Append('\n');
        }

        var objectStream = Deflate(Encoding.ASCII.GetBytes(header.ToString() + body));
        var output = new MemoryStream();
        void Write(string text) => output.Write(Encoding.Latin1.GetBytes(text));
        Write("%PDF-1.7\n%\u00e2\u00e3\u00cf\u00d3\n");
        var contentOffset = (int)output.Length;
        Write(string.Create(CultureInfo.InvariantCulture, $"5 0 obj\n<< /Length {content.Length} >>\nstream\n{content}\nendstream\nendobj\n"));
        var streamOffset = (int)output.Length;
        Write(string.Create(CultureInfo.InvariantCulture, $"6 0 obj\n<< /Type /ObjStm /N {packed.Length} /First {header.Length} /Filter /FlateDecode /Length {objectStream.Length} >>\nstream\n"));
        output.Write(objectStream);
        Write("\nendstream\nendobj\n");
        var xrefOffset = (int)output.Length;

        // Rows of [type(1) field2(4) field3(1)]: object 0 is free, 1-4 sit in object stream 6, 5-7 at byte offsets.
        var rows = new List<(byte Type, int Second, byte Third)> { (XrefFree, 0, byte.MaxValue) };
        for (var i = 0; i < packed.Length; i++)
        {
            rows.Add((XrefPacked, ObjectStreamNumber, (byte)i));
        }

        rows.Add((XrefOffset, contentOffset, 0));
        rows.Add((XrefOffset, streamOffset, 0));
        rows.Add((XrefOffset, xrefOffset, 0));
        var raw = PngUpRows(rows);
        var xref = Deflate(raw);
        Write(string.Create(CultureInfo.InvariantCulture, $"7 0 obj\n<< /Type /XRef /Size {rows.Count} /W [1 4 1] /Root 1 0 R /Filter /FlateDecode"));
        Write(string.Create(CultureInfo.InvariantCulture, $" /DecodeParms << /Predictor 12 /Columns {XrefRowLength} >> /Length {xref.Length} >>\nstream\n"));
        output.Write(xref);
        Write(string.Create(CultureInfo.InvariantCulture, $"\nendstream\nendobj\nstartxref\n{xrefOffset}\n%%EOF\n"));
        return output.ToArray();
    }

    /// <summary>
    /// Writes a two-column article: a running header and a page number on every page, a bold title across both columns
    /// on the first page, columns whose lines are written across the page row by row (as some generators do) and a
    /// small footnote. Reading order must give the title, the left column, the right column, then the footnote.
    /// </summary>
    /// <param name="pageCount">The page count.</param>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateArticle(int pageCount)
    {
        const int left = 72;
        const int right = 320;
        const int top = 640;
        const int leading = 13;
        const int marginSize = 9;
        const int headerDrop = 40;
        const int titleSize = 18;
        const int titleRise = 60;
        const int bodySize = 10;
        const int footnoteSize = 7;
        const int footnoteBaseline = 90;
        const int footerBaseline = 30;
        const int centre = 306;
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var regular = Add(objects, HelveticaFont);
        var bold = Add(objects, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        var pageIds = new int[pageCount];
        for (var i = 0; i < pageCount; i++)
        {
            var content = new StringBuilder();
            void Text(string font, int size, int x, int y, string text) =>
                content.Append(CultureInfo.InvariantCulture, $"BT /{font} {size} Tf {x} {y} Td ({text}) Tj ET\n");
            Text("F1", marginSize, left, PortraitHeight - headerDrop, "Journal of Calm Design");
            if (i == 0)
            {
                Text("F2", titleSize, left, top + titleRise, "Reading Order in Practice");
            }

            string[] leftLines = ["The left column opens with", "a first paragraph that ends.", string.Empty, "A second left paragraph", "follows after a gap."];
            string[] rightLines = ["The right column comes", "next and finishes here."];
            for (var row = 0; row < leftLines.Length; row++)
            {
                var y = top - (row * leading);
                if (leftLines[row].Length > 0)
                {
                    Text("F1", bodySize, left, y, leftLines[row]);
                }

                if (row < rightLines.Length)
                {
                    Text("F1", bodySize, right, y, rightLines[row]);
                }
            }

            Text("F1", footnoteSize, left, footnoteBaseline, "1 A footnote about the source.");
            Text("F1", marginSize, centre, footerBaseline, string.Create(CultureInfo.InvariantCulture, $"Page {i + 1}"));
            var stream = content.ToString();
            var contentId = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream"));
            pageIds[i] = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
                << /Type /Page /Parent {{pages}} 0 R /MediaBox [0 0 {{PortraitWidth}} {{PortraitHeight}}]
                   /Resources << /Font << /F1 {{regular}} 0 R /F2 {{bold}} 0 R >> >> /Contents {{contentId}} 0 R >>
                """));
        }

        var kids = string.Join(' ', pageIds.Select(static id => string.Create(CultureInfo.InvariantCulture, $"{id} 0 R")));
        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {pages} 0 R >>");
        var info = Add(objects, $"<< /Title (Article) /Author ({Author}) >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>
    /// Creates a one page document with two layers: "Drawing", shown, holds a black box on the left; "Notes", hidden by
    /// default, holds a black box on the right.
    /// </summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateWithLayers()
    {
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var page = Reserve(objects);
        var drawing = Add(objects, $"<< /Type /OCG /Name ({DrawingLayer}) >>");
        var notes = Add(objects, $"<< /Type /OCG /Name ({NotesLayer}) >>");
        var left = string.Create(CultureInfo.InvariantCulture, $"/OC /L1 BDC 0 0 0 rg {LayerBoxLeft} {LayerBoxBottom} {LayerBoxSize} {LayerBoxSize} re f EMC\n");
        var right = string.Create(CultureInfo.InvariantCulture, $"/OC /L2 BDC 0 0 0 rg {LayerBoxRight} {LayerBoxBottom} {LayerBoxSize} {LayerBoxSize} re f EMC\n");
        var stream = left + right;
        var content = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Length {stream.Length} >>\nstream\n{stream}endstream"));
        objects[page - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Page /Parent {{pages}} 0 R /MediaBox [0 0 {{PortraitWidth}} {{PortraitHeight}}]
               /Resources << /Properties << /L1 {{drawing}} 0 R /L2 {{notes}} 0 R >> >> /Contents {{content}} 0 R >>
            """);
        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{page} 0 R] /Count 1 >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Catalog /Pages {{pages}} 0 R
               /OCProperties << /OCGs [{{drawing}} 0 R {{notes}} 0 R] /D << /Order [{{drawing}} 0 R {{notes}} 0 R] /OFF [{{notes}} 0 R] >> >> >>
            """);
        var info = Add(objects, $"<< /Title (Layers) /Author ({Author}) >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>
    /// Creates a one page tagged document whose drawing order and position disagree with its logical order: a
    /// running header marked as an artifact, a level 1 heading, a first paragraph drawn below a second one, and a
    /// figure with alternative text.
    /// </summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateTagged()
    {
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var page = Reserve(objects);
        var font = Add(objects, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        var stream = string.Create(CultureInfo.InvariantCulture, $"""
            /Artifact BMC BT /F1 9 Tf 72 760 Td ({TaggedHeader}) Tj ET EMC
            /P << /MCID 2 >> BDC BT /F1 12 Tf 72 650 Td ({TaggedSecond}) Tj ET EMC
            /H1 << /MCID 0 >> BDC BT /F1 20 Tf 72 700 Td ({TaggedHeading}) Tj ET EMC
            /P << /MCID 1 >> BDC BT /F1 12 Tf 72 600 Td ({TaggedFirst}) Tj ET EMC
            /Figure << /MCID 3 >> BDC 0 0 0 rg 72 400 100 50 re f EMC

            """);
        var content = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Length {stream.Length} >>\nstream\n{stream}endstream"));
        var root = Reserve(objects);
        var document = Reserve(objects);
        var heading = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructElem /S /H1 /P {document} 0 R /Pg {page} 0 R /K 0 >>"));
        var first = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructElem /S /P /P {document} 0 R /Pg {page} 0 R /K 1 >>"));
        var second = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructElem /S /P /P {document} 0 R /Pg {page} 0 R /K 2 >>"));
        var figure = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructElem /S /Figure /P {document} 0 R /Pg {page} 0 R /K 3 /Alt ({TaggedFigure}) >>"));
        objects[document - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructElem /S /Document /P {root} 0 R /K [{heading} 0 R {first} 0 R {second} 0 R {figure} 0 R] >>");
        objects[root - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /StructTreeRoot /K [{{document}} 0 R]
               /ParentTree << /Nums [0 [{{heading}} 0 R {{first}} 0 R {{second}} 0 R {{figure}} 0 R]] >> /ParentTreeNextKey 1 >>
            """);
        objects[page - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Page /Parent {{pages}} 0 R /MediaBox [0 0 {{PortraitWidth}} {{PortraitHeight}}]
               /Resources << /Font << /F1 {{font}} 0 R >> >> /Contents {{content}} 0 R /StructParents 0 >>
            """);
        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{page} 0 R] /Count 1 >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {pages} 0 R /MarkInfo << /Marked true >> /StructTreeRoot {root} 0 R /Lang (en-GB) >>");
        var info = Add(objects, $"<< /Title (Tagged) /Author ({Author}) >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>
    /// Creates a one page drawing that declares its scale, as engineering PDFs do: a viewport covering the page whose
    /// measure dictionary says 1 in on paper is 10 ft, with a matching number format.
    /// </summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateWithViewport()
    {
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var page = Reserve(objects);
        const string stream = "0 0 0 RG 72 72 m 216 72 l S\n";
        var content = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Length {stream.Length} >>\nstream\n{stream}endstream"));
        var measure = Add(objects, $"<< /Type /Measure /Subtype /RL /R ({ViewportScale}) /X [<< /Type /NumberFormat /U (ft) /C 0.138889 /D 100 >>] >>");
        objects[page - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Page /Parent {{pages}} 0 R /MediaBox [0 0 {{PortraitWidth}} {{PortraitHeight}}] /Contents {{content}} 0 R
               /VP [<< /Type /Viewport /BBox [0 0 {{PortraitWidth}} {{PortraitHeight}}] /Measure {{measure}} 0 R >>] >>
            """);
        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{page} 0 R] /Count 1 >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {pages} 0 R >>");
        var info = Add(objects, $"<< /Title (Plan) /Author ({Author}) >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>
    /// Creates a one page file whose XMP claims PDF/A-2b but whose form asks viewers to redraw its fields
    /// (<c>/NeedAppearances true</c>), which PDF/A forbids; saving it drops the claim.
    /// </summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateWithFalsePdfAClaim()
    {
        const string packet = "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
            + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\">"
            + "<pdfaid:part>2</pdfaid:part><pdfaid:conformance>B</pdfaid:conformance></rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var page = Reserve(objects);
        var metadata = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Type /Metadata /Subtype /XML /Length {packet.Length} >>\nstream\n{packet}\nendstream"));
        objects[page - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Page /Parent {pages} 0 R /MediaBox [0 0 {PortraitWidth} {PortraitHeight}] >>");
        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{page} 0 R] /Count 1 >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {pages} 0 R /Metadata {metadata} 0 R /AcroForm << /Fields [] /NeedAppearances true >> >>");
        var info = Add(objects, $"<< /Author ({Author}) >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>Writes a PDF to a new temporary file.</summary>
    /// <param name="pageCount">The number of pages.</param>
    /// <returns>The file path.</returns>
    public static string WriteTempFile(int pageCount)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-test-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, Create(pageCount));
        return path;
    }

    /// <summary>Gets the size of a page in points.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public static void GetPageSize(int pageIndex, out int width, out int height)
    {
        var landscape = pageIndex == LandscapePage - 1;
        width = landscape ? PortraitHeight : PortraitWidth;
        height = landscape ? PortraitWidth : PortraitHeight;
    }

    /// <summary>Reserves an object number to be filled in later.</summary>
    /// <param name="objects">The object list.</param>
    /// <returns>The object number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Reserve(List<string> objects) => Add(objects, string.Empty);

    /// <summary>Adds an object.</summary>
    /// <param name="objects">The object list.</param>
    /// <param name="body">The object body.</param>
    /// <returns>The object number.</returns>
    private static int Add(List<string> objects, string body)
    {
        objects.Add(body);
        return objects.Count;
    }

    /// <summary>Encodes cross-reference rows with the PNG Up predictor: each row is a filter byte, then its bytes minus the row above.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The encoded bytes.</returns>
    private static byte[] PngUpRows(List<(byte Type, int Second, byte Third)> rows)
    {
        var raw = new byte[rows.Count * (XrefRowLength + 1)];
        var previous = new byte[XrefRowLength];
        var row = new byte[XrefRowLength];
        for (var r = 0; r < rows.Count; r++)
        {
            row[0] = rows[r].Type;
            BinaryPrimitives.WriteInt32BigEndian(row.AsSpan(1), rows[r].Second);
            row[^1] = rows[r].Third;
            raw[r * (XrefRowLength + 1)] = PngUp;
            for (var c = 0; c < XrefRowLength; c++)
            {
                raw[(r * (XrefRowLength + 1)) + 1 + c] = (byte)(row[c] - previous[c]);
            }

            row.CopyTo(previous, 0);
        }

        return raw;
    }

    /// <summary>Compresses bytes with zlib, as PDF's FlateDecode expects.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The compressed bytes.</returns>
    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }

    /// <summary>Writes one page, its content stream and its annotations.</summary>
    /// <param name="objects">The object list.</param>
    /// <param name="pageIds">The page object numbers.</param>
    /// <param name="index">The page index.</param>
    /// <param name="pages">The page tree object number.</param>
    /// <param name="font">The font object number.</param>
    private static void WritePage(List<string> objects, int[] pageIds, int index, int pages, int font)
    {
        GetPageSize(index, out var width, out var height);
        var number = index + 1;
        var baseline = height - Margin;
        var content = new StringBuilder();
        AppendText(content, baseline, HeadingSize, $"Page {number}");
        baseline -= ParagraphGap;
        AppendText(content, baseline, BodySize, Sentence);
        baseline -= LineHeight;
        AppendText(content, baseline, BodySize, $"Unique marker page{number}marker");
        var annotations = string.Empty;
        if (index == 0 && pageIds.Length >= LinkedPage)
        {
            baseline -= ParagraphGap;
            AppendText(content, baseline, BodySize, $"Go to page {LinkedPage}");
            var internalLink = Add(objects, $"<< /Type /Annot /Subtype /Link /Rect [{LinkRect(baseline)}] /Border [0 0 0] /Dest [{pageIds[LinkedPage - 1]} 0 R /XYZ 0 {height} 0] >>");
            baseline -= LineHeight;
            AppendText(content, baseline, BodySize, LinkUri);
            var uriLink = Add(objects, $"<< /Type /Annot /Subtype /Link /Rect [{LinkRect(baseline)}] /Border [0 0 0] /A << /S /URI /URI ({LinkUri}) >> >>");
            annotations = string.Create(CultureInfo.InvariantCulture, $" /Annots [{internalLink} 0 R {uriLink} 0 R]");
        }

        var stream = content.ToString();
        var contentId = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream"));
        objects[pageIds[index] - 1] = string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /Page /Parent {pages} 0 R /MediaBox [0 0 {width} {height}] /Resources << /Font << /F1 {font} 0 R >> >> /Contents {contentId} 0 R{annotations} >>");
    }

    /// <summary>Appends a line of text to a content stream.</summary>
    /// <param name="content">The content stream.</param>
    /// <param name="baseline">The baseline y coordinate.</param>
    /// <param name="size">The font size.</param>
    /// <param name="text">The text.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendText(StringBuilder content, int baseline, int size, string text) =>
        content.Append(CultureInfo.InvariantCulture, $"BT /F1 {size} Tf {Margin} {baseline} Td ({text}) Tj ET\n");

    /// <summary>Formats the rectangle of a link covering a line of body text.</summary>
    /// <param name="baseline">The baseline.</param>
    /// <returns>The rectangle as PDF array contents.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string LinkRect(int baseline) =>
        string.Create(CultureInfo.InvariantCulture, $"{Margin} {baseline - LinkDescent} {Margin + LinkWidth} {baseline + LineHeight - LinkDescent}");

    /// <summary>Writes the outline entries, one per page.</summary>
    /// <param name="objects">The object list.</param>
    /// <param name="pageIds">The page object numbers.</param>
    /// <param name="parent">The outline root object number.</param>
    /// <returns>The outline entry object numbers.</returns>
    private static int[] WriteOutline(List<string> objects, int[] pageIds, int parent)
    {
        var ids = new int[pageIds.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            ids[i] = Reserve(objects);
        }

        for (var i = 0; i < ids.Length; i++)
        {
            GetPageSize(i, out _, out var height);
            var links = new StringBuilder();
            if (i > 0)
            {
                _ = links.Append(CultureInfo.InvariantCulture, $" /Prev {ids[i - 1]} 0 R");
            }

            if (i < ids.Length - 1)
            {
                _ = links.Append(CultureInfo.InvariantCulture, $" /Next {ids[i + 1]} 0 R");
            }

            objects[ids[i] - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Title (Chapter {i + 1}) /Parent {parent} 0 R /Dest [{pageIds[i]} 0 R /XYZ 0 {height} 0]{links} >>");
        }

        return ids;
    }

    /// <summary>Serialises objects with a cross reference table.</summary>
    /// <param name="objects">The objects.</param>
    /// <param name="root">The catalog object number.</param>
    /// <param name="info">The info dictionary object number.</param>
    /// <returns>The file bytes.</returns>
    private static byte[] Serialize(List<string> objects, int root, int info)
    {
        var output = new StringBuilder("%PDF-1.7\n");
        var offsets = new int[objects.Count];
        for (var i = 0; i < objects.Count; i++)
        {
            offsets[i] = Encoding.ASCII.GetByteCount(output.ToString());
            _ = output.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = Encoding.ASCII.GetByteCount(output.ToString());
        _ = output.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            _ = output.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        _ = output.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Count + 1} /Root {root} 0 R /Info {info} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(output.ToString());
    }
}
