// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Optimizing;
using HyperPdfLibrary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Optimizing;
namespace HyperPdf.Engine;

/// <summary>Implements DocumentOptimization over the document's owned state.</summary>
internal static class HyperPdfDocumentOptimization
{
    /// <summary>Writes an optimised copy. The open document is never changed.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="destination">The writable stream that receives the new file; it holds a partial file after a failure or cancel.</param>
    /// <param name="settings">What may change.</param>
    /// <param name="progress">Receives progress, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What was done.</returns>
    /// <exception cref="System.OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="System.IO.InvalidDataException">The document cannot be read or written.</exception>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    internal static async Task<OptimizeReport> OptimizeAsync(
        HyperPdfDocument self,
        Stream destination,
        OptimizeSettings settings,
        IProgress<OptimizeProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(settings);
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var snapshot =HyperPdfOptimization.TakeSnapshot(self);
            var options = OptimizeMapping.ToOptions(settings);
            var report = await PdfOptimizer.OptimizeSnapshotAsync(snapshot, destination, options, OptimizeMapping.ToProgress(progress), cancellationToken).ConfigureAwait(false);
            return OptimizeMapping.ToReport(report);
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }
}
