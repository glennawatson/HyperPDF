// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;
using static HyperPdfLibrary.Tests.Graphics.IccTestProfileBuilder;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>
/// Tests black point compensation in ICC conversion (ISO 32000-2 8.6.5.9): on and off give different results for a
/// profile whose black is lighter than the sRGB black, and absolute colorimetric never compensates.
/// </summary>
public sealed class BlackPointCompensationTests
{
    /// <summary>The components of CMYK.</summary>
    private const int CmykComponents = 4;

    /// <summary>The components of RGB.</summary>
    private const int RgbComponents = 3;

    /// <summary>The grid points of the CMYK test table.</summary>
    private const int CmykGrid = 3;

    /// <summary>The stored L* of unprinted paper, as a fraction of 100.</summary>
    private const double Paper = 0.95;

    /// <summary>How much each of cyan, magenta and yellow darkens the model, as a fraction of 100 L*.</summary>
    private const double InkDrop = 0.2;

    /// <summary>How much black ink darkens the model, as a fraction of 100 L*.</summary>
    private const double BlackDrop = 0.15;

    /// <summary>The stored value of a zero a* or b* in an 'mft1' table.</summary>
    private const double NeutralChroma = 128.0 / 255.0;

    /// <summary>The lightest channel a compensated black may have, as a fraction.</summary>
    private const float CompensatedBlackCeiling = 0.02F;

    /// <summary>The darkest channel the uncompensated black of the test profiles has, as a fraction.</summary>
    private const float PlainBlackFloor = 0.1F;

    /// <summary>The value the black-lifted curves give for zero input.</summary>
    private const double LiftedBlack = 0.1;

    /// <summary>The bytes of an XYZ tag.</summary>
    private const int XyzTagSize = 20;

    /// <summary>The offset of the first number in an XYZ tag.</summary>
    private const int XyzOffset = 8;

    /// <summary>The scale of an s15Fixed16 number.</summary>
    private const double FixedScale = 65_536;

    /// <summary>The smallest difference between two results that counts as different, as a fraction.</summary>
    private const float Distinct = 0.05F;

    /// <summary>The sRGB red colorant in D50 XYZ.</summary>
    private static readonly double[] RedColorant = [0.4360747, 0.2225045, 0.0139322];

    /// <summary>The sRGB green colorant in D50 XYZ.</summary>
    private static readonly double[] GreenColorant = [0.3850649, 0.7168786, 0.0971045];

    /// <summary>The sRGB blue colorant in D50 XYZ.</summary>
    private static readonly double[] BlueColorant = [0.1430804, 0.0606169, 0.7141733];

    /// <summary>The full rich black of the CMYK model.</summary>
    private static readonly float[] RichBlack = [1, 1, 1, 1];

    /// <summary>On and off differ for a CMYK look-up profile whose darkest colour is L* 20: on maps it to sRGB black.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LookUpProfileBlackDiffersWithAndWithoutCompensation()
    {
        var space = IccSpace(CmykProfile(), CmykComponents);
        var on = Convert(space.ForRendering(new(IccIntent.RelativeColorimetric, true)), RichBlack);
        var off = Convert(space.ForRendering(new(IccIntent.RelativeColorimetric, false)), RichBlack);

        await Assert.That(on.Max()).IsLessThan(CompensatedBlackCeiling);
        await Assert.That(off.Min()).IsGreaterThan(PlainBlackFloor);
        await Assert.That(off[1] - on[1]).IsGreaterThan(Distinct);
    }

    /// <summary>Absolute colorimetric is treated as off even when compensation is asked for (ISO 32000-2 8.6.5.9).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AbsoluteColorimetricNeverCompensates()
    {
        var space = IccSpace(CmykProfile(), CmykComponents);
        var asked = Convert(space.ForRendering(new(IccIntent.AbsoluteColorimetric, true)), RichBlack);
        var off = Convert(space.ForRendering(new(IccIntent.RelativeColorimetric, false)), RichBlack);

        await Assert.That(asked).IsEquivalentTo(off);
        await Assert.That(IccRendering.From(IccIntent.AbsoluteColorimetric, BlackPointCompensationMode.On).Compensate).IsFalse();
    }

