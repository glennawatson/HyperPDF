// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>Wraps a picture file in a one page PDF, so a viewer can open a picture the way it opens any document.</summary>
public static class PdfImageDocument
{
    /// <summary>The points in a CSS pixel: a picture of unknown resolution is shown at 96 dots to the inch.</summary>
    private const float PointsPerPixel = 0.75F;

    /// <summary>The largest page side PDF readers are guaranteed to handle, in points.</summary>
    private const float MaximumSide = 14400F;

    /// <summary>Makes a PDF with one page that shows a picture file edge to edge.</summary>
    /// <param name="file">The picture file's bytes: JPEG, PNG, GIF, BMP or TIFF.</param>
    /// <param name="title">The document title, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the decode and the encoding between their steps.</param>
    /// <returns>The PDF file.</returns>
    /// <exception cref="NotSupportedException">The bytes are not a JPEG, PNG, GIF, BMP or TIFF picture.</exception>
    /// <exception cref="InvalidDataException">The picture is damaged, or uses a feature or size this library does not read.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static byte[] FromImageFile(ReadOnlySpan<byte> file, string? title, CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(new() { Title = title });
        var image = creator.AddImageFile(file, cancellationToken);
        var scale = Math.Min(PointsPerPixel, MaximumSide / Math.Max(image.Width, image.Height));
        var width = image.Width * scale;
        var height = image.Height * scale;
        var canvas = creator.BeginPage(width, height);
        try
        {
            canvas.DrawImage(image, 0, 0, width, height);
            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        return creator.ToArray();
    }
}
