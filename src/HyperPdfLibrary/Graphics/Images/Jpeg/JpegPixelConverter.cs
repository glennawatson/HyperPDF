// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>Turns rows of source pixels into component samples: the JFIF RGB-to-YCbCr conversion, CMYK inversion, alpha compositing, edge padding and chroma reduction.</summary>
internal static class JpegPixelConverter
{
    /// <summary>The fixed-point bits of the conversion constants.</summary>
    private const int FixedBits = 16;

    /// <summary>Half a unit in fixed point, for rounding.</summary>
    private const int Half = 1 << (FixedBits - 1);

    /// <summary>The chroma offset in fixed point, plus a rounding term that maps neutral grays to exactly 128.</summary>
    private const int ChromaOffset = (128 << FixedBits) + Half - 1;

    /// <summary>0.299 in fixed point.</summary>
    private const int LumaRed = 19_595;

    /// <summary>0.587 in fixed point.</summary>
    private const int LumaGreen = 38_470;

    /// <summary>0.114 in fixed point.</summary>
    private const int LumaBlue = 7_471;

    /// <summary>0.168736 in fixed point.</summary>
    private const int BlueFromRed = 11_058;

    /// <summary>0.331264 in fixed point.</summary>
    private const int BlueFromGreen = 21_710;

    /// <summary>0.5 in fixed point.</summary>
    private const int HalfUnit = 32_768;

    /// <summary>0.418688 in fixed point.</summary>
    private const int RedFromGreen = 27_439;

    /// <summary>0.081312 in fixed point.</summary>
    private const int RedFromBlue = 5_329;

    /// <summary>The largest sample.</summary>
    private const int MaxSample = 255;

    /// <summary>The bits of the divide-by-255 approximation.</summary>
    private const int DivideShift = 8;

    /// <summary>The bytes of a three-byte pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The bytes of a four-byte pixel.</summary>
    private const int QuadBytes = 4;

    /// <summary>The offset of the alpha or black byte in a four-byte pixel.</summary>
    private const int FourthByte = 3;

    /// <summary>The offset of the blue or yellow byte in a four-byte pixel.</summary>
    private const int ThirdByte = 2;

    /// <summary>Gets the bytes in a source pixel.</summary>
    /// <param name="format">The pixel format.</param>
    /// <returns>The bytes.</returns>
    internal static int BytesPerPixel(PdfJpegPixelFormat format) => format switch
    {
        PdfJpegPixelFormat.Gray8 => 1,
        PdfJpegPixelFormat.Rgb24 => RgbBytes,
        _ => QuadBytes,
    };

    /// <summary>Converts one row of pixels to component rows and pads each to the full strip width by repeating its last sample.</summary>
    /// <param name="row">The source pixels of the row.</param>
    /// <param name="format">The source format.</param>
    /// <param name="kind">The colour model to produce.</param>
    /// <param name="width">The pixels in the row.</param>
    /// <param name="planes">The components, each with a destination row of at least the padded width.</param>
    /// <param name="offset">The offset of the destination row in each component's strip.</param>
    internal static void ConvertRow(ReadOnlySpan<byte> row, PdfJpegPixelFormat format, JpegColorKind kind, int width, ReadOnlySpan<JpegEncoderComponent> planes, int offset)
    {
        var stride = planes[0].FullStride;
        var first = planes[0].FullStrip.Slice(offset, stride);
        switch (kind)
        {
            case JpegColorKind.Gray:
            {
                row[..width].CopyTo(first);
                break;
            }

            case JpegColorKind.YCbCr:
            {
                ConvertYcc(row, format, width, first, planes[1].FullStrip.Slice(offset, stride), planes[2].FullStrip.Slice(offset, stride));
                break;
            }

            default:
            {
                ConvertInk(row, kind, width, planes, offset);
                break;
            }
        }

        for (var c = 0; c < planes.Length; c++)
        {
            var padded = planes[c].FullStrip.Slice(offset, stride);
            padded[width..].Fill(padded[width - 1]);
        }
    }

    /// <summary>Averages blocks of samples to the sampling of a reduced component.</summary>
    /// <param name="component">The component, whose full strip holds the MCU row at the finest sampling.</param>
    /// <param name="horizontalFactor">The samples averaged across.</param>
    /// <param name="verticalFactor">The samples averaged down.</param>
    internal static void Reduce(JpegEncoderComponent component, int horizontalFactor, int verticalFactor)
    {
        var source = component.FullStrip;
        var target = component.ReducedStrip;
        var count = horizontalFactor * verticalFactor;
        var shift = BitOperations.TrailingZeroCount(count);
        var rounding = count >> 1;
        for (var y = 0; y < component.PlaneRows; y++)
        {
            var output = target.Slice(y * component.PlaneStride, component.PlaneStride);
            for (var x = 0; x < output.Length; x++)
            {
                var sum = rounding;
                for (var dy = 0; dy < verticalFactor; dy++)
                {
                    var line = source.Slice(((y * verticalFactor) + dy) * component.FullStride, component.FullStride);
                    for (var dx = 0; dx < horizontalFactor; dx++)
                    {
                        sum += line[(x * horizontalFactor) + dx];
                    }
                }

                output[x] = (byte)(sum >> shift);
            }
        }
    }

