// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Turns the rows of a bitmap into the raster formats a PDF holds.</summary>
internal static class BmpPixels
{
    /// <summary>The colours of a bilevel palette.</summary>
    private const int TwoColours = 2;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The bytes in an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits in a 32-bit word, to which every row is padded.</summary>
    private const int WordBits = 32;

    /// <summary>The bytes in a 32-bit word.</summary>
    private const int WordBytes = 4;

    /// <summary>The bits in a 16-bit pixel.</summary>
    private const int HalfWordBits = 16;

    /// <summary>The bits in a 24-bit pixel.</summary>
    private const int TrueColourBits = 24;

    /// <summary>The offset of the green sample in a pixel.</summary>
    private const int GreenIndex = 1;

    /// <summary>The offset of the blue sample in a pixel.</summary>
    private const int BlueIndex = 2;

    /// <summary>The offset of the alpha sample in a BGRA pixel.</summary>
    private const int AlphaIndex = 3;

    /// <summary>Makes a picture from a bitmap with a palette.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <param name="header">The header.</param>
    /// <param name="palette">The palette: red, green and blue bytes for each entry.</param>
    /// <param name="decoded">Palette indexes already decoded from run-length coding, or <see langword="null"/> to read them from the file.</param>
    /// <param name="cancellationToken">Cancels between rows.</param>
    /// <returns>The picture.</returns>
    /// <exception cref="InvalidDataException">The pixel data is cut short.</exception>
    internal static PdfRasterImage FromPalette(ReadOnlySpan<byte> file, in BmpHeader header, byte[] palette, byte[]? decoded, CancellationToken cancellationToken)
    {
        var indexes = decoded ?? ReadIndexes(file, header, cancellationToken);
        var entries = palette.Length / RgbBytes;
        var upright = new byte[indexes.Length];
        for (var row = 0; row < header.Height; row++)
        {
            var target = header.TopDown ? row : header.Height - 1 - row;
            indexes.AsSpan(row * header.Width, header.Width).CopyTo(upright.AsSpan(target * header.Width, header.Width));
        }

        if (header.BitsPerPixel == 1 && entries == TwoColours && IsBlackAndWhite(palette))
        {
            return Bilevel(header, upright, palette[0] == 0);
        }

        if (IsGrey(palette))
        {
            var grey = new byte[upright.Length];
            for (var i = 0; i < grey.Length; i++)
            {
                grey[i] = palette[Math.Min((int)upright[i], entries - 1) * RgbBytes];
            }

            return new(PdfRasterFormat.Gray8, header.Width, header.Height, grey);
        }

        var rgb = new byte[upright.Length * RgbBytes];
        for (var i = 0; i < upright.Length; i++)
        {
            Array.Copy(palette, Math.Min((int)upright[i], entries - 1) * RgbBytes, rgb, i * RgbBytes, RgbBytes);
        }

        return new(PdfRasterFormat.Rgb24, header.Width, header.Height, rgb);
    }

    /// <summary>Makes a picture from a bitmap of 16, 24 or 32-bit pixels.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <param name="header">The header.</param>
    /// <param name="cancellationToken">Cancels between rows.</param>
    /// <returns>The picture.</returns>
    /// <exception cref="InvalidDataException">The pixel data is cut short.</exception>
    internal static PdfRasterImage FromMasks(ReadOnlySpan<byte> file, in BmpHeader header, CancellationToken cancellationToken)
    {
        var stride = GetStride(header);
        CheckLength(file, header, stride);
        var hasAlpha = header.BitsPerPixel != TrueColourBits && header.Masks.Alpha != 0;
        var step = hasAlpha ? BgraBytes : RgbBytes;
        var pixels = new byte[header.Width * header.Height * step];
        for (var row = 0; row < header.Height; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = file.Slice(header.DataOffset + (row * stride), stride);
            var target = ((header.TopDown ? row : header.Height - 1 - row) * header.Width) * step;
            for (var x = 0; x < header.Width; x++)
            {
                WritePixel(source, x, header, pixels.AsSpan(target + (x * step), step));
            }
        }

        return new(hasAlpha ? PdfRasterFormat.Bgra32 : PdfRasterFormat.Rgb24, header.Width, header.Height, pixels);
    }

