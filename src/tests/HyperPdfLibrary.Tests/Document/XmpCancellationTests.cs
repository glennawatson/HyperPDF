// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Checks metadata cancellation and shared catalog and owner snapshots.</summary>
public sealed class XmpCancellationTests
{
    /// <summary>Catalog and owner cache reads honor cancellation even when a result is ready.</summary>
    /// <param name="catalog">Whether to use the document-wide cache.</param>
    /// <param name="warm">Whether to populate the cache before cancellation.</param>
    /// <returns>The test task.</returns>
    [Test]
    [Arguments(true, true)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(false, false)]
    public async Task CancelledReadDoesNotPublishOrReturnMetadata(bool catalog, bool warm)
    {
        using var document = Open();
        var owner = catalog ? document.Catalog : PdfDocumentPages.GetPage(document, 0).Dictionary;
        if (warm)
        {
            _ = PdfDocumentMetadata.ReadXmp(document, owner);
        }

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.That(() => PdfDocumentMetadata.ReadOwnerXmp(document, owner, cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(PdfDocumentMetadata.ReadXmp(document, owner)!.Title).IsEqualTo("Kept");
    }

    /// <summary>Token-aware and synchronous reads share the same retained result.</summary>
    /// <param name="catalog">Whether to use the catalog cache.</param>
    /// <returns>The test task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task TokenAwareReadsShareTheSynchronousSnapshot(bool catalog)
    {
        using var document = Open();
        var owner = catalog ? document.Catalog : PdfDocumentPages.GetPage(document, 0).Dictionary;
        var first = PdfDocumentMetadata.ReadOwnerXmp(document, owner, CancellationToken.None);
        await Assert.That(ReferenceEquals(first, PdfDocumentMetadata.ReadXmp(document, owner))).IsTrue();
    }

    /// <summary>Missing streams do not hide cancellation.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task MissingStreamDoesNotSwallowCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        PdfStream? stream = null;
        await Assert.That(() => stream.DecodeOrEmpty(cancellation.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Opens a memory-only document with catalog and page XMP.</summary>
    /// <returns>The document.</returns>
    private static PdfDocument Open()
    {
        var bytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Metadata 4 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] /Metadata 4 0 R >>",
            MiniPdf.Stream(
                "/Type /Metadata /Subtype /XML",
                """
                <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
                  <rdf:Description xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:title>Kept</dc:title></rdf:Description>
                </rdf:RDF>
                """));
        return PdfDocumentReader.Open(bytes, null);
    }
}
