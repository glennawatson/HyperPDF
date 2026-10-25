// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Signing;
using HyperPdfLibrary.Tests.Editing;
using HyperPdfLibrary.Tests.Signatures;
using HyperPdfLibrary.Tests.Signing;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>What a document caches follows its edits: nothing stays from before an edit, and a reader that raced an edit does not keep what it read.</summary>
public sealed class DocumentCacheEditTests
{
    /// <summary>The page count of the test documents.</summary>
    private const int Pages = 3;

    /// <summary>The last page index.</summary>
    private const int Last = 2;

    /// <summary>The first page's text.</summary>
    private const string PageOne = "Page 1";

    /// <summary>The third page's text.</summary>
    private const string PageThree = "Page 3";

    /// <summary>The bookmarks added while another thread reads the outline.</summary>
    private const int Bookmarks = 60;

    /// <summary>The object number of the first optional content group.</summary>
    private const int GroupA = 4;

    /// <summary>The catalog entries of a file with an empty structure tree.</summary>
    private const string TaggedCatalog = "/StructTreeRoot 4 0 R /MarkInfo << /Marked true >>";

    /// <summary>The title an edit sets.</summary>
    private const string EditedTitle = "Edited";

    /// <summary>A value that is published after the edit it predates.</summary>
    private const string StaleValue = "stale";

    /// <summary>A value that is published at the current revision.</summary>
    private const string CurrentValue = "current";

    /// <summary>The first value published to a slot.</summary>
    private const string FirstValue = "first";

