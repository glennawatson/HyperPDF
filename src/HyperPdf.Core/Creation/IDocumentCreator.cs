// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Creation;

/// <summary>Creates new PDF documents from text, images and pages of other PDFs.</summary>
public interface IDocumentCreator
{
    /// <summary>
    /// Creates a document and writes it to a stream. To add the pages to an open document, write the result to a file and
    /// insert it with <see cref="Documents.IPageManager.InsertAsync(int, string[], CancellationToken)"/>.
    /// </summary>
    /// <param name="request">The content and description of the new document.</param>
    /// <param name="destination">The stream receiving the PDF.</param>
    /// <param name="cancellationToken">Cancels reading source files, laying out pages and writing.</param>
    /// <returns>What the document contains and what it could not promise.</returns>
    /// <exception cref="ArgumentException">The request is not usable, for example a font file that cannot be embedded or an image whose pixels do not fill its size.</exception>
    /// <exception cref="Documents.DocumentOpenException">A source PDF cannot be opened.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    ValueTask<DocumentCreationResult> CreateAsync(DocumentCreationRequest request, Stream destination, CancellationToken cancellationToken);
}
