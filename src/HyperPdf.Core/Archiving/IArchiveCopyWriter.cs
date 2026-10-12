// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Archiving;

/// <summary>Writes a PDF/A copy of a document for long-term keeping. Not every engine can; ask with a type check.</summary>
public interface IArchiveCopyWriter : IDocumentFeature
{
    /// <summary>
    /// Writes a copy that meets a PDF/A level, or explains why it cannot. The open document is never changed, and
    /// nothing is written to <paramref name="destination"/> when the copy cannot be made.
    /// </summary>
    /// <param name="destination">The writable stream that receives the copy.</param>
    /// <param name="level">The level the copy must meet.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What was changed, or why the copy could not be made.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    Task<ArchiveCopyResult> WriteArchiveCopyAsync(Stream destination, ArchiveLevel level, CancellationToken cancellationToken);
}
