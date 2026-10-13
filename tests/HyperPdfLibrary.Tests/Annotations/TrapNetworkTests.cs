// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Annotations;

/// <summary>
/// Tests that saves keep trap networks true (ISO 32000-2 §14.11.6.2): a network that still matches its page is kept with
/// its appearance state, one an edit made out of date is removed and reported, and malformed or repeated networks never
/// reach the saved file as if they were valid.
/// </summary>
public sealed class TrapNetworkTests
{
    /// <summary>The annotations page 1 keeps besides its trap network.</summary>
    private const int OtherAnnotations = 2;

    /// <summary>The flags a trap network must not have alone: Hidden and Print.</summary>
    private const int WrongFlags = 6;

    /// <summary>The appearance and flags of a conforming network, as <see cref="TrapNetworkPdf"/> writes them.</summary>
    private const string Appearance = "/F 68 /AP << /N << /Default 11 0 R >> >> /AS /Default";

    /// <summary>The <c>/Version</c> and <c>/AnnotStates</c> that match page 1 of a <see cref="TrapNetworkPdf"/> file.</summary>
    private const string Tracking = "/Version [4 0 R 5 0 R 6 0 R 7 0 R] /AnnotStates [null /On]";

    /// <summary>Page 1's content with its text moved.</summary>
    private const string MovedText = "BT /F1 24 Tf 72 650 Td (Moved text) Tj ET /Fm1 Do";

    /// <summary>A fill opacity other than the graphics state's.</summary>
    private const double HalfOpacity = 0.5;

    /// <summary>The index of page 3.</summary>
    private const int ThirdPage = 2;

    /// <summary>A page date before <see cref="TrapNetworkPdf.NetworkDate"/>.</summary>
    private const string EarlierDate = "D:20190101000000Z";

    /// <summary>A page date after <see cref="TrapNetworkPdf.NetworkDate"/>.</summary>
    private const string LaterDate = "D:20210101000000Z";

    /// <summary>The page list naming only page 1.</summary>
    private static readonly int[] FirstPage = [0];

    /// <summary>The box of a comment added to page 1.</summary>
    private static readonly PdfRectangle CommentBox = new(10, 10, 50, 50);

    /// <summary>A conforming network with a /Version array passes the check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidVersionedNetworkHasNoProblems()
    {
        using var document = Open(TrapNetworkPdf.Versioned);

        var check = CheckFirstPage(document);

        await Assert.That(check.Problems).IsEqualTo(PdfTrapNetworkProblems.None);
        await Assert.That(check.Touched).IsFalse();
    }

    /// <summary>Saving after an edit to another page keeps the network, its flags and its appearance state.</summary>
    /// <param name="incremental">Whether the save is incremental rather than compact.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UnrelatedEditKeepsNetwork(bool incremental)
    {
        using var document = Open(TrapNetworkPdf.Versioned);
        ReplaceContent(document, TrapNetworkPdf.SecondContentObject, "BT /F1 24 Tf 72 700 Td (Edited second page) Tj ET");
        var report = new PdfSaveReport();

        var saved = Save(document, incremental, report);

        await Assert.That(report.TrapNetworks).IsEmpty();
        using var reopened = Reopen(saved);
        var network = Network(reopened);
        await Assert.That(network).IsNotNull();
        await Assert.That(network!.GetName(KnownName.AS)).IsEqualTo(reopened.Objects.Names.Intern("Default"u8));
        await Assert.That(network.GetInt32(KnownName.F)).IsEqualTo(PdfTrapNetworkValidator.RequiredFlags);
        await Assert.That(CheckFirstPage(reopened).Problems).IsEqualTo(PdfTrapNetworkProblems.None);
    }

