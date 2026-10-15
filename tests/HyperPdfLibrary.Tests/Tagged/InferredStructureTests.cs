// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Tests for the tables, lists, pagination artifacts, figures, alignment and language inferred from hand-built pages.</summary>
[NotInParallel]
public sealed class InferredStructureTests
{
    /// <summary>The rows of the ruled table.</summary>
    private const int TableRowCount = 4;

    /// <summary>The cells of a full ruled table row.</summary>
    private const int FullRowCells = 3;

    /// <summary>The cells in the total row, whose first cell spans two columns.</summary>
    private const int TotalRowCells = 2;

    /// <summary>The number of columns a spanning cell covers.</summary>
    private const int SpanOfTwo = 2;

    /// <summary>The items in the numbered list.</summary>
    private const int ListItems = 3;

    /// <summary>The data rows of the aligned table, after its header.</summary>
    private const int AlignedRows = 4;

    /// <summary>The pages of the running header sample.</summary>
    private const int PagesWithHeaders = 3;

    /// <summary>The description the host gives the figure.</summary>
    private const string FigureDescription = "A grey square";

    /// <summary>The left edge of the first recognised word.</summary>
    private const float WordLeft = 72;

    /// <summary>The left edge of the second recognised word.</summary>
    private const float SecondWordLeft = 145;

    /// <summary>The width of a recognised word.</summary>
    private const float WordWidth = 68;

    /// <summary>The top of the recognised words.</summary>
    private const float WordTop = 100;

    /// <summary>The height of the recognised words.</summary>
    private const float WordHeight = 12;

    /// <summary>The recogniser's confidence.</summary>
    private const float WordConfidence = 90;

    /// <summary>A ruled table becomes Table, TR, TH and TD, with the header row and a column span.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InfersRuledTable()
    {
        using var document = TaggedTestDocument.Open(InferredSamples.RuledTable());
        var page = PdfReadingStructure.InferLayout(document, 0);

        var table = page.Nodes.Single(static node => node.Role == PdfSemanticRole.Table);
        await Assert.That(table.Children.Count).IsEqualTo(TableRowCount);
        await Assert.That(table.Children.All(static row => row.Role == PdfSemanticRole.TableRow)).IsTrue();
        var header = table.Children[0].Children;
        await Assert.That(header.Count).IsEqualTo(FullRowCells);
        await Assert.That(header.All(static cell => cell.Role == PdfSemanticRole.TableHeaderCell)).IsTrue();
        await Assert.That(header[0].Text).IsEqualTo("Item");
        await Assert.That(header[0].Scope).IsEqualTo(PdfTableScope.Column);
        var body = table.Children[1].Children;
        await Assert.That(body.All(static cell => cell.Role == PdfSemanticRole.TableCell)).IsTrue();
        await Assert.That(body[2].Text).IsEqualTo("1.20");
        var total = table.Children[TableRowCount - 1].Children;
        await Assert.That(total.Count).IsEqualTo(TotalRowCells);
        await Assert.That(total[0].ColumnSpan).IsEqualTo(SpanOfTwo);
        await Assert.That(total[0].Text).IsEqualTo("Total");
    }

    /// <summary>Rows of text pieces that line up in columns become a table without ruling lines; the heading stays a heading.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InfersAlignedTable()
    {
        using var document = TaggedTestDocument.Open(InferredSamples.AlignedTable());
        var page = PdfReadingStructure.InferLayout(document, 0);

        await Assert.That(page.Nodes[0].Role).IsEqualTo(PdfSemanticRole.Heading);
        var table = page.Nodes.Single(static node => node.Role == PdfSemanticRole.Table);
        await Assert.That(table.Children.Count).IsEqualTo(AlignedRows);
        await Assert.That(table.Children[0].Children[0].Role).IsEqualTo(PdfSemanticRole.TableHeaderCell);
        await Assert.That(table.Children[AlignedRows - 1].Children[2].Text).IsEqualTo("30");
    }

