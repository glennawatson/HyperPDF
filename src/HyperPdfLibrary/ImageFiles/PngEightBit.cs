// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>
/// Reads the PNGs that are by far the most common: eight bits a sample, not interlaced, grey, colour or colour with alpha. The
/// rows are unfiltered straight into the picture's own array, so the samples are never widened and copied again.
/// </summary>
internal static class PngEightBit
{
    /// <summary>The bits in a sample this reader takes.</summary>
    private const int SampleBits = 8;

    /// <summary>The bytes of a grey transparency key, past which a grey picture needs an alpha channel.</summary>
    private const int GreyKeyBytes = 2;

    /// <summary>The bytes of a colour transparency key.</summary>
    private const int ColourKeyBytes = 6;

    /// <summary>The red and blue bytes of an RGBA pixel read as a little endian word, cleared.</summary>
    private const uint GreenAlphaMask = 0xFF00FF00;

    /// <summary>The offset of the blue byte in an RGBA pixel.</summary>
    private const int BlueByte = 2;

    /// <summary>The mask of one byte.</summary>
    private const uint ByteMask = 0xFF;

    /// <summary>The shift between the red and blue bytes of a pixel.</summary>
    private const int RedBlueShift = 16;

    /// <summary>Reads a PNG when it is one of the common kinds.</summary>
    /// <param name="parts">The chunks of the file.</param>
    /// <param name="cancellationToken">Cancels the decode between rows.</param>
    /// <returns>The picture, or <see langword="null"/> when the PNG needs the general reader.</returns>
    /// <exception cref="InvalidDataException">The image data is damaged or too short.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static PdfRasterImage? TryDecode(in PngParts parts, CancellationToken cancellationToken)
    {
        if (parts.Header is not { Interlaced: false, Depth: SampleBits } header || GetFormat(header, parts.Transparency) is not { } format)
        {
            return null;
        }

        var inflated = default(PooledBuffer);
        try
        {
            _ = ZLibCodec.Decompress(parts.Data, false, ref inflated, cancellationToken);
            var stride = header.GetStride(header.Width);
            var raw = inflated.WrittenSpan;
            if (raw.Length < (long)header.Height * (stride + 1))
            {
                throw new InvalidDataException("The PNG's image data is shorter than its size needs.");
            }

            var pixels = new byte[stride * header.Height];
            UnfilterRows(raw, pixels, stride, header.BitsPerPixel / SampleBits, cancellationToken);
            if (format == PdfRasterFormat.Bgra32)
            {
                SwapRedAndBlue(pixels);
            }

            return new(format, header.Width, header.Height, pixels);
        }
        finally
        {
            inflated.Dispose();
        }
    }

    /// <summary>Swaps the red and blue bytes of RGBA pixels, which makes them BGRA pixels.</summary>
    /// <param name="pixels">The pixels, four bytes each, changed in place.</param>
    internal static void SwapRedAndBlue(Span<byte> pixels)
    {
        var count = pixels.Length / sizeof(uint);
        if (!BitConverter.IsLittleEndian)
        {
            for (var i = 0; i < count * sizeof(uint); i += sizeof(uint))
            {
                (pixels[i], pixels[i + BlueByte]) = (pixels[i + BlueByte], pixels[i]);
            }

            return;
        }

        var words = MemoryMarshal.Cast<byte, uint>(pixels);
        var done = 0;
        if (Vector.IsHardwareAccelerated)
        {
            var width = Vector<uint>.Count;
            for (; done + width <= words.Length; done += width)
            {
                var vector = new Vector<uint>(words.Slice(done, width));
                var swapped = (vector & new Vector<uint>(GreenAlphaMask)) | ((vector & new Vector<uint>(ByteMask)) << RedBlueShift) | ((vector >> RedBlueShift) & new Vector<uint>(ByteMask));
                swapped.CopyTo(words.Slice(done, width));
            }
        }

        for (; done < words.Length; done++)
        {
            var word = words[done];
            words[done] = (word & GreenAlphaMask) | ((word & ByteMask) << RedBlueShift) | ((word >> RedBlueShift) & ByteMask);
        }
    }

    /// <summary>Gets the raster format of a PNG this reader takes.</summary>
    /// <param name="header">The header.</param>
    /// <param name="transparency">The transparency chunk's data, or empty.</param>
    /// <returns>The format, or <see langword="null"/> for a PNG that needs the general reader.</returns>
    private static PdfRasterFormat? GetFormat(PngHeader header, byte[] transparency) => header.ColorType switch
    {
        PngHeader.Grey when transparency.Length < GreyKeyBytes => PdfRasterFormat.Gray8,
        PngHeader.Colour when transparency.Length < ColourKeyBytes => PdfRasterFormat.Rgb24,
        PngHeader.ColourAlpha => PdfRasterFormat.Bgra32,
        _ => null,
    };

    /// <summary>Copies each row into the picture and undoes its filter there, using the row above as it goes.</summary>
    /// <param name="raw">The filtered rows, each led by its filter type.</param>
    /// <param name="pixels">Receives the plain rows.</param>
    /// <param name="stride">The bytes in a row.</param>
    /// <param name="bytesPerPixel">The bytes in a pixel.</param>
    /// <param name="cancellationToken">Cancels between rows.</param>
    private static void UnfilterRows(ReadOnlySpan<byte> raw, byte[] pixels, int stride, int bytesPerPixel, CancellationToken cancellationToken)
    {
        var zeros = new byte[stride];
        var rows = pixels.Length / stride;
        for (var row = 0; row < rows; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filtered = raw.Slice(row * (stride + 1), stride + 1);
            var line = pixels.AsSpan(row * stride, stride);
            filtered[1..].CopyTo(line);
            PngUnfilter.Apply(filtered[0], line, row == 0 ? zeros : pixels.AsSpan((row - 1) * stride, stride), bytesPerPixel);
        }
    }
}
