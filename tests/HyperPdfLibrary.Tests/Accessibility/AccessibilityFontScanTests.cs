// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks font scanning when the document has no pages to walk.</summary>
public sealed class AccessibilityFontScanTests
{
    /// <summary>An empty document skips font-walk state and collection construction.</summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Run_EmptyDocument_DoesNotAllocate(CancellationToken cancellationToken)
    {
        using var warm = OpenEmpty();
        var warmFindings = AccessibilityFindingCollection.Rent(warm.Objects.ScratchPools);
        try
        {
            AccessibilityFontScan.Run(warm, warmFindings, cancellationToken);
        }
        finally
        {
            AccessibilityFindingCollection.Release(warm.Objects.ScratchPools, warmFindings);
        }

        using var document = OpenEmpty();
        var findings = AccessibilityFindingCollection.Rent(document.Objects.ScratchPools);
        try
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            AccessibilityFontScan.Run(document, findings, cancellationToken);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            await Assert.That(findings.Findings.Count).IsEqualTo(0);
            await Assert.That(allocated).IsEqualTo(0);
        }
        finally
        {
            AccessibilityFindingCollection.Release(document.Objects.ScratchPools, findings);
        }
    }

    /// <summary>An empty document cannot bypass an already cancelled scan.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Run_EmptyDocumentCancelled_Throws()
    {
        using var document = OpenEmpty();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var findings = AccessibilityFindingCollection.Rent(document.Objects.ScratchPools);
        try
        {
            await Assert.That(() => AccessibilityFontScan.Run(document, findings, cancelled.Token))
                .Throws<OperationCanceledException>();
        }
        finally
        {
            AccessibilityFindingCollection.Release(document.Objects.ScratchPools, findings);
        }
    }

    /// <summary>Opens a page tree with no pages or content.</summary>
    /// <returns>The document.</returns>
    private static PdfDocument OpenEmpty() => PdfDocumentReader.Open(
        MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [] /Count 0 >>"),
        null);
}
