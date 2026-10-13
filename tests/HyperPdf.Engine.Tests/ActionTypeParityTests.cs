// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;
using TUnit.Assertions.Enums;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Compares the action type of every link annotation with PDFium's <c>FPDFAction_GetType</c>, and the destination page
/// with <c>FPDFDest_GetDestPageIndex</c> where both engines report one. PDFium classifies only GoTo, GoToR, URI, Launch
/// and GoToE; HyperPDF reads every other subtype as typed data, which these tests count as PDFium's unsupported type.
/// The deliberate differences are:
/// <list type="number">
/// <item><description>A GoTo action whose destination is not in the document: PDFium reports GoTo with no page; HyperPDF reads it as unsupported.</description></item>
/// <item><description>A GoToR action whose file specification is a URL (<c>/FS /URL</c>): PDFium reports GoToR; HyperPDF reads it as a URI action.</description></item>
/// <item><description>An action dictionary whose <c>/Type</c> is not <c>/Action</c>: PDFium reports it as unsupported; HyperPDF reads it by its <c>/S</c>.</description></item>
/// <item><description>
/// A GoToE destination that is a page object or a name: PDFium finds the page in this document; HyperPDF reports no
/// page, because the destination belongs to the target document (ISO 32000-2 12.3.2.2 and 12.6.4.4).
/// </description></item>
/// </list>
/// </summary>
public sealed class ActionTypeParityTests
{
    /// <summary>The environment variable naming the folder of downloaded reference PDFs.</summary>
    private const string ReferenceFolderVariable = "HYPERPDF_REFERENCE_PDF_DIR";

    /// <summary>The page index used when a link has no destination page.</summary>
    private const int NoPage = -1;

    /// <summary>The third page of the hand-built document.</summary>
    private const int ThirdPage = 2;

    /// <summary>The page the hand-built remote go-to action names in the other file.</summary>
    private const int RemotePage = 2;

    /// <summary>The subtype of a go-to action.</summary>
    private const string GoTo = "GoTo";

    /// <summary>The subtype of a remote go-to action.</summary>
    private const string RemoteGoTo = "GoToR";

    /// <summary>The subtype of an embedded go-to action.</summary>
    private const string EmbeddedGoTo = "GoToE";

    /// <summary>The links of the hand-built document's first page, in <c>/Annots</c> order, with the action types and pages both engines give.</summary>
    private static readonly LinkCase[] HandBuiltCases =
    [
        new("<< /S /GoTo /D [5 0 R /Fit] >>", null, new(PdfiumActionType.GoTo, ThirdPage), new(PdfiumActionType.GoTo, ThirdPage)),
        new("<< /S /GoTo /D (Intro) >>", null, new(PdfiumActionType.GoTo, ThirdPage), new(PdfiumActionType.GoTo, ThirdPage)),
        new("<< /S /GoTo /D [99 0 R /Fit] >>", null, new(PdfiumActionType.GoTo, NoPage), new(PdfiumActionType.Unsupported, NoPage)),
        new(null, "[4 0 R /XYZ 0 0 0]", new(PdfiumActionType.NoAction, 1), new(PdfiumActionType.NoAction, 1)),
        new("99 0 R", "[4 0 R /Fit]", new(PdfiumActionType.NoAction, 1), new(PdfiumActionType.NoAction, 1)),
        new("<< /S /URI /URI (https://example.com/) >>", null, new(PdfiumActionType.Uri, NoPage), new(PdfiumActionType.Uri, NoPage)),
        new("<< /S /GoToR /F (other.pdf) /D [2 /Fit] >>", null, new(PdfiumActionType.RemoteGoTo, RemotePage), new(PdfiumActionType.RemoteGoTo, RemotePage)),
        new("<< /S /GoToR /F << /FS /URL /F (https://example.com/o.pdf) >> /D [0 /Fit] >>", null, new(PdfiumActionType.RemoteGoTo, 0), new(PdfiumActionType.Uri, NoPage)),
        new("<< /S /Launch /F (notes.txt) >>", null, new(PdfiumActionType.Launch, NoPage), new(PdfiumActionType.Launch, NoPage)),
        new("<< /S /GoToE /D [1 /Fit] /T << /R /C /N (inner) >> >>", null, new(PdfiumActionType.EmbeddedGoTo, 1), new(PdfiumActionType.EmbeddedGoTo, 1)),
        new("<< /S /GoToE /D [4 0 R /Fit] /T << /R /C /N (inner) >> >>", null, new(PdfiumActionType.EmbeddedGoTo, 1), new(PdfiumActionType.EmbeddedGoTo, NoPage)),
        new("<< /S /GoToE /D (Intro) /T << /R /C /N (inner) >> >>", null, new(PdfiumActionType.EmbeddedGoTo, ThirdPage), new(PdfiumActionType.EmbeddedGoTo, NoPage)),
        new("<< /S /Named /N /NextPage >>", null, new(PdfiumActionType.Unsupported, NoPage), new(PdfiumActionType.Unsupported, NoPage)),
        new("<< /S /JavaScript /JS (1) >>", null, new(PdfiumActionType.Unsupported, NoPage), new(PdfiumActionType.Unsupported, NoPage)),
        new("<< /S /SubmitForm /F (https://example.com/submit) >>", null, new(PdfiumActionType.Unsupported, NoPage), new(PdfiumActionType.Unsupported, NoPage)),
        new("<< /S /Hide /T (field) >>", null, new(PdfiumActionType.Unsupported, NoPage), new(PdfiumActionType.Unsupported, NoPage)),
        new("<< /S /Frobnicate >>", null, new(PdfiumActionType.Unsupported, NoPage), new(PdfiumActionType.Unsupported, NoPage)),
        new("<< /D [5 0 R /Fit] >>", null, new(PdfiumActionType.Unsupported, NoPage), new(PdfiumActionType.Unsupported, NoPage)),
        new("<< /Type /Bogus /S /URI /URI (https://example.com/) >>", null, new(PdfiumActionType.Unsupported, NoPage), new(PdfiumActionType.Uri, NoPage)),
        new("<< /S /GoTo /D [5 0 R /Fit] /Next << /S /URI /URI (https://example.com/next) >> >>", null, new(PdfiumActionType.GoTo, ThirdPage), new(PdfiumActionType.GoTo, ThirdPage)),
    ];

