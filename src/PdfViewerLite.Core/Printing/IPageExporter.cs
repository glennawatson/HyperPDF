// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.Core.Printing;

/// <summary>Writes chosen pages, as they look now with annotations and filled fields, to a new PDF. Safe from any thread.</summary>
public interface IPageExporter : IDocumentFeature
{
    /// <summary>Writes a new PDF holding the given pages, in order, laid onto sheets.</summary>
    /// <param name="pages">Zero based page indices.</param>
    /// <param name="layout">Pages per sheet, paper and whether annotations are included.</param>
    /// <param name="destination">The stream receiving the PDF.</param>
    /// <returns><see langword="true"/> when written.</returns>
    bool ExportPages(ReadOnlySpan<int> pages, in SheetLayout layout, Stream destination);
}
