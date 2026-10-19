// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Tests for reading the structure tree: kids, role maps, namespaces, attributes, ids and the parent tree.</summary>
public sealed class StructureTreeTests
{
    /// <summary>The number of elements under the basic document.</summary>
    private const int BasicKids = 4;

    /// <summary>The marked content id of the first paragraph.</summary>
    private const int FirstMcid = 1;

    /// <summary>The marked content id of the second paragraph, given through a marked content reference.</summary>
    private const int SecondMcid = 2;

    /// <summary>The figure's place among the document's kids.</summary>
    private const int FigureKid = 3;

    /// <summary>The second paragraph's place among the document's kids.</summary>
    private const int SecondKid = 2;

    /// <summary>The number of table rows.</summary>
    private const int TableRows = 3;

    /// <summary>The span of the wide cell.</summary>
    private const int WideSpan = 2;

    /// <summary>The note's place in the chapter.</summary>
    private const int NoteKid = 2;

    /// <summary>The loop document's kids once its repeat of itself is refused.</summary>
    private const int LoopRootKids = 2;

    /// <summary>The kid of the second loop element that is a marked content id.</summary>
    private const int LoopMcid = 1;

    /// <summary>A document without a structure tree has none, and is not marked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntaggedDocumentHasNoTree()
    {
        using var document = PdfDocumentReader.Open(TaggedSamples.TwoColumns(), null);

        await Assert.That(PdfDocumentTagged.GetStructureTree(document)).IsNull();
        await Assert.That(PdfStructureTree.Load(document)).IsNull();
    }

    /// <summary>The tree is read once and shared.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TreeIsReadOnce()
    {
        using var document = PdfDocumentReader.Open(TaggedSamples.Basic(), null);

        await Assert.That(PdfDocumentTagged.GetStructureTree(document)).IsSameReferenceAs(PdfDocumentTagged.GetStructureTree(document));
        await Assert.That(PdfDocumentTagged.GetStructureTree(document)!.IsMarked).IsTrue();
    }

    /// <summary>Elements resolve through the role map, keep their own type, and read their kids in order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsElementsThroughTheRoleMap()
    {
        using var document = PdfDocumentReader.Open(TaggedSamples.Basic(), null);
        var tree = PdfDocumentTagged.GetStructureTree(document)!;
        var root = tree.Roots[0];
        var kids = root.Kids;

        await Assert.That(tree.Roots.Count).IsEqualTo(1);
        await Assert.That(root.Type).IsEqualTo(PdfStructureType.Document);
        await Assert.That(kids.Count).IsEqualTo(BasicKids);
        await Assert.That(kids[0].Element!.RawType).IsEqualTo("Heading1");
        await Assert.That(kids[0].Element!.MappedType).IsEqualTo("H1");
        await Assert.That(kids[0].Element!.Type).IsEqualTo(PdfStructureType.Heading1);
        await Assert.That(kids[0].Element!.HeadingLevel).IsEqualTo(1);
        await Assert.That(kids[1].Element!.Type).IsEqualTo(PdfStructureType.Paragraph);
        await Assert.That(kids[FigureKid].Element!.Role).IsEqualTo(PdfSemanticRole.Figure);
        await Assert.That(kids[FigureKid].Element!.AlternateText).IsEqualTo(TaggedSamples.FigureAlt);
        await Assert.That(tree.ElementCount).IsEqualTo(BasicKids + 1);
    }

    /// <summary>Integer kids and marked content references both give marked content ids on the element's page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsMarkedContentKids()
    {
        using var document = PdfDocumentReader.Open(TaggedSamples.Basic(), null);
        var kids = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids;
        var first = kids[1].Element!.Kids[0];
        var second = kids[SecondKid].Element!.Kids[0];

        await Assert.That(first.Kind).IsEqualTo(PdfStructureKidKind.MarkedContent);
        await Assert.That(first.Mcid).IsEqualTo(FirstMcid);
        await Assert.That(first.PageIndex).IsEqualTo(0);
        await Assert.That(second.Kind).IsEqualTo(PdfStructureKidKind.MarkedContent);
        await Assert.That(second.Mcid).IsEqualTo(SecondMcid);
        await Assert.That(second.PageIndex).IsEqualTo(0);
    }

    /// <summary>The language is inherited from the parent, and from the catalog at the top.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InheritsTheLanguage()
    {
        using var document = PdfDocumentReader.Open(TaggedSamples.Basic(), null);
        var tree = PdfDocumentTagged.GetStructureTree(document)!;
        var kids = tree.Roots[0].Kids;

        await Assert.That(tree.Language).IsEqualTo(TaggedSamples.DocumentLanguage);
        await Assert.That(tree.Roots[0].Language).IsEqualTo(TaggedSamples.DocumentLanguage);
        await Assert.That(kids[0].Element!.Language).IsEqualTo(TaggedSamples.DocumentLanguage);
        await Assert.That(kids[1].Element!.Language).IsEqualTo(TaggedSamples.FrenchLanguage);
    }

    /// <summary>The page's parent tree entry leads from a marked content id back to its element.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParentTreeLeadsBackToElements()
    {
        using var document = PdfDocumentReader.Open(TaggedSamples.Basic(), null);
        var tree = PdfDocumentTagged.GetStructureTree(document)!;
        var parents = new List<PdfStructureElement?>();
        tree.GetPageParents(0, parents);

        await Assert.That(tree.GetStructParents(0)).IsEqualTo(0);
        await Assert.That(parents.Count).IsEqualTo(BasicKids);
        await Assert.That(tree.GetMarkedContentParent(0, FirstMcid)).IsSameReferenceAs(tree.Roots[0].Kids[1].Element);
        await Assert.That(tree.GetMarkedContentParent(0, BasicKids)).IsNull();
    }

