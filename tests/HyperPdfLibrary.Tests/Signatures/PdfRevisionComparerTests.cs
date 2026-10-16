// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>Tests comparison-local ownership and signed object roles.</summary>
public sealed class PdfRevisionComparerTests
{
    /// <summary>The page defined in the earlier revision.</summary>
    private const int PageNumber = 3;

    /// <summary>The security store introduced in the later revision.</summary>
    private const int SecurityStoreNumber = 4;

    /// <summary>The catalog before any security-store update.</summary>
    private const string CatalogDictionary = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The single-page tree.</summary>
    private const string PageTreeDictionary = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";

    /// <summary>The page protected by the earlier revision.</summary>
    private const string PageDictionary = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>";

    /// <summary>Ownership cycles terminate, and ownership cannot disguise a replaced signed page.</summary>
    /// <param name="cancellationToken">Cancels document parsing.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Compare_CircularSecurityStore_DoesNotDisguiseSignedPage(CancellationToken cancellationToken)
    {
        var earlier = MiniPdf.Build(CatalogDictionary, PageTreeDictionary, PageDictionary);
        var later = IncrementalPdf.Append(earlier, new Dictionary<int, string>
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R /DSS 4 0 R >>",
            [PageNumber] = SignedPageRevisions.Annotation,
            [SecurityStoreNumber] = "<< /Data [3 0 R 4 0 R] >>",
        });
        using var signed = await PdfDocumentReader.OpenAsync(earlier, null, cancellationToken);
        using var current = await PdfDocumentReader.OpenAsync(later, null, cancellationToken);

        var changes = PdfRevisionComparer.Compare(current.Objects, signed.Objects, earlier.Length);
        var page = Array.Find(changes, static change => change.ObjectNumber == PageNumber);
        var securityStore = Array.Find(changes, static change => change.ObjectNumber == SecurityStoreNumber);
        var repeated = PdfRevisionComparer.Compare(current.Objects, signed.Objects, earlier.Length);
        var unchanged = PdfRevisionComparer.Compare(signed.Objects, signed.Objects, earlier.Length);

        await Assert.That(page.Kind).IsEqualTo(PdfModificationKinds.Pages);
        await Assert.That(securityStore.Kind).IsEqualTo(PdfModificationKinds.SecurityStore);
        await Assert.That(repeated).IsEquivalentTo(changes);
        await Assert.That(ReferenceEquals(repeated, changes)).IsFalse();
        await Assert.That(unchanged.Length).IsEqualTo(0);
    }

    /// <summary>A failed ownership walk clears leased references before returning its working storage.</summary>
    /// <param name="cancellationToken">Cancels document parsing.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Compare_InvalidSecurityStore_ClearsScratch(CancellationToken cancellationToken)
    {
        var earlier = MiniPdf.Build(CatalogDictionary, PageTreeDictionary, PageDictionary);
        var later = IncrementalPdf.Append(earlier, new Dictionary<int, string>
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R /DSS 4 0 R >>",
            [SecurityStoreNumber] = "<< /Length -1 >>\nstream\nx\nendstream",
        });
        using var signed = await PdfDocumentReader.OpenAsync(earlier, null, cancellationToken);
        using var current = await PdfDocumentReader.OpenWithAsync(
            new MemoryPdfByteSource(later),
            true,
            PdfOpenOptions.Default with { Recovery = false },
            cancellationToken);

        await Assert.That(() => PdfRevisionComparer.Compare(current.Objects, signed.Objects, earlier.Length)).Throws<PdfException>();

        var owned = current.Objects.ScratchPools.Slots<Dictionary<int, PdfObjectChange>>().Take();
        var pending = current.Objects.ScratchPools.Slots<Stack<PdfValue>>().Take();
        await Assert.That(owned).IsNotNull();
        await Assert.That(pending).IsNotNull();
        await Assert.That(owned!.Count).IsEqualTo(0);
        await Assert.That(pending!.Count).IsEqualTo(0);
    }

    /// <summary>A cancelled document operation stops before scanning its revision's objects.</summary>
    /// <param name="cancellationToken">Cancels document parsing.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Compare_Cancelled_StopsBeforeObjectWalk(CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(CatalogDictionary, PageTreeDictionary, PageDictionary);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, operation.Token);
        await operation.CancelAsync();

        await Assert.That(() => PdfRevisionComparer.Compare(document.Objects, document.Objects, bytes.Length)).Throws<OperationCanceledException>();
    }
}