    /// <summary>An RGB matrix profile whose curves start above zero has a black above XYZ zero; on maps it to sRGB black.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MatrixProfileBlackDiffersWithAndWithoutCompensation()
    {
        var curve = TableCurve(LiftedBlack, 1);
        var profile = Profile(
            Version2,
            "RGB ",
            "XYZ ",
            new Tag("rXYZ", Xyz(RedColorant)),
            new Tag("gXYZ", Xyz(GreenColorant)),
            new Tag("bXYZ", Xyz(BlueColorant)),
            new Tag("rTRC", curve),
            new Tag("gTRC", curve),
            new Tag("bTRC", curve));
        var space = IccSpace(profile, RgbComponents);
        var on = Convert(space.ForRendering(IccRendering.Default), [0, 0, 0]);
        var off = Convert(space.ForRendering(new(IccIntent.Perceptual, false)), [0, 0, 0]);
        var white = Convert(space.ForRendering(IccRendering.Default), [1, 1, 1]);

        await Assert.That(on.Max()).IsLessThan(CompensatedBlackCeiling);
        await Assert.That(off.Min()).IsGreaterThan(PlainBlackFloor);
        await Assert.That(white.Min()).IsGreaterThan(1 - CompensatedBlackCeiling);
        await Assert.That(ReferenceEquals(space.ForRendering(IccRendering.Default), space)).IsTrue();
    }

    /// <summary>A gray curve that starts above zero has a black above zero; on maps it to sRGB black in rows as well.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GrayProfileBlackDiffersWithAndWithoutCompensation()
    {
        var space = IccSpace(Profile(Version2, "GRAY", "XYZ ", new Tag("kTRC", TableCurve(LiftedBlack, 1))), 1);
        var on = new byte[PixelConverter.BytesPerPixel];
        var off = new byte[PixelConverter.BytesPerPixel];
        space.ForRendering(IccRendering.Default).ConvertRow([0], on, 1);
        space.ForRendering(new(IccIntent.RelativeColorimetric, false)).ConvertRow([0], off, 1);

        await Assert.That((int)on[1]).IsLessThan((int)(CompensatedBlackCeiling * PixelConverter.MaxByte));
        await Assert.That((int)off[1]).IsGreaterThan((int)(PlainBlackFloor * PixelConverter.MaxByte));
    }

    /// <summary>A matrix profile whose black is XYZ zero converts the same way with and without compensation, and shares one space.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProfileWithTrueBlackIgnoresTheChoice()
    {
        var curve = GammaCurve(1);
        var profile = Profile(
            Version2,
            "RGB ",
            "XYZ ",
            new Tag("rXYZ", Xyz(RedColorant)),
            new Tag("gXYZ", Xyz(GreenColorant)),
            new Tag("bXYZ", Xyz(BlueColorant)),
            new Tag("rTRC", curve),
            new Tag("gTRC", curve),
            new Tag("bTRC", curve));
        var space = IccSpace(profile, RgbComponents);

        await Assert.That(ReferenceEquals(space.ForRendering(new(IccIntent.Perceptual, false)), space)).IsTrue();
        await Assert.That(ReferenceEquals(space.ForRendering(new(IccIntent.Saturation, true)), space)).IsTrue();
    }

    /// <summary>The same rendering always gives the same space and transform, so warm conversions reuse their tables.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RenderingVariantsAreShared()
    {
        var space = IccSpace(CmykProfile(), CmykComponents);
        var off = new IccRendering(IccIntent.Perceptual, false);

        await Assert.That(ReferenceEquals(space.ForRendering(off), space.ForRendering(off))).IsTrue();
        await Assert.That(ReferenceEquals(space.ForRendering(IccRendering.Default), space)).IsTrue();
        await Assert.That(ReferenceEquals(space.ForRendering(off), space)).IsFalse();
    }

