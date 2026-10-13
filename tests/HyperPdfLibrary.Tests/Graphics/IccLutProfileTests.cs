// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;
using static HyperPdfLibrary.Tests.Graphics.IccTestProfileBuilder;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Tests for ICCBased colour spaces whose profiles hold look-up tables (CMYK, Lab connection space, N-colour).</summary>
public sealed class IccLutProfileTests
{
    /// <summary>The largest per-channel difference from the reference, in 8-bit steps, that the issue allows for pure colours.</summary>
    private const double ByteTolerance = 3.0;

    /// <summary>The tolerance for points between nodes, in 8-bit steps.</summary>
    private const double MidpointTolerance = 2.0;

    /// <summary>The scale from a 0 to 1 value to a byte.</summary>
    private const double ByteScale = 255.0;

    /// <summary>The largest difference between a row conversion and a single conversion, in bytes.</summary>
    private const double RowTolerance = 1.0;

    /// <summary>The components of CMYK.</summary>
    private const int CmykComponents = 4;

    /// <summary>The components of RGB.</summary>
    private const int RgbComponents = 3;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The grid points of the CMYK test tables.</summary>
    private const int CmykGrid = 5;

    /// <summary>The grid points of the RGB test tables.</summary>
    private const int RgbGrid = 33;

    /// <summary>The bits of a sample byte.</summary>
    private const int SampleBits = 8;

    /// <summary>The grid points of the Lab identity table.</summary>
    private const int LabGrid = 17;

    /// <summary>The grid points of the small RGB table.</summary>
    private const int SmallGrid = 3;

    /// <summary>The grid points of a table with only the corners.</summary>
    private const int CornerGrid = 2;

    /// <summary>The grid points of the N-colour test table.</summary>
    private const int WideGrid = 3;

    /// <summary>The input channels of the N-colour test profile.</summary>
    private const int WideComponents = 5;

    /// <summary>The calls made before measuring allocations.</summary>
    private const int Warmup = 20;

    /// <summary>The pixels converted by the allocation test.</summary>
    private const int Pixels = 64;

    /// <summary>The step between the sample values of the allocation test.</summary>
    private const int SampleStride = 7;

    /// <summary>The number of generated points compared for accuracy.</summary>
    private const int SamplePoints = 400;

    /// <summary>The multiplier of the point generator.</summary>
    private const uint GeneratorMultiplier = 2_654_435_761;

    /// <summary>The step between the generator inputs of consecutive points.</summary>
    private const int GeneratorStride = 31;

    /// <summary>The L* of white in the Lab models.</summary>
    private const double LabRange = 100.0;

    /// <summary>The stored value of a zero a* or b*, in whole steps.</summary>
    private const double ChromaOffset = 128.0;

    /// <summary>The range of a* and b* in the v4 and 8-bit encoding.</summary>
    private const double ChromaRange = 255.0;

    /// <summary>The highest L* used for the black point of a v2 profile.</summary>
    private const double MaxBlackLightness = 50.0;

    /// <summary>The largest 16-bit value.</summary>
    private const double Maximum16 = 65_535.0;

    /// <summary>The 16-bit value of L* 100 in the legacy encoding, relative to the largest value.</summary>
    private const double LegacyWhite = 65_280.0 / Maximum16;

    /// <summary>The stored value of a zero a* or b* in the legacy encoding.</summary>
    private const double LegacyZero = 32_768.0 / Maximum16;

    /// <summary>The L* the legacy model gives the paper.</summary>
    private const double LegacyPaper = 0.9;

    /// <summary>How much cyan darkens the legacy model, as a fraction.</summary>
    private const double LegacyCyanDrop = 0.45;

    /// <summary>How much each ink darkens the five-colour model, as a fraction.</summary>
    private const double WideDrop = 0.15;

    /// <summary>How far the five-colour model shifts a* and b* per unit of ink, as a fraction of the range.</summary>
    private const double WideShift = 0.2;

    /// <summary>The stored value of a neutral a* or b* in the five-colour model.</summary>
    private const double WideCentre = 0.5;

    /// <summary>The gamma of the input curves in the all-stages test.</summary>
    private const double CurveGamma = 1.8;

    /// <summary>The distance from the end of an array to its second last element.</summary>
    private const int SecondFromEnd = 2;

