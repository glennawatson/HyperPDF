// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>Interleaves component rows and applies the JPEG YCbCr and YCCK conversions with libjpeg's fixed-point constants.</summary>
internal static class JpegColor
{
    /// <summary>The fixed-point bits of the constants.</summary>
    private const int FixedBits = 16;

    /// <summary>Half a unit in fixed point, for rounding.</summary>
    private const int Half = 1 << (FixedBits - 1);

    /// <summary>The offset of chroma samples.</summary>
    private const int ChromaCenter = 128;

    /// <summary>1.40200 in fixed point.</summary>
    private const int RedFromCr = 91_881;

    /// <summary>1.77200 in fixed point.</summary>
    private const int BlueFromCb = 116_130;

    /// <summary>0.71414 in fixed point.</summary>
    private const int GreenFromCr = 46_802;

    /// <summary>0.34414 in fixed point.</summary>
    private const int GreenFromCb = 22_554;

    /// <summary>The row of the red chroma component.</summary>
    private const int CrRow = 2;

    /// <summary>The row of the black component.</summary>
    private const int BlackRow = 3;

    /// <summary>The largest sample.</summary>
    private const int MaxSample = 255;

    /// <summary>The components of a YCbCr or RGB image.</summary>
    private const int ThreeComponents = 3;

    /// <summary>The components of a YCCK or CMYK image.</summary>
    private const int FourComponents = 4;

    /// <summary>Joins one row per component into interleaved pixels.</summary>
    /// <param name="rows">The component rows, <paramref name="width"/> bytes each, one after the other.</param>
    /// <param name="components">The number of components.</param>
    /// <param name="width">The pixels in the row.</param>
    /// <param name="convert">Whether to convert YCbCr to RGB (three components) or YCCK to CMYK (four).</param>
    /// <param name="destination">Receives width * components bytes.</param>
    internal static void Interleave(ReadOnlySpan<byte> rows, int components, int width, bool convert, Span<byte> destination)
    {
        if (components == 1)
        {
            rows[..width].CopyTo(destination);
        }
        else if (components == ThreeComponents && convert)
        {
            YccToRgb(rows, width, destination);
        }
        else if (components == FourComponents && convert)
        {
            YcckToCmyk(rows, width, destination);
        }
        else
        {
            Join(rows, components, width, destination);
        }
    }

    /// <summary>Joins component rows without conversion.</summary>
    /// <param name="rows">The component rows.</param>
    /// <param name="components">The number of components.</param>
    /// <param name="width">The pixels in the row.</param>
    /// <param name="destination">Receives the pixels.</param>
    private static void Join(ReadOnlySpan<byte> rows, int components, int width, Span<byte> destination)
    {
        for (var c = 0; c < components; c++)
        {
            var row = rows.Slice(c * width, width);
            for (var x = 0; x < width; x++)
            {
                destination[(x * components) + c] = row[x];
            }
        }
    }

    /// <summary>Converts YCbCr rows to interleaved RGB.</summary>
    /// <param name="rows">The Y, Cb and Cr rows.</param>
    /// <param name="width">The pixels in the row.</param>
    /// <param name="destination">Receives RGB pixels.</param>
    private static void YccToRgb(ReadOnlySpan<byte> rows, int width, Span<byte> destination)
    {
        var luma = rows[..width];
        var blue = rows.Slice(width, width);
        var red = rows.Slice(CrRow * width, width);
        for (var x = JpegColorVectors.YccToRgb(luma, blue, red, width, destination); x < width; x++)
        {
            ConvertPixel(luma[x], blue[x], red[x], destination.Slice(x * ThreeComponents, ThreeComponents));
        }
    }

    /// <summary>Converts YCCK rows to interleaved CMYK; K passes through.</summary>
    /// <param name="rows">The Y, Cb, Cr and K rows.</param>
    /// <param name="width">The pixels in the row.</param>
    /// <param name="destination">Receives CMYK pixels.</param>
    private static void YcckToCmyk(ReadOnlySpan<byte> rows, int width, Span<byte> destination)
    {
        var luma = rows[..width];
        var blue = rows.Slice(width, width);
        var red = rows.Slice(CrRow * width, width);
        var black = rows.Slice(BlackRow * width, width);
        for (var x = 0; x < width; x++)
        {
            var pixel = destination.Slice(x * FourComponents, FourComponents);
            ConvertPixel(luma[x], blue[x], red[x], pixel);
            pixel[0] = (byte)(MaxSample - pixel[0]);
            pixel[1] = (byte)(MaxSample - pixel[1]);
            pixel[2] = (byte)(MaxSample - pixel[2]);
            pixel[ThreeComponents] = black[x];
        }
    }

    /// <summary>Converts one YCbCr pixel to RGB.</summary>
    /// <param name="y">The luma.</param>
    /// <param name="cb">The blue chroma.</param>
    /// <param name="cr">The red chroma.</param>
    /// <param name="rgb">Receives the first three bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConvertPixel(int y, int cb, int cr, Span<byte> rgb)
    {
        var blueDelta = cb - ChromaCenter;
        var redDelta = cr - ChromaCenter;
        rgb[0] = Clamp(y + (((RedFromCr * redDelta) + Half) >> FixedBits));
        rgb[1] = Clamp(y + ((Half - (GreenFromCb * blueDelta) - (GreenFromCr * redDelta)) >> FixedBits));
        rgb[2] = Clamp(y + (((BlueFromCb * blueDelta) + Half) >> FixedBits));
    }

    /// <summary>Clamps a value to a byte.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value from 0 to 255.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Clamp(int value) => (byte)Math.Clamp(value, 0, MaxSample);
}
