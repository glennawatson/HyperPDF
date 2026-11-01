// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Archiving;

/// <summary>Writes a PDF 2.0 copy of the open document.</summary>
public interface IPdf20CopyWriter : IDocumentFeature
{
    /// <summary>
    /// Writes a copy with a PDF 2.0 header that carries no feature PDF 2.0 deprecates, or explains why it cannot. The open
    /// document is never changed, and nothing is written to <paramref name="destination"/> when the copy cannot be made.
    /// </summary>
    /// <param name="destination">The writable stream that receives the copy.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What was changed, or why the copy could not be made.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    Task<Pdf20CopyResult> WritePdf20CopyAsync(Stream destination, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a copy as <see cref="WritePdf20CopyAsync(Stream, CancellationToken)"/> does, dropping the signatures and the
    /// protection when the options say so. A writer that cannot drop them refuses a signed or protected document.
    /// </summary>
    /// <param name="destination">The writable stream that receives the copy.</param>
    /// <param name="options">What the copy may drop.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What was changed, or why the copy could not be made.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    Task<Pdf20CopyResult> WritePdf20CopyAsync(Stream destination, Pdf20CopyOptions options, CancellationToken cancellationToken) =>
        WritePdf20CopyAsync(destination, cancellationToken);
}
