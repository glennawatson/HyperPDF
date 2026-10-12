// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Redaction;
using HyperPdfLibrary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Redaction;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Engine;

/// <summary>Implements DocumentRedaction over the document's owned state.</summary>
internal static class HyperPdfDocumentRedaction
{
    /// <summary>Applies every redaction mark and writes a new file. The open document is never changed, and neither is its file.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="destination">The writable stream that receives the new file; it holds a partial file after a failure or cancel.</param>
    /// <param name="settings">What to remove under the marks, and what to clean up.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What was removed.</returns>
    /// <exception cref="System.OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="System.IO.InvalidDataException">The document cannot be read or written.</exception>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    internal static async Task<RedactionReport> ApplyAsync(HyperPdfDocument self, Stream destination, RedactionSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(settings);
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        var options = HyperPdfRedaction.ToOptions(settings);
        try
        {
            // The copy carries the marks and every unsaved edit; the open document keeps its marks and stays as it is.
            var copy = PdfDocumentOptimizing.OpenWorkingCopy(self.Document);
            var report = await Task.Run(() => PdfRedactor.Apply(copy, options, cancellationToken), cancellationToken).ConfigureAwait(false);
            var save = new PdfSaveReport();
            await PdfCompactWriter.SaveAsync(copy.Objects, options.Layout, destination, save, cancellationToken).ConfigureAwait(false);
            return new(report.Regions, report.Pages, report.GlyphsRemoved, report.ImagesRemoved + report.ImagesBlanked, report.PathsRemoved, report.AnnotationsRemoved)
            {
                ClaimRemoved = ArchiveMapping.ToRemoval(save.Claim),
            };
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }
}