    /// <summary>Every hand-built link gives the recorded PDFium and HyperPDF results, and they agree apart from the recorded differences.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HandBuiltLinkActionsMatchPdfium()
    {
        using var pair = new EnginePair(CreateLinkDocument());
        var pdfium = PdfiumReferenceLinks.GetLinks(pair.Pdfium, 0).Select(static link => new LinkAction(link.Action, link.PageIndex)).ToList();
        var hyperPdf = Describe(pair.Library, 0);
        var mismatches = new List<string>();
        Compare(pair.Library, 0, PdfiumReferenceLinks.GetLinks(pair.Pdfium, 0), mismatches);

        await Assert.That(pdfium).IsEquivalentTo(HandBuiltCases.Select(static link => link.Pdfium), CollectionOrdering.Matching);
        await Assert.That(hyperPdf).IsEquivalentTo(HandBuiltCases.Select(static link => link.HyperPdf), CollectionOrdering.Matching);
        await Assert.That(mismatches).IsEmpty();
    }

    /// <summary>The typed actions behind PDFium's unsupported type are still read as data, and a GoToE action keeps its own target's destination.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsupportedPdfiumTypesAreTypedInHyperPdf()
    {
        using var pair = new EnginePair(CreateLinkDocument());
        var annotations = LinkAnnotations(pair.Library, 0);
        var actions = annotations.Select(annotation => annotation.GetDictionary(KnownName.A) is { } action ? PdfDocumentNavigation.ReadAction(pair.Library, action).Value : null).ToList();

        await Assert.That(actions.OfType<NamedAction>().Single().Name).IsEqualTo("NextPage");
        await Assert.That(actions.OfType<JavaScriptAction>().Single().Script).IsEqualTo("1");
        await Assert.That(actions.OfType<SubmitFormAction>().Single().Url).IsEqualTo("https://example.com/submit");
        await Assert.That(actions.OfType<HideAction>().Single().Targets).IsEquivalentTo(["field"]);
        await Assert.That(actions.OfType<UnsupportedAction>().Select(static action => action.Subtype)).IsEquivalentTo([GoTo, "Frobnicate", string.Empty]);
        await Assert.That(actions.OfType<EmbeddedGoToAction>().Select(static action => action.NamedDestination)).IsEquivalentTo([null, null, "Intro"]);
    }

    /// <summary>
    /// Every link in the downloaded reference PDFs has the same action type as PDFium, apart from the recorded differences,
    /// and the same destination page where both report one. Skips when <c>HYPERPDF_REFERENCE_PDF_DIR</c> names no PDFs.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReferencePdfLinkActionsMatchPdfium()
    {
        var mismatches = new List<string>();
        var links = 0;
        foreach (var file in RequireReferencePdfs())
        {
            using var pair = new EnginePair(await File.ReadAllBytesAsync(file));
            for (var page = 0; page < pair.Pdfium.PageCount; page++)
            {
                var expected = PdfiumReferenceLinks.GetLinks(pair.Pdfium, page);
                links += expected.Count;
                Compare(pair.Library, page, expected, mismatches, Path.GetFileName(file));
            }
        }

        TestContext.Current?.Output.WriteLine($"Compared {links} links with PDFium.");
        await Assert.That(mismatches).IsEmpty();
    }

