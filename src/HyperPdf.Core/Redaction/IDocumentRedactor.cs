// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Redaction;

/// <summary>
/// Removes marked content for good. Marks are made with the annotation editor (<c>AnnotationKind.Redaction</c>); this
/// writes a new file in which what lies under every mark is gone. Not every engine can; ask with a type check.
/// </summary>
public interface IDocumentRedactor : IDocumentFeature
{
    /// <summary>Applies every redaction mark and writes a new file. The open document is never changed, and neither is its file.</summary>
    /// <param name="destination">The writable stream that receives the new file; it holds a partial file after a failure or cancel.</param>
    /// <param name="settings">What to remove under the marks, and what to clean up.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What was removed.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="InvalidDataException">The document cannot be read or written.</exception>
    Task<RedactionReport> ApplyAsync(Stream destination, RedactionSettings settings, CancellationToken cancellationToken);
}
