// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.ImageFiles;

namespace HyperPdfLibrary.Export;

/// <summary>An image as tightly packed 8-bit samples: grey, red-green-blue, or red-green-blue-alpha with straight alpha.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Channels">The samples per pixel: 1, 3 or 4.</param>
/// <param name="Data">The samples, rows top to bottom with no padding.</param>
[DebuggerDisplay("RasterSamples: {Width}x{Height} x{Channels}")]
internal sealed record RasterSamples(int Width, int Height, int Channels, byte[] Data)
{
    /// <summary>The samples of a grey pixel.</summary>
    internal const int GreyChannels = 1;

    /// <summary>The samples of a colour pixel with no alpha.</summary>
    internal const int RgbChannels = 3;

    /// <summary>The samples of a colour pixel with alpha.</summary>
    internal const int RgbaChannels = 4;

    /// <summary>The largest 8-bit sample.</summary>
    internal const byte Full = 255;

    /// <summary>The divisor that halves a number, to round a division to the nearest.</summary>
    private const int Halve = 2;

    /// <summary>The pixels of a bilevel byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits of a bilevel byte, counted from the most significant.</summary>
    private const int TopBit = 7;

    /// <summary>The offset of the blue sample in a BGRA pixel.</summary>
    private const int BlueOffset = 0;

    /// <summary>The offset of the green sample in a BGRA pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The offset of the red sample in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of the alpha sample in a BGRA pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The offset of the green sample in an RGB pixel.</summary>
    private const int RgbGreen = 1;

    /// <summary>The offset of the blue sample in an RGB pixel.</summary>
    private const int RgbBlue = 2;

    /// <summary>Gets a value indicating whether the image is grey.</summary>
    internal bool IsGrey => Channels == GreyChannels;

    /// <summary>Gets a value indicating whether the image has an alpha channel.</summary>
    internal bool HasAlpha => Channels == RgbaChannels;

    /// <summary>Gets the bytes in one row.</summary>
    internal int RowBytes => Width * Channels;

    /// <summary>Unpacks a raster image into samples.</summary>
    /// <param name="image">The image, which must be valid.</param>
    /// <returns>The samples.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The pixel format is not known.</exception>
    internal static RasterSamples From(PdfRasterImage image) => image.Format switch
    {
        PdfRasterFormat.Gray8 => new(image.Width, image.Height, GreyChannels, Share(image, GreyChannels)),
        PdfRasterFormat.Rgb24 => new(image.Width, image.Height, RgbChannels, Share(image, RgbChannels)),
        PdfRasterFormat.Bgra32 => new(image.Width, image.Height, RgbaChannels, FromBgra(image)),
        PdfRasterFormat.Bilevel1 => new(image.Width, image.Height, GreyChannels, FromBilevel(image)),
        _ => throw new ArgumentOutOfRangeException(nameof(image), image.Format, "The pixel format is not known."),
    };

    /// <summary>Makes a straight-alpha raster image from premultiplied BGRA pixels, as the renderer writes them.</summary>
    /// <param name="premultiplied">The premultiplied pixels, rows packed with no padding.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The image.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the image.</exception>
    internal static PdfRasterImage ToStraight(ReadOnlySpan<byte> premultiplied, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var length = checked(width * height * RgbaChannels);
        if (premultiplied.Length < length)
        {
            throw new ArgumentException("The pixels do not fill the image.", nameof(premultiplied));
        }

        var straight = new byte[length];
        for (var i = 0; i < length; i += RgbaChannels)
        {
            var alpha = premultiplied[i + AlphaOffset];

            // Rendered pages are mostly opaque, where premultiplied and straight pixels are the same four bytes; a clear
            // pixel stays the zeros the array started with.
            if (alpha == Full)
            {
                MemoryMarshal.Write(straight.AsSpan(i), MemoryMarshal.Read<uint>(premultiplied[i..]));
                continue;
            }

            if (alpha == 0)
            {
                continue;
            }

            straight[i + BlueOffset] = Divide(premultiplied[i + BlueOffset], alpha);
            straight[i + GreenOffset] = Divide(premultiplied[i + GreenOffset], alpha);
            straight[i + RedOffset] = Divide(premultiplied[i + RedOffset], alpha);
            straight[i + AlphaOffset] = alpha;
        }

        return new(PdfRasterFormat.Bgra32, width, height, straight);
    }

