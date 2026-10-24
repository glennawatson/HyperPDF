// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks Well-Tagged PDF table regularity (8.2.5.26) and the Ref entries of table of contents items (8.2.5.8).</summary>
public sealed class WtpdfTableReferenceTests
{
    /// <summary>The clause of the table rule.</summary>
    private const string TableClause = "8.2.5.26";

    /// <summary>The catalog of the small tagged files these tests build.</summary>
    private const string CatalogObject = "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> /Lang (en) >>";

    /// <summary>The page tree of the small tagged files.</summary>
    private const string PagesObject = "<< /Type /Pages /Count 1 /Kids [3 0 R] >>";

    /// <summary>The page of the small files.</summary>
    private const string PageObject = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>";

    /// <summary>The Document tag of the small tagged files.</summary>
    private const string DocumentObject = "<< /Type /StructElem /S /Document /P 5 0 R /K [6 0 R] >>";

    /// <summary>The structure tree root of the small tagged files.</summary>
    private const string RootObject = "<< /Type /StructTreeRoot /K 4 0 R >>";

    /// <summary>The object number of the first widget in the form test's file.</summary>
    private const int FirstWidgetObject = 7;

    /// <summary>The zero based slot of the first row object.</summary>
    private const int FirstRowSlot = 6;

    /// <summary>The text that starts a cell's text when the cell is a header (TH) instead of a data cell.</summary>
    private const string HeaderPrefix = "TH;";

    /// <summary>
    /// A table whose rows have the same width passes, counting column and row spans; a ragged table is reported once. Header cells must
    /// say what they label: a header inside the table needs a Scope, Headers names header cells, and Headers is on every cell that has headers.
    /// </summary>
    /// <param name="firstRow">The first row's cell attributes, one entry per cell.</param>
    /// <param name="secondRow">The second row's cell attributes.</param>
    /// <param name="expected">The expected number of table findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("|", "|", 0)]
    [Arguments("|", "||", 1)]
    [Arguments("/A << /O /Table /ColSpan 2 >>|", "||", 0)]
    [Arguments("/A << /O /Table /RowSpan 2 >>|", "", 0)]
    [Arguments("/A << /O /Table /RowSpan 2 >>|", "|", 1)]
    [Arguments("TH;|TH;|", "TH;||", 0)]
    [Arguments("|TH;|", "||", 0)]
    [Arguments("TH;|TH;|", "|TH;|", 1)]
    [Arguments("TH;|TH;|", "|TH;/A << /O /Table /Scope /Row >>|", 0)]
    [Arguments("TH;/ID (h1)|TH;|", "/A << /O /Table /Headers [(nope)] >>|/A << /O /Table /Headers [(h1)] >>|", 1)]
    [Arguments("TH;/ID (h1)|TH;/ID (h2)|", "/A << /O /Table /Headers [(h1)] >>||", 1)]
    [Arguments("TH;/ID (h1)|TH;/ID (h2)|", "/A << /O /Table /Headers [(h1)] >>|/A << /O /Table /Headers [(h2)] >>|", 0)]
    public async Task TablesMustBeRegular(string firstRow, string secondRow, int expected, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(BuildTable(firstRow, secondRow), null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        await Assert.That(report.Findings.Count(static f => f.Clause == TableClause)).IsEqualTo(expected);
    }

    /// <summary>A cell may not span out of its row group.</summary>
    /// <param name="rowSpan">The first body row's row span.</param>
    /// <param name="expected">The expected number of table findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, 0)]
    [Arguments(2, 1)]
    public async Task CellsDoNotSpanOutOfARowGroup(int rowSpan, int expected, CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
        [
            CatalogObject,
            PagesObject,
            PageObject,
            DocumentObject,
            RootObject,
            "<< /Type /StructElem /S /Table /P 4 0 R /K [7 0 R 10 0 R] >>",
            "<< /Type /StructElem /S /THead /P 6 0 R /K [8 0 R] >>",
            "<< /Type /StructElem /S /TR /P 7 0 R /K [9 0 R] >>",
            $"<< /Type /StructElem /S /TD /P 8 0 R /Pg 3 0 R /K 0 /A << /O /Table /RowSpan {rowSpan} >> >>",
            "<< /Type /StructElem /S /TBody /P 6 0 R /K [11 0 R] >>",
            "<< /Type /StructElem /S /TR /P 10 0 R /K [12 0 R] >>",
            "<< /Type /StructElem /S /TD /P 11 0 R /Pg 3 0 R /K 0 >>",
        ]);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        await Assert.That(report.Findings.Count(static f => f.Clause == TableClause)).IsEqualTo(expected);
    }