    /// <summary>A numbered list becomes L, LI, Lbl and LBody, and a hanging line stays in its item.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InfersNumberedList()
    {
        using var document = TaggedTestDocument.Open(InferredSamples.NumberedList());
        var page = PdfReadingStructure.InferLayout(document, 0);

        var list = page.Nodes.Single(static node => node.Role == PdfSemanticRole.List);
        await Assert.That(list.ListNumbering).IsEqualTo(PdfListNumbering.Decimal);
        await Assert.That(list.Children.Count).IsEqualTo(ListItems);
        await Assert.That(list.Children[0].Children[0].Role).IsEqualTo(PdfSemanticRole.ListLabel);
        await Assert.That(list.Children[0].Children[0].Text).IsEqualTo("1.");
        await Assert.That(list.Children[0].Children[1].Role).IsEqualTo(PdfSemanticRole.ListBody);
        await Assert.That(list.Children[1].Children[1].Text).IsEqualTo("Read the card that was inside the box and keep it");
        await Assert.That(page.Nodes[0].Role).IsEqualTo(PdfSemanticRole.Paragraph);
        await Assert.That(page.Nodes[^1].Role).IsEqualTo(PdfSemanticRole.Heading);
    }

    /// <summary>Lines that start with a dash make a bulleted list.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InfersDashList()
    {
        using var document = TaggedTestDocument.Open(InferredSamples.DashList());
        var page = PdfReadingStructure.InferLayout(document, 0);

        await Assert.That(page.Nodes.Count).IsEqualTo(1);
        await Assert.That(page.Nodes[0].Role).IsEqualTo(PdfSemanticRole.List);
        await Assert.That(page.Nodes[0].Children.Count).IsEqualTo(ListItems);
    }

    /// <summary>Page numbers are set aside by their form; repeated margin text is set aside with the document's profile.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SetsAsidePaginationArtifacts()
    {
        using var document = TaggedTestDocument.Open(InferredSamples.RunningHeadersAndFooters());
        var profile = await PdfPaginationProfile.FindAsync(document, CancellationToken.None);
        var page = PdfReadingStructure.InferLayout(document, 1, new PdfLayoutInference { Pagination = profile }, CancellationToken.None);
        var alone = PdfReadingStructure.InferLayout(document, 1);

        await Assert.That(document.PageCount).IsEqualTo(PagesWithHeaders);
        await Assert.That(page.Nodes.All(static node => node.Role != PdfSemanticRole.Artifact)).IsTrue();
        await Assert.That(page.Nodes.Any(static node => node.Text.Contains("Annual", StringComparison.Ordinal))).IsFalse();
        PdfPaginationKind[] expected = [PdfPaginationKind.Header, PdfPaginationKind.Footer, PdfPaginationKind.PageNumber];
        await Assert.That(page.Artifacts.Select(static node => node.Pagination).Order()).IsEquivalentTo(expected.Order());
        await Assert.That(page.Artifacts.Single(static node => node.Pagination == PdfPaginationKind.PageNumber).Text).IsEqualTo("Page 2");
        await Assert.That(alone.Artifacts.Single().Pagination).IsEqualTo(PdfPaginationKind.PageNumber);
    }

    /// <summary>A caption is linked to the figure it sits under, and the host's description reaches the figure.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinksCaptionAndDescribesFigure()
    {
        using var document = TaggedTestDocument.OpenWithImages(InferredSamples.FigureWithCaption());
        string? seenCaption = null;
        var options = new PdfLayoutInference { FigureAltText = (_, _, caption) => Describe(caption, ref seenCaption) };
        var page = PdfReadingStructure.InferLayout(document, 0, options, CancellationToken.None);

        var figure = page.Nodes.Single(static node => node.Role == PdfSemanticRole.Figure);
        await Assert.That(figure.Children.Single().Role).IsEqualTo(PdfSemanticRole.Caption);
        await Assert.That(figure.Children[0].Text).IsEqualTo("Figure 1. A grey square");
        await Assert.That(figure.AlternateText).IsEqualTo(FigureDescription);
        await Assert.That(seenCaption).IsEqualTo("Figure 1. A grey square");
        await Assert.That(page.Nodes.Count(static node => node.Role == PdfSemanticRole.Paragraph)).IsEqualTo(SpanOfTwo);
    }

