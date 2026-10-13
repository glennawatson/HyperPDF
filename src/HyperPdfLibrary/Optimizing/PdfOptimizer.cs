// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Writes a smaller copy of a document: images downsampled and re-encoded, streams recompressed, duplicates merged,
/// unused objects dropped, fonts subset, optional cleanup, and accessibility entries filled in. The source document is
/// never changed; the optimiser works on a private copy over the same file. Objects are written to the destination as
/// they are produced, and images and fonts are re-encoded one at a time while they are written, so memory stays bounded.
/// </summary>
/// <remarks>
/// A signed document is never rewritten: either additions such as text layers are appended as an incremental update, or
/// the file is copied unchanged. An encrypted document keeps its encryption unless the options remove it. A document
/// that claims PDF/A conformance only gets changes that keep the claimed part valid; refused options are listed in the
/// report. The PDF version never goes down.
/// </remarks>
public static class PdfOptimizer
{
    /// <summary>Optimises a document into a stream with the default options.</summary>
    /// <param name="source">The document.</param>
    /// <param name="destination">The writable stream that receives the new file.</param>
    /// <param name="options">What may change.</param>
    /// <returns>What was done.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The document cannot be read or written.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfOptimizeReport Optimize(PdfDocument source, Stream destination, PdfOptimizeOptions options) =>
        Optimize(source, destination, options, null, CancellationToken.None);

    /// <summary>Optimises a document into a stream.</summary>
    /// <param name="source">The document.</param>
    /// <param name="destination">The writable stream that receives the new file.</param>
    /// <param name="options">What may change.</param>
    /// <param name="progress">Receives progress, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the run; the destination then holds a partial file.</param>
    /// <returns>What was done.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The document cannot be read or written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfOptimizeReport Optimize(PdfDocument source, Stream destination, PdfOptimizeOptions options, IProgress<PdfOptimizeProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        using var session = new OptimizeSession(source, options, progress, cancellationToken);
        var plan = session.Prepare();
        return session.Finish(plan.WriteTo(destination, cancellationToken), plan);
    }

    /// <summary>Optimises a document into a stream asynchronously, writing to the destination with asynchronous I/O.</summary>
    /// <param name="source">The document.</param>
    /// <param name="destination">The writable stream that receives the new file.</param>
    /// <param name="options">What may change.</param>
    /// <param name="progress">Receives progress, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the run; the destination then holds a partial file.</param>
    /// <returns>What was done.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The document cannot be read or written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<PdfOptimizeReport> OptimizeAsync(
        PdfDocument source,
        Stream destination,
        PdfOptimizeOptions options,
        IProgress<PdfOptimizeProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        return await RunAsync(new(source, options, progress, cancellationToken), destination, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Optimises a private working copy in place, so no second copy is made; the copy is left changed.</summary>
    /// <param name="snapshot">A private copy from <c>OpenWorkingCopy</c> that nothing else uses.</param>
    /// <param name="destination">The writable stream that receives the new file.</param>
    /// <param name="options">What may change.</param>
    /// <param name="progress">Receives progress, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the run; the destination then holds a partial file.</param>
    /// <returns>What was done.</returns>
    internal static async Task<PdfOptimizeReport> OptimizeSnapshotAsync(
        PdfDocument snapshot,
        Stream destination,
        PdfOptimizeOptions options,
        IProgress<PdfOptimizeProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        return await RunAsync(new(snapshot, options, progress, true, cancellationToken), destination, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Plans and writes one session.</summary>
    /// <param name="session">The session.</param>
    /// <param name="destination">The stream.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What was done.</returns>
    private static async Task<PdfOptimizeReport> RunAsync(OptimizeSession session, Stream destination, CancellationToken cancellationToken)
    {
        using var owned = session;
        var plan = session.Prepare();
        var written = await plan.WriteToAsync(destination, cancellationToken).ConfigureAwait(false);
        return session.Finish(written, plan);
    }
}