    /// <summary>Changing a comment's text on the trapped page keeps the network, as no appearance state changed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CommentTextEditKeepsNetwork()
    {
        using var document = Open(TrapNetworkPdf.Versioned);
        var square = StoreReading.GetDictionary(document.Objects, new(TrapNetworkPdf.SquareObject, 0))!.Clone();
        square.Set(KnownName.Contents, PdfValue.FromString("A changed note"u8.ToArray()));
        StoreEditing.Replace(document.Objects, new(TrapNetworkPdf.SquareObject, 0), PdfValue.FromDictionary(square));
        var report = new PdfSaveReport();

        using var reopened = Reopen(Save(document, true, report));

        await Assert.That(report.TrapNetworks).IsEmpty();
        await Assert.That(Network(reopened)).IsNotNull();
    }

    /// <summary>Editing a content stream the network lists removes the network before saving and says why.</summary>
    /// <param name="incremental">Whether the save is incremental rather than compact.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ContentEditRemovesNetwork(bool incremental)
    {
        using var document = Open(TrapNetworkPdf.Versioned);
        ReplaceContent(document, TrapNetworkPdf.ContentObject, MovedText);
        var report = new PdfSaveReport();

        var saved = Save(document, incremental, report);

        await AssertRemoved(report, PdfTrapNetworkProblems.ContentChanged);
        await Assert.That(report.TrapNetworks[0].Message).Contains("page 1");
        using var reopened = Reopen(saved);
        await Assert.That(Network(reopened)).IsNull();
        await Assert.That(PdfPageAnnotations.GetArray(reopened.Objects, PdfDocumentPages.GetPage(reopened, 0))!.Count).IsEqualTo(OtherAnnotations);
    }

    /// <summary>Adding a content stream to the page makes it name an object the network does not list.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddedContentStreamRemovesNetwork()
    {
        using var document = Open(TrapNetworkPdf.Versioned);
        var store = document.Objects;
        var added = StoreEditing.Add(store, PdfValue.FromStream(new(new PdfDictionary(store), "0 0 1 rg 10 10 20 20 re f"u8.ToArray())));
        var page = StoreReading.GetDictionary(store, new(TrapNetworkPdf.FirstPageObject, 0))!.Clone();
        var contents = new PdfArray(store, OtherAnnotations);
        contents.Add(PdfValue.FromReference(new(TrapNetworkPdf.ContentObject, 0)));
        contents.Add(PdfValue.FromReference(added));
        page.Set(KnownName.Contents, PdfValue.FromArray(contents));
        StoreEditing.Replace(store, new(TrapNetworkPdf.FirstPageObject, 0), PdfValue.FromDictionary(page));
        var report = new PdfSaveReport();

        using var reopened = Reopen(Save(document, true, report));

        await AssertRemoved(report, PdfTrapNetworkProblems.ContentChanged);
        await Assert.That(Network(reopened)).IsNull();
    }

    /// <summary>Editing a resource of a form the page draws also makes the network out of date.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NestedResourceEditRemovesNetwork()
    {
        using var document = Open(TrapNetworkPdf.Versioned);
        var state = StoreReading.GetDictionary(document.Objects, new(TrapNetworkPdf.GraphicsStateObject, 0))!.Clone();
        state.Set(document.Objects.Names.Intern("ca"u8), PdfValue.FromReal(HalfOpacity));
        StoreEditing.Replace(document.Objects, new(TrapNetworkPdf.GraphicsStateObject, 0), PdfValue.FromDictionary(state));
        var report = new PdfSaveReport();

        _ = Save(document, false, report);

        await AssertRemoved(report, PdfTrapNetworkProblems.ContentChanged);
    }

    /// <summary>Changing another annotation's appearance state no longer matches /AnnotStates.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppearanceStateChangeRemovesNetwork()
    {
        using var document = Open(TrapNetworkPdf.Versioned);
        var stamp = StoreReading.GetDictionary(document.Objects, new(TrapNetworkPdf.StampObject, 0))!.Clone();
        stamp.Set(KnownName.AS, PdfValue.FromName(KnownName.Off));
        StoreEditing.Replace(document.Objects, new(TrapNetworkPdf.StampObject, 0), PdfValue.FromDictionary(stamp));
        var report = new PdfSaveReport();

        _ = Save(document, true, report);

        await AssertRemoved(report, PdfTrapNetworkProblems.AnnotationsChanged);
    }

