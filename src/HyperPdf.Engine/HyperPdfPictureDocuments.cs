// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine;

/// <summary>Makes PDF 2.0 bytes with a page for each picture, fitted on A4.</summary>
internal static class HyperPdfPictureDocuments
{
    /// <summary>The short side of A4 paper in points.</summary>
    private const float A4Short = 595.28F;

    /// <summary>The long side of A4 paper in points.</summary>
    private const float A4Long = 841.89F;

    /// <summary>The blank border kept around a picture on its page, in points.</summary>
    private const float Margin = 36F;

    /// <summary>Makes the PDF bytes.</summary>
    /// <param name="images">The pictures, at least one.</param>
    /// <param name="cancellationToken">Cancels building the pages.</param>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] Create(PageImageSource[] images, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(images);

        // Paper is turned to match a landscape first picture, so a photo does not make a page metres wide.
        var landscape = images.Length > 0 && images[0].Width > images[0].Height;
        var options = new PdfImagePageOptions { PaperWidth = landscape ? A4Long : A4Short, PaperHeight = landscape ? A4Short : A4Long, Margin = Margin };
        var sources = new PdfPageImage[images.Length];
        for (var i = 0; i < images.Length; i++)
        {
            sources[i] = new(images[i].Pixels, images[i].Width, images[i].Height);
        }

        return PdfImagePages.CreateDocument(sources, options, cancellationToken);
    }
}
