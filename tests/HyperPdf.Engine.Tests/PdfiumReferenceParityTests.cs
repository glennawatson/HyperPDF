// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Documents;
using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using TUnit.Assertions.Enums;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Smoke checks that <see cref="EnginePair"/> and the PDFium reference helpers read each non-rendering API family,
/// comparing HyperPDF with PDFium on hand-built PDFs. Deliberate differences are asserted and named here.
/// </summary>
public sealed class PdfiumReferenceParityTests
{
    /// <summary>The name HyperPDF gives a single-stream XFA packet; PDFium gives it an empty name.</summary>
    private const string SingleStreamPacketName = "xdp";

    /// <summary>A word that appears once on each page of <see cref="TestPdf.Create(int)"/>.</summary>
    private const string SearchWord = "fox";

    /// <summary>The pages <see cref="TestPdf.Create(int)"/> needs for its first page to link to the third.</summary>
    private const int LinkedPageCount = 3;

    /// <summary>The test file's user password; it protects nothing.</summary>
    private const string UserPassword = "reader-2026";

    /// <summary>The test file's owner password; it protects nothing.</summary>
    private const string OwnerPassword = "owner-2026";

    /// <summary>A password the test file does not accept.</summary>
    private const string WrongPassword = "not-the-password";

    /// <summary>The catalog <c>/Lang</c> of <see cref="TestPdf.CreateTagged"/>.</summary>
    private const string CatalogLanguage = "en-GB";

    /// <summary>The page index used when a link has no destination page.</summary>
    private const int NoPage = -1;

    /// <summary>Both engines open a revision 6 AES-256 file with its user password and refuse it without one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PasswordOpenMatches()
    {
        var pdf = UserPasswordPdf.Create(UserPassword, OwnerPassword);
        using var pair = new EnginePair(pdf, UserPassword);

        await Assert.That(pair.Pdfium.PageCount).IsEqualTo(1);
        await Assert.That(pair.HyperPdf.PageCount).IsEqualTo(pair.Pdfium.PageCount);
        await Assert.That(PdfiumReferenceDocument.GetOpenError(pdf, OwnerPassword)).IsEqualTo(PdfiumOpenError.Success);
        await Assert.That(PdfiumReferenceDocument.GetOpenError(pdf, null)).IsEqualTo(PdfiumOpenError.Password);
        await Assert.That(PdfiumReferenceDocument.GetOpenError(pdf, WrongPassword)).IsEqualTo(PdfiumOpenError.Password);
        var refused = await Assert.That(() => new HyperPdfEngine().Open(pair.FilePath, WrongPassword)).Throws<DocumentOpenException>();
        await Assert.That(refused!.Error).IsEqualTo(DocumentOpenError.Password);
    }

    /// <summary>
    /// <c>/MarkInfo /Marked</c>, and the page's structure types and alternate text, match. Languages differ on purpose:
    /// PDFium gives an element's own <c>/Lang</c>, while HyperPDF gives the language that applies, inherited from the catalog.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedStructureMatches()
    {
        using var pair = new EnginePair(TestPdf.CreateTagged());
        var tree = PdfDocumentTagged.GetStructureTree(pair.Library);
        var roots = PdfiumReferenceStructure.GetPageTree(pair.Pdfium, 0);
        var expected = new List<string>();
        foreach (var root in roots)
        {
            Flatten(root, expected);
        }

        var actual = new List<string>();
        foreach (var root in tree!.Roots)
        {
            Flatten(root, actual);
        }

        await Assert.That(PdfiumReferenceStructure.IsTagged(pair.Pdfium)).IsTrue();
        await Assert.That(tree.IsMarked).IsEqualTo(PdfiumReferenceStructure.IsTagged(pair.Pdfium));
        await Assert.That(expected.Count).IsGreaterThan(1);
        await Assert.That(actual).IsEquivalentTo(expected, CollectionOrdering.Matching);
        await Assert.That(roots[0].Language).IsNull();
        await Assert.That(tree.Roots[0].Language).IsEqualTo(CatalogLanguage);
    }