    /// <summary>The bytes per sample of 16-bit tables.</summary>
    private const int WideBytes = 2;

    /// <summary>The gap between the largest ink and black that a rich black may have, as a fraction.</summary>
    private const float RichBlackMargin = 0.05F;

    /// <summary>The lowest channel a white may have on screen, as a fraction.</summary>
    private const float WhiteFloor = 0.85F;

    /// <summary>The highest channel a black may have on screen, as a fraction.</summary>
    private const float BlackCeiling = 0.35F;

    /// <summary>The highest channel a model black may have, as a fraction.</summary>
    private const float ModelBlackCeiling = 0.1F;

    /// <summary>The lowest green a model white may have, as a fraction.</summary>
    private const float ModelWhiteFloor = 0.9F;

    /// <summary>The smallest difference that shows a profile converts through its table, as a fraction.</summary>
    private const float TableDifference = 0.001F;

    /// <summary>The smallest difference in green between the two tables of the intent test, as a fraction.</summary>
    private const float IntentGap = 0.3F;

    /// <summary>Relative colorimetric without black point compensation, which converts exactly what the table holds.</summary>
    private static readonly IccRendering Relative = new(IccIntent.RelativeColorimetric, false);

    /// <summary>Perceptual with black point compensation.</summary>
    private static readonly IccRendering CompensatedPerceptual = new(IccIntent.Perceptual, true);

    /// <summary>The lightness weights of the cyan, magenta, yellow and black inks in the CMYK model.</summary>
    private static readonly double[] InkLightness = [0.45, 0.35, 0.15, 0];

    /// <summary>The a* weights of the inks in the CMYK model.</summary>
    private static readonly double[] InkAlpha = [-60, 60, 20, 0];

    /// <summary>The b* weights of the inks in the CMYK model.</summary>
    private static readonly double[] InkBeta = [-40, -10, 80, 0];

    /// <summary>The base of the XYZ model of the all-stages test.</summary>
    private static readonly double[] XyzBase = [0.1, 0.1, 0.1];

    /// <summary>The slope of the XYZ model of the all-stages test.</summary>
    private static readonly double[] XyzSlope = [0.5, 0.5, 0.4];

    /// <summary>The matrix of the all-stages test: nine coefficients then three offsets.</summary>
    private static readonly double[] StageMatrix = [0.9, 0.1, 0.0, 0.05, 0.9, 0.05, 0.0, 0.1, 0.9, 0.02, 0.0, 0.01];

    /// <summary>The RGB points of the all-stages test.</summary>
    private static readonly double[][] RgbPoints = [[0, 0, 0], [1, 1, 1], [0.5, 0.25, 0.75], [1, 0, 0], [0.2, 0.9, 0.4]];

    /// <summary>The Lab colours of the Lab profile test, in L*, a* and b*.</summary>
    private static readonly double[][] LabPoints = [[50, 20, -30], [90, -40, 60], [10, 0, 0], [75, 60, 80]];

    /// <summary>The default ranges of an ICCBased Lab space.</summary>
    private static readonly float[] LabRanges = [0, 100, -128, 127, -128, 127];

    /// <summary>The gray levels of the gray profile test.</summary>
    private static readonly double[] GrayPoints = [0, 0.25, 0.6, 1];

    /// <summary>A dark stored Lab colour for the intent test.</summary>
    private static readonly double[] DarkLab = [0.2, 0.5, 0.5];

    /// <summary>A light stored Lab colour for the intent test.</summary>
    private static readonly double[] LightLab = [0.8, 0.5, 0.5];

    /// <summary>The v4 perceptual black point, which a v4 profile compensates from.</summary>
    private static readonly double[] V4Black = [0.00336, 0.0034731, 0.00287];

    /// <summary>The black point of the sRGB output profile.</summary>
    private static readonly double[] OutputBlack = [0, 0, 0];

    /// <summary>The five-colour points.</summary>
    private static readonly double[][] WidePoints = [[0, 0, 0, 0, 0], [1, 0, 0, 0, 0], [0.5, 0.5, 0.5, 0.5, 0.5], [0, 1, 1, 0, 1]];

    /// <summary>The colour used to compare a corrupt profile with its alternate.</summary>
    private static readonly float[] CorruptProbe = [0.3F, 0.6F, 0.1F, 0.2F];