    /// <summary>A table of contents item without a Ref is reported, and one with a Ref is not.</summary>
    /// <param name="reference">The item's Ref entry, or empty.</param>
    /// <param name="expected">The expected number of findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("", 1)]
    [Arguments("/Ref [4 0 R]", 0)]
    public async Task TocItemsNeedARef(string reference, int expected, CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
        [
            CatalogObject,
            PagesObject,
            PageObject,
            DocumentObject,
            RootObject,
            "<< /Type /StructElem /S /TOC /P 4 0 R /K [7 0 R] >>",
            $"<< /Type /StructElem /S /TOCI /P 6 0 R /Pg 3 0 R /K 0 {reference} >>",
        ]);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        await Assert.That(report.Findings.Count(static f => f.Clause == "8.2.5.8")).IsEqualTo(expected);
    }

    /// <summary>A hidden or zero-size annotation tagged as content is reported; a normal one, or one inside an Artifact tag, is not.</summary>
    /// <param name="flags">The annotation's flags.</param>
    /// <param name="rect">The annotation's rectangle.</param>
    /// <param name="parentType">The type of the tag that holds the annotation.</param>
    /// <param name="expected">The expected number of findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(4, "[0 0 10 10]", "Link", 0)]
    [Arguments(1, "[0 0 10 10]", "Link", 1)]
    [Arguments(32, "[0 0 10 10]", "Link", 1)]
    [Arguments(288, "[0 0 10 10]", "Link", 0)]
    [Arguments(4, "[5 5 5 5]", "Link", 1)]
    [Arguments(1, "[0 0 10 10]", "Artifact", 0)]
    public async Task HiddenAnnotationsMustBeArtifacts(int flags, string rect, string parentType, int expected, CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
        [
            CatalogObject,
            PagesObject,
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Tabs /S /Annots [7 0 R] >>",
            DocumentObject,
            RootObject,
            $"<< /Type /StructElem /S /{parentType} /P 4 0 R /Pg 3 0 R /K [<< /Type /OBJR /Obj 7 0 R /Pg 3 0 R >>] >>",
            $"<< /Type /Annot /Subtype /Widget /FT /Btn /T (b) /F {flags} /Rect {rect} >>",
        ]);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        await Assert.That(report.Findings.Count(static f => f.Clause == "8.9.2.2")).IsEqualTo(expected);
    }

    /// <summary>A footnote without a Ref is reported, and one with a Ref is not.</summary>
    /// <param name="reference">The note's Ref entry, or empty.</param>
    /// <param name="expected">The expected number of findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("", 1)]
    [Arguments("/Ref [4 0 R]", 0)]
    public async Task FootnotesNeedARef(string reference, int expected, CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
        [
            CatalogObject,
            PagesObject,
            PageObject,
            DocumentObject,
            RootObject,
            $"<< /Type /StructElem /S /FENote /P 4 0 R /Pg 3 0 R /K 0 {reference} >>",
        ]);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        await Assert.That(report.Findings.Count(static f => f.Clause == "8.2.5.14.1")).IsEqualTo(expected);
    }

    /// <summary>An embedded non-PDF file with no declaration in its metadata is reported at the accessibility level; one with a declaration, or a PDF, is not.</summary>
    /// <param name="fileName">The embedded file's name.</param>
    /// <param name="withDeclaration">Whether the file's stream has metadata that holds a declaration.</param>
    /// <param name="expected">The expected number of findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("data.csv", false, 1)]
    [Arguments("data.csv", true, 0)]
    [Arguments("other.pdf", false, 0)]
    public async Task EmbeddedFilesNeedADeclaration(string fileName, bool withDeclaration, int expected, CancellationToken cancellationToken)
    {
        const string packet = "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">"
            + "<rdf:Description rdf:about=\"\" xmlns:pdfd=\"http://pdfa.org/declarations/\"><pdfd:declarations><rdf:Bag><rdf:li rdf:parseType=\"Resource\">"
            + "<pdfd:conformsTo>https://www.w3.org/TR/WCAG22/</pdfd:conformsTo></rdf:li></rdf:Bag></pdfd:declarations></rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";
        var bytes = MiniPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles 4 0 R >> /Lang (en) >>",
            PagesObject,
            PageObject,
            $"<< /Names [({fileName}) 5 0 R] >>",
            $"<< /Type /Filespec /F ({fileName}) /UF ({fileName}) /EF << /F 6 0 R >> >>",
            MiniPdf.Stream(withDeclaration ? "/Type /EmbeddedFile /Metadata 7 0 R" : "/Type /EmbeddedFile", "payload"),
            MiniPdf.Stream("/Type /Metadata /Subtype /XML", withDeclaration ? packet : "<x/>"),
        ]);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        await Assert.That(report.Findings.Count(static f => f.Clause == "7.2.2" && f.Profile == PdfWtpdfProfile.Accessibility)).IsEqualTo(expected);
    }

    /// <summary>A Form tag with two widgets is reported; one widget per Form is not.</summary>
    /// <param name="widgets">The number of widgets the Form encloses.</param>
    /// <param name="expected">The expected number of findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, 0)]
    [Arguments(2, 1)]
    public async Task FormEnclosesAtMostOneWidget(int widgets, int expected, CancellationToken cancellationToken)
    {
        var refs = string.Join(' ', Enumerable.Range(0, widgets).Select(static i => $"<< /Type /OBJR /Obj {FirstWidgetObject + i} 0 R /Pg 3 0 R >>"));
        var annots = string.Join(' ', Enumerable.Range(0, widgets).Select(static i => $"{FirstWidgetObject + i} 0 R"));
        var objects = new List<string>
        {
            CatalogObject,
            PagesObject,
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Tabs /S /Annots [{annots}] >>",
            DocumentObject,
            RootObject,
            $"<< /Type /StructElem /S /Form /P 4 0 R /Pg 3 0 R /K [{refs}] >>",
        };
        for (var i = 0; i < widgets; i++)
        {
            objects.Add("<< /Type /Annot /Subtype /Widget /Rect [0 0 10 10] /FT /Btn /T (b) >>");
        }

        using var document = await PdfDocumentReader.OpenAsync(MiniPdf.Build([.. objects]), null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        await Assert.That(report.Findings.Count(static f => f.Clause == "8.10.1")).IsEqualTo(expected);
    }

    /// <summary>Builds a table of two rows; each '|' in a row text ends one cell, and the text before it is that cell's extra entries.</summary>
    /// <param name="firstRow">The first row.</param>
    /// <param name="secondRow">The second row.</param>
    /// <returns>The file.</returns>
    private static byte[] BuildTable(string firstRow, string secondRow)
    {
        var objects = new List<string>
        {
            CatalogObject,
            PagesObject,
            PageObject,
            DocumentObject,
            RootObject,
            "<< /Type /StructElem /S /Table /P 4 0 R /K [7 0 R 8 0 R] >>",
            string.Empty,
            string.Empty,
        };
        AddRow(objects, FirstRowSlot, firstRow);
        AddRow(objects, FirstRowSlot + 1, secondRow);
        return MiniPdf.Build([.. objects]);
    }

    /// <summary>Fills a row object and adds its cells.</summary>
    /// <param name="objects">The file's objects; the row's slot is filled and the cells are appended.</param>
    /// <param name="slot">The zero based slot of the row.</param>
    /// <param name="cells">The cell text.</param>
    private static void AddRow(List<string> objects, int slot, string cells)
    {
        var parent = slot + 1;
        var refs = new List<string>();
        foreach (var cell in cells.Split('|')[..^1])
        {
            var header = cell.StartsWith(HeaderPrefix, StringComparison.Ordinal);
            objects.Add($"<< /Type /StructElem /S /{(header ? "TH" : "TD")} /P {parent} 0 R /Pg 3 0 R /K 0 {(header ? cell[HeaderPrefix.Length..] : cell)} >>");
            refs.Add($"{objects.Count} 0 R");
        }

        objects[slot] = $"<< /Type /StructElem /S /TR /P 6 0 R /K [{string.Join(' ', refs)}] >>";
    }
}