    /// <summary>The character count, page text and search results match.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextAndSearchMatch()
    {
        using var pair = new EnginePair(TestPdf.Create(1));
        var expected = PdfiumReferenceText.GetTextPage(pair.Pdfium, 0);
        var count = pair.HyperPdf.GetCharacterCount(0);
        var expectedMatches = PdfiumReferenceText.Find(pair.Pdfium, 0, SearchWord, PdfiumSearchFlags.None);
        var actualMatches = new List<TextMatch>();
        pair.HyperPdf.Find(0, SearchWord, SearchOptions.None, actualMatches);

        await Assert.That(expected.Characters.Length).IsGreaterThan(0);
        await Assert.That(expected.Characters.Count(static character => character.Box is not null)).IsGreaterThan(0);
        await Assert.That(count).IsEqualTo(expected.Characters.Length);
        await Assert.That(pair.HyperPdf.GetText(0, 0, count)).IsEqualTo(expected.Text);
        await Assert.That(expectedMatches.Count).IsEqualTo(1);
        await Assert.That(actualMatches.Select(static match => new PdfiumMatch(match.Start, match.Length))).IsEquivalentTo(expectedMatches, CollectionOrdering.Matching);
    }

    /// <summary>Link rectangles, page destinations and URI actions match.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinksAndActionsMatch()
    {
        using var pair = new EnginePair(TestPdf.Create(LinkedPageCount));
        var expected = PdfiumReferenceLinks.GetLinks(pair.Pdfium, 0);

        await Assert.That(expected.Select(static link => link.Action)).IsEquivalentTo([PdfiumActionType.NoAction, PdfiumActionType.Uri], CollectionOrdering.Matching);
        await AssertSameLinksAsync(pair, expected);
    }

    /// <summary>GoToR and Launch actions name the same file and page; GoToE is classified the same way and goes to the same page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FileActionsMatch()
    {
        using var pair = new EnginePair(TestPdf.CreateWithFileLinks());
        var expected = PdfiumReferenceLinks.GetLinks(pair.Pdfium, 0);
        PdfiumActionType[] actions = [PdfiumActionType.RemoteGoTo, PdfiumActionType.Launch, PdfiumActionType.EmbeddedGoTo];

        await Assert.That(expected.Select(static link => link.Action)).IsEquivalentTo(actions, CollectionOrdering.Matching);
        await Assert.That(expected[0].FilePath).IsEqualTo(TestPdf.LinkedDocumentName);
        await Assert.That(expected[1].FilePath).IsEqualTo(TestPdf.LaunchedFileName);
        await AssertSameLinksAsync(pair, expected);
    }

    /// <summary>Embedded file names and decoded bytes match.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AttachmentsMatch()
    {
        using var pair = new EnginePair(TestPdf.CreateWithAttachment());
        var expected = PdfiumReferenceAttachments.GetAttachments(pair.Pdfium);
        var actual = PdfDocumentAttachments.GetAttachments(pair.Library);

        await Assert.That(expected.Count).IsEqualTo(1);
        await Assert.That(expected[0].Name).IsEqualTo(TestPdf.AttachmentName);
        await Assert.That(Encoding.ASCII.GetString(expected[0].Data!)).IsEqualTo(TestPdf.AttachmentText);
        await Assert.That(actual.Select(static attachment => attachment.Name)).IsEquivalentTo(expected.Select(static attachment => attachment.Name), CollectionOrdering.Matching);
        await Assert.That(actual[0].Data!.DecodeOrEmpty()).IsEquivalentTo(expected[0].Data!, CollectionOrdering.Matching);
    }

    /// <summary>Document-level JavaScript names and text match; nothing runs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentScriptsMatch()
    {
        using var pair = new EnginePair(TestPdf.CreateWithUnsupportedContent());
        var expected = PdfiumReferenceScripts.GetDocumentScripts(pair.Pdfium);
        var actual = PdfDocumentActions.GetDocumentScripts(pair.Library);

        await Assert.That(PdfiumReferenceScripts.GetDocumentScriptCount(pair.Pdfium)).IsEqualTo(1);
        await Assert.That(expected.Count).IsEqualTo(1);
        await Assert.That(actual.Select(static script => script.Name)).IsEquivalentTo(expected.Select(static script => script.Name), CollectionOrdering.Matching);
        await Assert.That(actual.Select(static script => script.Script)).IsEquivalentTo(expected.Select(static script => script.Script), CollectionOrdering.Matching);
    }

    /// <summary>XFA packet names and bytes match for an XFA array.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task XfaPacketArrayMatches()
    {
        using var pair = new EnginePair(MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [] /XFA [(template) 4 0 R (datasets) 5 0 R] >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
            MiniPdf.Stream(string.Empty, "<template/>"),
            MiniPdf.Stream(string.Empty, "<xfa:datasets/>")));
        var expected = PdfiumReferenceXfa.GetPackets(pair.Pdfium);
        var actual = PdfDocumentXfa.GetXfa(pair.Library)!.Packets;