    /// <summary>The colour used to tell a table from the device conversion.</summary>
    private static readonly double[] TableProbe = [0.5, 0.2, 0.7, 0.1];

    /// <summary>The CMYK points compared with the reference: corners, mid points and grid nodes.</summary>
    private static readonly double[][] CmykNodes =
    [
        [0, 0, 0, 0],
        [1, 0, 0, 0],
        [0, 1, 0, 0],
        [0, 0, 1, 0],
        [0, 0, 0, 1],
        [0.5, 0.5, 0, 0],
        [0.25, 0.75, 0.5, 0.25],
        [0.5, 0.5, 0.5, 0.5],
        [1, 1, 1, 1],
        [0.75, 0, 0.25, 0.5],
    ];

    /// <summary>The v2 'mft1' profile converts the table's grid nodes like the reference.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Lut8CmykNodesMatchReference()
    {
        var profile = Profile(Version2, "CMYK", "Lab ", new Tag("A2B0", Lut8(CmykComponents, CmykGrid, CmykModel)));
        var space = IccSpace(profile, CmykComponents).ForRendering(Relative);

        foreach (var node in CmykNodes)
        {
            var expected = IccReference.XyzToSrgb(IccReference.LabV4ToXyz(Quantize8(CmykModel(node))));
            await Assert.That(MaxDifference(space, node, expected)).IsLessThanOrEqualTo(ByteTolerance);
        }
    }

    /// <summary>The v2 'mft2' profile uses the legacy 16-bit Lab encoding: stored 0xFF00 is white.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Lut16CmykUsesLegacyLabEncoding()
    {
        var profile = Profile(Version2, "CMYK", "Lab ", new Tag("A2B0", Lut16(CmykComponents, CmykGrid, LegacyModel)));
        var space = IccSpace(profile, CmykComponents).ForRendering(Relative);

        foreach (var node in CmykNodes)
        {
            var expected = IccReference.XyzToSrgb(IccReference.LabLegacyToXyz(LegacyModel(node)));
            await Assert.That(MaxDifference(space, node, expected)).IsLessThanOrEqualTo(ByteTolerance);
        }
    }

    /// <summary>An 'mAB ' table with input curves, matrix curves, a matrix and output curves matches the reference.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AToBWithAllStagesMatchesReference()
    {
        var stages = new MabStages(
            [GammaCurve(1), GammaCurve(1), GammaCurve(1)],
            StageMatrix,
            [GammaCurve(1), GammaCurve(1), GammaCurve(1)],
            [GammaCurve(CurveGamma), GammaCurve(CurveGamma), GammaCurve(CurveGamma)],
            WideBytes);
        var tag = AToB(RgbComponents, [SmallGrid, SmallGrid, SmallGrid], XyzModel, stages);
        var profile = Profile(Version4, "RGB ", "XYZ ", new Tag("A2B0", tag));
        var space = IccSpace(profile, RgbComponents).ForRendering(Relative);

        foreach (var input in RgbPoints)
        {
            var stored = XyzModel([.. input.Select(static v => Math.Pow(v, CurveGamma))]);
            var moved = new double[RgbComponents];
            for (var row = 0; row < RgbComponents; row++)
            {
                moved[row] = (StageMatrix[row * RgbComponents] * stored[0])
                    + (StageMatrix[(row * RgbComponents) + 1] * stored[1])
                    + (StageMatrix[((row + 1) * RgbComponents) - 1] * stored[^1])
                    + StageMatrix[(RgbComponents * RgbComponents) + row];
            }

            var expected = IccReference.XyzToSrgb(IccReference.StoredToXyz(moved));
            await Assert.That(MaxDifference(space, input, expected)).IsLessThanOrEqualTo(ByteTolerance);
        }
    }

    /// <summary>Perceptual conversion of a v4 profile compensates for its black point, as Little CMS does for the sRGB output.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PerceptualIntentCompensatesBlackPoint()
    {
        var profile = Profile(Version4, "CMYK", "Lab ", new Tag("A2B0", Lut8(CmykComponents, CmykGrid, CmykModel)));
        var perceptual = IccSpace(profile, CmykComponents).ForRendering(CompensatedPerceptual);

        foreach (var node in CmykNodes)
        {
            var xyz = IccReference.LabV4ToXyz(Quantize8(CmykModel(node)));
            var expected = IccReference.XyzToSrgb(IccReference.Compensate(xyz, V4Black, OutputBlack));
            await Assert.That(MaxDifference(perceptual, node, expected)).IsLessThanOrEqualTo(ByteTolerance);
        }
    }