    /// <summary>Converts one pixel from RGB to YCbCr.</summary>
    /// <param name="r">The red sample.</param>
    /// <param name="g">The green sample.</param>
    /// <param name="b">The blue sample.</param>
    /// <param name="y">Receives the luma.</param>
    /// <param name="cb">Receives the blue chroma.</param>
    /// <param name="cr">Receives the red chroma.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ToYcc(int r, int g, int b, out byte y, out byte cb, out byte cr)
    {
        y = (byte)(((LumaRed * r) + (LumaGreen * g) + (LumaBlue * b) + Half) >> FixedBits);
        cb = (byte)((ChromaOffset - (BlueFromRed * r) - (BlueFromGreen * g) + (HalfUnit * b)) >> FixedBits);
        cr = (byte)((ChromaOffset + (HalfUnit * r) - (RedFromGreen * g) - (RedFromBlue * b)) >> FixedBits);
    }

    /// <summary>Converts a row of RGB-family pixels to luma and chroma rows.</summary>
    /// <param name="row">The source pixels.</param>
    /// <param name="format">The source format.</param>
    /// <param name="width">The pixels in the row.</param>
    /// <param name="luma">Receives the luma samples.</param>
    /// <param name="blue">Receives the blue chroma samples.</param>
    /// <param name="red">Receives the red chroma samples.</param>
    private static void ConvertYcc(ReadOnlySpan<byte> row, PdfJpegPixelFormat format, int width, Span<byte> luma, Span<byte> blue, Span<byte> red)
    {
        for (var x = 0; x < width; x++)
        {
            ReadRgb(row, format, x, out var r, out var g, out var b);
            ToYcc(r, g, b, out luma[x], out blue[x], out red[x]);
        }
    }

    /// <summary>Reads one pixel as red, green and blue, compositing alpha over white.</summary>
    /// <param name="row">The source pixels.</param>
    /// <param name="format">The source format.</param>
    /// <param name="x">The pixel index.</param>
    /// <param name="r">Receives red.</param>
    /// <param name="g">Receives green.</param>
    /// <param name="b">Receives blue.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReadRgb(ReadOnlySpan<byte> row, PdfJpegPixelFormat format, int x, out int r, out int g, out int b)
    {
        if (format == PdfJpegPixelFormat.Rgb24)
        {
            var offset = x * RgbBytes;
            r = row[offset];
            g = row[offset + 1];
            b = row[offset + ThirdByte];
            return;
        }

        var quad = x * QuadBytes;
        if (format == PdfJpegPixelFormat.Rgbx32)
        {
            r = row[quad];
            g = row[quad + 1];
            b = row[quad + ThirdByte];
            return;
        }

        var alpha = row[quad + FourthByte];
        b = OverWhite(row[quad], alpha);
        g = OverWhite(row[quad + 1], alpha);
        r = OverWhite(row[quad + ThirdByte], alpha);
    }

    /// <summary>Composites a straight-alpha channel over white.</summary>
    /// <param name="channel">The channel value.</param>
    /// <param name="alpha">The alpha.</param>
    /// <returns>The composited value, rounded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int OverWhite(int channel, int alpha)
    {
        if (alpha == MaxSample)
        {
            return channel;
        }

        // channel * alpha + 255 * (255 - alpha), divided by 255 with rounding.
        var total = (channel * alpha) + (MaxSample * (MaxSample - alpha)) + (MaxSample >> 1);
        return (total + (total >> DivideShift) + 1) >> DivideShift;
    }

    /// <summary>Converts a row of CMYK pixels to four inverted ink rows, or to YCCK rows.</summary>
    /// <param name="row">The source pixels, 255 meaning full ink.</param>
    /// <param name="kind">Plain CMYK or YCCK.</param>
    /// <param name="width">The pixels in the row.</param>
    /// <param name="planes">The four components.</param>
    /// <param name="offset">The offset of the destination row in each strip.</param>
    private static void ConvertInk(ReadOnlySpan<byte> row, JpegColorKind kind, int width, ReadOnlySpan<JpegEncoderComponent> planes, int offset)
    {
        var stride = planes[0].FullStride;
        var first = planes[0].FullStrip.Slice(offset, stride);
        var second = planes[1].FullStrip.Slice(offset, stride);
        var third = planes[2].FullStrip.Slice(offset, stride);
        var fourth = planes[FourthByte].FullStrip.Slice(offset, stride);
        for (var x = 0; x < width; x++)
        {
            var quad = x * QuadBytes;
            if (kind == JpegColorKind.Ycck)
            {
                // The ink amounts take the place of red, green and blue; the black passes through, inverted like the Adobe samples.
                ToYcc(row[quad], row[quad + 1], row[quad + ThirdByte], out first[x], out second[x], out third[x]);
            }
            else
            {
                first[x] = (byte)(MaxSample - row[quad]);
                second[x] = (byte)(MaxSample - row[quad + 1]);
                third[x] = (byte)(MaxSample - row[quad + ThirdByte]);
            }

            fourth[x] = (byte)(MaxSample - row[quad + FourthByte]);
        }
    }
}