    /// <summary>Table and list attributes come from /A and from classes in the class map.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsAttributesAndClasses()
    {
        using var document = PdfDocumentReader.Open(TaggedSamples.TableAndList(), null);
        var tree = PdfDocumentTagged.GetStructureTree(document)!;
        var table = tree.Roots[0];
        var list = tree.Roots[1];
        var body = table.Kids[1].Element!;
        var bob = body.Kids[1].Element!.Kids[0].Element!;
        var total = body.Kids[TableRows - 1].Element!.Kids[0].Element!;

        await Assert.That(table.Attributes.Summary).IsEqualTo("Ages");
        await Assert.That(list.Attributes.ListNumbering).IsEqualTo("Decimal");
        await Assert.That(bob.Attributes.Headers).IsEquivalentTo(["hname"]);
        await Assert.That(total.Attributes.ColumnSpan).IsEqualTo(WideSpan);
        await Assert.That(total.Attributes.RowSpan).IsEqualTo(1);
        await Assert.That(tree.FindById("hname")!.Type).IsEqualTo(PdfStructureType.TableHeaderCell);
    }

    /// <summary>PDF 2.0 types stay standard, custom namespaces map through their role maps, and PDF 1.7-only types are not standard in PDF 2.0.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResolvesNamespaces()
    {
        using var document = PdfDocumentReader.Open(TaggedSamples.Namespaces(), null);
        var tree = PdfDocumentTagged.GetStructureTree(document)!;
        var chapter = tree.Roots[0].Kids[0].Element!;

        await Assert.That(tree.Roots[0].Namespace).IsEqualTo(PdfStructureTypes.Pdf20Namespace);
        await Assert.That(chapter.Type).IsEqualTo(PdfStructureType.Section);
        await Assert.That(chapter.MappedType).IsEqualTo("Sect");
        await Assert.That(chapter.Kids[0].Element!.Type).IsEqualTo(PdfStructureType.Title);
        await Assert.That(chapter.Kids[0].Element!.HeadingLevel).IsEqualTo(1);
        await Assert.That(chapter.Kids[1].Element!.Type).IsEqualTo(PdfStructureType.Paragraph);
        await Assert.That(chapter.Kids[NoteKid].Element!.Type).IsEqualTo(PdfStructureType.Unknown);
    }

    /// <summary>The ID tree finds an element by its id.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsElementsThroughTheIdTree()
    {
        using var document = PdfDocumentReader.Open(TaggedSamples.Namespaces(), null);
        var tree = PdfDocumentTagged.GetStructureTree(document)!;

        await Assert.That(tree.FindById("intro")!.RawType).IsEqualTo("Para");
        await Assert.That(tree.FindById("missing")).IsNull();
    }

    /// <summary>Loops, repeated kids, deep nesting and a role map loop end without hanging or overflowing the stack.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SurvivesLoopsAndDeepNesting()
    {
        using var document = PdfDocumentReader.Open(TaggedSamples.Cycles(), null);
        var tree = PdfDocumentTagged.GetStructureTree(document)!;
        var root = tree.Roots[0];
        var loop = root.Kids[0].Element!.Kids[0].Element!;

        await Assert.That(tree.Roots.Count).IsEqualTo(1);
        await Assert.That(root.Kids.Count).IsEqualTo(LoopRootKids);
        await Assert.That(loop.Type).IsEqualTo(PdfStructureType.Unknown);
        await Assert.That(loop.Kids.Count).IsEqualTo(1);
        await Assert.That(loop.Kids[0].Mcid).IsEqualTo(LoopMcid);
        await Assert.That(tree.ElementCount).IsLessThan(TaggedSamples.ChainLength);
        await Assert.That(Deepest(root)).IsLessThanOrEqualTo(PdfLimits.MaxNesting);
    }

    /// <summary>Spellings resolve to standard types, including PDF 2.0 headings deeper than six.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsStandardTypes()
    {
        await Assert.That(PdfStructureTypes.Find("TOCI"u8)).IsEqualTo(PdfStructureType.TableOfContentsItem);
        await Assert.That(PdfStructureTypes.Find("H9"u8)).IsEqualTo(PdfStructureType.NumberedHeading);
        await Assert.That(PdfStructureTypes.Find("H0"u8)).IsEqualTo(PdfStructureType.Unknown);
        await Assert.That(PdfStructureTypes.Find("Paragraph"u8)).IsEqualTo(PdfStructureType.Unknown);
        await Assert.That(PdfStructureTypes.IsGrouping(PdfStructureType.TableRow)).IsTrue();
        await Assert.That(PdfStructureTypes.IsGrouping(PdfStructureType.TableDataCell)).IsFalse();
    }

    /// <summary>A structure tree built straight from bytes with no tree root has no elements.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingKidsGiveAnEmptyTree()
    {
        var bytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 4 0 R /MarkInfo << /Marked 1 >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
            "<< /Type /StructTreeRoot >>");
        using var document = PdfDocumentReader.Open(bytes, null);
        var tree = PdfDocumentTagged.GetStructureTree(document)!;

        await Assert.That(tree.IsMarked).IsTrue();
        await Assert.That(tree.Roots.Count).IsEqualTo(0);
        await Assert.That(tree.GetMarkedContentParent(0, 0)).IsNull();
    }

    /// <summary>Finds the deepest element under an element.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The greatest depth.</returns>
    private static int Deepest(PdfStructureElement element)
    {
        var deepest = element.Depth;
        foreach (var kid in element.Kids)
        {
            if (kid.Element is { } child)
            {
                deepest = Math.Max(deepest, Deepest(child));
            }
        }

        return deepest;
    }
}
