// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Flattening;

/// <summary>Writes a flattened copy of the open document: form fields and annotations drawn into the page content.</summary>
public interface IFlattenCopyWriter : IDocumentFeature
{
    /// <summary>
    /// Writes a copy in which the chosen form fields and annotations are part of the page, or explains why it cannot. The
    /// open document is never changed, and nothing is written to <paramref name="destination"/> when the copy cannot be made.
    /// A signed document is refused, because flattening would break its signature.
    /// </summary>
    /// <param name="destination">The writable stream that receives the copy.</param>
    /// <param name="options">What to flatten and which look to keep.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What was flattened and what was left alone, or why the copy could not be made.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    Task<FlattenCopyResult> WriteFlattenedCopyAsync(Stream destination, FlattenCopyOptions options, CancellationToken cancellationToken);
}
