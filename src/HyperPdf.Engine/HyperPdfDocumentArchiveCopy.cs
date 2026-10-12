// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Archiving;
using HyperPdfLibrary;
using HyperPdfLibrary.Conformance;

namespace HyperPdf.Engine;

/// <summary>Writes PDF/A copies of the open document with the library's converter.</summary>
internal static class HyperPdfDocumentArchiveCopy
{
    /// <summary>Writes a copy that meets a PDF/A level, or explains why it cannot. The open document is never changed.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="destination">The writable stream that receives the copy; nothing is written when the copy cannot be made.</param>
    /// <param name="level">The level the copy must meet.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What was changed, or why the copy could not be made.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    internal static async Task<ArchiveCopyResult> WriteAsync(HyperPdfDocument self, Stream destination, ArchiveLevel level, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // The snapshot carries every unsaved edit, so the copy matches what the user sees.
            using var snapshot = HyperPdfOptimization.TakeSnapshot(self);
            var result = await PdfAConverter.SaveAsPdfAAsync(snapshot, ArchiveMapping.ToLibrary(level), destination, cancellationToken).ConfigureAwait(false);
            return ArchiveMapping.ToResult(result);
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }
}
