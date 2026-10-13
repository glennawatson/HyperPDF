// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Archiving;
using HyperPdfLibrary;
using HyperPdfLibrary.Pdf20;

namespace HyperPdf.Engine;

/// <summary>Writes a PDF 2.0 copy of an open document.</summary>
internal static class HyperPdfDocumentPdf20Copy
{
    /// <summary>Writes a PDF 2.0 copy, or explains why it cannot. The open document is never changed.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="destination">The writable stream that receives the copy; nothing is written when the copy cannot be made.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What was changed, or why the copy could not be made.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    internal static async Task<Pdf20CopyResult> WriteAsync(HyperPdfDocument self, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // The snapshot carries every unsaved edit, so the copy matches what the user sees.
            using var snapshot =HyperPdfOptimization.TakeSnapshot(self);
            var result = await Pdf20Converter.SaveAsPdf20Async(snapshot, destination, cancellationToken).ConfigureAwait(false);
            return new(ToNotes(result.Changes), ToNotes(result.Problems));
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>Converts conversion notes.</summary>
    /// <param name="notes">The library notes.</param>
    /// <returns>The app notes.</returns>
    private static ArchiveNote[] ToNotes(Pdf20ConversionNote[] notes)
    {
        var converted = new ArchiveNote[notes.Length];
        for (var i = 0; i < notes.Length; i++)
        {
            converted[i] = new(notes[i].Description, notes[i].Clause);
        }

        return converted;
    }
}