    /// <summary>A v2 profile compensates from the lightness of its darkest colorant.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PerceptualIntentUsesDarkestColorantForV2Profiles()
    {
        var profile = Profile(Version2, "CMYK", "Lab ", new Tag("A2B0", Lut8(CmykComponents, CmykGrid, CmykModel)));
        var perceptual = IccSpace(profile, CmykComponents).ForRendering(CompensatedPerceptual);
        var darkest = IccReference.XyzToLab(IccReference.LabV4ToXyz(Quantize8(CmykModel([1, 1, 1, 1]))));
        var sourceBlack = IccReference.LabToXyz(Math.Min(darkest[0], MaxBlackLightness), 0, 0);

        foreach (var node in CmykNodes)
        {
            var xyz = IccReference.LabV4ToXyz(Quantize8(CmykModel(node)));
            var expected = IccReference.XyzToSrgb(IccReference.Compensate(xyz, sourceBlack, OutputBlack));
            await Assert.That(MaxDifference(perceptual, node, expected)).IsLessThanOrEqualTo(ByteTolerance);
        }
    }

    /// <summary>Pure inks give the expected hue: cyan is not red, magenta is not green, yellow is not blue, and black is dark.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PureInksMapToTheirHues()
    {
        var profile = Profile(Version2, "CMYK", "Lab ", new Tag("A2B0", Lut8(CmykComponents, CmykGrid, CmykModel)));
        var space = IccSpace(profile, CmykComponents);
        var cyan = Convert(space, [1, 0, 0, 0]);
        var magenta = Convert(space, [0, 1, 0, 0]);
        var yellow = Convert(space, [0, 0, 1, 0]);
        var black = Convert(space, [0, 0, 0, 1]);
        var white = Convert(space, [0, 0, 0, 0]);

        await Assert.That(cyan[0]).IsLessThan(cyan[^1]);
        await Assert.That(magenta[1]).IsLessThan(magenta[0]);
        await Assert.That(yellow[^1]).IsLessThan(yellow[0]);
        await Assert.That(black[0]).IsLessThan(ModelBlackCeiling);
        await Assert.That(white[1]).IsGreaterThan(ModelWhiteFloor);
    }

    /// <summary>Points between the table's nodes stay within the allowed difference of the reference.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InterpolatedPointsStayCloseToReference()
    {
        var profile = Profile(Version2, "CMYK", "Lab ", new Tag("A2B0", Lut16(CmykComponents, CmykGrid, LegacyModel)));
        var space = IccSpace(profile, CmykComponents).ForRendering(Relative);
        var worst = 0.0;
        for (var i = 0; i < SamplePoints; i++)
        {
            double[] point = [Unit(i, 0), Unit(i, 1), Unit(i, RgbComponents - 1), Unit(i, RgbComponents)];

            // A multilinear reference of the stored table values is what the table itself defines between nodes.
            var expected = IccReference.XyzToSrgb(IccReference.LabLegacyToXyz(MultilinearStored(point)));
            worst = Math.Max(worst, MaxDifference(space, point, expected));
        }

        // Between nodes the grid, the tetrahedral interpolation and the sampling of the colour conversion each add a little.
        await Assert.That(worst).IsLessThanOrEqualTo(MidpointTolerance);
    }

