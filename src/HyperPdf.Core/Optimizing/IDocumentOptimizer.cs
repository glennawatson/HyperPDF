// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Optimizing;

/// <summary>Writes a smaller or more accessible copy of a document. Not every engine can; ask with a type check.</summary>
public interface IDocumentOptimizer : IDocumentFeature
{
    /// <summary>Writes an optimised copy. The open document is never changed.</summary>
    /// <param name="destination">The writable stream that receives the new file; it holds a partial file after a failure or cancel.</param>
    /// <param name="settings">What may change.</param>
    /// <param name="progress">Receives progress, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What was done.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    Task<OptimizeReport> OptimizeAsync(Stream destination, OptimizeSettings settings, IProgress<OptimizeProgress>? progress, CancellationToken cancellationToken);
}