    /// <summary>Reads one pixel and writes it as red, green, blue and, with alpha, alpha.</summary>
    /// <param name="row">The stored row.</param>
    /// <param name="x">The pixel's column.</param>
    /// <param name="header">The header.</param>
    /// <param name="target">Receives three or four samples.</param>
    private static void WritePixel(ReadOnlySpan<byte> row, int x, in BmpHeader header, Span<byte> target)
    {
        if (header.BitsPerPixel == TrueColourBits)
        {
            var at = x * RgbBytes;
            target[0] = row[at + BlueIndex];
            target[GreenIndex] = row[at + GreenIndex];
            target[BlueIndex] = row[at];
            return;
        }

        var value = header.BitsPerPixel == HalfWordBits
            ? BinaryPrimitives.ReadUInt16LittleEndian(row[(x * sizeof(ushort))..])
            : BinaryPrimitives.ReadUInt32LittleEndian(row[(x * WordBytes)..]);
        var masks = header.Masks;
        if (target.Length == BgraBytes)
        {
            target[0] = BmpMasks.Extract(value, masks.Blue);
            target[GreenIndex] = BmpMasks.Extract(value, masks.Green);
            target[BlueIndex] = BmpMasks.Extract(value, masks.Red);
            target[AlphaIndex] = BmpMasks.Extract(value, masks.Alpha);
            return;
        }

        target[0] = BmpMasks.Extract(value, masks.Red);
        target[GreenIndex] = BmpMasks.Extract(value, masks.Green);
        target[BlueIndex] = BmpMasks.Extract(value, masks.Blue);
    }

    /// <summary>Reads the palette index of every pixel from uncompressed rows.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <param name="header">The header.</param>
    /// <param name="cancellationToken">Cancels between rows.</param>
    /// <returns>The indexes, rows in the order they are stored.</returns>
    /// <exception cref="InvalidDataException">The pixel data is cut short.</exception>
    private static byte[] ReadIndexes(ReadOnlySpan<byte> file, in BmpHeader header, CancellationToken cancellationToken)
    {
        var stride = GetStride(header);
        CheckLength(file, header, stride);
        var indexes = new byte[header.Width * header.Height];
        var mask = (1 << header.BitsPerPixel) - 1;
        for (var row = 0; row < header.Height; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = file.Slice(header.DataOffset + (row * stride), stride);
            for (var x = 0; x < header.Width; x++)
            {
                var bit = x * header.BitsPerPixel;
                indexes[(row * header.Width) + x] = (byte)((source[bit / ByteBits] >> (ByteBits - header.BitsPerPixel - (bit % ByteBits))) & mask);
            }
        }

        return indexes;
    }

    /// <summary>Gets the bytes in a stored row, which is padded to a multiple of four.</summary>
    /// <param name="header">The header.</param>
    /// <returns>The bytes.</returns>
    private static int GetStride(in BmpHeader header) => (int)((((long)header.Width * header.BitsPerPixel) + WordBits - 1) / WordBits) * WordBytes;

    /// <summary>Checks that the file holds every stored row.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <param name="header">The header.</param>
    /// <param name="stride">The bytes in a stored row.</param>
    /// <exception cref="InvalidDataException">The pixel data is cut short.</exception>
    private static void CheckLength(ReadOnlySpan<byte> file, in BmpHeader header, int stride)
    {
        if (header.DataOffset < 0 || (long)header.DataOffset + ((long)stride * header.Height) > file.Length)
        {
            throw new InvalidDataException("The bitmap's pixel data is cut short.");
        }
    }

    /// <summary>Determines whether a two-colour palette is black and white, in either order.</summary>
    /// <param name="palette">The palette.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsBlackAndWhite(byte[] palette) =>
        IsGrey(palette) && ((palette[0] == 0 && palette[RgbBytes] == byte.MaxValue) || (palette[0] == byte.MaxValue && palette[RgbBytes] == 0));

    /// <summary>Determines whether every colour of a palette is a grey.</summary>
    /// <param name="palette">The palette.</param>
    /// <returns><see langword="true"/> when red, green and blue agree in every entry.</returns>
    private static bool IsGrey(byte[] palette)
    {
        for (var i = 0; i < palette.Length; i += RgbBytes)
        {
            if (palette[i] != palette[i + GreenIndex] || palette[i] != palette[i + BlueIndex])
            {
                return false;
            }
        }

        return palette.Length > 0;
    }

    /// <summary>Packs one-bit indexes into a one-bit picture.</summary>
    /// <param name="header">The header.</param>
    /// <param name="indexes">The indexes, 0 or 1, top row first.</param>
    /// <param name="zeroIsBlack">Whether index 0 is black; otherwise index 0 is white.</param>
    /// <returns>The picture, where a set bit is white.</returns>
    private static PdfRasterImage Bilevel(in BmpHeader header, byte[] indexes, bool zeroIsBlack)
    {
        var rowBytes = (header.Width + ByteBits - 1) / ByteBits;
        var packed = new byte[rowBytes * header.Height];
        for (var y = 0; y < header.Height; y++)
        {
            for (var x = 0; x < header.Width; x++)
            {
                var white = (indexes[(y * header.Width) + x] == 1) == zeroIsBlack;
                if (white)
                {
                    packed[(y * rowBytes) + (x / ByteBits)] |= (byte)(0x80 >> (x % ByteBits));
                }
            }
        }

        return new(PdfRasterFormat.Bilevel1, header.Width, header.Height, packed);
    }
}
