// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Accessibility;

namespace HyperPdfLibrary.Tests.Async;

/// <summary>Checks real async loading of indirect configuration data outside the opening cache.</summary>
public sealed class OptionalContentReadAheadTests
{
    /// <summary>Reading configuration dictionaries, arrays and scalar references never adds a blocking stream read.</summary>
    /// <param name="indirectArray">Whether the Configs array is indirect.</param>
    /// <param name="cancellationToken">Cancels opening and reporting.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task IndirectConfigurationsLoadAsynchronously(bool indirectArray, CancellationToken cancellationToken)
    {
        var bytes = OptionalContentPdf.Create(AccessibilityPdfs.Packet(1, true), indirectArray);
        await using var stream = new ThrottledStream(bytes, TimeSpan.Zero);
        using var document = await PdfDocumentReader.OpenAsync(stream, null, cancellationToken);
        var syncBefore = stream.SyncReads;
        var asyncBefore = stream.AsyncReads;
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(report.GetCount(PdfAccessibilityCode.OptionalContentAutomaticState)).IsEqualTo(1);
        await Assert.That(report.GetCount(PdfAccessibilityCode.OptionalContentNameMissing)).IsEqualTo(0);
        await Assert.That(stream.SyncReads).IsEqualTo(syncBefore);
        await Assert.That(stream.AsyncReads).IsGreaterThan(asyncBefore);
        var reads = stream.AsyncReads;
        await Assert.That(await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken)).IsSameReferenceAs(report);
        await Assert.That(stream.AsyncReads).IsEqualTo(reads);
    }
}
