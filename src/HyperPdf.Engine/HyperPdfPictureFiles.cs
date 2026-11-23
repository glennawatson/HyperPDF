// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpeg;
using HyperPdfLibrary.ImageFiles;

namespace HyperPdf.Engine;

/// <summary>
/// Reads a picture file into pixels, for a picture of a signature or any other picture the user brings in. PNG, GIF, BMP
/// and TIFF files are read by the library's picture file decoders, and JPEG files by its JPEG decoder.
/// </summary>
public static class HyperPdfPictureFiles
{
    /// <summary>The largest picture file read: 64 MiB, far more than a scan of a page needs.</summary>
    private const long MaxFileBytes = 64L * 1024 * 1024;

    /// <summary>The most pixels a picture may have: 64 million, a 8000 by 8000 scan.</summary>
    private const long MaxPixels = 64L * 1000 * 1000;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The components of an RGB sample.</summary>
    private const int RgbComponents = 3;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The top bit of a byte, the first pixel of a one-bit row.</summary>
    private const int TopBit = 0x80;

    /// <summary>Reads a picture file.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The picture, or <see langword="null"/> when the file is missing, too large, damaged or not a picture this reads.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static HyperPdfPicture? Read(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        try
        {
            var file = new FileInfo(path);
            return file.Exists && file.Length <= MaxFileBytes ? Decode(File.ReadAllBytes(path), cancellationToken) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or InvalidDataException or ArgumentException)
        {
            // A file that cannot be opened or decoded is not a picture the user can use; the caller says so.
            return null;
        }
    }

    /// <summary>Decodes the bytes of a picture file.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    /// <returns>The picture, or <see langword="null"/> when the bytes are not a picture this reads or the picture is too large.</returns>
    /// <exception cref="InvalidDataException">The file is damaged, or uses a feature the decoders do not read.</exception>
    /// <exception cref="NotSupportedException">The format is not one the decoders read.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static HyperPdfPicture? Decode(ReadOnlySpan<byte> file, CancellationToken cancellationToken)
    {
        if (PdfImageFiles.Detect(file) == PdfImageFileFormat.Jpeg)
        {
            return DecodeJpeg(file, cancellationToken);
        }

        var image = PdfImageFiles.Decode(file, cancellationToken);
        if ((long)image.Width * image.Height > MaxPixels)
        {
            return null;
        }

        var pixels = new byte[image.Width * image.Height * BgraBytes];
        var source = image.Pixels.Span;
        for (var y = 0; y < image.Height; y++)
        {
            var row = source.Slice(y * image.RowBytes, image.RowBytes);
            var target = pixels.AsSpan(y * image.Width * BgraBytes, image.Width * BgraBytes);
            WriteRow(image.Format, row, target);
        }

        return new(pixels, image.Width, image.Height);
    }

    /// <summary>Decodes a grey or colour JPEG file. A CMYK file, as a press makes, is not read.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    /// <returns>The picture, or <see langword="null"/> when it cannot be read.</returns>
    private static HyperPdfPicture? DecodeJpeg(ReadOnlySpan<byte> file, CancellationToken cancellationToken)
    {
        if (!JpegMarkers.TryReadInfo(file, out var info)
            || !info.IsSupported
            || info.Components is not (1 or RgbComponents)
            || (long)info.Width * info.Height > MaxPixels)
        {
            return null;
        }

        var samples = new byte[info.Width * info.Height * info.Components];
        if (!JpegDecoder.TryDecode(file, info.UsesColorTransform(JpegInfo.NoColorTransform), samples, cancellationToken))
        {
            return null;
        }

        var pixels = new byte[info.Width * info.Height * BgraBytes];
        var format = info.Components == 1 ? PdfRasterFormat.Gray8 : PdfRasterFormat.Rgb24;
        var rowBytes = info.Width * info.Components;
        for (var y = 0; y < info.Height; y++)
        {
            WriteRow(format, samples.AsSpan(y * rowBytes, rowBytes), pixels.AsSpan(y * info.Width * BgraBytes, info.Width * BgraBytes));
        }

        return new(pixels, info.Width, info.Height);
    }

    /// <summary>Writes one row of a decoded picture as BGRA pixels with straight alpha.</summary>
    /// <param name="format">How the row's samples are laid out.</param>
    /// <param name="row">The row's samples.</param>
    /// <param name="target">Receives the row's pixels.</param>
    /// <exception cref="NotSupportedException">The format is not one a picture file decodes to.</exception>
    private static void WriteRow(PdfRasterFormat format, ReadOnlySpan<byte> row, Span<byte> target)
    {
        switch (format)
        {
            case PdfRasterFormat.Bgra32:
                {
                    row[..target.Length].CopyTo(target);
                    break;
                }

            case PdfRasterFormat.Rgb24:
                {
                    for (var x = 0; x < target.Length / BgraBytes; x++)
                    {
                        var sample = row.Slice(x * RgbComponents, RgbComponents);
                        WriteOpaque(target.Slice(x * BgraBytes, BgraBytes), sample[2], sample[1], sample[0]);
                    }

                    break;
                }

            case PdfRasterFormat.Gray8:
                {
                    for (var x = 0; x < target.Length / BgraBytes; x++)
                    {
                        WriteOpaque(target.Slice(x * BgraBytes, BgraBytes), row[x], row[x], row[x]);
                    }

                    break;
                }

            case PdfRasterFormat.Bilevel1:
                {
                    for (var x = 0; x < target.Length / BgraBytes; x++)
                    {
                        // A set bit is white.
                        var value = (row[x / ByteBits] & (TopBit >> (x % ByteBits))) != 0 ? byte.MaxValue : byte.MinValue;
                        WriteOpaque(target.Slice(x * BgraBytes, BgraBytes), value, value, value);
                    }

                    break;
                }

            default:
                {
                    throw new NotSupportedException("The picture's pixel format is not known.");
                }
        }
    }

    /// <summary>Writes one opaque BGRA pixel.</summary>
    /// <param name="pixel">The four bytes of the pixel.</param>
    /// <param name="blue">The blue sample.</param>
    /// <param name="green">The green sample.</param>
    /// <param name="red">The red sample.</param>
    private static void WriteOpaque(Span<byte> pixel, byte blue, byte green, byte red)
    {
        pixel[0] = blue;
        pixel[1] = green;
        pixel[2] = red;
        pixel[BgraBytes - 1] = byte.MaxValue;
    }
}
