// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Writes a new PDF 2.0 file with a page for each picture.</summary>
public interface IPictureDocumentWriter
{
    /// <summary>Writes the pictures as a new PDF, one page each, fitted on A4.</summary>
    /// <param name="images">The pictures, at least one.</param>
    /// <param name="destination">The stream receiving the PDF.</param>
    /// <param name="cancellationToken">Cancels building the pages and writing.</param>
    /// <returns>A task completing after the file is written.</returns>
    /// <exception cref="ArgumentException">There are no pictures, or the pixels do not fill a picture.</exception>
    ValueTask WritePicturesAsync(PageImageSource[] images, Stream destination, CancellationToken cancellationToken);
}