    /// <summary>Compares one page's links with PDFium's, recording each difference that is not a recorded one.</summary>
    /// <param name="document">The managed library document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <param name="expected">PDFium's links on the page.</param>
    /// <param name="mismatches">Receives a line per difference.</param>
    private static void Compare(PdfDocument document, int pageIndex, List<PdfiumLink> expected, List<string> mismatches) =>
        Compare(document, pageIndex, expected, mismatches, "hand-built");

    /// <summary>Compares one page's links with PDFium's, recording each difference that is not a recorded one.</summary>
    /// <param name="document">The managed library document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <param name="expected">PDFium's links on the page.</param>
    /// <param name="mismatches">Receives a line per difference.</param>
    /// <param name="source">The file name used in the difference lines.</param>
    private static void Compare(PdfDocument document, int pageIndex, List<PdfiumLink> expected, List<string> mismatches, string source)
    {
        var annotations = LinkAnnotations(document, pageIndex);
        if (annotations.Count != expected.Count)
        {
            mismatches.Add($"{source} page {pageIndex}: {annotations.Count} links, PDFium {expected.Count}");
            return;
        }

        for (var i = 0; i < annotations.Count; i++)
        {
            var actual = Describe(document, annotations[i]);
            var wanted = Reconcile(annotations[i], new(expected[i].Action, expected[i].PageIndex));
            var pagesDiffer = actual.PageIndex >= 0 && wanted.PageIndex >= 0 && actual.PageIndex != wanted.PageIndex;
            if (actual.Type != wanted.Type || pagesDiffer)
            {
                mismatches.Add($"{source} page {pageIndex} link {i}: HyperPDF {actual}, PDFium {expected[i].Action} page {expected[i].PageIndex}");
            }
        }
    }

    /// <summary>Applies the recorded differences to PDFium's result for one link.</summary>
    /// <param name="annotation">The link annotation.</param>
    /// <param name="pdfium">PDFium's action type and page.</param>
    /// <returns>The result HyperPDF gives on purpose.</returns>
    private static LinkAction Reconcile(PdfDictionary annotation, LinkAction pdfium)
    {
        if (annotation.GetDictionary(KnownName.A) is not { } action)
        {
            return pdfium;
        }

        var subtype = action.NameText("S") ?? string.Empty;
        return action.NameText("Type") is { } type && type != "Action" ? new(TypeOf(subtype), pdfium.PageIndex) : Reconcile(action, subtype, pdfium);
    }

    /// <summary>Applies the recorded differences that depend on an action's subtype.</summary>
    /// <param name="action">The action dictionary.</param>
    /// <param name="subtype">The <c>/S</c> name.</param>
    /// <param name="pdfium">PDFium's action type and page.</param>
    /// <returns>The result HyperPDF gives on purpose.</returns>
    private static LinkAction Reconcile(PdfDictionary action, string subtype, LinkAction pdfium) => subtype switch
    {
        GoTo when pdfium.PageIndex < 0 => new(PdfiumActionType.Unsupported, NoPage),
        RemoteGoTo when action.Dict("F")?.NameText("FS") == "URL" => new(PdfiumActionType.Uri, NoPage),
        EmbeddedGoTo when action.Value("D").AsArray()?.Get(0).IsNumber != true => new(PdfiumActionType.EmbeddedGoTo, NoPage),
        _ => pdfium,
    };

    /// <summary>Gets the PDFium action type PDFium gives a subtype name when it reads the action.</summary>
    /// <param name="subtype">The <c>/S</c> name.</param>
    /// <returns>The type.</returns>
    private static PdfiumActionType TypeOf(string subtype) => subtype switch
    {
        GoTo => PdfiumActionType.GoTo,
        RemoteGoTo => PdfiumActionType.RemoteGoTo,
        "URI" => PdfiumActionType.Uri,
        "Launch" => PdfiumActionType.Launch,
        EmbeddedGoTo => PdfiumActionType.EmbeddedGoTo,
        _ => PdfiumActionType.Unsupported,
    };

    /// <summary>Describes each link on a page as HyperPDF reads it.</summary>
    /// <param name="document">The managed library document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <returns>The action types and pages, in <c>/Annots</c> order.</returns>
    private static List<LinkAction> Describe(PdfDocument document, int pageIndex) =>
        [.. LinkAnnotations(document, pageIndex).Select(annotation => Describe(document, annotation))];

