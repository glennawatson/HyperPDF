// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Checks the integrity protection of an encrypted PDF 2.0 document: the PDF MAC tokens of ISO/TS 32004:2024. The result
/// describes the bytes the document was opened from, not edits made since. A document that is not encrypted, or that neither
/// requires nor holds a PDF MAC, gives <see cref="PdfMacStatus.NotApplicable"/>.
/// </summary>
public static class PdfDocumentMac
{
    /// <summary>Checks every revision's PDF MAC, once; later calls return the same report.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static PdfMacReport GetIntegrity(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Read(document, CancellationToken.None);
    }

    /// <summary>Checks every revision's PDF MAC, once, loading the file first with asynchronous reads.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the loading and the check.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfMacReport> GetIntegrityAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (Volatile.Read(ref document.State.MacReport) is { } cached)
        {
            return cached;
        }

        // The check hashes the whole file when a PDF MAC is present, so load it before the synchronous core runs.
        if (document.Objects.Security is not null && document.Objects.Source.NeedsPrefetch)
        {
            await document.Objects.Source.PrefetchAsync(0, document.Objects.Source.Length, cancellationToken).ConfigureAwait(false);
        }

        return Read(document, cancellationToken);
    }

    /// <summary>Reads the cached report, or checks the document and caches the report.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The report.</returns>
    private static PdfMacReport Read(PdfDocument document, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref document.State.MacReport) is { } cached)
        {
            return cached;
        }

        var report = PdfMacValidator.Validate(document, cancellationToken);
        Volatile.Write(ref document.State.MacReport, report);
        return report;
    }
}
