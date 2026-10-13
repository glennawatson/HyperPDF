// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>The CIEDE2000 colour difference between two sRGB colours (CIE 142-2001), under D65 and the 2 degree observer.</summary>
internal static class CmykColorDifference
{
    /// <summary>The D65 white point X.</summary>
    private const double WhiteX = 0.95047;

    /// <summary>The D65 white point Z.</summary>
    private const double WhiteZ = 1.08883;

    /// <summary>The CIE epsilon, 216/24389.</summary>
    private const double Epsilon = 216.0 / 24_389;

    /// <summary>The CIE kappa, 24389/27.</summary>
    private const double Kappa = 24_389.0 / 27;

    /// <summary>The 25 to the seventh power of the chroma weighting.</summary>
    private const double Pow25To7 = 6_103_515_625.0;

    /// <summary>The exponent of the chroma weighting.</summary>
    private const double ChromaExponent = 7;

    /// <summary>The blue column of the sRGB to XYZ matrix.</summary>
    private const int BlueColumn = 2;

    /// <summary>One half.</summary>
    private const double Half = 0.5;

    /// <summary>The number two, used as a factor and an exponent.</summary>
    private const double Two = 2;

    /// <summary>The degrees in a half turn.</summary>
    private const double HalfTurn = 180;

    /// <summary>The degrees in a turn.</summary>
    private const double FullTurn = 360;

    /// <summary>The hue weighting of the 30 degree term.</summary>
    private const double HueWeight30 = 0.17;

    /// <summary>The hue weighting of the doubled term.</summary>
    private const double HueWeight2 = 0.24;

    /// <summary>The hue weighting of the tripled term.</summary>
    private const double HueWeight3 = 0.32;

    /// <summary>The hue weighting of the quadrupled term.</summary>
    private const double HueWeight4 = 0.2;

    /// <summary>The angle offset of the first hue term.</summary>
    private const double HueOffset30 = 30;

    /// <summary>The angle offset of the tripled hue term.</summary>
    private const double HueOffset6 = 6;

    /// <summary>The angle offset of the quadrupled hue term.</summary>
    private const double HueOffset63 = 63;

    /// <summary>The multiplier of the tripled hue term.</summary>
    private const double Triple = 3;

    /// <summary>The multiplier of the quadrupled hue term.</summary>
    private const double Quadruple = 4;

    /// <summary>The centre of the rotation hue band, in degrees.</summary>
    private const double RotationCentre = 275;

    /// <summary>The width of the rotation hue band, in degrees.</summary>
    private const double RotationWidth = 25;

    /// <summary>The size of the rotation, in degrees.</summary>
    private const double RotationSize = 30;

    /// <summary>The lightness weighting slope.</summary>
    private const double LightnessSlope = 0.015;

    /// <summary>The middle lightness.</summary>
    private const double LightnessMiddle = 50;

    /// <summary>The lightness weighting offset.</summary>
    private const double LightnessOffset = 20;

    /// <summary>The chroma weighting slope.</summary>
    private const double ChromaSlope = 0.045;

    /// <summary>The hue weighting slope.</summary>
    private const double HueSlope = 0.015;

    /// <summary>The lightness of the Lab transfer function.</summary>
    private const double LabLightness = 116;

    /// <summary>The offset of the Lab transfer function.</summary>
    private const double LabOffset = 16;

    /// <summary>The a* scale.</summary>
    private const double LabAScale = 500;

    /// <summary>The b* scale.</summary>
    private const double LabBScale = 200;

    /// <summary>The red row of the sRGB to XYZ matrix.</summary>
    private static readonly double[] XRow = [0.4124564, 0.3575761, 0.1804375];

    /// <summary>The green row of the sRGB to XYZ matrix.</summary>
    private static readonly double[] YRow = [0.2126729, 0.7151522, 0.0721750];

    /// <summary>The blue row of the sRGB to XYZ matrix.</summary>
    private static readonly double[] ZRow = [0.0193339, 0.1191920, 0.9503041];

    /// <summary>Gets the CIEDE2000 difference of two colours.</summary>
    /// <param name="first">The first colour.</param>
    /// <param name="second">The second colour.</param>
    /// <returns>The difference.</returns>
    internal static double Between(CmykSrgb first, CmykSrgb second)
    {
        var lab1 = Lab(first);
        var lab2 = Lab(second);
        var meanChroma = (Chroma(lab1.A, lab1.B) + Chroma(lab2.A, lab2.B)) * Half;
        var chroma7 = Math.Pow(meanChroma, ChromaExponent);
        var g = Half * (1 - Math.Sqrt(chroma7 / (chroma7 + Pow25To7)));
        var ap1 = (1 + g) * lab1.A;
        var ap2 = (1 + g) * lab2.A;
        var cp1 = Chroma(ap1, lab1.B);
        var cp2 = Chroma(ap2, lab2.B);
        var hp1 = Hue(lab1.B, ap1);
        var hp2 = Hue(lab2.B, ap2);
        var hueAngle = HueDifference(cp1, cp2, hp1, hp2);
        var hueDelta = Two * Math.Sqrt(cp1 * cp2) * Math.Sin(Radians(hueAngle) * Half);
        var meanCp = (cp1 + cp2) * Half;
        var meanH = MeanHue(cp1, cp2, hp1, hp2);
        var termL = (lab2.L - lab1.L) / LightnessWeight((lab1.L + lab2.L) * Half);
        var termC = (cp2 - cp1) / (1 + (ChromaSlope * meanCp));
        var termH = hueDelta / (1 + (HueSlope * meanCp * HueTerm(meanH)));
        var rt = -Math.Sin(Radians(Two * RotationSize * Math.Exp(-Math.Pow((meanH - RotationCentre) / RotationWidth, Two)))) * ChromaRotation(meanCp);
        return Math.Sqrt((termL * termL) + (termC * termC) + (termH * termH) + (rt * termC * termH));
    }

