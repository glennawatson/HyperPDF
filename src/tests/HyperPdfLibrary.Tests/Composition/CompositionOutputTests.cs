// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Composition;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Tests.Creation;

namespace HyperPdfLibrary.Tests.Composition;

/// <summary>Writes composed documents and reads them back: the fields, outline, bookmarks, links and structure.</summary>
public sealed class CompositionOutputTests
{
    /// <summary>The height of the test pages, so a page holds a few paragraphs.</summary>
    private const float PageHeight = 400F;

    /// <summary>The number of pages in the field test.</summary>
    private const int PageCount = 3;

    /// <summary>The number of top level outline entries in the outline test.</summary>
    private const int TopLevelEntries = 2;

    /// <summary>The number of named destinations in the outline test: three headings in the outline and one bookmark.</summary>
    private const int Destinations = 4;

    /// <summary>The level of the nested heading.</summary>
    private const int NestedLevel = 2;

    /// <summary>The number of links in the link test.</summary>
    private const int LinkCount = 2;

    /// <summary>The address of the web link.</summary>
    private const string Address = "https://example.com/page";

    /// <summary>The text of the first page's header.</summary>
    private const string CoverHeader = "Cover header";

    /// <summary>The page number and count in the footer are filled in for every page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FieldsAreFilledInForEveryPage()
    {
        PdfParagraph paragraph = new([new PdfRun("Page "), PdfField.PageNumber, new PdfRun(" of "), PdfField.PageCount]) { Style = PdfStyleNames.Footer };
        var footer = PdfRunningContent.Of(paragraph);
        var blocks = new PdfBlock[] { PdfParagraph.Of("one"), PdfPageBreak.Instance, PdfParagraph.Of("two"), PdfPageBreak.Instance, PdfParagraph.Of("three") };

        var pdf = await WriteAsync(false, new PdfSection(blocks) { Setup = SmallPage(), Footer = footer });

        await Assert.That(await CreatedFiles.TextOfAsync(pdf, 0)).Contains("Page 1 of 3");
        await Assert.That(await CreatedFiles.TextOfAsync(pdf, 1)).Contains("Page 2 of 3");
        await Assert.That(await CreatedFiles.TextOfAsync(pdf, PageCount - 1)).Contains("Page 3 of 3");
    }

    /// <summary>A section can show Roman page numbers starting from its own number, and the first page can have its own header.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FirstPageAndNumberFormatsApply()
    {
        var setup = SmallPage() with { DifferentFirstPage = true, PageNumberFormat = PdfNumberFormat.LowerRoman, StartPageNumber = PageCount };
        var section = new PdfSection([PdfParagraph.Of("front"), PdfPageBreak.Instance, PdfParagraph.Of("back")])
        {
            Setup = setup,
            Footer = PdfRunningContent.Of(PdfParagraph.Of(PdfField.PageNumber)),
            FirstPageHeader = PdfRunningContent.Of(PdfParagraph.Of(CoverHeader)),
        };

        var pdf = await WriteAsync(false, section);

        await Assert.That(await CreatedFiles.TextOfAsync(pdf, 0)).Contains(CoverHeader);
        await Assert.That(await CreatedFiles.TextOfAsync(pdf, 1)).DoesNotContain(CoverHeader);
        await Assert.That(await CreatedFiles.TextOfAsync(pdf, 1)).Contains("iv");
    }

    /// <summary>Headings become outline entries nested by level, and bookmarks become named destinations.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutlineAndBookmarksAreWritten()
    {
        PdfBlock[] blocks =
        [
            PdfParagraph.Heading(1, "First"),
            PdfParagraph.Of(new PdfBookmark("here"), new PdfRun("body")),
            PdfParagraph.Heading(NestedLevel, "Nested"),
            PdfParagraph.Heading(1, "Second"),
        ];

        var pdf = await WriteAsync(false, new PdfSection(blocks) { Setup = SmallPage() });
        using var document = PdfDocumentReader.Open(pdf, null);
        var outline = PdfDocumentNavigation.GetOutline(document);
        var names = PdfDocumentNamedDestinations.GetNamedDestinations(document);

        await Assert.That(outline.Count).IsEqualTo(TopLevelEntries);
        await Assert.That(outline[0].Title).IsEqualTo("First");
        await Assert.That(outline[0].Children.Length).IsEqualTo(1);
        await Assert.That(outline[0].Children[0].Title).IsEqualTo("Nested");
        await Assert.That(outline[1].Title).IsEqualTo("Second");
        await Assert.That(names.Count).IsEqualTo(Destinations);
        await Assert.That(names.Any(static n => n.Name == "here")).IsTrue();
    }

