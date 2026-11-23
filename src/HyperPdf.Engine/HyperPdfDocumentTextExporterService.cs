// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Export;

namespace HyperPdf.Engine;

/// <summary>Writes the text of every page through the library's text export.</summary>
/// <param name="owner">The owning document.</param>
internal sealed class HyperPdfDocumentTextExporterService(HyperPdfDocument owner) : IDocumentTextExporter
{
    /// <inheritdoc/>
    public IDocument Document => owner;

    /// <inheritdoc/>
    public Task WriteAsync(TextWriter output, DocumentTextFormat format, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        ObjectDisposedException.ThrowIf(owner.IsDisposed, owner);
        return format switch
        {
            DocumentTextFormat.Html => PdfTextExport.WriteHtmlAsync(owner.Document, output, cancellationToken),
            DocumentTextFormat.Markdown => PdfTextExport.WriteMarkdownAsync(owner.Document, output, cancellationToken),
            _ => WritePlainAsync(owner.Document, output, cancellationToken),
        };
    }

    /// <summary>Writes each page's text with a blank line between pages.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="output">Receives the text.</param>
    /// <param name="cancellationToken">Stops the work between pages.</param>
    /// <returns>A task that completes once every page is written.</returns>
    private static async Task WritePlainAsync(PdfDocument document, TextWriter output, CancellationToken cancellationToken)
    {
        for (var index = 0; index < document.PageCount; index++)
        {
            var page = await PdfDocumentText.GetTextPageAsync(document, index, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(page.Text.AsMemory(), cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(ReadOnlyMemory<char>.Empty, cancellationToken).ConfigureAwait(false);
        }
    }
}
