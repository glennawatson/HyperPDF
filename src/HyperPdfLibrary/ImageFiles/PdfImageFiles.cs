// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>
/// Reads picture files into raster images a PDF can hold: PNG, GIF, BMP and TIFF, all in managed code. A JPEG is not
/// decoded here, because a PDF carries its bytes as they are (see <see cref="PdfDocumentCreator.AddImageFile(ReadOnlySpan{byte}, CancellationToken)"/>).
/// </summary>
public static class PdfImageFiles
{
    /// <summary>Gets the signature of a PNG file.</summary>
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47];

    /// <summary>Gets the signature of a JPEG file.</summary>
    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];

    /// <summary>Gets the signature of a little-endian TIFF file.</summary>
    private static ReadOnlySpan<byte> TiffLittleSignature => [0x49, 0x49, 0x2A, 0x00];

    /// <summary>Gets the signature of a big-endian TIFF file.</summary>
    private static ReadOnlySpan<byte> TiffBigSignature => [0x4D, 0x4D, 0x00, 0x2A];

    /// <summary>Works out which picture format some bytes are, from their first few bytes.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <returns>The format, or <see cref="PdfImageFileFormat.Unknown"/>.</returns>
    public static PdfImageFileFormat Detect(ReadOnlySpan<byte> file)
    {
        if (file.StartsWith(PngSignature))
        {
            return PdfImageFileFormat.Png;
        }

        if (file.StartsWith(JpegSignature))
        {
            return PdfImageFileFormat.Jpeg;
        }

        if (file.StartsWith("GIF87a"u8) || file.StartsWith("GIF89a"u8))
        {
            return PdfImageFileFormat.Gif;
        }

        if (file.StartsWith("BM"u8))
        {
            return PdfImageFileFormat.Bmp;
        }

        return file.StartsWith(TiffLittleSignature) || file.StartsWith(TiffBigSignature) ? PdfImageFileFormat.Tiff : PdfImageFileFormat.Unknown;
    }

    /// <summary>Decodes a picture file.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <param name="cancellationToken">Cancels the decode between rows.</param>
    /// <returns>The picture: grey, RGB, BGRA with straight alpha, or one-bit.</returns>
    /// <exception cref="NotSupportedException">The format is not one this method reads, such as JPEG or an unknown format.</exception>
    /// <exception cref="InvalidDataException">The file is damaged, or uses a feature or size this library does not read.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfRasterImage Decode(ReadOnlySpan<byte> file, CancellationToken cancellationToken) => Detect(file) switch
    {
        PdfImageFileFormat.Png => PngFileDecoder.Decode(file, cancellationToken),
        PdfImageFileFormat.Gif => GifFileDecoder.Decode(file, cancellationToken),
        PdfImageFileFormat.Bmp => BmpFileDecoder.Decode(file, cancellationToken),
        PdfImageFileFormat.Tiff => TiffFileDecoder.Decode(file, cancellationToken),
        _ => throw new NotSupportedException("The file is not a PNG, GIF, BMP or TIFF picture."),
    };

    /// <summary>Reads a stream to its end and decodes it as a picture file.</summary>
    /// <param name="stream">The stream, read from its current position.</param>
    /// <param name="cancellationToken">Cancels the read and the decode.</param>
    /// <returns>The picture.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The format is not one this method reads.</exception>
    /// <exception cref="InvalidDataException">The file is damaged, or uses a feature or size this library does not read.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfRasterImage> DecodeAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return Decode(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), cancellationToken);
    }
}
