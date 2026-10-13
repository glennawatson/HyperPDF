// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// Converts CIE XYZ to sRGB for a colour space with its own white point (ISO 32000-2 8.6.5). The chromaticities of the sRGB
/// primaries (IEC 61966-2-1) are scaled so the white point maps to RGB (1, 1, 1), which keeps neutral colours neutral, and
/// the result is encoded with the sRGB transfer curve.
/// </summary>
[DebuggerDisplay("WhitePointConverter")]
internal sealed class WhitePointConverter
{
    /// <summary>The numbers in a white point.</summary>
    internal const int WhitePointLength = 3;

    /// <summary>The D65 white X.</summary>
    internal const float D65X = 0.95047F;

    /// <summary>The D65 white Z.</summary>
    internal const float D65Z = 1.08883F;

    /// <summary>The sRGB red primary x.</summary>
    private const float RedX = 0.64F;

    /// <summary>The sRGB red primary y.</summary>
    private const float RedY = 0.33F;

    /// <summary>The sRGB green primary x.</summary>
    private const float GreenX = 0.30F;

    /// <summary>The sRGB green primary y.</summary>
    private const float GreenY = 0.60F;

    /// <summary>The sRGB blue primary x.</summary>
    private const float BlueX = 0.15F;

    /// <summary>The sRGB blue primary y.</summary>
    private const float BlueY = 0.06F;

    /// <summary>How far a white point's Y may be from 1.</summary>
    private const float UnitTolerance = 1e-3F;

    /// <summary>The index of the blue component.</summary>
    private const int BlueIndex = 2;

    /// <summary>The XYZ to linear RGB matrix.</summary>
    private readonly Matrix3 _matrix;

    /// <summary>Initializes a new instance of the <see cref="WhitePointConverter"/> class.</summary>
    /// <param name="x">The white point X.</param>
    /// <param name="y">The white point Y.</param>
    /// <param name="z">The white point Z.</param>
    internal WhitePointConverter(float x, float y, float z)
    {
        Matrix3 primaries = new(RedX, GreenX, BlueX, RedY, GreenY, BlueY, 1 - RedX - RedY, 1 - GreenX - GreenY, 1 - BlueX - BlueY);
        var sum = primaries.Inverse().Transform(x, y, z);
        Matrix3 scaled = new(
            primaries.A * sum.X,
            primaries.B * sum.Y,
            primaries.C * sum.Z,
            primaries.D * sum.X,
            primaries.E * sum.Y,
            primaries.F * sum.Z,
            primaries.G * sum.X,
            primaries.H * sum.Y,
            primaries.I * sum.Z);
        _matrix = scaled.Inverse();
        WhiteX = x;
        WhiteZ = z;
    }

    /// <summary>Gets the converter for the D65 white point.</summary>
    internal static WhitePointConverter D65 { get; } = new(D65X, 1, D65Z);

    /// <summary>Gets the X of the white point (Y is 1).</summary>
    internal float WhiteX { get; }

    /// <summary>Gets the Z of the white point (Y is 1).</summary>
    internal float WhiteZ { get; }

    /// <summary>Reads a /WhitePoint array.</summary>
    /// <param name="dictionary">The colour space dictionary, or <see langword="null"/>.</param>
    /// <returns>A converter for the white point, or the D65 converter when it is missing or invalid.</returns>
    internal static WhitePointConverter FromDictionary(PdfDictionary? dictionary)
    {
        Span<float> white = stackalloc float[WhitePointLength];
        var array = dictionary?.GetArray(KnownName.WhitePoint);
        var isValid = array?.ReadNumbers(white) == WhitePointLength && white[0] > 0 && white[^1] > 0 && MathF.Abs(white[1] - 1) < UnitTolerance;
        return isValid ? new(white[0], white[1], white[^1]) : D65;
    }

    /// <summary>Converts XYZ to sRGB.</summary>
    /// <param name="x">The X value.</param>
    /// <param name="y">The Y value.</param>
    /// <param name="z">The Z value.</param>
    /// <param name="rgb">Receives red, green and blue from 0 to 1.</param>
    internal void ToRgb(float x, float y, float z, Span<float> rgb)
    {
        var linear = _matrix.Transform(x, y, z);
        rgb[0] = SrgbTransfer.Encode(linear.X);
        rgb[1] = SrgbTransfer.Encode(linear.Y);
        rgb[BlueIndex] = SrgbTransfer.Encode(linear.Z);
    }
}
