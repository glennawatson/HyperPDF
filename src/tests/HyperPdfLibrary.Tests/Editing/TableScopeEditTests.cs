// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Checks that the scope of a table header cell can be set, replaced and cleared, and undone.</summary>
public sealed class TableScopeEditTests
{
    /// <summary>The layout attribute object a header cell may already carry.</summary>
    private const string LayoutAttributes = "/A << /O /Layout /Placement /Block >>";

    /// <summary>The rows a cell is given in the span test.</summary>
    private const int Rows = 2;

    /// <summary>The columns a cell is given in the span test.</summary>
    private const int Columns = 3;

    /// <summary>A scope is added, replaced and cleared whatever the cell's attributes looked like, other attribute objects stay, and undo restores the file.</summary>
    /// <param name="attributes">The cell's attribute entry.</param>
    /// <param name="scope">The scope to set.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("", PdfTableScope.Column)]
    [Arguments("/A << /O /Table /Scope /Row >>", PdfTableScope.Both)]
    [Arguments("/A << /O /Table /Scope /Row >>", PdfTableScope.None)]
    [Arguments(LayoutAttributes, PdfTableScope.Row)]
    [Arguments("/A [<< /O /Layout /Placement /Block >> << /O /Table /Scope /Row >>]", PdfTableScope.Column)]
    public async Task ScopeIsSetAndUndone(string attributes, PdfTableScope scope, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(attributes), null, cancellationToken);
        var before = FindHeader(document).Attributes;

        await PdfDocumentStructureEditing.SetTableScopeAsync(document, FindHeader(document), scope, cancellationToken);
        var after = FindHeader(document).Attributes;

