// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>
/// A double precision reference for the connection space to sRGB step, derived from the sRGB primaries and the Bradford
/// adaptation instead of from the library's constants.
/// </summary>
internal static class IccReference
{
    /// <summary>The D50 white point X.</summary>
    internal const double WhiteX = 0.9642;

    /// <summary>The D50 white point Y.</summary>
    internal const double WhiteY = 1.0;

    /// <summary>The D50 white point Z.</summary>
    internal const double WhiteZ = 0.8249;

    /// <summary>The numbers in a colour and the rows and columns of a matrix.</summary>
    private const int Size = 3;

    /// <summary>The Lab epsilon, (6/29) cubed.</summary>
    private const double Epsilon = 216.0 / 24389.0;

    /// <summary>The Lab kappa, (29/3) cubed.</summary>
    private const double Kappa = 24389.0 / 27.0;

    /// <summary>The XYZ value that a stored 1.0 stands for, 1 + 32767/32768.</summary>
    private const double XyzScale = 1.0 + (32767.0 / 32768.0);

    /// <summary>The L* of a stored 1.0.</summary>
    private const double Hundred = 100.0;

    /// <summary>The offset added to L* in the Lab definition.</summary>
    private const double LightnessOffset = 16.0;

    /// <summary>The divisor of L* plus 16.</summary>
    private const double LightnessDivisor = 116.0;

    /// <summary>The divisor of a*.</summary>
    private const double AlphaDivisor = 500.0;

    /// <summary>The divisor of b*.</summary>
    private const double BetaDivisor = 200.0;

    /// <summary>The stored value of a zero a* or b*, in whole steps.</summary>
    private const double ChromaOffset = 128.0;

    /// <summary>The range of a* and b* in the v4 and 8-bit encoding.</summary>
    private const double ChromaRange = 255.0;

    /// <summary>The 16-bit steps per unit of a* and b* in the legacy encoding.</summary>
    private const double LegacyChromaStep = 256.0;

    /// <summary>The 16-bit value that stands for an L* of 100 in the legacy encoding.</summary>
    private const double LegacyLightnessMaximum = 65280.0;

    /// <summary>The largest 16-bit value.</summary>
    private const double Maximum16 = 65535.0;

    /// <summary>The sRGB D65 white X.</summary>
    private const double D65X = 0.95047;

    /// <summary>The sRGB D65 white Z.</summary>
    private const double D65Z = 1.08883;

    /// <summary>The sRGB red x chromaticity.</summary>
    private const double RedX = 0.64;

    /// <summary>The sRGB red y chromaticity.</summary>
    private const double RedY = 0.33;

    /// <summary>The sRGB green x chromaticity.</summary>
    private const double GreenX = 0.30;

    /// <summary>The sRGB green y chromaticity.</summary>
    private const double GreenY = 0.60;

    /// <summary>The sRGB blue x chromaticity.</summary>
    private const double BlueX = 0.15;

    /// <summary>The sRGB blue y chromaticity.</summary>
    private const double BlueY = 0.06;

    /// <summary>The sRGB linear segment limit.</summary>
    private const double SrgbLimit = 0.0031308;

    /// <summary>The sRGB linear slope.</summary>
    private const double SrgbSlope = 12.92;

    /// <summary>The sRGB power scale.</summary>
    private const double SrgbScale = 1.055;

    /// <summary>The sRGB power offset.</summary>
    private const double SrgbOffset = 0.055;

    /// <summary>The sRGB exponent.</summary>
    private const double SrgbExponent = 2.4;

    /// <summary>The Bradford matrix.</summary>
    private static readonly double[] Bradford = [0.8951, 0.2664, -0.1614, -0.7502, 1.7135, 0.0367, 0.0389, -0.0685, 1.0296];

    /// <summary>The D50 white point.</summary>
    private static readonly double[] White = [WhiteX, WhiteY, WhiteZ];

    /// <summary>The matrix from D50 XYZ to linear sRGB.</summary>
    private static readonly double[] XyzToLinear = BuildXyzToLinear();

    /// <summary>Converts stored Lab values (v4 and 8-bit encoding) to D50 XYZ.</summary>
    /// <param name="stored">The three stored values from 0 to 1.</param>
    /// <returns>The XYZ colour.</returns>
    internal static double[] LabV4ToXyz(double[] stored) =>
        LabToXyz(stored[0] * Hundred, (stored[1] * ChromaRange) - ChromaOffset, (stored[^1] * ChromaRange) - ChromaOffset);