    /// <summary>Describes one link as HyperPDF reads it, using PDFium's action type names.</summary>
    /// <param name="document">The managed library document.</param>
    /// <param name="annotation">The link annotation.</param>
    /// <returns>The action type and destination page.</returns>
    private static LinkAction Describe(PdfDocument document, PdfDictionary annotation)
    {
        if (annotation.GetDictionary(KnownName.A) is not { } action)
        {
            return new(PdfiumActionType.NoAction, PdfDocumentNavigation.ResolveDestination(document, annotation.Get(KnownName.Dest))?.PageIndex ?? NoPage);
        }

        return PdfDocumentNavigation.ReadAction(document, action).Value switch
        {
            GoToAction goTo => new(PdfiumActionType.GoTo, goTo.Destination.PageIndex),
            RemoteGoToAction remote => new(PdfiumActionType.RemoteGoTo, remote.NamedDestination is null ? remote.PageIndex : NoPage),
            UriAction => new(PdfiumActionType.Uri, NoPage),
            LaunchAction => new(PdfiumActionType.Launch, NoPage),
            EmbeddedGoToAction embedded => new(PdfiumActionType.EmbeddedGoTo, embedded.Destination?.PageIndex ?? NoPage),
            _ => new(PdfiumActionType.Unsupported, NoPage),
        };
    }

    /// <summary>Gets a page's link annotations in <c>/Annots</c> order, as PDFium enumerates them.</summary>
    /// <param name="document">The managed library document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <returns>The link annotation dictionaries.</returns>
    private static List<PdfDictionary> LinkAnnotations(PdfDocument document, int pageIndex)
    {
        var annotations = PdfDocumentPages.GetPage(document, pageIndex).Dictionary.GetArray(KnownName.Annots);
        var links = new List<PdfDictionary>(annotations?.Count ?? 0);
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is { } annotation && annotation.IsName(KnownName.Subtype, KnownName.Link))
            {
                links.Add(annotation);
            }
        }

        return links;
    }

    /// <summary>Gets the downloaded reference PDFs, or skips the test when there are none.</summary>
    /// <returns>The files.</returns>
    private static string[] RequireReferencePdfs()
    {
        var folder = Environment.GetEnvironmentVariable(ReferenceFolderVariable);
        var files = folder is { Length: > 0 } && Directory.Exists(folder) ? Directory.GetFiles(folder, "*.pdf").Order(StringComparer.Ordinal).ToArray() : [];
        if (files.Length == 0)
        {
            Skip.Test($"No reference PDFs. Fetch entries of tests/reference-pdfs/manifest.json, such as pypdf-outline, with scripts/FetchReferencePdfs.cs and set {ReferenceFolderVariable}.");
        }

        return files;
    }

    /// <summary>Builds a three-page document whose first page holds one link per case in <see cref="HandBuiltCases"/>.</summary>
    /// <returns>The file bytes.</returns>
    private static byte[] CreateLinkDocument()
    {
        var annotations = string.Concat(HandBuiltCases.Select(static link => $"<< /Type /Annot /Subtype /Link /Rect [0 0 10 10]{Entry("A", link.Action)}{Entry("Dest", link.Destination)} >> "));
        return MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Names << /Dests << /Names [(Intro) [5 0 R /Fit]] >> >> >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Annots [{annotations}] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>");
    }

    /// <summary>Writes one dictionary entry, or nothing when the value is missing.</summary>
    /// <param name="key">The key, without its slash.</param>
    /// <param name="value">The value text, or <see langword="null"/>.</param>
    /// <returns>The entry text.</returns>
    private static string Entry(string key, string? value) => value is null ? string.Empty : $" /{key} {value}";

    /// <summary>A link's action type, with PDFium's names, and its destination page.</summary>
    /// <param name="Type">The action type.</param>
    /// <param name="PageIndex">The zero-based destination page, or -1.</param>
    private readonly record struct LinkAction(PdfiumActionType Type, int PageIndex);

    /// <summary>One hand-built link and the results each engine gives.</summary>
    /// <param name="Action">The <c>/A</c> value, or <see langword="null"/>.</param>
    /// <param name="Destination">The <c>/Dest</c> value, or <see langword="null"/>.</param>
    /// <param name="Pdfium">PDFium's action type and page.</param>
    /// <param name="HyperPdf">HyperPDF's action type and page.</param>
    private sealed record LinkCase(string? Action, string? Destination, LinkAction Pdfium, LinkAction HyperPdf);
}
