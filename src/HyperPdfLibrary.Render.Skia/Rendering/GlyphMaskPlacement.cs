// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Where and how big a glyph is on the device, brought to values a mask can be shared at: the matrix to 1/256ths and the
/// glyph's origin to a quarter pixel, or to a whole pixel when the hinting choice fits glyphs to the grid.
/// </summary>
/// <param name="A">The horizontal scale, as a whole number of 2^-<paramref name="Shift"/>.</param>
/// <param name="B">The matrix entry that moves a point down as it moves right, as a whole number of 2^-<paramref name="Shift"/>.</param>
/// <param name="C">The matrix entry that moves a point right as it moves down, as a whole number of 2^-<paramref name="Shift"/>.</param>
/// <param name="D">The vertical scale, as a whole number of 2^-<paramref name="Shift"/>.</param>
/// <param name="Shift">The power of two the matrix entries are scaled by, chosen so the largest entry has about 16 bits.</param>
/// <param name="SubX">How far the origin is right of its pixel, in quarter pixels from 0 to 3.</param>
/// <param name="SubY">How far the origin is down its pixel, in quarter pixels from 0 to 3.</param>
/// <param name="OriginX">The device pixel column the origin falls in.</param>
/// <param name="OriginY">The device pixel row the origin falls in.</param>
[DebuggerDisplay("GlyphMaskPlacement: ({OriginX}, {OriginY}) + ({SubX}, {SubY})/4")]
internal readonly record struct GlyphMaskPlacement(int A, int B, int C, int D, int Shift, byte SubX, byte SubY, int OriginX, int OriginY)
{
    /// <summary>The steps a pixel is divided into for positioning.</summary>
    internal const int PixelSteps = 4;

    /// <summary>The bits the largest matrix entry is kept to, which keeps the error of a glyph's shape far below a pixel at any size.</summary>
    private const int EntryBits = 15;

    /// <summary>The largest origin, in pixels, a placement is made for; text further away is not drawn from a mask.</summary>
    private const float MaxOrigin = 1_000_000;

    /// <summary>The bits that give the position within a pixel.</summary>
    private const int SubMask = PixelSteps - 1;

    /// <summary>The bits that shift a quarter pixel position down to its pixel.</summary>
    private const int SubShift = 2;

    /// <summary>Makes a placement from the matrix that maps a glyph's outline to device pixels.</summary>
    /// <param name="toDevice">The matrix from the outline's space to device pixels.</param>
    /// <param name="hinting">How glyphs are fitted to the pixel grid.</param>
    /// <param name="placement">Receives the placement.</param>
    /// <returns><see langword="false"/> when the matrix is not finite or the glyph is too far away to place.</returns>
    internal static bool TryCreate(Matrix3x2 toDevice, PdfTextHinting hinting, out GlyphMaskPlacement placement)
    {
        placement = default;
        if (!float.IsFinite(toDevice.M31) || !float.IsFinite(toDevice.M32) || MathF.Abs(toDevice.M31) > MaxOrigin || MathF.Abs(toDevice.M32) > MaxOrigin)
        {
            return false;
        }

        var largest = MathF.Max(MathF.Max(MathF.Abs(toDevice.M11), MathF.Abs(toDevice.M12)), MathF.Max(MathF.Abs(toDevice.M21), MathF.Abs(toDevice.M22)));
        if (!float.IsFinite(largest) || largest <= 0)
        {
            return false;
        }

        // The entries of a matrix from font units are tiny, so they are kept to a fixed number of bits of their own size.
        var shift = EntryBits - MathF.ILogB(largest);
        var a = (int)MathF.Round(MathF.ScaleB(toDevice.M11, shift));
        var b = (int)MathF.Round(MathF.ScaleB(toDevice.M12, shift));
        var c = (int)MathF.Round(MathF.ScaleB(toDevice.M21, shift));
        var d = (int)MathF.Round(MathF.ScaleB(toDevice.M22, shift));
        var x = Snap(toDevice.M31, hinting == PdfTextHinting.Full);
        var y = Snap(toDevice.M32, hinting != PdfTextHinting.None);
        placement = new(a, b, c, d, shift, (byte)(x & SubMask), (byte)(y & SubMask), x >> SubShift, y >> SubShift);
        return true;
    }

    /// <summary>Brings a position to the grid a mask is shared at.</summary>
    /// <param name="value">The position in device pixels.</param>
    /// <param name="wholePixel">Whether to snap to a whole pixel rather than a quarter.</param>
    /// <returns>The position in quarter pixels.</returns>
    private static int Snap(float value, bool wholePixel) =>
        wholePixel ? (int)MathF.Round(value) * PixelSteps : (int)MathF.Round(value * PixelSteps);
}
