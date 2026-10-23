// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>
/// The physical colour model behind the DeviceCMYK to sRGB polynomial: a modified spectral Neugebauer model with 16
/// Neugebauer primaries (every on or off combination of the four inks), a dot-gain curve per ink and a Yule-Nielsen
/// n-factor. The model works on the three linear sRGB channels instead of a full spectrum, which is the usual
/// three-band reduction. The primaries are conventional process-ink colours on coated stock, written as sRGB and
/// documented here as approximations: paper 255,255,255; cyan about 0,174,239; magenta about 236,0,140; yellow about
/// 255,242,0; black about 35,31,32. An overprint of several inks multiplies the linear transmittances of its inks, and
/// each transmittance has a small floor so that a saturated ink never absorbs a band completely.
/// </summary>
internal static class CmykNeugebauerModel
{
    /// <summary>The inks: cyan, magenta, yellow and black.</summary>
    internal const int InkCount = 4;

    /// <summary>The colour bands: red, green and blue.</summary>
    private const int BandCount = 3;

    /// <summary>The Neugebauer primaries: every combination of the inks.</summary>
    private const int PrimaryCount = 1 << InkCount;

    /// <summary>The Yule-Nielsen n-factor, which stands in for the scattering of light inside the paper.</summary>
    private const double YuleNielsenN = 2;

    /// <summary>The smallest linear transmittance of a solid ink in any band.</summary>
    private const double TransmittanceFloor = 0.012;

    /// <summary>The largest byte.</summary>
    private const double MaxByte = 255;

    /// <summary>The sRGB value below which the encoding is linear.</summary>
    private const double LinearLimit = 0.04045;

    /// <summary>The slope of the linear part of the sRGB encoding.</summary>
    private const double LinearSlope = 12.92;

    /// <summary>The offset of the power part of the sRGB encoding.</summary>
    private const double EncodingOffset = 0.055;

    /// <summary>The scale of the power part of the sRGB encoding.</summary>
    private const double EncodingScale = 1.055;

    /// <summary>The exponent of the sRGB encoding.</summary>
    private const double EncodingGamma = 2.4;

    /// <summary>The linear value below which the sRGB encoding is linear.</summary>
    private const double EncodedLimit = 0.0031308;

    /// <summary>The factor of the dot-gain curve: the curve adds this times the gain at 50 per cent coverage.</summary>
    private const double GainFactor = 4;

    /// <summary>The mechanical dot gain of each ink at 50 per cent coverage. The Yule-Nielsen factor already carries the optical gain.</summary>
    private static readonly double[] DotGain = [0, 0, 0, 0];

    /// <summary>The sRGB bytes of the solid inks.</summary>
    private static readonly double[][] InkSrgb = [[0, 174, 239], [236, 0, 140], [255, 242, 0], [35, 31, 32]];

    /// <summary>The linear colour of each primary, indexed by the set of inks (bit 0 cyan, bit 1 magenta, bit 2 yellow, bit 3 black).</summary>
    private static readonly double[][] Primaries = BuildPrimaries();

    /// <summary>Converts a CMYK colour to sRGB.</summary>
    /// <param name="cyan">The cyan coverage from 0 to 1.</param>
    /// <param name="magenta">The magenta coverage from 0 to 1.</param>
    /// <param name="yellow">The yellow coverage from 0 to 1.</param>
    /// <param name="black">The black coverage from 0 to 1.</param>
    /// <returns>The sRGB colour, each channel from 0 to 1.</returns>
    internal static CmykSrgb ToSrgb(double cyan, double magenta, double yellow, double black)
    {
        Span<double> coverage = [cyan, magenta, yellow, black];
        for (var ink = 0; ink < InkCount; ink++)
        {
            var value = coverage[ink];
            coverage[ink] = Math.Clamp(value + (GainFactor * DotGain[ink] * value * (1 - value)), 0, 1);
        }

        Span<double> mixed = stackalloc double[3];
        for (var primary = 0; primary < PrimaryCount; primary++)
        {
            var weight = DemichelWeight(coverage, primary);
            for (var band = 0; band < mixed.Length; band++)
            {
                mixed[band] += weight * Math.Pow(Primaries[primary][band], 1 / YuleNielsenN);
            }
        }

        return new(Encode(Math.Pow(mixed[0], YuleNielsenN)), Encode(Math.Pow(mixed[1], YuleNielsenN)), Encode(Math.Pow(mixed[2], YuleNielsenN)));
    }

    /// <summary>Converts an sRGB value in a 0 to 1 range to linear light.</summary>
    /// <param name="encoded">The encoded value.</param>
    /// <returns>The linear value.</returns>
    internal static double Linearize(double encoded) =>
        encoded <= LinearLimit ? encoded / LinearSlope : Math.Pow((encoded + EncodingOffset) / EncodingScale, EncodingGamma);

    /// <summary>Converts linear light to an sRGB value in a 0 to 1 range.</summary>
    /// <param name="linear">The linear value.</param>
    /// <returns>The encoded value.</returns>
    internal static double Encode(double linear) =>
        linear <= EncodedLimit ? linear * LinearSlope : (EncodingScale * Math.Pow(linear, 1 / EncodingGamma)) - EncodingOffset;

    /// <summary>Gets the share of the page that a primary covers when the inks print without regard to each other (Demichel).</summary>
    /// <param name="coverage">The effective coverage of each ink.</param>
    /// <param name="primary">The set of inks of the primary.</param>
    /// <returns>The weight.</returns>
    private static double DemichelWeight(ReadOnlySpan<double> coverage, int primary)
    {
        var weight = 1.0;
        for (var ink = 0; ink < InkCount; ink++)
        {
            weight *= ((primary >> ink) & 1) != 0 ? coverage[ink] : 1 - coverage[ink];
        }

        return weight;
    }

    /// <summary>Builds the 16 primaries by multiplying the linear transmittances of their inks.</summary>
    /// <returns>The primaries.</returns>
    private static double[][] BuildPrimaries()
    {
        var transmittance = new double[InkCount][];
        for (var ink = 0; ink < InkCount; ink++)
        {
            transmittance[ink] = new double[3];
            for (var band = 0; band < transmittance[ink].Length; band++)
            {
                transmittance[ink][band] = Math.Max(Linearize(InkSrgb[ink][band] / MaxByte), TransmittanceFloor);
            }
        }

        var primaries = new double[PrimaryCount][];
        for (var primary = 0; primary < PrimaryCount; primary++)
        {
            primaries[primary] = [1, 1, 1];
            for (var ink = 0; ink < InkCount; ink++)
            {
                if (((primary >> ink) & 1) == 0)
                {
                    continue;
                }

                for (var band = 0; band < BandCount; band++)
                {
                    primaries[primary][band] *= transmittance[ink][band];
                }
            }
        }

        return primaries;
    }
}