        using (Assert.Multiple())
        {
            await Assert.That(after.Scope).IsEqualTo(scope);
            await Assert.That(after.Placement).IsEqualTo(before.Placement);
        }

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(FindHeader(document).Attributes.Scope).IsEqualTo(before.Scope);
    }

    /// <summary>A list's numbering is set, replaced and cleared next to other attribute objects, a made-up name and a non-list are refused, and undo restores it.</summary>
    /// <param name="attributes">The list's attribute entry.</param>
    /// <param name="numbering">The numbering to set; empty clears it.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("", "Decimal")]
    [Arguments("/A << /O /List /ListNumbering /Disc >>", "UpperRoman")]
    [Arguments("/A << /O /List /ListNumbering /Disc >>", "")]
    [Arguments("/A << /O /Layout /Placement /Block >>", "Decimal")]
    public async Task ListNumberingIsSetAndUndone(string attributes, string numbering, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(BuildList(attributes), null, cancellationToken);
        var before = FindList(document).Attributes;

        PdfDocumentStructureEditing.SetListNumbering(document, FindList(document), numbering);
        var after = FindList(document).Attributes;

        using (Assert.Multiple())
        {
            await Assert.That(after.ListNumbering).IsEqualTo(numbering);
            await Assert.That(after.Placement).IsEqualTo(before.Placement);
            await Assert.That(() => PdfDocumentStructureEditing.SetListNumbering(document, FindList(document), "Wingdings")).Throws<ArgumentException>();
        }

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(FindList(document).Attributes.ListNumbering).IsEqualTo(before.ListNumbering);
    }

    /// <summary>A placement is set beside other attributes, cleared with an empty name, refused when unknown, and restored by one undo.</summary>
    /// <param name="attributes">The element's attributes.</param>
    /// <param name="placement">The placement to set.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("", "Block")]
    [Arguments("/A << /O /Layout /Placement /Inline >>", "Before")]
    [Arguments("/A << /O /Layout /Placement /Inline >>", "")]
    [Arguments("/A << /O /List /ListNumbering /Disc >>", "Block")]
    public async Task PlacementIsSetAndUndone(string attributes, string placement, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(BuildList(attributes), null, cancellationToken);
        var before = FindList(document).Attributes;

        PdfDocumentStructureEditing.SetPlacement(document, FindList(document), placement);
        var after = FindList(document).Attributes;

        using (Assert.Multiple())
        {
            await Assert.That(after.Placement).IsEqualTo(placement);
            await Assert.That(after.ListNumbering).IsEqualTo(before.ListNumbering);
            await Assert.That(() => PdfDocumentStructureEditing.SetPlacement(document, FindList(document), "Floating")).Throws<ArgumentException>();
        }

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(FindList(document).Attributes.Placement).IsEqualTo(before.Placement);
    }

    /// <summary>A cell's spans are set next to its scope, a span of 1 clears the entry, a span below 1 is refused, and one undo restores the cell.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task CellSpansAreSetAndUndone(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build("/A << /O /Table /Scope /Row >>"), null, cancellationToken);

        PdfDocumentStructureEditing.SetCellSpans(document, FindHeader(document), Rows, Columns);
        var spanned = FindHeader(document).Attributes;
        PdfDocumentStructureEditing.SetCellSpans(document, FindHeader(document), 1, 1);
        var cleared = FindHeader(document).Attributes;

        using (Assert.Multiple())
        {
            await Assert.That(spanned.RowSpan).IsEqualTo(Rows);
            await Assert.That(spanned.ColumnSpan).IsEqualTo(Columns);
            await Assert.That(spanned.Scope).IsEqualTo(PdfTableScope.Row);
            await Assert.That(cleared.RowSpan).IsEqualTo(1);
            await Assert.That(cleared.ColumnSpan).IsEqualTo(1);
            await Assert.That(() => PdfDocumentStructureEditing.SetCellSpans(document, FindHeader(document), 0, 1)).Throws<ArgumentException>();
        }

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(FindHeader(document).Attributes.ColumnSpan).IsEqualTo(Columns);
    }

    /// <summary>A cell that is not a header has no scope to set.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonHeaderIsRefused(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(string.Empty), null, cancellationToken);
        var table = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;

        await Assert.That(() => PdfDocumentStructureEditing.SetTableScope(document, table, PdfTableScope.Row)).Throws<ArgumentException>();
    }

    /// <summary>Finds the list.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The L element.</returns>
    private static PdfStructureElement FindList(PdfDocument document) => PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;

    /// <summary>Builds a document with one list.</summary>
    /// <param name="attributes">The list's attribute entry.</param>
    /// <returns>The file.</returns>
    private static byte[] BuildList(string attributes) => MiniPdf.Build(
    [
        "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> >>",
        "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
        "<< /Type /StructElem /S /Document /P 5 0 R /K [6 0 R] >>",
        "<< /Type /StructTreeRoot /K 4 0 R >>",
        $"<< /Type /StructElem /S /L /P 4 0 R /Pg 3 0 R /K 0 {attributes} >>",
    ]);

    /// <summary>Finds the header cell.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The TH element.</returns>
    /// <exception cref="InvalidOperationException">The document has no header cell.</exception>
    private static PdfStructureElement FindHeader(PdfDocument document)
    {
        var pending = new Stack<PdfStructureElement>(PdfDocumentTagged.GetStructureTree(document)!.Roots);
        while (pending.TryPop(out var element))
        {
            if (element.MappedType == "TH")
            {
                return element;
            }

            foreach (var kid in element.Kids)
            {
                if (kid.Element is { } child)
                {
                    pending.Push(child);
                }
            }
        }

        throw new InvalidOperationException("The document has no header cell.");
    }

    /// <summary>Builds a document with a table holding one row with one header cell.</summary>
    /// <param name="attributes">The cell's attribute entry.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(string attributes) => MiniPdf.Build(
    [
        "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> >>",
        "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
        "<< /Type /StructElem /S /Document /P 5 0 R /K [6 0 R] >>",
        "<< /Type /StructTreeRoot /K 4 0 R >>",
        "<< /Type /StructElem /S /Table /P 4 0 R /K [7 0 R] >>",
        "<< /Type /StructElem /S /TR /P 6 0 R /K [8 0 R] >>",
        $"<< /Type /StructElem /S /TH /P 7 0 R /Pg 3 0 R /K 0 {attributes} >>",
    ]);
}
