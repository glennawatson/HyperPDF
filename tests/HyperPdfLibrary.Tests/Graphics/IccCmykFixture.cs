// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using static HyperPdfLibrary.Tests.Graphics.IccTestProfileBuilder;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>
/// Generates a CMYK output profile from a fixed, documented ink model, so tests need no host profile and no licensed profile.
/// </summary>
/// <remarks>
/// The model (ICC.1:2004-10 clause 10.8 'lut16Type' and ICC.1:2022 clause 10.12 'lutAToBType'). For an ink amount x from 0 to 1:
/// <c>gain(x) = x + DotGain * x * (1 - x)</c> and, with c, m, y and k each passed through <c>gain</c>:
/// <c>R = (1 - c) * (1 - BlackDensity * k)</c>, <c>G = (1 - m) * (1 - BlackDensity * k)</c>, <c>B = (1 - y) * (1 - BlackDensity * k)</c>.
/// The result is an sRGB-encoded colour, so it is always inside the sRGB gamut. The table stores that colour as Lab: the sRGB
/// transfer function gives linear light, the sRGB primaries adapted to D50 with the Bradford matrix give XYZ, and the CIE
/// definition gives Lab. A2B0 (perceptual) uses <see cref="Perceptual"/>, A2B1 (relative colorimetric) <see cref="Relative"/> and
/// A2B2 (saturation) <see cref="Saturation"/>. The tables are written deterministically with big-endian integers, so the bytes
/// are the same on every platform.
/// </remarks>
internal static class IccCmykFixture
{
    /// <summary>The grid points per channel of the 'mft2' profile.</summary>
    internal const int Mft2Grid = 17;

    /// <summary>The grid points per channel of the 'mAB ' profile.</summary>
    internal const int MabGrid = 9;

    /// <summary>The components of CMYK.</summary>
    internal const int Components = 4;

    /// <summary>The perceptual ink model: a black that is not quite full density.</summary>
    internal static readonly InkModel Perceptual = new(0.2, 0.92);

    /// <summary>The relative colorimetric ink model: full black density.</summary>
    internal static readonly InkModel Relative = new(0.2, 1.0);

    /// <summary>The saturation ink model: less dot gain, so the inks are stronger.</summary>
    internal static readonly InkModel Saturation = new(0.1, 0.92);

    /// <summary>The bytes per sample of the 'mAB ' table.</summary>
    private const int WideBytes = 2;

    /// <summary>The L* of a stored 1.0.</summary>
    private const double Hundred = 100.0;

    /// <summary>The stored value of a zero a* or b*, in whole steps.</summary>
    private const double ChromaOffset = 128.0;

    /// <summary>The range of a* and b* in the v4 encoding.</summary>
    private const double ChromaRange = 255.0;

    /// <summary>The index of the yellow ink from the end of a CMYK array.</summary>
    private const int YellowFromEnd = 2;

    /// <summary>Gets the sRGB colour the model gives for an ink mix.</summary>
    /// <param name="model">The ink model.</param>
    /// <param name="cmyk">Cyan, magenta, yellow and black from 0 to 1.</param>
    /// <returns>Red, green and blue from 0 to 1.</returns>
    internal static double[] Rgb(in InkModel model, double[] cmyk)
    {
        var black = 1.0 - (model.BlackDensity * Gain(model, cmyk[^1]));
        return
        [
            (1.0 - Gain(model, cmyk[0])) * black,
            (1.0 - Gain(model, cmyk[1])) * black,
            (1.0 - Gain(model, cmyk[^YellowFromEnd])) * black,
        ];
    }

    /// <summary>Gets the model's colour as stored v2 16-bit Lab values.</summary>
    /// <param name="model">The ink model.</param>
    /// <param name="cmyk">The ink mix.</param>
    /// <returns>The stored values from 0 to 1.</returns>
    internal static double[] StoredLegacy(in InkModel model, double[] cmyk) =>
        IccReference.LabToLegacyStored(IccReference.XyzToLab(IccReference.SrgbToXyz(Rgb(model, cmyk))));

    /// <summary>Gets the model's colour as stored v4 Lab values.</summary>
    /// <param name="model">The ink model.</param>
    /// <param name="cmyk">The ink mix.</param>
    /// <returns>The stored values from 0 to 1.</returns>
    internal static double[] StoredV4(in InkModel model, double[] cmyk)
    {
        var lab = IccReference.XyzToLab(IccReference.SrgbToXyz(Rgb(model, cmyk)));
        return [lab[0] / Hundred, (lab[1] + ChromaOffset) / ChromaRange, (lab[^1] + ChromaOffset) / ChromaRange];
    }

    /// <summary>Builds the v2 profile: 'mft2' tables with Lab connection space in the legacy 16-bit encoding.</summary>
    /// <returns>The profile bytes.</returns>
    internal static byte[] BuildMft2() => Profile(
        Version2,
        "CMYK",
        "Lab ",
        new Tag("A2B0", Lut16(Components, Mft2Grid, static p => StoredLegacy(Perceptual, p))),
        new Tag("A2B1", Lut16(Components, Mft2Grid, static p => StoredLegacy(Relative, p))),
        new Tag("A2B2", Lut16(Components, Mft2Grid, static p => StoredLegacy(Saturation, p))));

    /// <summary>Builds the v4 profile: 'mAB ' tables with Lab connection space.</summary>
    /// <returns>The profile bytes.</returns>
    internal static byte[] BuildMab() => Profile(
        Version4,
        "CMYK",
        "Lab ",
        new Tag("A2B0", Mab(Perceptual)),
        new Tag("A2B1", Mab(Relative)),
        new Tag("A2B2", Mab(Saturation)));

    /// <summary>Applies the dot gain to an ink amount.</summary>
    /// <param name="model">The ink model.</param>
    /// <param name="amount">The ink amount from 0 to 1.</param>
    /// <returns>The gained amount.</returns>
    private static double Gain(in InkModel model, double amount) => amount + (model.DotGain * amount * (1.0 - amount));

    /// <summary>Builds an 'mAB ' tag with identity curves and a 16-bit table.</summary>
    /// <param name="model">The ink model.</param>
    /// <returns>The tag data.</returns>
    private static byte[] Mab(InkModel model)
    {
        byte[][] identity = [GammaCurve(1), GammaCurve(1), GammaCurve(1)];
        byte[][] inputs = [GammaCurve(1), GammaCurve(1), GammaCurve(1), GammaCurve(1)];
        var stages = new MabStages(identity, null, null, inputs, WideBytes);
        return AToB(Components, [MabGrid, MabGrid, MabGrid, MabGrid], p => StoredV4(model, p), stages);
    }

    /// <summary>A set of ink parameters.</summary>
    /// <param name="DotGain">The dot gain factor; 50 percent ink gains DotGain / 4 of coverage.</param>
    /// <param name="BlackDensity">How strongly full black darkens the colour, from 0 to 1.</param>
    internal readonly record struct InkModel(double DotGain, double BlackDensity);
}