    /// <summary>A byte row converts to the same colours as single colours do.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RowConversionMatchesSingleColours()
    {
        var profile = Profile(Version2, "CMYK", "Lab ", new Tag("A2B0", Lut8(CmykComponents, CmykGrid, CmykModel)));
        var space = IccSpace(profile, CmykComponents);
        var samples = new byte[CmykNodes.Length * CmykComponents];
        for (var p = 0; p < CmykNodes.Length; p++)
        {
            for (var c = 0; c < CmykComponents; c++)
            {
                samples[(p * CmykComponents) + c] = (byte)Math.Round(CmykNodes[p][c] * ByteScale);
            }
        }

        var row = new byte[CmykNodes.Length * BytesPerPixel];
        space.ConvertRow(samples, row, CmykNodes.Length);
        var rgb = new float[RgbComponents];
        for (var p = 0; p < CmykNodes.Length; p++)
        {
            space.ToRgb([.. CmykNodes[p].Select(static v => (float)v)], rgb);
            var red = row[(p * BytesPerPixel) + RgbComponents - 1];
            var green = row[(p * BytesPerPixel) + 1];
            var blue = row[p * BytesPerPixel];

            await Assert.That(Math.Abs(red - (rgb[0] * ByteScale))).IsLessThanOrEqualTo(RowTolerance);
            await Assert.That(Math.Abs(green - (rgb[1] * ByteScale))).IsLessThanOrEqualTo(RowTolerance);
            await Assert.That(Math.Abs(blue - (rgb[^1] * ByteScale))).IsLessThanOrEqualTo(RowTolerance);
        }
    }

    /// <summary>A profile with AToB1 and AToB0 tables uses the table that matches the intent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IntentSelectsTheMatchingTable()
    {
        var profile = Profile(
            Version2,
            "CMYK",
            "Lab ",
            new Tag("A2B0", Lut8(CmykComponents, CornerGrid, static _ => DarkLab)),
            new Tag("A2B1", Lut8(CmykComponents, CornerGrid, static _ => LightLab)));
        var space = IccSpace(profile, CmykComponents);
        var perceptual = Convert(space.ForRendering(new(IccIntent.Perceptual, false)), [0, 0, 0, 0]);
        var relative = Convert(space.ForRendering(Relative), [0, 0, 0, 0]);
        var absolute = Convert(space.ForRendering(new(IccIntent.AbsoluteColorimetric, false)), [0, 0, 0, 0]);
        var saturation = Convert(space.ForRendering(new(IccIntent.Saturation, false)), [0, 0, 0, 0]);

        await Assert.That(relative[1]).IsGreaterThan(perceptual[1] + IntentGap);
        await Assert.That(absolute[1]).IsEqualTo(relative[1]);
        await Assert.That(saturation[1]).IsEqualTo(perceptual[1]);
    }

    /// <summary>An RGB look-up profile that encodes sRGB is recognised, so device RGB stands in for it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SrgbLookUpProfileUsesDeviceRgb()
    {
        var profile = Profile(Version2, "RGB ", "Lab ", new Tag("A2B0", Lut16(RgbComponents, RgbGrid, SrgbLegacy)));
        var space = IccSpace(profile, RgbComponents);
        var icc = new byte[BytesPerPixel];
        var device = new byte[BytesPerPixel];
        space.ConvertRow([0x40, 0x80, 0xC0], icc, 1);
        PdfColorSpace.DeviceRgb.ConvertRow([0x40, 0x80, 0xC0], device, 1);

        await Assert.That(space.Kind).IsEqualTo(PdfColorSpaceKind.IccBased);
        await Assert.That(icc).IsEquivalentTo(device);
    }

    /// <summary>A five-colour profile converts through its table with no alternate space.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FiveColourProfileConverts()
    {
        var profile = Profile(Version2, "5CLR", "Lab ", new Tag("A2B0", Lut8(WideComponents, WideGrid, WideModel)));
        var space = IccSpace(profile, WideComponents).ForRendering(Relative);

        await Assert.That(space.Components).IsEqualTo(WideComponents);
        foreach (var node in WidePoints)
        {
            var expected = IccReference.XyzToSrgb(IccReference.LabV4ToXyz(Quantize8(WideModel(node))));
            await Assert.That(MaxDifference(space, node, expected)).IsLessThanOrEqualTo(ByteTolerance);
        }
    }

    /// <summary>A Lab profile takes components in L*, a* and b* units, as the PDF default range for Lab says.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LabProfileConvertsLabUnits()
    {
        var profile = Profile(Version2, "Lab ", "Lab ", new Tag("A2B0", Lut8(RgbComponents, LabGrid, static p => p)));
        var space = IccSpace(profile, RgbComponents).ForRendering(Relative);

        foreach (var lab in LabPoints)
        {
            var expected = IccReference.XyzToSrgb(IccReference.LabToXyz(lab[0], lab[1], lab[^1]));
            await Assert.That(MaxDifference(space, lab, expected)).IsLessThanOrEqualTo(ByteTolerance);
        }

        await Assert.That(space.GetDefaultDecode(SampleBits)).IsEquivalentTo(LabRanges);
    }

