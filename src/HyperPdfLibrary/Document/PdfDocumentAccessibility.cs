// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Accessibility;

namespace HyperPdfLibrary.Document;

/// <summary>Reads document accessibility information.</summary>
public static class PdfDocumentAccessibility
{
    /// <summary>Reads the document's PDF/UA claim and looks for common accessibility faults. This is a reading report, not a validator: use veraPDF to check conformance.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The report.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfAccessibilityReport GetAccessibilityReport(PdfDocument document) => PdfDocumentAccessibility.GetAccessibilityReport(document, CancellationToken.None);

    /// <summary>Reads the document's PDF/UA claim and looks for common accessibility faults. This is a reading report, not a validator: use veraPDF to check conformance.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellation">A token that stops the work between elements and pages.</param>
    /// <returns>The report.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfAccessibilityReport GetAccessibilityReport(PdfDocument document, CancellationToken cancellation) => PdfAccessibilityReader.Read(document, cancellation);

    /// <summary>Reads the document's PDF/UA claim and looks for common accessibility faults, fetching each page first with asynchronous I/O.
    /// The report lists what it finds; it does not state whether a file meets a standard.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">A token that stops the reads and the work between elements and pages.</param>
    /// <returns>The report.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfAccessibilityReport> GetAccessibilityReportAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        for (var index = 0; index < document.PageCount; index++)
        {
            _ = await PdfDocumentPages.GetPageAsync(document, index, cancellationToken).ConfigureAwait(false);
        }

        return PdfAccessibilityReader.Read(document, cancellationToken);
    }
}
