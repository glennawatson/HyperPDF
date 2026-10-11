// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests for name trees, URI actions, scripts and file specifications.</summary>
public sealed class NavigationTests
{
    /// <summary>The object number of the first action dictionary in the sample documents.</summary>
    private const int ActionObject = 5;

    /// <summary>The size of an oversized script, in characters.</summary>
    private const int OversizedScript = 2_000_000;

    /// <summary>The most script bytes kept.</summary>
    private const int ScriptLimit = 1 << 20;

    /// <summary>The page that the second sample destination leads to.</summary>
    private const int SecondPage = 1;

    /// <summary>A path in PDF file specification syntax that starts with a drive letter.</summary>
    private const string DrivePath = "/c/dir/file.pdf";

    /// <summary>Two pages with an outline-free catalog, followed by the extra objects.</summary>
    private const string TwoPages = "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>";

    /// <summary>The first page.</summary>
    private const string FirstPage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>";

    /// <summary>The second page.</summary>
    private const string OtherPage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>";

    /// <summary>A kid whose range covers the key is searched, and when it does not hold the key the search backtracks to the next.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NameTreeSearchBacktracksAcrossOverlappingKids()
    {
        using var document = Open(
        "/Names << /Dests << /Kids [5 0 R 6 0 R 7 0 R] >> >>",
        "<< /Limits [(a) (z)] /Names [(m) [3 0 R /Fit]] >>",
        "<< /Limits [(b) (c)] /Names [(bb) [4 0 R /Fit]] >>",
        "<< /Names [(zz) [3 0 R /Fit]] >>");
        await Assert.That(PdfDocumentNavigation.ResolveDestination(document, PdfValue.FromString("bb"u8.ToArray()))?.PageIndex).IsEqualTo(SecondPage);
    }

    /// <summary>A key outside its kid's wrong /Limits is still found through a table of the whole tree.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NameTreeFallsBackToAFullTableWhenLimitsAreWrong()
    {
        using var document = Open("/Names << /Dests << /Kids [5 0 R] >> >>", "<< /Limits [(a) (b)] /Names [(a) [3 0 R /Fit] (zz) [4 0 R /Fit]] >>");
        await Assert.That(PdfDocumentNavigation.ResolveDestination(document, PdfValue.FromString("zz"u8.ToArray()))?.PageIndex).IsEqualTo(SecondPage);
        await Assert.That(PdfDocumentNavigation.ResolveDestination(document, PdfValue.FromString("absent"u8.ToArray()))).IsNull();
    }

    /// <summary>An ownerless tree rebuilt after a fallback sees changes to its entries.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OwnerlessNameTreeFallbackReadsChangedEntry()
    {
        const long UpdatedValue = 2;
        var limits = new PdfArray(null, [PdfValue.FromString("a"u8.ToArray()), PdfValue.FromString("b"u8.ToArray())]);
        var names = new PdfArray(null, [PdfValue.FromString("z"u8.ToArray()), PdfValue.FromInteger(1)]);
        var child = new PdfDictionary(null);
        child.Set(KnownName.Limits, PdfValue.FromArray(limits));
        child.Set(KnownName.Names, PdfValue.FromArray(names));
        var root = new PdfDictionary(null);
        root.Set(KnownName.Kids, PdfValue.FromArray(new(null, [PdfValue.FromDictionary(child)])));

        await Assert.That(NameTree.Find(root, "z"u8).AsInteger()).IsEqualTo(1);
        names.SetAt(1, PdfValue.FromInteger(UpdatedValue));
        await Assert.That(NameTree.Find(root, "z"u8).AsInteger()).IsEqualTo(UpdatedValue);
    }

    /// <summary>A named destination is looked up in the name tree before the catalog's /Dests dictionary.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NameTreeComesBeforeTheDestsDictionary()
    {
        using var document = Open("/Names << /Dests << /Names [(x) [4 0 R /Fit]] >> >> /Dests << /x [3 0 R /Fit] >>");
        await Assert.That(PdfDocumentNavigation.ResolveDestination(document, PdfValue.FromName(document.Objects.Names.Intern("x")))?.PageIndex).IsEqualTo(SecondPage);
    }

