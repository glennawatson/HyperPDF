// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Async;

/// <summary>A document and the stream it reads, disposed together.</summary>
/// <param name="Document">The document.</param>
/// <param name="Stream">The stream the document reads, or <see langword="null"/>.</param>
internal sealed record OpenedAsync(PdfDocument Document, Stream? Stream) : IDisposable
{
    /// <summary>Disposes the document, then its stream.</summary>
    public void Dispose()
    {
        Document.Dispose();
        Stream?.Dispose();
    }
}
