// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Forms;

/// <summary>Loads pages with asynchronous I/O before a synchronous form edit reads them.</summary>
internal static class FormFieldPrefetch
{
    /// <summary>Loads every page of the document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>A task completing when the pages are loaded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    internal static async ValueTask LoadAllPagesAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        for (var page = 0; page < document.PageCount; page++)
        {
            _ = await PdfDocumentPages.GetPageAsync(document, page, cancellationToken).ConfigureAwait(false);
        }
    }
}
