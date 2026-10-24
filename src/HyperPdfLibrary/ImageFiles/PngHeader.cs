// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>The header chunk of a PNG file.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Depth">The bits in each sample: 1, 2, 4, 8 or 16.</param>
/// <param name="ColorType">The colour type: 0 grey, 2 colour, 3 palette, 4 grey with alpha, 6 colour with alpha.</param>
/// <param name="Interlaced">Whether the rows are interlaced in seven passes.</param>
[DebuggerDisplay("PngHeader: {Width}x{Height}, depth {Depth}, type {ColorType}")]
internal readonly record struct PngHeader(int Width, int Height, int Depth, int ColorType, bool Interlaced)
{
    /// <summary>The colour type for grey.</summary>
    internal const int Grey = 0;

    /// <summary>The colour type for red, green and blue.</summary>
    internal const int Colour = 2;

    /// <summary>The colour type for a palette.</summary>
    internal const int Palette = 3;

    /// <summary>The colour type for grey with alpha.</summary>
    internal const int GreyAlpha = 4;

    /// <summary>The colour type for red, green, blue and alpha.</summary>
    internal const int ColourAlpha = 6;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The samples of a colour pixel.</summary>
    private const int ColourSamples = 3;

    /// <summary>The samples of a pixel with alpha: a grey and an alpha.</summary>
    private const int GreyAlphaSamples = 2;

    /// <summary>The samples of a colour pixel with alpha.</summary>
    private const int ColourAlphaSamples = 4;

    /// <summary>The widest sample.</summary>
    private const int WidestSample = 16;

    /// <summary>A depth of two bits.</summary>
    private const int TwoBits = 2;

    /// <summary>A depth of four bits.</summary>
    private const int FourBits = 4;

    /// <summary>Gets the samples in each pixel.</summary>
    internal int Channels => ColorType switch
    {
        Colour => ColourSamples,
        GreyAlpha => GreyAlphaSamples,
        ColourAlpha => ColourAlphaSamples,
        _ => 1,
    };

    /// <summary>Gets the bits in each pixel.</summary>
    internal int BitsPerPixel => Channels * Depth;

    /// <summary>Gets a value indicating whether the colour type and bit depth go together as the PNG standard says.</summary>
    internal bool IsValid => ColorType switch
    {
        Grey => IsSmallDepth || IsByteOrWide,
        Palette => IsSmallDepth || Depth == ByteBits,
        Colour or GreyAlpha or ColourAlpha => IsByteOrWide,
        _ => false,
    };

    /// <summary>Gets a value indicating whether the depth is 1, 2 or 4 bits.</summary>
    private bool IsSmallDepth => Depth is 1 or TwoBits or FourBits;

    /// <summary>Gets a value indicating whether the depth is 8 or 16 bits.</summary>
    private bool IsByteOrWide => Depth is ByteBits or WidestSample;

    /// <summary>Gets the bytes in a row of a given width, without the filter byte.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <returns>The bytes.</returns>
    internal int GetStride(int width) => (int)(((long)width * BitsPerPixel) + ByteBits - 1) / ByteBits;
}
