// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// Runs the DeviceCMYK to sRGB polynomial of <see cref="CmykPolynomial"/> over pixels of any lane width. The scalar and every
/// vector width run this same code with fused multiply-adds, so they give the same bytes.
/// </summary>
internal static class CmykKernel
{
    /// <summary>The bytes in one CMYK or BGRA pixel.</summary>
    internal const int BytesPerPixel = 4;

    /// <summary>The largest byte as a float.</summary>
    internal const float ByteScale = 255F;

    /// <summary>The factor that turns a byte into a value from 0 to 1.</summary>
    internal const float InverseByteScale = 1F / ByteScale;

    /// <summary>The bias that makes a truncating conversion round to the nearest byte.</summary>
    internal const float RoundingBias = 0.5F;

    /// <summary>The shift of the green byte in a BGRA word.</summary>
    internal const int GreenShift = 8;

    /// <summary>The shift of the red byte in a BGRA word.</summary>
    internal const int RedShift = 16;

    /// <summary>The alpha byte of an opaque BGRA word.</summary>
    internal const uint OpaqueAlpha = 0xFF00_0000;

    /// <summary>The shift of the cyan byte in a CMYK word.</summary>
    private const int CyanShift = 0;

    /// <summary>The shift of the magenta byte in a CMYK word.</summary>
    private const int MagentaShift = 8;

    /// <summary>The shift of the yellow byte in a CMYK word.</summary>
    private const int YellowShift = 16;

    /// <summary>The shift of the black byte in a CMYK word.</summary>
    private const int BlackShift = 24;

    /// <summary>Converts pixels of CMYK bytes to opaque BGRA pixels.</summary>
    /// <typeparam name="TLanes">The lane operations.</typeparam>
    /// <typeparam name="TFloat">The float lanes.</typeparam>
    /// <typeparam name="TWord">The pixel lanes.</typeparam>
    /// <param name="pixels">The CMYK pixels.</param>
    /// <returns>The BGRA pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TWord Convert<TLanes, TFloat, TWord>(TWord pixels)
        where TLanes : ICmykLanes<TFloat, TWord>
        where TFloat : unmanaged
        where TWord : unmanaged
    {
        CmykPolynomial.Evaluate<TLanes, TFloat, TWord>(
            TLanes.Channel(pixels, CyanShift),
            TLanes.Channel(pixels, MagentaShift),
            TLanes.Channel(pixels, YellowShift),
            TLanes.Channel(pixels, BlackShift),
            out var red,
            out var green,
            out var blue);
        return TLanes.Pack(red, green, blue);
    }

    /// <summary>Converts a row of CMYK pixels with the widest whole blocks of one lane width.</summary>
    /// <typeparam name="TLanes">The lane operations.</typeparam>
    /// <typeparam name="TFloat">The float lanes.</typeparam>
    /// <typeparam name="TWord">The pixel lanes.</typeparam>
    /// <param name="source">The first CMYK byte.</param>
    /// <param name="destination">The first BGRA byte.</param>
    /// <param name="start">The first pixel to convert.</param>
    /// <param name="count">The pixels in the row.</param>
    /// <returns>The index of the first pixel that was not converted.</returns>
    internal static int ConvertBlocks<TLanes, TFloat, TWord>(ref byte source, ref byte destination, int start, int count)
        where TLanes : ICmykLanes<TFloat, TWord>
        where TFloat : unmanaged
        where TWord : unmanaged
    {
        var width = TLanes.Count;
        var i = start;
        for (; i <= count - width; i += width)
        {
            var offset = (nuint)i * BytesPerPixel;
            TLanes.Store(Convert<TLanes, TFloat, TWord>(TLanes.Load(ref source, offset)), ref destination, offset);
        }

        return i;
    }
}