    /// <summary>Gets the chroma of an a*, b* pair.</summary>
    /// <param name="a">The a* value.</param>
    /// <param name="b">The b* value.</param>
    /// <returns>The chroma.</returns>
    private static double Chroma(double a, double b) => Math.Sqrt((a * a) + (b * b));

    /// <summary>Gets the lightness weighting.</summary>
    /// <param name="meanL">The mean lightness.</param>
    /// <returns>The weighting.</returns>
    private static double LightnessWeight(double meanL)
    {
        var shifted = Math.Pow(meanL - LightnessMiddle, Two);
        return 1 + (LightnessSlope * shifted / Math.Sqrt(LightnessOffset + shifted));
    }

    /// <summary>Gets the hue term T of the hue weighting.</summary>
    /// <param name="meanH">The mean hue.</param>
    /// <returns>The term.</returns>
    private static double HueTerm(double meanH) =>
        1
        - (HueWeight30 * Math.Cos(Radians(meanH - HueOffset30)))
        + (HueWeight2 * Math.Cos(Radians(Two * meanH)))
        + (HueWeight3 * Math.Cos(Radians((Triple * meanH) + HueOffset6)))
        - (HueWeight4 * Math.Cos(Radians((Quadruple * meanH) - HueOffset63)));

    /// <summary>Gets the chroma rotation factor.</summary>
    /// <param name="meanCp">The mean adjusted chroma.</param>
    /// <returns>The factor.</returns>
    private static double ChromaRotation(double meanCp)
    {
        var power = Math.Pow(meanCp, ChromaExponent);
        return Two * Math.Sqrt(power / (power + Pow25To7));
    }

    /// <summary>Converts degrees to radians.</summary>
    /// <param name="degrees">The angle.</param>
    /// <returns>The angle in radians.</returns>
    private static double Radians(double degrees) => degrees * Math.PI / HalfTurn;

    /// <summary>Gets a hue angle from 0 to 360 degrees; a colourless colour has hue 0.</summary>
    /// <param name="b">The b* value.</param>
    /// <param name="a">The adjusted a* value.</param>
    /// <returns>The hue.</returns>
    private static double Hue(double b, double a) => b == 0 && a == 0 ? 0 : ((Math.Atan2(b, a) * HalfTurn / Math.PI) + FullTurn) % FullTurn;

    /// <summary>Gets the shortest signed hue difference.</summary>
    /// <param name="c1">The first chroma.</param>
    /// <param name="c2">The second chroma.</param>
    /// <param name="h1">The first hue.</param>
    /// <param name="h2">The second hue.</param>
    /// <returns>The difference.</returns>
    private static double HueDifference(double c1, double c2, double h1, double h2)
    {
        if (c1 * c2 == 0)
        {
            return 0;
        }

        var difference = h2 - h1;
        if (difference > HalfTurn)
        {
            return difference - FullTurn;
        }

        return difference < -HalfTurn ? difference + FullTurn : difference;
    }

    /// <summary>Gets the mean hue.</summary>
    /// <param name="c1">The first chroma.</param>
    /// <param name="c2">The second chroma.</param>
    /// <param name="h1">The first hue.</param>
    /// <param name="h2">The second hue.</param>
    /// <returns>The mean hue.</returns>
    private static double MeanHue(double c1, double c2, double h1, double h2)
    {
        if (c1 * c2 == 0)
        {
            return h1 + h2;
        }

        if (Math.Abs(h1 - h2) <= HalfTurn)
        {
            return (h1 + h2) * Half;
        }

        return h1 + h2 < FullTurn ? (h1 + h2 + FullTurn) * Half : (h1 + h2 - FullTurn) * Half;
    }

    /// <summary>Converts an sRGB colour to CIE L*a*b*.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The lightness and the two chroma axes.</returns>
    private static CmykLab Lab(CmykSrgb color)
    {
        var r = CmykNeugebauerModel.Linearize(color.Red);
        var g = CmykNeugebauerModel.Linearize(color.Green);
        var b = CmykNeugebauerModel.Linearize(color.Blue);
        var fx = Transfer(Mix(XRow, r, g, b) / WhiteX);
        var fy = Transfer(Mix(YRow, r, g, b));
        var fz = Transfer(Mix(ZRow, r, g, b) / WhiteZ);
        return new((LabLightness * fy) - LabOffset, LabAScale * (fx - fy), LabBScale * (fy - fz));
    }

    /// <summary>Applies one row of the sRGB to XYZ matrix.</summary>
    /// <param name="row">The matrix row.</param>
    /// <param name="r">The linear red.</param>
    /// <param name="g">The linear green.</param>
    /// <param name="b">The linear blue.</param>
    /// <returns>The tristimulus value.</returns>
    private static double Mix(double[] row, double r, double g, double b) => (row[0] * r) + (row[1] * g) + (row[BlueColumn] * b);

    /// <summary>The CIE Lab transfer function.</summary>
    /// <param name="t">The relative tristimulus value.</param>
    /// <returns>The transferred value.</returns>
    private static double Transfer(double t) => t > Epsilon ? Math.Cbrt(t) : (((Kappa * t) + LabOffset) / LabLightness);
}
