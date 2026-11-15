// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>Reads the value a luminosity soft mask gives a pixel (ISO 32000-2, 11.5.3).</summary>
internal static class SoftMasks
{
    /// <summary>The weight of red in the brightness of a colour, out of 256.</summary>
    private const uint RedWeight = 77;

    /// <summary>The weight of green in the brightness of a colour, out of 256.</summary>
    private const uint GreenWeight = 151;

    /// <summary>The weight of blue in the brightness of a colour, out of 256.</summary>
    private const uint BlueWeight = 28;

    /// <summary>The shift of the red byte of a pixel.</summary>
    private const int RedShift = 16;

    /// <summary>The bits of one byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The mask of one byte.</summary>
    private const uint ByteMask = 0xFF;

    /// <summary>Gets the brightness of a pixel of a mask.</summary>
    /// <param name="pixel">The premultiplied pixel, with blue in the lowest byte.</param>
    /// <returns>The brightness, 0 to 255.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Brightness(uint pixel) =>
        ((((pixel >> RedShift) & ByteMask) * RedWeight) + (((pixel >> ByteBits) & ByteMask) * GreenWeight) + ((pixel & ByteMask) * BlueWeight)) >> ByteBits;
}