    /// <summary>Gets the image with transparency placed over white, as a grey or red-green-blue image.</summary>
    /// <returns>The same image when it has no alpha channel; otherwise a copy with three samples a pixel.</returns>
    internal RasterSamples OverWhite()
    {
        if (!HasAlpha)
        {
            return this;
        }

        var flat = new byte[Width * Height * RgbChannels];
        for (int source = 0, target = 0; source < Data.Length; source += RgbaChannels, target += RgbChannels)
        {
            var alpha = Data[source + AlphaOffset];
            if (alpha == Full)
            {
                flat[target] = Data[source];
                flat[target + RgbGreen] = Data[source + RgbGreen];
                flat[target + RgbBlue] = Data[source + RgbBlue];
                continue;
            }

            flat[target] = Blend(Data[source], alpha);
            flat[target + RgbGreen] = Blend(Data[source + RgbGreen], alpha);
            flat[target + RgbBlue] = Blend(Data[source + RgbBlue], alpha);
        }

        return new(Width, Height, RgbChannels, flat);
    }

    /// <summary>
    /// Gets the pixels of a grey or red-green-blue image for the writers, which only read them: the image's own array when it
    /// holds exactly the pixels, so a large page is not copied; otherwise a copy.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="channels">The samples a pixel.</param>
    /// <returns>The samples, which the caller must not change.</returns>
    private static byte[] Share(PdfRasterImage image, int channels)
    {
        var length = image.Width * image.Height * channels;
        return MemoryMarshal.TryGetArray(image.Pixels, out var segment) && segment.Offset == 0 && segment.Array!.Length == length
            ? segment.Array
            : Copy(image, channels);
    }

    /// <summary>Copies the pixels of a grey or red-green-blue image.</summary>
    /// <param name="image">The image.</param>
    /// <param name="channels">The samples a pixel.</param>
    /// <returns>The samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] Copy(PdfRasterImage image, int channels) => image.Pixels.Span[..(image.Width * image.Height * channels)].ToArray();

    /// <summary>Reorders blue-green-red-alpha pixels to red-green-blue-alpha.</summary>
    /// <param name="image">The image.</param>
    /// <returns>The samples.</returns>
    private static byte[] FromBgra(PdfRasterImage image)
    {
        var data = Copy(image, RgbaChannels);

        // Swapping red and blue is the same in both directions, and the image reader already does it a vector at a time.
        PngEightBit.SwapRedAndBlue(data);
        return data;
    }

    /// <summary>Expands one-bit pixels, where a set bit is white, to grey samples.</summary>
    /// <param name="image">The image.</param>
    /// <returns>The samples.</returns>
    private static byte[] FromBilevel(PdfRasterImage image)
    {
        var data = new byte[image.Width * image.Height];
        var rowBytes = image.RowBytes;
        var pixels = image.Pixels.Span;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var set = (pixels[(y * rowBytes) + (x / ByteBits)] >> (TopBit - (x % ByteBits))) & 1;
                data[(y * image.Width) + x] = set == 0 ? (byte)0 : Full;
            }
        }

        return data;
    }

    /// <summary>Divides a premultiplied sample by its alpha.</summary>
    /// <param name="value">The premultiplied sample.</param>
    /// <param name="alpha">The alpha.</param>
    /// <returns>The straight sample.</returns>
    private static byte Divide(byte value, byte alpha) => alpha switch
    {
        0 => 0,
        Full => value,
        _ => (byte)Math.Min(Full, ((value * Full) + (alpha / Halve)) / alpha),
    };

    /// <summary>Places a sample over white.</summary>
    /// <param name="value">The straight sample.</param>
    /// <param name="alpha">The alpha.</param>
    /// <returns>The blended sample.</returns>
    private static byte Blend(byte value, byte alpha) => (byte)(((value * alpha) + (Full * (Full - alpha)) + (Full / Halve)) / Full);
}