    /// <summary>Adding a comment after the network puts it out of place and out of date; the network is removed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddedCommentRemovesNetwork()
    {
        using var document = Open(TrapNetworkPdf.Versioned);
        var page = PdfDocumentPages.GetPage(document, 0);
        _ = PdfPageAnnotations.Append(document.Objects, page, PdfAnnotations.Create(document.Objects, KnownName.Square, CommentBox));
        var report = new PdfSaveReport();

        using var reopened = Reopen(Save(document, false, report));

        await AssertRemoved(report, PdfTrapNetworkProblems.AnnotationsChanged);
        await Assert.That(Network(reopened)).IsNull();
        await Assert.That(PdfPageAnnotations.GetArray(reopened.Objects, PdfDocumentPages.GetPage(reopened, 0))!.Count).IsEqualTo(OtherAnnotations + 1);
    }

    /// <summary>A page dated later than its network has out of date traps, which a full save removes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LaterPageDateRemovesNetwork()
    {
        using var document = Open(TrapNetworkPdf.Dated(LaterDate));
        var report = new PdfSaveReport();

        using var reopened = Reopen(Save(document, false, report));

        await AssertRemoved(report, PdfTrapNetworkProblems.PageNewer);
        await Assert.That(Network(reopened)).IsNull();
    }

    /// <summary>A page dated before its network keeps the network, as does a page with no date.</summary>
    /// <param name="pageDate">The page's date, or empty.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(EarlierDate)]
    [Arguments("")]
    public async Task EarlierPageDateKeepsNetwork(string pageDate)
    {
        using var document = Open(TrapNetworkPdf.Dated(pageDate));
        var report = new PdfSaveReport();

        using var reopened = Reopen(Save(document, false, report));

        await Assert.That(report.TrapNetworks).IsEmpty();
        await Assert.That(Network(reopened)).IsNotNull();
    }

    /// <summary>A dated network is also out of date when an edit changes its page, as editors do not always update the page date.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContentEditRemovesDatedNetwork()
    {
        using var document = Open(TrapNetworkPdf.Dated(EarlierDate));
        ReplaceContent(document, TrapNetworkPdf.ContentObject, MovedText);
        var report = new PdfSaveReport();

        _ = Save(document, true, report);

        await AssertRemoved(report, PdfTrapNetworkProblems.ContentChanged);
    }

    /// <summary>A dated network is out of date when an edit changes another annotation's state.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StateEditRemovesDatedNetwork()
    {
        using var document = Open(TrapNetworkPdf.Dated(EarlierDate));
        var stamp = StoreReading.GetDictionary(document.Objects, new(TrapNetworkPdf.StampObject, 0))!.Clone();
        stamp.Set(KnownName.AS, PdfValue.FromName(KnownName.Off));
        StoreEditing.Replace(document.Objects, new(TrapNetworkPdf.StampObject, 0), PdfValue.FromDictionary(stamp));
        var report = new PdfSaveReport();

        _ = Save(document, true, report);

        await AssertRemoved(report, PdfTrapNetworkProblems.AnnotationsChanged);
    }