    /// <summary>The PDF names map to their values; misspelled /UseBlackPtComp names are refused and unknown intents read as relative colorimetric.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamesAreReadExactly()
    {
        await Assert.That(ColorRenderingNames.ReadIntent("Perceptual"u8)).IsEqualTo(IccIntent.Perceptual);
        await Assert.That(ColorRenderingNames.ReadIntent("Saturation"u8)).IsEqualTo(IccIntent.Saturation);
        await Assert.That(ColorRenderingNames.ReadIntent("AbsoluteColorimetric"u8)).IsEqualTo(IccIntent.AbsoluteColorimetric);
        await Assert.That(ColorRenderingNames.ReadIntent("RelativeColorimetric"u8)).IsEqualTo(IccIntent.RelativeColorimetric);
        await Assert.That(ColorRenderingNames.ReadIntent("Vivid"u8)).IsEqualTo(IccIntent.RelativeColorimetric);
        await Assert.That(ColorRenderingNames.TryReadBlackPointMode("ON"u8, out var on) && on == BlackPointCompensationMode.On).IsTrue();
        await Assert.That(ColorRenderingNames.TryReadBlackPointMode("OFF"u8, out var off) && off == BlackPointCompensationMode.Off).IsTrue();
        await Assert.That(ColorRenderingNames.TryReadBlackPointMode("Default"u8, out var initial) && initial == BlackPointCompensationMode.Default).IsTrue();
        await Assert.That(ColorRenderingNames.TryReadBlackPointMode("On"u8, out _)).IsFalse();
        await Assert.That(ColorRenderingNames.TryReadBlackPointMode("default"u8, out _)).IsFalse();
    }

    /// <summary>/Default compensates like /ON, and /OFF does not; the initial rendering is relative colorimetric with compensation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultCompensatesExceptForAbsoluteColorimetric()
    {
        await Assert.That(IccRendering.Default).IsEqualTo(new(IccIntent.RelativeColorimetric, true));
        await Assert.That(IccRendering.From(IccIntent.Perceptual, BlackPointCompensationMode.Default).Compensate).IsTrue();
        await Assert.That(IccRendering.From(IccIntent.Saturation, BlackPointCompensationMode.Off).Compensate).IsFalse();
        await Assert.That(IccRendering.From(IccIntent.AbsoluteColorimetric, BlackPointCompensationMode.Default).Compensate).IsFalse();
    }

    /// <summary>Builds the v2 CMYK profile: inks darken a neutral Lab model, so the rich black is L* 20.</summary>
    /// <returns>The profile bytes.</returns>
    internal static byte[] CmykProfile() =>
        Profile(Version2, "CMYK", "Lab ", new Tag("A2B0", Lut8(CmykComponents, CmykGrid, CmykModel)));

    /// <summary>Creates an ICCBased colour space from profile bytes.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="components">The /N value.</param>
    /// <returns>The space.</returns>
    internal static PdfColorSpace IccSpace(byte[] profile, int components)
    {
        var stream = new PdfStream(FixTestHelpers.Dictionary(KnownName.N, PdfValue.FromInteger(components)), profile);
        return PdfColorSpace.Parse(FixTestHelpers.Array(FixTestHelpers.Name(KnownName.ICCBased), PdfValue.FromStream(stream)), null);
    }

    /// <summary>Maps CMYK positions to stored Lab: each ink lowers L* and the colour stays neutral.</summary>
    /// <param name="p">The ink amounts from 0 to 1.</param>
    /// <returns>The stored L*, a* and b*.</returns>
    private static double[] CmykModel(double[] p) =>
        [Paper - (InkDrop * (p[0] + p[1] + p[RgbComponents - 1])) - (BlackDrop * p[RgbComponents]), NeutralChroma, NeutralChroma];

    /// <summary>Converts a colour to RGB.</summary>
    /// <param name="space">The space.</param>
    /// <param name="components">The components.</param>
    /// <returns>Red, green and blue from 0 to 1.</returns>
    private static float[] Convert(PdfColorSpace space, float[] components)
    {
        var rgb = new float[RgbComponents];
        space.ToRgb(components, rgb);
        return rgb;
    }

    /// <summary>Builds an XYZ tag.</summary>
    /// <param name="xyz">The X, Y and Z.</param>
    /// <returns>The tag data.</returns>
    private static byte[] Xyz(double[] xyz)
    {
        var tag = new byte[XyzTagSize];
        "XYZ "u8.CopyTo(tag);
        for (var i = 0; i < xyz.Length; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(tag.AsSpan(XyzOffset + (i * Number32Size)), (int)Math.Round(xyz[i] * FixedScale));
        }

        return tag;
    }
}
