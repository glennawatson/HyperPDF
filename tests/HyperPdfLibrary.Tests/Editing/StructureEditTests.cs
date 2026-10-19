// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for <see cref="PdfDocumentStructureEditing"/>.</summary>
public sealed class StructureEditTests
{
    /// <summary>The description the tests give the figure.</summary>
    private const string NewDescription = "A thick black bar across the page";

    /// <summary>The catalog of the small tagged documents the tests build.</summary>
    private const string CatalogObject = "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> >>";

    /// <summary>The page tree of the small tagged documents.</summary>
    private const string PagesObject = "<< /Type /Pages /Count 1 /Kids [3 0 R] >>";

    /// <summary>The page of the small tagged documents.</summary>
    private const string PageObject = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>";

    /// <summary>The number of children the document keeps after a refused change.</summary>
    private const int Two = 2;

    /// <summary>The depth of a paragraph inside a section under the document.</summary>
    private const int NestedDepth = 2;

    /// <summary>A type that only the PDF 2.0 namespace defines.</summary>
    private const string AsideType = "Aside";

    /// <summary>A language tag the tests set.</summary>
    private const string Language = "fr-CA";

    /// <summary>The elements left after the section is removed: the document and the paragraph.</summary>
    private const int GroupedWithoutSection = 2;

    /// <summary>A description and a language set on an element survive saving, and undo restores the original.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextEntriesSurviveSaveAndUndo(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateTagged(), null);
        var figure = Find(document, PdfStructureType.Figure);

        await PdfDocumentStructureEditing.SetTextAsync(document, figure, PdfStructureTextField.AlternateText, NewDescription, cancellationToken);
        await PdfDocumentStructureEditing.SetTextAsync(document, Find(document, PdfStructureType.Figure), PdfStructureTextField.Language, Language, cancellationToken);