    /// <summary>A narrow sidebar is read after the main text and grouped on its own.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsSidebarAfterMainText()
    {
        using var document = TaggedTestDocument.Open(InferredSamples.Sidebar());
        var page = PdfReadingStructure.InferLayout(document, 0);

        await Assert.That(page.Nodes.Count).IsEqualTo(SpanOfTwo);
        await Assert.That(page.Nodes[0].Role).IsEqualTo(PdfSemanticRole.Paragraph);
        await Assert.That(page.Nodes[0].Text).StartsWith("Main column");
        await Assert.That(page.Nodes[1].Role).IsEqualTo(PdfSemanticRole.Group);
        await Assert.That(page.Nodes[1].Text).Contains("Aside note");
    }

    /// <summary>Paragraphs are tagged with the alignment their line edges show.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DetectsAlignment()
    {
        using var document = TaggedTestDocument.Open(InferredSamples.Alignments());
        var page = PdfReadingStructure.InferLayout(document, 0);

        await Assert.That(page.Nodes.Count).IsEqualTo(ListItems);
        await Assert.That(page.Nodes[0].Alignment).IsEqualTo(PdfTextAlignment.Center);
        await Assert.That(page.Nodes[1].Alignment).IsEqualTo(PdfTextAlignment.Start);
        await Assert.That(page.Nodes[SpanOfTwo].Alignment).IsEqualTo(PdfTextAlignment.End);
    }

    /// <summary>A language is guessed only on request, from the node's own text, and is kept apart from the document language.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GuessesLanguageOnRequest()
    {
        using var document = TaggedTestDocument.Open(InferredSamples.Languages());
        var without = PdfReadingStructure.InferLayout(document, 0);
        var guessed = PdfReadingStructure.InferLayout(document, 0, new PdfLayoutInference { GuessLanguage = true }, CancellationToken.None);

        await Assert.That(without.Nodes.All(static node => node.InferredLanguage is null)).IsTrue();
        await Assert.That(guessed.Nodes[0].InferredLanguage).IsEqualTo("en");
        await Assert.That(guessed.Nodes[1].InferredLanguage).IsEqualTo("de");
        await Assert.That(guessed.Nodes[0].Language).IsNull();
    }

    /// <summary>An image-only page is handed to the recogniser, and its words become the page's nodes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsImageOnlyPageThroughRecogniser()
    {
        using var document = TaggedTestDocument.OpenWithImages(InferredSamples.ImageOnlyPage());
        var calls = 0;
        PdfOcrWord[] words = [Word("Recognised", WordLeft), Word("text", SecondWordLeft)];
        var page = await PdfReadingStructure.ReadAsync(
            document,
            0,
            (_, _) =>
            {
                calls++;
                return ValueTask.FromResult<IReadOnlyList<PdfOcrWord>?>(words);
            },
            CancellationToken.None);

        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(page.Origin).IsEqualTo(PdfNodeOrigin.Ocr);
        await Assert.That(page.Nodes.Single().Text).IsEqualTo("Recognised text");
    }

    /// <summary>A page with text is never handed to the recogniser.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkipsRecogniserForTextPages()
    {
        using var document = TaggedTestDocument.Open(InferredSamples.Languages());
        var calls = 0;
        var page = await PdfReadingStructure.ReadAsync(
            document,
            0,
            (_, _) =>
            {
                calls++;
                return ValueTask.FromResult<IReadOnlyList<PdfOcrWord>?>(null);
            },
            CancellationToken.None);

        await Assert.That(calls).IsEqualTo(0);
        await Assert.That(page.Origin).IsEqualTo(PdfNodeOrigin.Inferred);
    }

    /// <summary>Records the caption a figure hook saw and gives the figure's description.</summary>
    /// <param name="caption">The caption text.</param>
    /// <param name="seen">Receives the caption.</param>
    /// <returns>The description.</returns>
    private static string Describe(string? caption, ref string? seen)
    {
        seen = caption;
        return FigureDescription;
    }

    /// <summary>Makes a recognised word on the first line.</summary>
    /// <param name="text">The word.</param>
    /// <param name="left">The left edge.</param>
    /// <returns>The recognised word.</returns>
    private static PdfOcrWord Word(string text, float left) => new(text, new(left, WordTop, left + WordWidth, WordTop + WordHeight), WordConfidence);
}
