// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Text;

/// <summary>Writes the text of every page to a file: plain text, Markdown or HTML.</summary>
public interface IDocumentTextExporter : IDocumentFeature
{
    /// <summary>Writes the text of every page, in page order.</summary>
    /// <param name="output">Receives the text.</param>
    /// <param name="format">The kind of file.</param>
    /// <param name="cancellationToken">Stops the work between pages.</param>
    /// <returns>A task that completes once every page is written.</returns>
    Task WriteAsync(TextWriter output, DocumentTextFormat format, CancellationToken cancellationToken);
}