        foreach (var file in new[] { EditChecks.SaveIncremental(document), EditChecks.SaveCompact(document) })
        {
            using var reopened = PdfDocumentReader.Open(file, null);
            var element = Find(reopened, PdfStructureType.Figure);
            await Assert.That(element.AlternateText).IsEqualTo(NewDescription);
            await Assert.That(element.Language).IsEqualTo(Language);
        }

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(Find(document, PdfStructureType.Figure).AlternateText).IsEqualTo(TestPdf.TaggedFigure);
    }

    /// <summary>A heading changes level to another standard type and a non-standard type is refused with nothing changed.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task TypeChangeAcceptsStandardTypesOnly(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateTagged(), null);
        var heading = Find(document, PdfStructureType.Heading1);

        await Assert.That(() => PdfDocumentStructureEditing.SetType(document, heading, "MadeUp")).Throws<ArgumentException>();
        await PdfDocumentStructureEditing.SetTypeAsync(document, heading, "H2", cancellationToken);

        await Assert.That(Find(document, PdfStructureType.Heading2).RawType).IsEqualTo("H2");
    }

    /// <summary>An invalid language tag is refused and an empty text clears the entry.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidLanguageIsRefusedAndEmptyClears(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateTagged(), null);
        var figure = Find(document, PdfStructureType.Figure);

        await Assert.That(() => PdfDocumentStructureEditing.SetText(document, figure, PdfStructureTextField.Language, "not a tag!")).Throws<ArgumentException>();
        await PdfDocumentStructureEditing.SetTextAsync(document, figure, PdfStructureTextField.AlternateText, string.Empty, cancellationToken);

        await Assert.That(Find(document, PdfStructureType.Figure).AlternateText).IsNull();
    }

    /// <summary>Moving a paragraph earlier changes the reading order and survives saving; moving past the end does nothing.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task MoveChangesReadingOrder(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateTagged(), null);
        var paragraph = Find(document, PdfStructureType.Paragraph);
        var siblings = paragraph.Parent is { } parent ? parent.Kids.Count : PdfDocumentTagged.GetStructureTree(document)!.Roots.Count;
        var before = Order(document);

        var moved = await PdfDocumentStructureEditing.MoveAsync(document, paragraph, siblings, cancellationToken);

        await Assert.That(moved).IsTrue();
        await Assert.That(Order(document).SequenceEqual(before)).IsFalse();
        await Assert.That(Order(document)[^1]).IsEqualTo(before[2]);
        using var reopened = PdfDocumentReader.Open(EditChecks.SaveIncremental(document), null);
        await Assert.That(Order(reopened).SequenceEqual(Order(document))).IsTrue();
        await Assert.That(await PdfDocumentStructureEditing.MoveAsync(reopened, Find(reopened, PdfStructureType.Heading1), -1, cancellationToken)).IsFalse();
    }

    /// <summary>Every offered type is accepted, and the PDF 1.7 file does not offer the PDF 2.0 only types.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OfferedTypesAreAcceptedAndMatchTheVersion(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateTagged(), null);
        var heading = Find(document, PdfStructureType.Heading1);
        var choices = PdfDocumentStructureEditing.GetTypeChoices(document, heading).ToArray();

        await Assert.That(choices).Contains("P").And.Contains("H3").And.DoesNotContain(AsideType);
        var number = heading.Id.Number;
        foreach (var choice in choices)
        {
            await PdfDocumentStructureEditing.SetTypeAsync(document, FindById(document, number), choice, cancellationToken);
            await Assert.That(FindById(document, number).RawType).IsEqualTo(choice);
        }

        await Assert.That(() => PdfDocumentStructureEditing.SetType(document, FindById(document, number), AsideType)).Throws<ArgumentException>();
    }

    /// <summary>An element with content is not removed.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ElementWithContentIsNotRemoved(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateTagged(), null);
        var paragraph = Find(document, PdfStructureType.Paragraph);

        await Assert.That(async () => await PdfDocumentStructureEditing.RemoveAsync(document, paragraph, cancellationToken)).Throws<InvalidOperationException>();
    }

    /// <summary>An empty grouping tag is removed and the tag inside it moves up to its parent; undo puts the group back.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyGroupIsRemovedAndItsKidsMoveUp(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(GroupedPdf(), null);
        var group = Find(document, PdfStructureType.Section);

        await PdfDocumentStructureEditing.RemoveAsync(document, group, cancellationToken);

        using var reopened = PdfDocumentReader.Open(EditChecks.SaveIncremental(document), null);
        foreach (var source in new[] { document, reopened })
        {
            var paragraph = Find(source, PdfStructureType.Paragraph);
            await Assert.That(paragraph.Parent?.Type).IsEqualTo(PdfStructureType.Document);
            await Assert.That(Order(source).Count).IsEqualTo(GroupedWithoutSection);
        }

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(Find(document, PdfStructureType.Paragraph).Parent?.Type).IsEqualTo(PdfStructureType.Section);
    }

    /// <summary>A tag moves out of its parent and back into the tag above it; both survive a save, undo works, and the first tag has nothing above.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnnestAndNestMoveATagBetweenLevels(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(GroupedPdf(), null);

        await PdfDocumentStructureEditing.UnnestAsync(document, Find(document, PdfStructureType.Paragraph), cancellationToken);

        using var moved = PdfDocumentReader.Open(EditChecks.SaveIncremental(document), null);
        foreach (var source in new[] { document, moved })
        {
            var paragraph = Find(source, PdfStructureType.Paragraph);
            await Assert.That(paragraph.Parent?.Type).IsEqualTo(PdfStructureType.Document);
            await Assert.That(paragraph.Depth).IsEqualTo(1);
        }

        await PdfDocumentStructureEditing.NestAsync(document, Find(document, PdfStructureType.Paragraph), cancellationToken);
        var nested = Find(document, PdfStructureType.Paragraph);
        await Assert.That(nested.Parent?.Type).IsEqualTo(PdfStructureType.Section);
        await Assert.That(nested.Depth).IsEqualTo(NestedDepth);
        await Assert.That(async () => await PdfDocumentStructureEditing.NestAsync(document, Find(document, PdfStructureType.Section), cancellationToken)).Throws<InvalidOperationException>();
        await Assert.That(async () => await PdfDocumentStructureEditing.UnnestAsync(document, Find(document, PdfStructureType.Document), cancellationToken)).Throws<InvalidOperationException>();

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(Find(document, PdfStructureType.Paragraph).Parent?.Type).IsEqualTo(PdfStructureType.Document);
    }

    /// <summary>An element in the PDF 2.0 namespace is offered the PDF 2.0 types, takes one, and keeps its namespace through a save.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task Pdf20NamespaceElementTakesPdf20Types(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(Pdf20Pdf(), null);
        var paragraph = Find(document, PdfStructureType.Paragraph);

        await Assert.That(PdfDocumentStructureEditing.GetTypeChoices(document, paragraph).ToArray()).Contains(AsideType);
        await PdfDocumentStructureEditing.SetTypeAsync(document, paragraph, AsideType, cancellationToken);

        using var reopened = PdfDocumentReader.Open(EditChecks.SaveIncremental(document), null);
        var aside = Find(reopened, PdfStructureType.Aside);
        await Assert.That(aside.RawType).IsEqualTo(AsideType);
        await Assert.That(aside.Namespace).IsEqualTo(PdfStructureTypes.Pdf20Namespace);
    }

    /// <summary>In a PDF 2.0 tree a type that cannot go inside the parent is not offered and is refused, and a paragraph cannot be nested inside a paragraph.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task Pdf20HierarchyRulesRefuseMisplacedTags(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(Pdf20TwoParagraphsPdf(), null);
        var second = Find(document, PdfStructureType.Paragraph).Parent!.Kids[1].Element!;

        var choices = PdfDocumentStructureEditing.GetTypeChoices(document, second);
        var typeError = await Assert.That(() => PdfDocumentStructureEditing.SetType(document, second, "TD")).Throws<InvalidOperationException>();
        var nestError = await Assert.That(async () => await PdfDocumentStructureEditing.NestAsync(document, second, cancellationToken)).Throws<InvalidOperationException>();

        using (Assert.Multiple())
        {
            await Assert.That(choices.Contains("Sect")).IsTrue();
            await Assert.That(choices.Contains("TD")).IsFalse();
            await Assert.That(typeError!.Message).Contains("cannot go inside");
            await Assert.That(nestError!.Message).Contains("cannot go inside");
            await Assert.That(second.Parent!.Kids.Count).IsEqualTo(Two);
        }
    }

    /// <summary>Makes a PDF 2.0 document with two paragraphs in the Document.</summary>
    /// <returns>The file.</returns>
    private static byte[] Pdf20TwoParagraphsPdf() => MiniPdf.Build(
    [
        CatalogObject,
        PagesObject,
        PageObject,
        "<< /Type /StructElem /S /Document /NS 8 0 R /P 5 0 R /K [6 0 R 7 0 R] >>",
        "<< /Type /StructTreeRoot /K 4 0 R /Namespaces [8 0 R] >>",
        "<< /Type /StructElem /S /P /NS 8 0 R /P 4 0 R /Pg 3 0 R /K 0 >>",
        "<< /Type /StructElem /S /P /NS 8 0 R /P 4 0 R /Pg 3 0 R /K 1 >>",
        "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>",
    ]);

    /// <summary>Makes a document whose tags are in the PDF 2.0 namespace.</summary>
    /// <returns>The file.</returns>
    private static byte[] Pdf20Pdf() => MiniPdf.Build(
    [
        CatalogObject,
        PagesObject,
        PageObject,
        "<< /Type /StructElem /S /Document /NS 7 0 R /P 5 0 R /K [6 0 R] >>",
        "<< /Type /StructTreeRoot /K 4 0 R /Namespaces [7 0 R] >>",
        "<< /Type /StructElem /S /P /NS 7 0 R /P 4 0 R /Pg 3 0 R /K 0 >>",
        "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>",
    ]);

    /// <summary>Makes a document whose paragraph sits inside a section that holds nothing else.</summary>
    /// <returns>The file.</returns>
    private static byte[] GroupedPdf() => MiniPdf.Build(
    [
        CatalogObject,
        PagesObject,
        PageObject,
        "<< /Type /StructElem /S /Document /P 5 0 R /K [6 0 R] >>",
        "<< /Type /StructTreeRoot /K 4 0 R >>",
        "<< /Type /StructElem /S /Sect /P 4 0 R /K [7 0 R] >>",
        "<< /Type /StructElem /S /P /P 6 0 R /Pg 3 0 R /K 0 >>",
    ]);

    /// <summary>Finds the first element of a type.</summary>
    /// <param name="document">The document.</param>
    /// <param name="type">The type.</param>
    /// <returns>The element.</returns>
    /// <exception cref="InvalidOperationException">The document has no element of the type.</exception>
    private static PdfStructureElement Find(PdfDocument document, PdfStructureType type)
    {
        var stack = new Stack<PdfStructureElement>(PdfDocumentTagged.GetStructureTree(document)!.Roots.Reverse());
        while (stack.Count > 0)
        {
            var element = stack.Pop();
            if (element.Type == type)
            {
                return element;
            }

            foreach (var kid in element.Kids.Reverse())
            {
                if (kid.Element is { } child)
                {
                    stack.Push(child);
                }
            }
        }

        throw new InvalidOperationException($"No {type} element.");
    }

    /// <summary>Finds an element by its object number.</summary>
    /// <param name="document">The document.</param>
    /// <param name="number">The object number.</param>
    /// <returns>The element.</returns>
    /// <exception cref="InvalidOperationException">The document has no such element.</exception>
    private static PdfStructureElement FindById(PdfDocument document, int number)
    {
        var stack = new Stack<PdfStructureElement>(PdfDocumentTagged.GetStructureTree(document)!.Roots);
        while (stack.TryPop(out var element))
        {
            if (element.Id.Number == number)
            {
                return element;
            }

            foreach (var kid in element.Kids)
            {
                if (kid.Element is { } child)
                {
                    stack.Push(child);
                }
            }
        }

        throw new InvalidOperationException($"No element {number}.");
    }

    /// <summary>Lists the object numbers of the elements in reading order.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The object numbers.</returns>
    private static List<int> Order(PdfDocument document)
    {
        var result = new List<int>();
        var stack = new Stack<PdfStructureElement>(PdfDocumentTagged.GetStructureTree(document)!.Roots.Reverse());
        while (stack.Count > 0)
        {
            var element = stack.Pop();
            result.Add(element.Id.Number);
            foreach (var kid in element.Kids.Reverse())
            {
                if (kid.Element is { } child)
                {
                    stack.Push(child);
                }
            }
        }

        return result;
    }
}