    /// <summary>A network whose match cannot be checked, or that has nothing to print, is removed by a full save.</summary>
    /// <param name="network">The network's entries.</param>
    /// <param name="problem">The problem expected.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Appearance, PdfTrapNetworkProblems.MissingChangeTracking)]
    [Arguments($"{Appearance} {Tracking} /LastModified (D:20200601000000Z)", PdfTrapNetworkProblems.MissingChangeTracking)]
    [Arguments($"{Appearance} /Version [4 0 R 5 0 R 6 0 R 7 0 R]", PdfTrapNetworkProblems.MissingChangeTracking)]
    [Arguments($"{Appearance} /LastModified (not a date)", PdfTrapNetworkProblems.MissingChangeTracking)]
    [Arguments($"/F 68 {Tracking}", PdfTrapNetworkProblems.MissingAppearance)]
    [Arguments($"/F 68 /AP << /N << /Default 11 0 R >> >> /AS /Missing {Tracking}", PdfTrapNetworkProblems.MissingAppearance)]
    [Arguments($"{Appearance} /Version [4 0 R 5 0 R 6 0 R] /AnnotStates [null /On]", PdfTrapNetworkProblems.ContentChanged)]
    [Arguments($"{Appearance} /Version [4 0 R 5 0 R 6 0 R 7 0 R 99 0 R] /AnnotStates [null /On]", PdfTrapNetworkProblems.ContentChanged)]
    [Arguments($"{Appearance} /Version [4 0 R 5 0 R 6 0 R 7 0 R] /AnnotStates [/On]", PdfTrapNetworkProblems.AnnotationsChanged)]
    public async Task MalformedNetworkIsRemoved(string network, PdfTrapNetworkProblems problem)
    {
        using var document = Open(new(network, string.Empty, false, false));
        var report = new PdfSaveReport();

        using var reopened = Reopen(Save(document, false, report));

        await AssertRemoved(report, problem);
        await Assert.That(Network(reopened)).IsNull();
    }

    /// <summary>A page with two networks cannot say which is right, so both are removed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MultipleNetworksAreRemoved()
    {
        using var document = Open(TrapNetworkPdf.Versioned with { TwoNetworks = true });
        var report = new PdfSaveReport();

        using var reopened = Reopen(Save(document, false, report));

        await AssertRemoved(report, PdfTrapNetworkProblems.MultipleNetworks);
        await Assert.That(Network(reopened)).IsNull();
        await Assert.That(PdfPageAnnotations.GetArray(reopened.Objects, PdfDocumentPages.GetPage(reopened, 0))!.Count).IsEqualTo(OtherAnnotations);
    }

    /// <summary>A network that still matches but has the wrong flags and is not last is fixed and kept, with its state.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrongFlagsAndPlaceAreFixed()
    {
        var sample = new TrapNetworkSample($"/F 6 /AP << /N << /Default 11 0 R >> >> /AS /Default {Tracking}", string.Empty, true, false);
        using var document = Open(sample);
        var report = new PdfSaveReport();

        using var reopened = Reopen(Save(document, false, report));

        await Assert.That(report.TrapNetworks).Count().IsEqualTo(1);
        await Assert.That(report.TrapNetworks[0].Action).IsEqualTo(PdfTrapNetworkAction.Repaired);
        await Assert.That(report.TrapNetworks[0].Problems).IsEqualTo(PdfTrapNetworkProblems.NotLast | PdfTrapNetworkProblems.WrongFlags);
        var annotations = PdfPageAnnotations.GetArray(reopened.Objects, PdfDocumentPages.GetPage(reopened, 0))!;
        await Assert.That(PdfTrapNetworkReader.IsNetwork(annotations.GetDictionary(OtherAnnotations))).IsTrue();
        await Assert.That(annotations.GetDictionary(0)!.IsName(KnownName.Subtype, KnownName.Square)).IsTrue();
        await Assert.That(Network(reopened)!.GetInt32(KnownName.F)).IsEqualTo(PdfTrapNetworkValidator.RequiredFlags);
        await Assert.That(CheckFirstPage(reopened).Problems).IsEqualTo(PdfTrapNetworkProblems.None);
    }

    /// <summary>A network with one appearance and no /AS gets a state that names the appearance.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingStateIsFixed()
    {
        using var document = Open(new($"/F 68 /AP << /N 11 0 R >> {Tracking}", string.Empty, false, false));
        var report = new PdfSaveReport();

        using var reopened = Reopen(Save(document, false, report));

        await Assert.That(report.TrapNetworks[0].Action).IsEqualTo(PdfTrapNetworkAction.Repaired);
        var network = Network(reopened)!;
        var state = network.GetName(KnownName.AS);
        await Assert.That(state.IsNone).IsFalse();
        await Assert.That(network.GetDictionary(KnownName.AP)!.GetDictionary(KnownName.N)!.Get(state).AsStream()).IsNotNull();
    }