    /// <summary>Converts stored Lab values (16-bit legacy encoding) to D50 XYZ.</summary>
    /// <param name="stored">The three stored values from 0 to 1.</param>
    /// <returns>The XYZ colour.</returns>
    internal static double[] LabLegacyToXyz(double[] stored) => LabToXyz(
        stored[0] * Maximum16 / LegacyLightnessMaximum * Hundred,
        (stored[1] * Maximum16 / LegacyChromaStep) - ChromaOffset,
        (stored[^1] * Maximum16 / LegacyChromaStep) - ChromaOffset);

    /// <summary>Converts stored XYZ values to D50 XYZ.</summary>
    /// <param name="stored">The three stored values from 0 to 1.</param>
    /// <returns>The XYZ colour.</returns>
    internal static double[] StoredToXyz(double[] stored) => [stored[0] * XyzScale, stored[1] * XyzScale, stored[^1] * XyzScale];

    /// <summary>Converts Lab to D50 XYZ with the CIE definition.</summary>
    /// <param name="lightness">L*.</param>
    /// <param name="alpha">a*.</param>
    /// <param name="beta">b*.</param>
    /// <returns>The XYZ colour.</returns>
    internal static double[] LabToXyz(double lightness, double alpha, double beta)
    {
        var fy = (lightness + LightnessOffset) / LightnessDivisor;
        var fx = fy + (alpha / AlphaDivisor);
        var fz = fy - (beta / BetaDivisor);
        var x = Cube(fx) > Epsilon ? Cube(fx) : ((LightnessDivisor * fx) - LightnessOffset) / Kappa;
        var y = lightness > Kappa * Epsilon ? Cube(fy) : lightness / Kappa;
        var z = Cube(fz) > Epsilon ? Cube(fz) : ((LightnessDivisor * fz) - LightnessOffset) / Kappa;
        return [x * WhiteX, y * WhiteY, z * WhiteZ];
    }

    /// <summary>Converts sRGB to D50 XYZ.</summary>
    /// <param name="rgb">Red, green and blue from 0 to 1.</param>
    /// <returns>The XYZ colour.</returns>
    internal static double[] SrgbToXyz(double[] rgb)
    {
        double[] linear = [.. rgb.Select(static v => v <= SrgbLimit * SrgbSlope ? v / SrgbSlope : Math.Pow((v + SrgbOffset) / SrgbScale, SrgbExponent))];
        return Multiply(Invert(XyzToLinear), linear);
    }

    /// <summary>Converts D50 XYZ to Lab.</summary>
    /// <param name="xyz">The colour.</param>
    /// <returns>L*, a* and b*.</returns>
    internal static double[] XyzToLab(double[] xyz)
    {
        var fx = LabFunction(xyz[0] / WhiteX);
        var fy = LabFunction(xyz[1] / WhiteY);
        var fz = LabFunction(xyz[^1] / WhiteZ);
        return [(LightnessDivisor * fy) - LightnessOffset, AlphaDivisor * (fx - fy), BetaDivisor * (fy - fz)];
    }

    /// <summary>Encodes Lab the way a 16-bit table stores it, as values from 0 to 1.</summary>
    /// <param name="lab">L*, a* and b*.</param>
    /// <returns>The stored values.</returns>
    internal static double[] LabToLegacyStored(double[] lab) =>
    [
        lab[0] / Hundred * LegacyLightnessMaximum / Maximum16,
        (lab[1] + ChromaOffset) * LegacyChromaStep / Maximum16,
        (lab[^1] + ChromaOffset) * LegacyChromaStep / Maximum16,
    ];

    /// <summary>Moves a black point to the destination black point and keeps D50 white fixed, per axis.</summary>
    /// <param name="xyz">The colour.</param>
    /// <param name="blackIn">The source black point.</param>
    /// <param name="blackOut">The destination black point.</param>
    /// <returns>The compensated colour.</returns>
    internal static double[] Compensate(double[] xyz, double[] blackIn, double[] blackOut)
    {
        var result = new double[Size];
        for (var i = 0; i < result.Length; i++)
        {
            var scale = (blackOut[i] - White[i]) / (blackIn[i] - White[i]);
            result[i] = White[i] + (scale * (xyz[i] - White[i]));
        }

        return result;
    }

    /// <summary>Converts D50 XYZ to sRGB.</summary>
    /// <param name="xyz">The colour.</param>
    /// <returns>Red, green and blue from 0 to 1.</returns>
    internal static double[] XyzToSrgb(double[] xyz)
    {
        var rgb = new double[Size];
        var linear = Multiply(XyzToLinear, xyz);
        for (var i = 0; i < rgb.Length; i++)
        {
            var clipped = Math.Clamp(linear[i], 0, 1);
            rgb[i] = clipped <= SrgbLimit ? clipped * SrgbSlope : (SrgbScale * Math.Pow(clipped, 1.0 / SrgbExponent)) - SrgbOffset;
        }

        return rgb;
    }