    /// <summary>A URI without a scheme is joined to the catalog's /URI /Base.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RelativeUriUsesTheBase()
    {
        using var document = Open("/URI << /Base (https://example.com/docs/) >>", "<< /S /URI /URI (a.pdf) >>", "<< /S /URI /URI (mailto:me@example.com) >>");
        var relative = PdfDocumentNavigation.ReadAction(document, Action(document, ActionObject));
        var absolute = PdfDocumentNavigation.ReadAction(document, Action(document, ActionObject + 1));
        await Assert.That(relative.Value is UriAction { Uri: "https://example.com/docs/a.pdf" }).IsTrue();
        await Assert.That(absolute.Value is UriAction { Uri: "mailto:me@example.com" }).IsTrue();
    }

    /// <summary>A script stream is cut off at the size limit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScriptStreamsAreCapped()
    {
        using var document = Open(string.Empty, "<< /S /JavaScript /JS 6 0 R >>", MiniPdf.Stream(string.Empty, new('a', OversizedScript)));
        var script = PdfDocumentNavigation.ReadAction(document, Action(document, ActionObject)).Value as JavaScriptAction;
        await Assert.That(script?.Script.Length).IsEqualTo(ScriptLimit);
    }

    /// <summary>A launch action reads /DOS, and a URL file specification becomes a URI action.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LaunchReadsDosAndUrlSpecifications()
    {
        using var document = Open(string.Empty, "<< /S /Launch /F << /Type /Filespec /DOS (FILE.TXT) >> >>", "<< /S /Launch /F << /Type /Filespec /FS /URL /F (https://example.com/a) >> >>");
        await Assert.That(PdfDocumentNavigation.ReadAction(document, Action(document, ActionObject)).Value is LaunchAction { File: "FILE.TXT" }).IsTrue();
        await Assert.That(PdfDocumentNavigation.ReadAction(document, Action(document, ActionObject + 1)).Value is UriAction { Uri: "https://example.com/a" }).IsTrue();
    }

    /// <summary>A go-to-remote action with a named destination stays on page 0 and exposes the name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemoteGoToExposesNamedDestination()
    {
        using var document = Open(string.Empty, "<< /S /GoToR /F (other.pdf) /D (Chapter1) >>", "<< /S /GoToR /F << /FS /URL /F (https://example.com/o.pdf) >> /D (Chapter1) >>");
        var remoteAction = PdfDocumentNavigation.ReadAction(document, Action(document, ActionObject)).Value;
        await Assert.That(remoteAction is RemoteGoToAction { File: "other.pdf", PageIndex: 0, NamedDestination: "Chapter1" }).IsTrue();
        await Assert.That(PdfDocumentNavigation.ReadAction(document, Action(document, ActionObject + 1)).Value is UriAction).IsTrue();
    }

    /// <summary>On Windows, "/c/dir/file" and "../" become Windows paths; elsewhere paths are unchanged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FileSpecPathsConvertForTheTargetPlatform()
    {
        await Assert.That(PdfDocumentFileSpecs.ToPlatformPath(DrivePath, true)).IsEqualTo("c:\\dir\\file.pdf");
        await Assert.That(PdfDocumentFileSpecs.ToPlatformPath("//server/share/file.pdf", true)).IsEqualTo("\\\\server\\share\\file.pdf");
        await Assert.That(PdfDocumentFileSpecs.ToPlatformPath("/dir/file.pdf", true)).IsEqualTo("\\dir\\file.pdf");
        await Assert.That(PdfDocumentFileSpecs.ToPlatformPath("../up/file.pdf", true)).IsEqualTo("..\\up\\file.pdf");
        await Assert.That(PdfDocumentFileSpecs.ToPlatformPath(DrivePath, false)).IsEqualTo(DrivePath);
    }

    /// <summary>Gets an action dictionary from a sample document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="number">The object number.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Action(PdfDocument document, int number) => StoreReading.GetDictionary(document.Objects, new(number, 0))!;

    /// <summary>Opens a two page document; the catalog takes extra entries and objects 5 onwards follow.</summary>
    /// <param name="catalogEntries">The extra catalog entries.</param>
    /// <param name="objects">The bodies of objects 5 onwards.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string catalogEntries, params string[] objects)
    {
        string[] header = [$"<< /Type /Catalog /Pages 2 0 R {catalogEntries} >>", TwoPages, FirstPage, OtherPage];
        return PdfDocumentReader.Open(MiniPdf.Build([.. header, .. objects]), null);
    }
}