    /// <summary>A gray profile with a look-up table converts through the table.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GrayLookUpProfileConverts()
    {
        static double[] Gray(double[] p) => [p[0], ChromaOffset / ChromaRange, ChromaOffset / ChromaRange];
        var profile = Profile(Version2, "GRAY", "Lab ", new Tag("A2B0", Lut8(1, CmykGrid, Gray)));
        var space = IccSpace(profile, 1).ForRendering(Relative);

        foreach (var gray in GrayPoints)
        {
            var expected = IccReference.XyzToSrgb(IccReference.LabV4ToXyz(Quantize8(Gray([gray]))));
            await Assert.That(MaxDifference(space, [gray], expected)).IsLessThanOrEqualTo(ByteTolerance);
        }
    }

    /// <summary>A tag table that points outside the profile makes the space fall back to its alternate.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TagBeyondProfileFallsBackToAlternate()
    {
        var profile = Profile(Version2, "CMYK", "Lab ", new Tag("A2B0", Lut8(CmykComponents, CmykGrid, CmykModel)));

        // Make the tag's size larger than the profile.
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(HeaderSize + Number32Size + TagSizeField), (uint)profile.Length);

        await AssertFallsBack(profile);
    }

    /// <summary>A table with more nodes than the limit allows makes the space fall back to its alternate.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OversizeTableFallsBackToAlternate()
    {
        var tag = Lut8(CmykComponents, CornerGrid, CmykModel);

        // Declare 255 grid points per channel without supplying the nodes.
        tag[GridPointsOffset] = byte.MaxValue;

        await AssertFallsBack(Profile(Version2, "CMYK", "Lab ", new Tag("A2B0", tag)));
    }

    /// <summary>A table whose 'mAB ' offsets point outside the tag makes the space fall back to its alternate.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CorruptAToBFallsBackToAlternate()
    {
        var stages = new MabStages([GammaCurve(1), GammaCurve(1), GammaCurve(1)], null, null, null, 1);
        var tag = AToB(CmykComponents, [CornerGrid, CornerGrid, CornerGrid, CornerGrid], CmykModel, stages);

        // Point the table offset beyond the end of the tag.
        BinaryPrimitives.WriteUInt32BigEndian(tag.AsSpan(MabClutOffset), (uint)tag.Length);

        await AssertFallsBack(Profile(Version4, "CMYK", "Lab ", new Tag("A2B0", tag)));
    }

    /// <summary>Converting rows and single colours allocates nothing once the table is built.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConversionDoesNotAllocate()
    {
        var profile = Profile(Version2, "CMYK", "Lab ", new Tag("A2B0", Lut8(CmykComponents, CmykGrid, CmykModel)));
        var space = IccSpace(profile, CmykComponents);
        var samples = new byte[Pixels * CmykComponents];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (byte)(i * SampleStride);
        }

        var bgra = new byte[Pixels * BytesPerPixel];
        var components = new float[CmykComponents];
        var rgb = new float[RgbComponents];
        for (var i = 0; i < Warmup; i++)
        {
            space.ConvertRow(samples, bgra, Pixels);
            space.ToRgb(components, rgb);
        }

        var allocated = AllocationProbe.Minimum(() =>
        {
            space.ConvertRow(samples, bgra, Pixels);
            space.ToRgb(components, rgb);
        });

        await Assert.That(allocated).IsEqualTo(0L);
    }

    /// <summary>
    /// An optional smoke test: real CMYK profiles found on the machine convert paper white to near white and full black to a
    /// dark colour. It skips when the machine has none; <see cref="IccCmykFixtureTests"/> holds the coverage that needs no host profile.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SystemCmykProfilesConvertSensibly()
    {
        var found = 0;
        foreach (var path in FindSystemProfiles())
        {
            var profile = await File.ReadAllBytesAsync(path);
            if (profile.Length < HeaderSize || Encoding.ASCII.GetString(profile, ColorSpaceOffset, Number32Size) != "CMYK")
            {
                continue;
            }

            var space = IccSpace(profile, CmykComponents);
            if (!IsLookUp(space))
            {
                continue;
            }

            var white = Convert(space, [0, 0, 0, 0]);
            var black = Convert(space, [0, 0, 0, 1]);
            var rich = Convert(space, [1, 1, 1, 1]);
            found++;

            await Assert.That(white.Min()).IsGreaterThan(WhiteFloor);
            await Assert.That(black.Max()).IsLessThan(BlackCeiling);
            await Assert.That(rich.Max()).IsLessThan(black.Max() + RichBlackMargin);
        }

        if (found == 0)
        {
            Skip.Test("No CMYK ICC profile was found in the system profile folders.");
        }
    }

    /// <summary>Gets the sum of products of two equally long arrays.</summary>
    /// <param name="weights">The weights.</param>
    /// <param name="values">The values.</param>
    /// <returns>The dot product.</returns>
    private static double Dot(double[] weights, double[] values)
    {
        var sum = 0.0;
        for (var i = 0; i < weights.Length; i++)
        {
            sum += weights[i] * values[i];
        }

        return sum;
    }

    /// <summary>Models CMYK inks as stored Lab values (v4 encoding).</summary>
    /// <param name="p">Cyan, magenta, yellow and black from 0 to 1.</param>
    /// <returns>The stored values.</returns>
    private static double[] CmykModel(double[] p)
    {
        var lightness = LabRange * (1 - p[^1]) * (1 - Dot(InkLightness, p));
        return [lightness / LabRange, (Dot(InkAlpha, p) + ChromaOffset) / ChromaRange, (Dot(InkBeta, p) + ChromaOffset) / ChromaRange];
    }

    /// <summary>Models CMYK as stored legacy 16-bit Lab values: paper and a darkening black, with no colour cast.</summary>
    /// <param name="p">Cyan, magenta, yellow and black from 0 to 1.</param>
    /// <returns>The stored values.</returns>
    private static double[] LegacyModel(double[] p) => [LegacyPaper * (1 - p[^1]) * LegacyWhite * (1 - (LegacyCyanDrop * p[0])), LegacyZero, LegacyZero];

    /// <summary>Models the all-stages test's table: a slope on each channel.</summary>
    /// <param name="p">The position.</param>
    /// <returns>The stored XYZ values.</returns>
    private static double[] XyzModel(double[] p) => [XyzBase[0] + (XyzSlope[0] * p[0]), XyzBase[1] + (XyzSlope[1] * p[1]), XyzBase[^1] + (XyzSlope[^1] * p[^1])];

    /// <summary>Models a five-colour profile as stored Lab values.</summary>
    /// <param name="p">The five inks.</param>
    /// <returns>The stored values.</returns>
    private static double[] WideModel(double[] p) =>
        [1.0 - (WideDrop * p.Sum()), WideCentre + (WideShift * (p[1] - p[0])), WideCentre + (WideShift * (p[^1] - p[^SecondFromEnd]))];

    /// <summary>Stores sRGB colours as legacy 16-bit Lab values.</summary>
    /// <param name="p">Red, green and blue from 0 to 1.</param>
    /// <returns>The stored values.</returns>
    private static double[] SrgbLegacy(double[] p) => IccReference.LabToLegacyStored(IccReference.XyzToLab(IccReference.SrgbToXyz(p)));

    /// <summary>Rounds stored values to what an 8-bit table keeps.</summary>
    /// <param name="stored">The values.</param>
    /// <returns>The rounded values.</returns>
    private static double[] Quantize8(double[] stored) => [.. stored.Select(static v => Math.Round(Math.Clamp(v, 0, 1) * ByteScale) / ByteScale)];

    /// <summary>Rounds stored values to what a 16-bit table keeps.</summary>
    /// <param name="stored">The values.</param>
    /// <returns>The rounded values.</returns>
    private static double[] Quantize16(double[] stored) => [.. stored.Select(static v => Math.Round(Math.Clamp(v, 0, 1) * Maximum16) / Maximum16)];

    /// <summary>Evaluates the 16-bit table of <see cref="LegacyModel"/> between nodes by multilinear interpolation.</summary>
    /// <param name="point">The CMYK position.</param>
    /// <returns>The stored values.</returns>
    private static double[] MultilinearStored(double[] point)
    {
        var result = new double[RgbComponents];
        for (var corner = 0; corner < 1 << CmykComponents; corner++)
        {
            var weight = 1.0;
            var node = new double[CmykComponents];
            for (var c = 0; c < CmykComponents; c++)
            {
                var scaled = point[c] * (CmykGrid - 1);
                var cell = Math.Min((int)scaled, CmykGrid - CornerGrid);
                var fraction = scaled - cell;
                var upper = ((corner >> c) & 1) == 1;
                weight *= upper ? fraction : 1 - fraction;
                node[c] = (cell + (upper ? 1 : 0)) / (double)(CmykGrid - 1);
            }

            var stored = Quantize16(LegacyModel(node));
            for (var k = 0; k < result.Length; k++)
            {
                result[k] += weight * stored[k];
            }
        }

        return result;
    }

    /// <summary>Generates a repeatable number from 0 to 1.</summary>
    /// <param name="index">The point index.</param>
    /// <param name="axis">The axis.</param>
    /// <returns>The number.</returns>
    private static double Unit(int index, int axis) => (uint)(((index * GeneratorStride) + axis) * GeneratorMultiplier) / (double)uint.MaxValue;

    /// <summary>Converts a colour with the space and gets red, green and blue.</summary>
    /// <param name="space">The space.</param>
    /// <param name="input">The components.</param>
    /// <returns>Red, green and blue from 0 to 1.</returns>
    private static float[] Convert(PdfColorSpace space, double[] input)
    {
        var rgb = new float[RgbComponents];
        space.ToRgb([.. input.Select(static v => (float)v)], rgb);
        return rgb;
    }

    /// <summary>Gets the largest per-channel difference, in 8-bit steps, between a conversion and the reference.</summary>
    /// <param name="space">The space.</param>
    /// <param name="input">The components.</param>
    /// <param name="expected">The expected red, green and blue from 0 to 1.</param>
    /// <returns>The difference.</returns>
    private static double MaxDifference(PdfColorSpace space, double[] input, double[] expected)
    {
        var rgb = Convert(space, input);
        return Enumerable.Range(0, RgbComponents).Max(i => Math.Abs((rgb[i] - expected[i]) * ByteScale));
    }

    /// <summary>Checks that a corrupt CMYK profile converts like DeviceCMYK, the alternate, and does not throw.</summary>
    /// <param name="profile">The profile bytes.</param>
    /// <returns>A task.</returns>
    private static async Task AssertFallsBack(byte[] profile)
    {
        var space = IccSpace(profile, CmykComponents);
        var expected = new float[RgbComponents];
        var actual = new float[RgbComponents];
        space.ToRgb(CorruptProbe, actual);
        PdfColorSpace.DeviceCmyk.ToRgb(CorruptProbe, expected);

        await Assert.That(space.Components).IsEqualTo(CmykComponents);
        await Assert.That(actual).IsEquivalentTo(expected);
    }

    /// <summary>Determines whether an ICCBased space converts through a look-up table rather than its alternate.</summary>
    /// <param name="space">The space.</param>
    /// <returns><see langword="true"/> when it differs from DeviceCMYK for a mid colour.</returns>
    private static bool IsLookUp(PdfColorSpace space)
    {
        var icc = Convert(space, TableProbe);
        var device = Convert(PdfColorSpace.DeviceCmyk, TableProbe);
        return icc.Zip(device, static (a, b) => Math.Abs(a - b)).Max() > TableDifference;
    }

    /// <summary>Lists ICC profiles in the usual system folders.</summary>
    /// <returns>The paths, or none when the folders do not exist.</returns>
    private static IEnumerable<string> FindSystemProfiles()
    {
        string[] folders = ["/usr/share/color/icc", "/usr/share/ghostscript/iccprofiles", "/usr/share/texlive/texmf-dist/tex/generic/colorprofiles"];
        foreach (var folder in folders.Where(Directory.Exists))
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*.ic*", SearchOption.AllDirectories))
            {
                yield return path;
            }
        }
    }

    /// <summary>Creates an ICCBased colour space from profile bytes.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="components">The /N value.</param>
    /// <returns>The space.</returns>
    private static PdfColorSpace IccSpace(byte[] profile, int components)
    {
        var stream = new PdfStream(FixTestHelpers.Dictionary(KnownName.N, PdfValue.FromInteger(components)), profile);
        return PdfColorSpace.Parse(FixTestHelpers.Array(FixTestHelpers.Name(KnownName.ICCBased), PdfValue.FromStream(stream)), null);
    }
}
