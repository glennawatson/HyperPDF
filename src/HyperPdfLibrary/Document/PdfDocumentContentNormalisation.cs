// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Document;

/// <summary>Normalises the content of a document's pages without saving. A save can do the same through <see cref="PdfCompactOptions.Normalisation"/>.</summary>
public static class PdfDocumentContentNormalisation
{
    /// <summary>
    /// Joins <c>/Contents</c> arrays into one stream each and moves inline images larger than a threshold out into image
    /// XObjects, as one undoable edit in a transaction (the open one, or its own). Pages draw exactly as before. A page that
    /// needs no change is not touched; an inline image that names a colour space the page resources lack stays inline.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="changes">The changes to make.</param>
    /// <param name="inlineImageThreshold">The image data size in bytes above which an inline image is moved; zero or less means 4096.</param>
    /// <param name="cancellationToken">Cancels the work; nothing is changed then.</param>
    /// <returns>The number of pages changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static int Normalise(PdfDocument document, PdfContentNormalisation changes, int inlineImageThreshold, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);
        return PdfContentNormaliser.Apply(document.Objects, changes, inlineImageThreshold, PdfCompressionLevel.Balanced, cancellationToken);
    }
}
