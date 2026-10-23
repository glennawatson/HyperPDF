// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Exporting;

/// <summary>Writes pages of the open document as picture files or vector drawings, without changing the document.</summary>
public interface IPageFileExporter : IDocumentFeature
{
    /// <summary>Writes pages as one file.</summary>
    /// <param name="pages">The zero based pages to write, in order. Only PostScript holds more than one.</param>
    /// <param name="format">The file format.</param>
    /// <param name="options">How to write them.</param>
    /// <param name="destination">The stream that receives the file.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>What the export did; nothing is written to <paramref name="destination"/> when <see cref="PageFileExportResult.Written"/> is <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are no pages, or the format takes one page and more were given.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A page is not in the document, or the resolution is not positive.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    ValueTask<PageFileExportResult> ExportAsync(IReadOnlyList<int> pages, PageFileFormat format, PageFileExportOptions options, Stream destination, CancellationToken cancellationToken);
}