    /// <summary>A web link and a link to a bookmark are written as link annotations.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinksAreWritten()
    {
        var first = PdfHyperlink.To(PdfLinkTarget.ToUri(Address), "the page");
        var second = PdfHyperlink.To(PdfLinkTarget.ToBookmark("target"), "the target");
        PdfBlock[] blocks = [PdfParagraph.Of(new PdfRun("See "), first, new PdfRun(" or "), second), PdfParagraph.Of(new PdfBookmark("target"), new PdfRun("destination"))];

        var pdf = await WriteAsync(false, new PdfSection(blocks) { Setup = SmallPage() });
        using var document = PdfDocumentReader.Open(pdf, null);
        var links = PdfDocumentLinks.GetLinks(document, 0);

        await Assert.That(links.Count).IsEqualTo(LinkCount);
        await Assert.That(links.Any(static l => l.Action.Value is UriAction { Uri: Address })).IsTrue();
        await Assert.That(links.Any(static l => l.Action.Value is GoToAction)).IsTrue();
    }

    /// <summary>A tagged document has headings, paragraphs, lists, links and notes in its structure.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedDocumentsHaveStructure()
    {
        PdfBlock[] blocks =
        [
            PdfParagraph.Heading(1, "Title"),
            PdfParagraph.Of(new PdfRun("text "), PdfHyperlink.To(PdfLinkTarget.ToUri(Address), "link"), PdfFootnote.Of("a note")),
            PdfList.Bulleted("one", "two"),
        ];

        var pdf = await WriteAsync(true, new PdfSection(blocks) { Setup = SmallPage() });
        CreatedFiles.Keep("composition-tagged.pdf", pdf);
        using var document = PdfDocumentReader.Open(pdf, null);
        var types = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in PdfDocumentTagged.GetStructureTree(document)!.Roots)
        {
            Collect(root, types);
        }

        await Assert.That(types.IsSupersetOf(["H1", "P", "L", "LI", "Lbl", "LBody", "Link", "Note"])).IsTrue();
    }

    /// <summary>A link to a bookmark that does not exist is reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AMissingBookmarkIsReported()
    {
        var creator = CompositionKit.CreateCreator(false);
        var lost = PdfHyperlink.To(PdfLinkTarget.ToBookmark("nowhere"), "lost");
        var composition = new PdfComposition(CompositionKit.CreateStyles(creator), [new PdfSection([PdfParagraph.Of(lost)]) { Setup = SmallPage() }]);

        var report = await PdfComposer.ComposeAsync(creator, composition, null, CancellationToken.None);

        await Assert.That(report.Notes.Count).IsEqualTo(1);
        await Assert.That(report.Notes[0]).Contains("nowhere");
    }

    /// <summary>Makes a small page setup.</summary>
    /// <returns>The setup.</returns>
    private static PdfPageSetup SmallPage() => CompositionKit.CreateSetup(PageHeight);

    /// <summary>Collects the types of an element and everything below it.</summary>
    /// <param name="element">The element.</param>
    /// <param name="types">Receives the types.</param>
    private static void Collect(PdfStructureElement element, HashSet<string> types)
    {
        _ = types.Add(element.RawType);
        foreach (var kid in element.Kids)
        {
            if (kid.Element is { } child)
            {
                Collect(child, types);
            }
        }
    }

    /// <summary>Composes sections into a document and writes it.</summary>
    /// <param name="tagged">Whether the document is tagged.</param>
    /// <param name="sections">The sections.</param>
    /// <returns>The file.</returns>
    private static async Task<byte[]> WriteAsync(bool tagged, params PdfSection[] sections)
    {
        var creator = CompositionKit.CreateCreator(tagged);
        var composition = new PdfComposition(CompositionKit.CreateStyles(creator), sections) { Title = "Test", Author = "Tester" };
        _ = await PdfComposer.ComposeAsync(creator, composition, null, CancellationToken.None);
        return creator.ToArray();
    }
}