    /// <summary>An incremental save leaves a page no edit touched as it was, as it writes nothing of that page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IncrementalSaveLeavesUntouchedPage()
    {
        using var document = Open(new($"/F 4 /AP << /N << /Default 11 0 R >> >> /AS /Default {Tracking}", string.Empty, false, false));
        ReplaceContent(document, TrapNetworkPdf.SecondContentObject, "BT /F1 24 Tf 72 700 Td (Edited second page) Tj ET");
        var report = new PdfSaveReport();

        _ = Save(document, true, report);

        await Assert.That(report.TrapNetworks).IsEmpty();
    }

    /// <summary>The removal is one edit that can be undone, which brings the network back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovalCanBeUndone()
    {
        using var document = Open(TrapNetworkPdf.Versioned);
        ReplaceContent(document, TrapNetworkPdf.ContentObject, MovedText);

        _ = Save(document, true, new());

        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoLabel).IsEqualTo("Update trap networks");
        await Assert.That(Network(document)).IsNull();
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(Network(document)).IsNotNull();
    }

    /// <summary>A cancelled save changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledSaveChangesNothing()
    {
        using var document = Open(TrapNetworkPdf.Versioned);
        ReplaceContent(document, TrapNetworkPdf.ContentObject, MovedText);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await using var output = new MemoryStream();

        await Assert.That(async () => await PdfIncrementalWriter.SaveAsync(document.Objects, output, new(), cancelled.Token)).Throws<OperationCanceledException>();

        await Assert.That(Network(document)).IsNotNull();
    }

    /// <summary>An unedited document is saved as its original bytes, network and all.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UneditedIncrementalSaveKeepsOriginal()
    {
        var file = TrapNetworkPdf.Build(TrapNetworkPdf.Dated(LaterDate));
        using var document = PdfDocumentReader.Open(file, null);
        var report = new PdfSaveReport();

        var saved = Save(document, true, report);

        await Assert.That(report.TrapNetworks).IsEmpty();
        await Assert.That(saved).IsEquivalentTo(file);
    }

    /// <summary>A copied page keeps a network that matches it, renumbered with the page so it still matches.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InsertedPageKeepsValidNetwork()
    {
        using var source = Open(TrapNetworkPdf.Versioned);
        using var target = PdfDocumentReader.Open(TestPdf.Create(1), null);
        var copy = new PdfPageEditReport();

        PdfDocumentPageOperations.InsertPages(target, 1, source, [0], copy, CancellationToken.None);

        await Assert.That(copy.TrapNetworks).IsEmpty();
        var report = new PdfSaveReport();
        using var reopened = Reopen(Save(target, false, report));
        await Assert.That(report.TrapNetworks).IsEmpty();
        var pages = PdfTrapNetworkPages.Find(reopened.Objects, CancellationToken.None)!;
        await Assert.That(pages).Count().IsEqualTo(1);
        await Assert.That(pages[0].PageIndex).IsEqualTo(1);
        await Assert.That(PdfTrapNetworkValidator.Check(pages[0], new(reopened.Objects)).Problems).IsEqualTo(PdfTrapNetworkProblems.None);
    }

    /// <summary>Extracting a page keeps a network that matches it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtractedPageKeepsValidNetwork()
    {
        using var source = Open(TrapNetworkPdf.Versioned);
        var copy = new PdfPageEditReport();

        using var extracted = Reopen(PdfDocumentPageOperations.ExtractPages(source, [0], copy, CancellationToken.None));

        await Assert.That(copy.TrapNetworks).IsEmpty();
        await Assert.That(CheckFirstPage(extracted).Problems).IsEqualTo(PdfTrapNetworkProblems.None);
    }

    /// <summary>A copy leaves out a network the source's edits made out of date, and says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopyLeavesOutStaleNetwork()
    {
        using var source = Open(TrapNetworkPdf.Versioned);
        ReplaceContent(source, TrapNetworkPdf.ContentObject, MovedText);
        var copy = new PdfPageEditReport();

        using var extracted = Reopen(await PdfDocumentPageEditing.ExtractPagesAsync(source, FirstPage, copy, CancellationToken.None));

        await Assert.That(copy.TrapNetworks).Count().IsEqualTo(1);
        await Assert.That(copy.TrapNetworks[0].Action).IsEqualTo(PdfTrapNetworkAction.LeftOut);
        await Assert.That(copy.TrapNetworks[0].Message).Contains("did not copy");
        await Assert.That(Network(extracted)).IsNull();
    }

    /// <summary>A network whose /Version names a page cannot be kept valid by a copy, which leaves it out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopyLeavesOutNetworkNamingAPage()
    {
        var sample = new TrapNetworkSample($"{Appearance} /Version [4 0 R 5 0 R 6 0 R 7 0 R 12 0 R] /AnnotStates [null /On]", string.Empty, false, false);
        using var source = Open(sample);
        using var target = PdfDocumentReader.Open(TestPdf.Create(1), null);
        var copy = new PdfPageEditReport();

        await PdfDocumentPageEditing.InsertPagesAsync(target, 0, source, FirstPage, copy, CancellationToken.None);

        await Assert.That(copy.TrapNetworks[0].Problems & PdfTrapNetworkProblems.ReferencesLost).IsEqualTo(PdfTrapNetworkProblems.ReferencesLost);
        await Assert.That(Network(target)).IsNull();
    }

    /// <summary>A copy fixes the flags and place of a network that still matches, so the copy conforms.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopyFixesFlagsAndPlace()
    {
        var sample = new TrapNetworkSample($"/F 6 /AP << /N << /Default 11 0 R >> >> /AS /Default {Tracking}", string.Empty, true, false);
        using var source = Open(sample);
        var copy = new PdfPageEditReport();

        using var extracted = Reopen(PdfDocumentPageOperations.ExtractPages(source, [0], copy, CancellationToken.None));

        await Assert.That(copy.TrapNetworks[0].Action).IsEqualTo(PdfTrapNetworkAction.Repaired);
        await Assert.That(CheckFirstPage(extracted).Problems).IsEqualTo(PdfTrapNetworkProblems.None);
        await Assert.That(StoreReading.GetDictionary(source.Objects, new(TrapNetworkPdf.NetworkObject, 0))!.GetInt32(KnownName.F)).IsEqualTo(WrongFlags);
    }

    /// <summary>Copying one page with the page importer keeps a valid network and leaves out a stale one.</summary>
    /// <param name="stale">Whether the source page was edited after its traps were made.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PageImporterChecksNetwork(bool stale)
    {
        using var source = Open(TrapNetworkPdf.Versioned);
        if (stale)
        {
            ReplaceContent(source, TrapNetworkPdf.ContentObject, MovedText);
        }

        var builder = new PdfDocumentBuilder();
        _ = new PdfPageImporter(builder, source).ImportPage(PdfDocumentPages.GetPage(source, 0), PdfAnnotationFilter.All);

        using var copy = Reopen(builder.ToArray());
        await Assert.That(Network(copy) is null).IsEqualTo(stale);
    }

    /// <summary>A page tree that lists itself as a kid ends the walk, and a page listed twice counts as two pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LoopedPageTreeEnds()
    {
        var file = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 2 0 R 3 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>",
            "<< /Type /Annot /Subtype /TrapNet /Rect [0 0 612 792] /F 68 >>");
        using var store = StoreOpening.Open(file, null);

        var pages = PdfTrapNetworkPages.Find(store, CancellationToken.None);

        await Assert.That(pages).IsNotNull();
        await Assert.That(pages!).Count().IsEqualTo(OtherAnnotations);
        await Assert.That(pages[1].PageIndex).IsEqualTo(1);
    }

    /// <summary>Every message names the page and gives a reason in plain words.</summary>
    /// <param name="action">The action.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfTrapNetworkAction.Removed)]
    [Arguments(PdfTrapNetworkAction.Repaired)]
    [Arguments(PdfTrapNetworkAction.Unchanged)]
    [Arguments(PdfTrapNetworkAction.LeftOut)]
    public async Task MessagesNameThePage(PdfTrapNetworkAction action)
    {
        var change = new PdfTrapNetworkChange(ThirdPage, action, PdfTrapNetworkProblems.ContentChanged);

        await Assert.That(change.Message).Contains("page 3");
    }

    /// <summary>Opens a sample.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(TrapNetworkSample sample) => PdfDocumentReader.Open(TrapNetworkPdf.Build(sample), null);

    /// <summary>Reopens a saved file and checks it needed no repair.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The document.</returns>
    /// <exception cref="InvalidOperationException">The file needed repair to open.</exception>
    private static PdfDocument Reopen(byte[] file)
    {
        var document = PdfDocumentReader.Open(file, null);
        if (document.Objects.WasRepaired)
        {
            document.Dispose();
            throw new InvalidOperationException("The saved file needed repair to open.");
        }

        return document;
    }

    /// <summary>Saves a document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="incremental">Whether the save is incremental rather than compact.</param>
    /// <param name="report">The report.</param>
    /// <returns>The file.</returns>
    private static byte[] Save(PdfDocument document, bool incremental, PdfSaveReport report)
    {
        using var output = new MemoryStream();
        if (incremental)
        {
            PdfIncrementalWriter.Save(document.Objects, output, report);
        }
        else
        {
            PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default, output, report);
        }

        return output.ToArray();
    }

    /// <summary>Replaces a content stream's data.</summary>
    /// <param name="document">The document.</param>
    /// <param name="number">The stream's object number.</param>
    /// <param name="content">The new content.</param>
    private static void ReplaceContent(PdfDocument document, int number, string content) =>
        StoreEditing.Replace(document.Objects, new(number, 0), PdfValue.FromStream(new(new PdfDictionary(document.Objects), Encoding.ASCII.GetBytes(content))));

    /// <summary>Gets the trap network on the first page.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The network, or <see langword="null"/>.</returns>
    private static PdfDictionary? Network(PdfDocument document)
    {
        var annotations = PdfPageAnnotations.GetArray(document.Objects, PdfDocumentPages.GetPage(document, 0));
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            if (PdfTrapNetworkReader.IsNetwork(annotations.GetDictionary(i)))
            {
                return annotations.GetDictionary(i);
            }
        }

        return null;
    }

    /// <summary>Checks the trap network on the first page.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The check.</returns>
    private static PdfTrapNetworkCheck CheckFirstPage(PdfDocument document)
    {
        var page = PdfTrapNetworkPages.Find(document.Objects, CancellationToken.None)![0];
        return PdfTrapNetworkValidator.Check(page, new(document.Objects));
    }

    /// <summary>Checks a report lists one removal with a problem.</summary>
    /// <param name="report">The report.</param>
    /// <param name="problem">The problem expected among the change's problems.</param>
    /// <returns>A task.</returns>
    private static async Task AssertRemoved(PdfSaveReport report, PdfTrapNetworkProblems problem)
    {
        await Assert.That(report.TrapNetworks).Count().IsEqualTo(1);
        await Assert.That(report.TrapNetworks[0].Action).IsEqualTo(PdfTrapNetworkAction.Removed);
        await Assert.That(report.TrapNetworks[0].PageIndex).IsEqualTo(0);
        await Assert.That(report.TrapNetworks[0].Problems & problem).IsEqualTo(problem);
        await Assert.That(report.TrapNetworks[0].Message).Contains("removed the trap network");
    }
}