        await Assert.That(expected.Select(static packet => packet.Name)).IsEquivalentTo(["template", "datasets"], CollectionOrdering.Matching);
        await Assert.That(actual.Select(static packet => packet.Name)).IsEquivalentTo(expected.Select(static packet => packet.Name), CollectionOrdering.Matching);
        for (var i = 0; i < expected.Count; i++)
        {
            await Assert.That(actual[i].Data).IsEquivalentTo(expected[i].Content, CollectionOrdering.Matching);
        }
    }

    /// <summary>A single XFA stream has the same bytes; PDFium names it empty where HyperPDF names it <c>xdp</c>.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task XfaSingleStreamMatchesWithNamedDifference()
    {
        using var pair = new EnginePair(TestPdf.CreateWithUnsupportedContent());
        var expected = PdfiumReferenceXfa.GetPackets(pair.Pdfium);
        var actual = PdfDocumentXfa.GetXfa(pair.Library)!.Packets;

        await Assert.That(expected.Count).IsEqualTo(1);
        await Assert.That(expected[0].Name).IsEmpty();
        await Assert.That(actual[0].Name).IsEqualTo(SingleStreamPacketName);
        await Assert.That(actual[0].Data).IsEquivalentTo(expected[0].Content, CollectionOrdering.Matching);
    }

    /// <summary>Annotation subtypes match the managed library's <c>/Subtype</c> names.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationSubtypesMatch()
    {
        var pdf = TestPdf.CreateWithUnsupportedContent();
        using var pair = new EnginePair(pdf);
        var expected = PdfiumReferenceAnnotations.GetAnnotations(pair.Pdfium, 0);
        var actual = HyperPdfTestDocument.Dictionaries(pdf, 0).Select(static dictionary => HyperPdfTestDocument.Name(dictionary, KnownName.Subtype));

        await Assert.That(expected.Select(static annotation => annotation.SubtypeName)).IsEquivalentTo(["Widget", "Screen", "3D"], CollectionOrdering.Matching);
        await Assert.That(actual).IsEquivalentTo(expected.Select(static annotation => annotation.SubtypeName), CollectionOrdering.Matching);
    }

    /// <summary>Form field names and values match for text, check box and choice fields.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormFieldValuesMatch()
    {
        using var pair = new EnginePair(TestPdf.CreateForm());
        var expected = PdfiumReferenceAnnotations.GetAnnotations(pair.Pdfium, 0).Where(static annotation => annotation.FieldName is not null).ToList();
        var actual = new List<PdfFormWidget>();
        PdfFormReading.GetWidgets(PdfDocumentForms.GetForm(pair.Library), 0, actual);

        await Assert.That(expected.Count).IsGreaterThan(1);
        await Assert.That(actual.Select(static widget => widget.Name)).IsEquivalentTo(expected.Select(static annotation => annotation.FieldName), CollectionOrdering.Matching);
        await Assert.That(actual.Select(static widget => widget.Value)).IsEquivalentTo(expected.Select(static annotation => annotation.FieldValue ?? string.Empty), CollectionOrdering.Matching);
    }

    /// <summary>Adds an element and its descendants as one line each, depth first.</summary>
    /// <param name="element">The PDFium element.</param>
    /// <param name="output">The lines.</param>
    private static void Flatten(PdfiumStructElement element, List<string> output)
    {
        output.Add($"{element.Type}|{element.AlternateText}");
        foreach (var child in element.Children)
        {
            Flatten(child, output);
        }
    }

    /// <summary>Adds an element and its descendants as one line each, depth first.</summary>
    /// <param name="element">The HyperPDF element.</param>
    /// <param name="output">The lines.</param>
    private static void Flatten(PdfStructureElement element, List<string> output)
    {
        output.Add($"{element.MappedType}|{element.AlternateText}");
        foreach (var kid in element.Kids)
        {
            if (kid.Element is { } child)
            {
                Flatten(child, output);
            }
        }
    }

    /// <summary>
    /// Checks that the managed library reads the same link annotations as PDFium on page 1. The adapter's links are not
    /// used because they add web addresses found in the page text, which PDFium lists separately. A GoToE link is
    /// compared by kind and destination page: PDFium's file path is the <c>/F</c> root document, while HyperPDF keeps
    /// that and the <c>/T</c> target steps separately. <see cref="ActionTypeParityTests"/> records the GoToE pages that differ.
    /// </summary>
    /// <param name="pair">The documents.</param>
    /// <param name="expected">PDFium's links.</param>
    /// <returns>A task.</returns>
    private static async Task AssertSameLinksAsync(EnginePair pair, List<PdfiumLink> expected)
    {
        var actual = PdfDocumentLinks.GetLinks(pair.Library, 0).Select(Summarize).ToList();

        await Assert.That(actual).IsEquivalentTo(expected.Select(Summarize), CollectionOrdering.Matching);
    }

    /// <summary>Summarizes a PDFium link for comparison.</summary>
    /// <param name="link">PDFium's link.</param>
    /// <returns>The summary.</returns>
    private static LinkSummary Summarize(PdfiumLink link)
    {
        var kind = link.Action switch
        {
            PdfiumActionType.NoAction or PdfiumActionType.GoTo => link.PageIndex >= 0 ? LinkTargetKind.Page : LinkTargetKind.None,
            PdfiumActionType.Uri => LinkTargetKind.Uri,
            PdfiumActionType.RemoteGoTo => LinkTargetKind.OtherDocument,
            PdfiumActionType.Launch => LinkTargetKind.LaunchFile,
            PdfiumActionType.EmbeddedGoTo => LinkTargetKind.EmbeddedDocument,
            _ => LinkTargetKind.None,
        };
        var (left, top, right, bottom) = link.Bounds;
        return LinkSummary.Create(left, bottom, right, top, kind, link.PageIndex, kind == LinkTargetKind.EmbeddedDocument ? null : link.Uri ?? link.FilePath);
    }

    /// <summary>Summarizes a managed library link for comparison.</summary>
    /// <param name="link">HyperPDF's link.</param>
    /// <returns>The summary.</returns>
    private static LinkSummary Summarize(PdfLink link)
    {
        var (left, bottom, right, top) = link.Bounds;
        return link.Action.Value switch
        {
            GoToAction goTo => LinkSummary.Create(left, bottom, right, top, LinkTargetKind.Page, goTo.Destination.PageIndex, null),
            UriAction uri => LinkSummary.Create(left, bottom, right, top, LinkTargetKind.Uri, NoPage, uri.Uri),
            RemoteGoToAction remote => LinkSummary.Create(left, bottom, right, top, LinkTargetKind.OtherDocument, remote.PageIndex, remote.File),
            LaunchAction launch => LinkSummary.Create(left, bottom, right, top, LinkTargetKind.LaunchFile, NoPage, launch.File),
            EmbeddedGoToAction embedded => LinkSummary.Create(left, bottom, right, top, LinkTargetKind.EmbeddedDocument, embedded.Destination?.PageIndex ?? NoPage, null),
            _ => LinkSummary.Create(left, bottom, right, top, LinkTargetKind.None, NoPage, null),
        };
    }

    /// <summary>A link reduced to what both engines report, with its rectangle rounded to hundredths of a point.</summary>
    /// <param name="Left">The left edge.</param>
    /// <param name="Bottom">The bottom edge.</param>
    /// <param name="Right">The right edge.</param>
    /// <param name="Top">The top edge.</param>
    /// <param name="Kind">The target kind.</param>
    /// <param name="PageIndex">The destination page, or -1.</param>
    /// <param name="Target">The URI or file the link names, or <see langword="null"/>.</param>
    private readonly record struct LinkSummary(double Left, double Bottom, double Right, double Top, LinkTargetKind Kind, int PageIndex, string? Target)
    {
        /// <summary>The decimal places kept from each edge.</summary>
        private const int EdgeDigits = 2;

        /// <summary>Creates a summary with rounded edges.</summary>
        /// <param name="left">The left edge.</param>
        /// <param name="bottom">The bottom edge.</param>
        /// <param name="right">The right edge.</param>
        /// <param name="top">The top edge.</param>
        /// <param name="kind">The target kind.</param>
        /// <param name="pageIndex">The destination page, or -1.</param>
        /// <param name="target">The URI or file, or <see langword="null"/>.</param>
        /// <returns>The summary.</returns>
        internal static LinkSummary Create(float left, float bottom, float right, float top, LinkTargetKind kind, int pageIndex, string? target) =>
            new(Math.Round(left, EdgeDigits), Math.Round(bottom, EdgeDigits), Math.Round(right, EdgeDigits), Math.Round(top, EdgeDigits), kind, pageIndex, target);
    }
}
