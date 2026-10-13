// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Flattening;
using HyperPdfLibrary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Engine;

/// <summary>Writes flattened copies of an open document with the library's flatten.</summary>
internal static class HyperPdfDocumentFlattenCopy
{
    /// <summary>Writes a copy with the chosen fields and annotations drawn into the pages, or explains why it cannot. The open document is never changed.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="destination">The writable stream that receives the copy; nothing is written when the copy cannot be made.</param>
    /// <param name="options">What to flatten and which look to keep.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What was flattened, or why the copy could not be made.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    internal static async Task<FlattenCopyResult> WriteAsync(HyperPdfDocument self, Stream destination, FlattenCopyOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // The snapshot carries every unsaved edit, so the copy matches what the user sees.
            using var snapshot =HyperPdfOptimization.TakeSnapshot(self);
            if (PdfDocumentFlattening.GetRefusal(snapshot) != PdfFlattenRefusal.None)
            {
                return HyperPdfFlattenCopyMapping.Signed();
            }

            var report = await PdfDocumentFlattening.FlattenAllAsync(snapshot, HyperPdfFlattenCopyMapping.ToLibrary(options), cancellationToken).ConfigureAwait(false);
            await PdfCompactWriter.SaveAsync(snapshot.Objects, PdfCompactOptions.Default, destination, cancellationToken).ConfigureAwait(false);
            return HyperPdfFlattenCopyMapping.ToResult(report);
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }
}
