// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdf.Codecs.Video;

/// <summary>Turns 8 bit 4:2:0 pictures into blue, green, red and alpha bytes, with 256 bit or 128 bit vectors where the computer has them.</summary>
public static class YuvConverter
{
    /// <summary>Converts a decoded picture.</summary>
    /// <param name="frame">The picture.</param>
    /// <param name="matrix">The weights.</param>
    /// <param name="bgra">Receives <c>Width * Height * 4</c> bytes: blue, green, red, alpha for each pixel.</param>
    /// <exception cref="ArgumentException"><paramref name="bgra"/> is too short.</exception>
    public static void ToBgra(VideoFrame frame, in ColorMatrix matrix, Span<byte> bgra)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ToBgra(new YuvPlanes(frame.Y, frame.U, frame.V, frame.Width, frame.Height), matrix, bgra);
    }

    /// <summary>Converts the planes of a picture.</summary>
    /// <param name="planes">The planes.</param>
    /// <param name="matrix">The weights.</param>
    /// <param name="bgra">Receives <c>Width * Height * 4</c> bytes: blue, green, red, alpha for each pixel.</param>
    /// <exception cref="ArgumentException"><paramref name="bgra"/> is too short.</exception>
    public static void ToBgra(YuvPlanes planes, in ColorMatrix matrix, Span<byte> bgra)
    {
        var width = planes.Width;
        if (bgra.Length < width * planes.Height * sizeof(int))
        {
            throw new ArgumentException("The destination is too short for the picture.", nameof(bgra));
        }

        var chromaWidth = planes.ChromaWidth;
        var pixels = MemoryMarshal.Cast<byte, int>(bgra);
        for (var row = 0; row < planes.Height; row++)
        {
            var chroma = (row >> 1) * chromaWidth;
            ConvertRow(planes.Y.Slice(row * width, width), planes.U.Slice(chroma, chromaWidth), planes.V.Slice(chroma, chromaWidth), matrix, pixels.Slice(row * width, width));
        }
    }

    /// <summary>Converts one row of pixels.</summary>
    /// <param name="y">The luma samples of the row.</param>
    /// <param name="u">The Cb samples of the row, one for every two pixels.</param>
    /// <param name="v">The Cr samples of the row, one for every two pixels.</param>
    /// <param name="matrix">The weights.</param>
    /// <param name="pixels">Receives a pixel for each luma sample, as blue in the lowest byte and alpha in the highest.</param>
    public static void ConvertRow(ReadOnlySpan<byte> y, ReadOnlySpan<byte> u, ReadOnlySpan<byte> v, in ColorMatrix matrix, Span<int> pixels)
    {
        var done = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            done = YuvKernel256.Convert(y, u, v, matrix, pixels);
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            done = YuvKernel128.Convert(y, u, v, matrix, pixels);
        }

        for (var x = done; x < y.Length; x++)
        {
            pixels[x] = Pixel(y[x], u[x >> 1], v[x >> 1], matrix);
        }
    }

    /// <summary>Converts one pixel with whole numbers; the vector loops give the same result.</summary>
    /// <param name="y">The luma sample.</param>
    /// <param name="u">The Cb sample.</param>
    /// <param name="v">The Cr sample.</param>
    /// <param name="matrix">The weights.</param>
    /// <returns>The pixel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Pixel(int y, int u, int v, in ColorMatrix matrix)
    {
        var luma = (matrix.LumaScale * (y - matrix.LumaOffset)) + YuvConstants.Rounding;
        var cb = u - YuvConstants.ColorCentre;
        var cr = v - YuvConstants.ColorCentre;
        var red = Channel(luma + (matrix.RedFromCr * cr));
        var green = Channel(luma + (matrix.GreenFromCb * cb) + (matrix.GreenFromCr * cr));
        var blue = Channel(luma + (matrix.BlueFromCb * cb));
        return YuvConstants.OpaqueAlpha | (red << YuvConstants.RedShift) | (green << YuvConstants.GreenShift) | blue;
    }

    /// <summary>Drops the fractional bits of a channel and keeps it within a byte.</summary>
    /// <param name="value">The channel with its fractional bits.</param>
    /// <returns>The channel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Channel(int value) => Math.Clamp(value >> YuvConstants.FractionBits, 0, YuvConstants.ChannelMax);
}