    /// <summary>Cubes a number.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The cube.</returns>
    private static double Cube(double value) => value * value * value;

    /// <summary>Applies the Lab forward function.</summary>
    /// <param name="ratio">The XYZ value relative to the white point.</param>
    /// <returns>The function value.</returns>
    private static double LabFunction(double ratio) => ratio > Epsilon ? Math.Cbrt(ratio) : ((Kappa * ratio) + LightnessOffset) / LightnessDivisor;

    /// <summary>Builds the matrix from D50 XYZ to linear sRGB.</summary>
    /// <returns>The matrix, row by row.</returns>
    private static double[] BuildXyzToLinear()
    {
        var red = Chromaticity(RedX, RedY);
        var green = Chromaticity(GreenX, GreenY);
        var blue = Chromaticity(BlueX, BlueY);
        double[] primaries = [red[0], green[0], blue[0], red[1], green[1], blue[1], red[^1], green[^1], blue[^1]];
        var sums = Multiply(Invert(primaries), [D65X, 1.0, D65Z]);
        double[] scaled = [.. primaries.Select((v, i) => v * sums[i % Size])];

        // Bradford: scale the cone responses of the D65 white to those of D50 white.
        var cone65 = Multiply(Bradford, [D65X, 1.0, D65Z]);
        var cone50 = Multiply(Bradford, White);
        double[] gain = [cone50[0] / cone65[0], 0, 0, 0, cone50[1] / cone65[1], 0, 0, 0, cone50[^1] / cone65[^1]];
        var adapt = MultiplyMatrices(Invert(Bradford), MultiplyMatrices(gain, Bradford));
        return Invert(MultiplyMatrices(adapt, scaled));
    }

    /// <summary>Gets the XYZ of a chromaticity with Y of one.</summary>
    /// <param name="x">The x chromaticity.</param>
    /// <param name="y">The y chromaticity.</param>
    /// <returns>X, Y and Z.</returns>
    private static double[] Chromaticity(double x, double y) => [x / y, 1.0, (1.0 - x - y) / y];

    /// <summary>Multiplies a 3 by 3 matrix by a column.</summary>
    /// <param name="m">The matrix.</param>
    /// <param name="v">The column.</param>
    /// <returns>The product.</returns>
    private static double[] Multiply(double[] m, double[] v)
    {
        var product = new double[Size];
        for (var r = 0; r < Size; r++)
        {
            for (var c = 0; c < Size; c++)
            {
                product[r] += m[(r * Size) + c] * v[c];
            }
        }

        return product;
    }

    /// <summary>Multiplies two 3 by 3 matrices.</summary>
    /// <param name="a">The left matrix.</param>
    /// <param name="b">The right matrix.</param>
    /// <returns>The product.</returns>
    private static double[] MultiplyMatrices(double[] a, double[] b)
    {
        var product = new double[Size * Size];
        for (var r = 0; r < Size; r++)
        {
            for (var c = 0; c < Size; c++)
            {
                for (var k = 0; k < Size; k++)
                {
                    product[(r * Size) + c] += a[(r * Size) + k] * b[(k * Size) + c];
                }
            }
        }

        return product;
    }

    /// <summary>Inverts a 3 by 3 matrix.</summary>
    /// <param name="m">The matrix.</param>
    /// <returns>The inverse.</returns>
    private static double[] Invert(double[] m)
    {
        var determinant = (m[0] * ((m[4] * m[8]) - (m[5] * m[7]))) - (m[1] * ((m[3] * m[8]) - (m[5] * m[6]))) + (m[2] * ((m[3] * m[7]) - (m[4] * m[6])));
        return
        [
            ((m[4] * m[8]) - (m[5] * m[7])) / determinant,
            ((m[2] * m[7]) - (m[1] * m[8])) / determinant,
            ((m[1] * m[5]) - (m[2] * m[4])) / determinant,
            ((m[5] * m[6]) - (m[3] * m[8])) / determinant,
            ((m[0] * m[8]) - (m[2] * m[6])) / determinant,
            ((m[2] * m[3]) - (m[0] * m[5])) / determinant,
            ((m[3] * m[7]) - (m[4] * m[6])) / determinant,
            ((m[1] * m[6]) - (m[0] * m[7])) / determinant,
            ((m[0] * m[4]) - (m[1] * m[3])) / determinant,
        ];
    }
}
