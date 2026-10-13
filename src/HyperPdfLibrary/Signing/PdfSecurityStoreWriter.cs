// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Signing;

/// <summary>Writes long-term validation data into the document security store as an incremental update.</summary>
internal static class PdfSecurityStoreWriter
{
    /// <summary>Adds validation data to the store.</summary>
    /// <param name="document">The document.</param>
    /// <param name="data">The data to add.</param>
    /// <param name="includeVri">Whether to add a validation-related information entry for each signature.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The updated file.</returns>
    internal static PdfSigningResult Add(PdfDocument document, IReadOnlyList<PdfSignatureValidationData> data, bool includeVri, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);

        // The update is built on a private copy, so the document the caller shows is never edited and other edits do not join this transaction.
        using var working = PdfDocumentOptimizing.OpenWorkingCopy(document);
        var store = working.Objects;
        using var transaction = PdfDocumentEditing.BeginEdit(working, "Add validation data");
        var builder = new PdfSecurityStoreBuilder(store, transaction);
        try
        {
            var time = TimeProvider.System.GetUtcNow();
            for (var i = 0; i < data.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.Add(data[i], includeVri, time);
            }

            builder.Write();
        }
        finally
        {
            builder.Dispose();
        }

        PdfSigningDeclarations.DeclareFor(working, PdfSignatureFormat.Unknown, PdfDigestMethod.Unknown, false, true);
        var file = PdfIncrementalWriter.BuildSigningFile(store, null, out var updateStart, cancellationToken);
        List<string>? notes = null;
        PdfSigningVerification.CheckUpdate(file, updateStart, "the validation data", ref notes);
        return new(file, string.Empty, PdfSignatureFormat.Unknown, PdfDigestMethod.Unknown, 0, 0, 1, (IReadOnlyList<string>?)notes ?? []);
    }
}