    /// <summary>Signature details are read again after the signatures are removed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignatureDetailsFollowRemovedSignatures()
    {
        var signed = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Reason = "Cached" });
        using var document = PdfDocumentReader.Open(signed, null);
        var before = PdfDocumentSignatureValidation.GetSignatureDetails(document).Count(static detail => detail.IsSigned);

        _ = PdfDocumentSignatureEditing.RemoveSignatures(document);
        var after = PdfDocumentSignatureValidation.GetSignatureDetails(document).Count(static detail => detail.IsSigned);

        await Assert.That(before).IsEqualTo(1);
        await Assert.That(after).IsEqualTo(0);
    }

    /// <summary>The security store is read again after an edit.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecurityStoreIsReadAgainAfterAnEdit(CancellationToken cancellationToken)
    {
        var signed = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Certification = PdfMdpPermission.NoChanges });
        using var source = PdfDocumentReader.Open(signed, null);
        var updated = await PdfDocumentSigning.AddValidationDataAsync(source, [new([], [SignatureFixtures.Signer.RawData], [], [])], false, cancellationToken);
        using var document = PdfDocumentReader.Open(updated.Bytes, null);
        var first = PdfDocumentSignatureValidation.GetSecurityStore(document);
        var again = PdfDocumentSignatureValidation.GetSecurityStore(document);

        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });
        var after = PdfDocumentSignatureValidation.GetSecurityStore(document);

        await Assert.That(again).IsSameReferenceAs(first);
        await Assert.That(after).IsNotSameReferenceAs(first);
        await Assert.That(after.Certificates.Length).IsEqualTo(first.Certificates.Length);
    }

    /// <summary>A value computed before an edit is not kept in a cache after it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValueComputedBeforeAnEditIsNotKept()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var revision = PdfDocumentCaches.Revision(document);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });
        string? stale = null;
        string? current = null;

        var staleResult = PdfDocumentCaches.Publish(document, ref stale, StaleValue, revision);
        var currentResult = PdfDocumentCaches.Publish(document, ref current, CurrentValue, PdfDocumentCaches.Revision(document));

        await Assert.That(staleResult).IsEqualTo(StaleValue);
        await Assert.That(stale).IsNull();
        await Assert.That(currentResult).IsEqualTo(CurrentValue);
        await Assert.That(current).IsEqualTo(CurrentValue);
    }

    /// <summary>The first value published is kept, unless an edit ran since it was computed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FirstPublishedValueIsKeptUnlessAnEditRan()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        string? slot = null;
        var revision = PdfDocumentCaches.Revision(document);

        var first = PdfDocumentCaches.PublishFirst(document, ref slot, FirstValue, revision);
        var second = PdfDocumentCaches.PublishFirst(document, ref slot, CurrentValue, revision);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });
        string? stale = null;
        var late = PdfDocumentCaches.PublishFirst(document, ref stale, StaleValue, revision);

        await Assert.That(first).IsEqualTo(FirstValue);
        await Assert.That(second).IsEqualTo(FirstValue);
        await Assert.That(late).IsEqualTo(StaleValue);
        await Assert.That(stale).IsNull();
    }

    /// <summary>The outline read after an edit has the edit, while another thread keeps reading it.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutlineReadAfterAnEditHasTheEditWhileAnotherThreadReads(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var reading = new OutlineReader(document);
        var reader = DedicatedThread.Run(reading, static job => job.ReadUntilStopped(), cancellationToken);

        var stale = 0;
        for (var i = 1; i <= Bookmarks; i++)
        {
            _ = PdfDocumentOutlineEditing.AddBookmark(document, default, new($"Bookmark {i}", PdfBookmarkTarget.ToPage(PdfExplicitDestination.FitPage(0))));
            stale += PdfDocumentNavigation.GetOutline(document).Count == i ? 0 : 1;
        }

        reading.Stop();
        await reader;

        await Assert.That(stale).IsEqualTo(0);
    }

    /// <summary>The cached text of a page follows a reorder.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextPageFollowsReorder()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var before = PdfDocumentText.GetTextPage(document, 0).Text;

        PdfDocumentPageOperations.ReorderPages(document, [Last, 1, 0]);
        var after = PdfDocumentText.GetTextPage(document, 0).Text;

        await Assert.That(before).Contains(PageOne);
        await Assert.That(after).Contains(PageThree);
    }

    /// <summary>The cached marked content of a page follows a reorder.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarkedContentFollowsReorder()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var before = PdfDocumentTagged.GetMarkedContent(document, 0).Text;

        PdfDocumentPageOperations.ReorderPages(document, [Last, 1, 0]);
        var after = PdfDocumentTagged.GetMarkedContent(document, 0).Text;

        await Assert.That(before).Contains(PageOne);
        await Assert.That(after).Contains(PageThree);
    }

    /// <summary>Marked content can be read for a page inserted after the cache was made for fewer pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarkedContentCanBeReadForAnInsertedPage()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        _ = PdfDocumentTagged.GetMarkedContent(document, 0);

        PdfDocumentPageOperations.InsertPages(document, 0, source, [0]);
        var last = PdfDocumentTagged.GetMarkedContent(document, Pages);

        await Assert.That(last.Text).Contains(PageThree);
    }

    /// <summary>The structure tree is read again after an edit that changes the pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StructureTreeIsReadAgainAfterAnEdit()
    {
        using var document = StructureDocuments.Open(TaggedCatalog, "<< /Type /StructTreeRoot /K [] >>");
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        var first = PdfDocumentTagged.GetStructureTree(document);
        var again = PdfDocumentTagged.GetStructureTree(document);

        PdfDocumentPageOperations.InsertPages(document, 0, source, [0]);
        var after = PdfDocumentTagged.GetStructureTree(document);

        await Assert.That(first).IsNotNull();
        await Assert.That(again).IsSameReferenceAs(first);
        await Assert.That(after).IsNotSameReferenceAs(first);
    }

    /// <summary>The layers a reader chose to show stay when an edit leaves the layers alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayerVisibilitySurvivesAnUnrelatedEdit()
    {
        using var document = OpenWithLayers();
        var content = PdfDocumentLayers.GetOptionalContent(document);
        _ = content.SetVisible(GroupA, false);
        var version = content.Version;

        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });
        var after = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(after).IsSameReferenceAs(content);
        await Assert.That(after.Layers[0].IsVisible).IsFalse();
        await Assert.That(after.Version).IsNotEqualTo(version);
    }

    /// <summary>The layers are read again when an edit changes them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayersAreReadAgainWhenALayerChanges()
    {
        using var document = OpenWithLayers();
        var content = PdfDocumentLayers.GetOptionalContent(document);
        _ = content.SetVisible(GroupA, false);

        PdfDocumentLayerEditing.RenameLayer(document, GroupA, "Renamed");
        var after = PdfDocumentLayers.GetOptionalContent(document);

        await Assert.That(after).IsNotSameReferenceAs(content);
        await Assert.That(after.Layers[0].Name).IsEqualTo("Renamed");
        await Assert.That(after.Version).IsGreaterThan(content.Version);
    }

    /// <summary>Opens a one page file with two optional content groups, both shown.</summary>
    /// <returns>The document.</returns>
    private static PdfDocument OpenWithLayers() => PdfDocumentReader.Open(
        MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /OCProperties << /OCGs [4 0 R 5 0 R] /D << /Order [4 0 R 5 0 R] >> >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>",
            "<< /Type /OCG /Name (A) >>",
            "<< /Type /OCG /Name (B) >>"),
        null);

    /// <summary>Reads a document's outline again and again, until told to stop.</summary>
    /// <param name="document">The document.</param>
    private sealed class OutlineReader(PdfDocument document)
    {
        /// <summary>Set to 1 to stop the reader.</summary>
        private int _stopped;

        /// <summary>Reads the outline until <see cref="Stop"/> is called.</summary>
        public void ReadUntilStopped()
        {
            while (Volatile.Read(ref _stopped) == 0)
            {
                _ = PdfDocumentNavigation.GetOutline(document).Count;
            }
        }

        /// <summary>Tells the reader to stop.</summary>
        public void Stop() => Volatile.Write(ref _stopped, 1);
    }
}
