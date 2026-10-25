// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Export;

/// <summary>
/// Writes a raster image, such as a rendered page, as a picture file. All of the encoders are managed code and need no
/// image library from the host. Formats with no alpha channel place transparent pixels over white.
/// </summary>
public static class PdfRasterFiles
{
    /// <summary>Gets the file name extension, with no dot, that goes with a format.</summary>
    /// <param name="format">The format.</param>
    /// <returns>The extension.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The format is not known.</exception>
    public static string GetExtension(PdfRasterFileFormat format) => format switch
    {
        PdfRasterFileFormat.Png => "png",
        PdfRasterFileFormat.Jpeg => "jpg",
        PdfRasterFileFormat.Tiff => "tif",
        PdfRasterFileFormat.Bmp => "bmp",
        PdfRasterFileFormat.Pnm => "pnm",
        PdfRasterFileFormat.Pam => "pam",
        PdfRasterFileFormat.Tga => "tga",
        PdfRasterFileFormat.Psd => "psd",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "The file format is not known."),
    };

    /// <summary>Makes a straight-alpha image from the premultiplied BGRA pixels the page renderer writes.</summary>
    /// <param name="premultiplied">The pixels, rows packed with no padding.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The image, to pass to <see cref="Encode(PdfRasterImage, PdfRasterFileFormat, CancellationToken)"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the image.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfRasterImage FromPremultipliedBgra(ReadOnlySpan<byte> premultiplied, int width, int height) =>
        RasterSamples.ToStraight(premultiplied, width, height);

    /// <summary>Encodes an image as a picture file.</summary>
    /// <param name="image">The image: grey, RGB, BGRA with straight alpha, or one-bit.</param>
    /// <param name="format">The file format.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>The file's bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive, the format is not known, or the image is too large for the format.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the image.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static byte[] Encode(PdfRasterImage image, PdfRasterFileFormat format, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        image.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        var samples = RasterSamples.From(image);
        return format switch
        {
            PdfRasterFileFormat.Png => PdfPngWriter.Encode(samples),
            PdfRasterFileFormat.Jpeg => PdfJpegEncoder.Encode(ToJpegInput(samples), PdfJpegOptions.Default, cancellationToken),
            PdfRasterFileFormat.Tiff => TiffRasterWriter.Write(samples),
            PdfRasterFileFormat.Bmp => BmpRasterWriter.Write(samples),
            PdfRasterFileFormat.Pnm => NetpbmRasterWriter.WritePnm(samples),
            PdfRasterFileFormat.Pam => NetpbmRasterWriter.WritePam(samples),
            PdfRasterFileFormat.Tga => TgaRasterWriter.Write(samples),
            PdfRasterFileFormat.Psd => PsdRasterWriter.Write(samples),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "The file format is not known."),
        };
    }

    /// <summary>Encodes an image as a picture file and writes it to a stream.</summary>
    /// <param name="image">The image.</param>
    /// <param name="format">The file format.</param>
    /// <param name="destination">The stream to write the file to.</param>
    /// <param name="cancellationToken">Cancels the encode and the write.</param>
    /// <returns>A task that completes when the file is written.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive, the format is not known, or the image is too large for the format.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the image.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask EncodeAsync(PdfRasterImage image, PdfRasterFileFormat format, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);

        // The encode runs inline, then the bytes are written with real asynchronous I/O.
        var file = Encode(image, format, cancellationToken);
        await destination.WriteAsync(file, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Chooses the image the JPEG encoder takes: grey or red-green-blue, with any transparency placed over white.</summary>
    /// <param name="samples">The samples.</param>
    /// <returns>The image.</returns>
    private static PdfRasterImage ToJpegInput(RasterSamples samples)
    {
        var flat = samples.OverWhite();
        return new(flat.IsGrey ? PdfRasterFormat.Gray8 : PdfRasterFormat.Rgb24, flat.Width, flat.Height, flat.Data);
    }
}
