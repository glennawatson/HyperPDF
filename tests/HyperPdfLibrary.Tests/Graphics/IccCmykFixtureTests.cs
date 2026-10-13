// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;
using static HyperPdfLibrary.Tests.Graphics.IccCmykFixture;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Checks the generated CMYK fixture profiles against the ink model that defines them, without any host profile.</summary>
public sealed class IccCmykFixtureTests
{
    /// <summary>
    /// The largest per-channel difference, in 8-bit steps, between a conversion and the ink model. It covers the 16-bit
    /// table rounding, the table interpolation between nodes and the library's colour conversion sampling. The worst
    /// differences measured were about 1.1 steps for the 17-point 'mft2' tables and 2.5 steps for the 9-point 'mAB ' tables.
    /// </summary>
    private const double ChannelTolerance = 3.0;

    /// <summary>The number of generated points compared with the model.</summary>
    private const int SamplePoints = 500;

    /// <summary>The scale from a 0 to 1 value to a byte.</summary>
    private const double ByteScale = 255.0;

    /// <summary>The components of RGB.</summary>
    private const int RgbComponents = 3;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The multiplier of the point generator.</summary>
    private const uint GeneratorMultiplier = 2_654_435_761;

    /// <summary>The step between the generator inputs of consecutive points.</summary>
    private const int GeneratorStride = 31;

    /// <summary>The calls made before measuring allocations.</summary>
    private const int Warmup = 20;

    /// <summary>The pixels converted by the allocation test.</summary>
    private const int Pixels = 256;

    /// <summary>The step between the sample values of the allocation test.</summary>
    private const int SampleStride = 7;

    /// <summary>The smallest difference between intents that shows they use different tables, in 8-bit steps.</summary>
    private const double IntentGap = 10.0;

    /// <summary>The offset of the red byte in a BGRA pixel.</summary>
    private const int RedByte = 2;

    /// <summary>The offset of the green byte in a BGRA pixel.</summary>
    private const int GreenByte = 1;

    /// <summary>The axis of the yellow ink.</summary>
    private const int YellowAxis = 2;

    /// <summary>The axis of the black ink.</summary>
    private const int BlackAxis = 3;

    /// <summary>The ink mixes compared at fixed positions: corners, single inks, greys and mixed tints.</summary>
    private static readonly double[][] Mixes =
    [
        [0, 0, 0, 0],
        [1, 0, 0, 0],
        [0, 1, 0, 0],
        [0, 0, 1, 0],
        [0, 0, 0, 1],
        [1, 1, 0, 0],
        [0, 1, 1, 0],
        [1, 0, 1, 0],
        [1, 1, 1, 1],
        [0.5, 0.5, 0.5, 0],
        [0.25, 0.75, 0.5, 0.25],
        [0.1, 0.2, 0.3, 0.4],
        [0.9, 0.05, 0.6, 0.15],
    ];

    /// <summary>The 'mft2' profile converts every sampled mix like the model.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Mft2ProfileMatchesInkModel()
    {
        var space = IccSpace(BuildMft2()).ForRendering(new(IccIntent.RelativeColorimetric, false));

        await Assert.That(WorstDifference(space, Relative)).IsLessThanOrEqualTo(ChannelTolerance);
    }

    /// <summary>The 'mAB ' profile converts every sampled mix like the model.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MabProfileMatchesInkModel()
    {
        var space = IccSpace(BuildMab()).ForRendering(new(IccIntent.RelativeColorimetric, false));

        await Assert.That(WorstDifference(space, Relative)).IsLessThanOrEqualTo(ChannelTolerance);
    }

    /// <summary>Each intent reads its own table: A2B0 perceptual, A2B1 relative and A2B2 saturation, for both table types.</summary>
    /// <param name="v4">Whether to use the 'mAB ' profile.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task IntentsReadTheirOwnTables(bool v4)
    {
        var space = IccSpace(v4 ? BuildMab() : BuildMft2());
        var perceptualSpace = space.ForRendering(new(IccIntent.Perceptual, false));
        var relativeSpace = space.ForRendering(new(IccIntent.RelativeColorimetric, false));

        await Assert.That(WorstDifference(perceptualSpace, Perceptual)).IsLessThanOrEqualTo(ChannelTolerance);
        await Assert.That(WorstDifference(relativeSpace, Relative)).IsLessThanOrEqualTo(ChannelTolerance);
        await Assert.That(WorstDifference(space.ForRendering(new(IccIntent.Saturation, false)), Saturation)).IsLessThanOrEqualTo(ChannelTolerance);

        // Full black is where the perceptual and relative models differ most.
        var perceptual = Convert(perceptualSpace, [0, 0, 0, 1]);
        var relative = Convert(relativeSpace, [0, 0, 0, 1]);
        await Assert.That((perceptual[0] - relative[0]) * ByteScale).IsGreaterThan(IntentGap);
    }

    /// <summary>A byte row converts like the model, in both table types.</summary>
    /// <param name="v4">Whether to use the 'mAB ' profile.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RowConversionMatchesInkModel(bool v4)
    {
        var space = IccSpace(v4 ? BuildMab() : BuildMft2()).ForRendering(new(IccIntent.RelativeColorimetric, false));
        var samples = new byte[Mixes.Length * Components];
        for (var p = 0; p < Mixes.Length; p++)
        {
            for (var c = 0; c < Components; c++)
            {
                samples[(p * Components) + c] = (byte)Math.Round(Mixes[p][c] * ByteScale);
            }
        }

        var row = new byte[Mixes.Length * BytesPerPixel];
        space.ConvertRow(samples, row, Mixes.Length);
        var worst = 0.0;
        for (var p = 0; p < Mixes.Length; p++)
        {
            var mix = new double[Components];
            for (var c = 0; c < Components; c++)
            {
                mix[c] = samples[(p * Components) + c] / ByteScale;
            }

            var expected = Rgb(Relative, mix);
            worst = Math.Max(worst, Math.Abs(row[(p * BytesPerPixel) + RedByte] - (expected[0] * ByteScale)));
            worst = Math.Max(worst, Math.Abs(row[(p * BytesPerPixel) + GreenByte] - (expected[1] * ByteScale)));
            worst = Math.Max(worst, Math.Abs(row[p * BytesPerPixel] - (expected[RgbComponents - 1] * ByteScale)));
        }

        await Assert.That(worst).IsLessThanOrEqualTo(ChannelTolerance);
    }

    /// <summary>The fixture bytes are repeatable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FixtureIsRepeatable()
    {
        await Assert.That(BuildMft2().AsSpan().SequenceEqual(BuildMft2())).IsTrue();
        await Assert.That(BuildMab().AsSpan().SequenceEqual(BuildMab())).IsTrue();
    }

    /// <summary>Warm conversion through the 17-point 'mft2' and 9-point 'mAB ' tables allocates nothing.</summary>
    /// <param name="v4">Whether to use the 'mAB ' profile.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task WarmConversionDoesNotAllocate(bool v4)
    {
        var space = IccSpace(v4 ? BuildMab() : BuildMft2());
        var samples = new byte[Pixels * Components];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (byte)(i * SampleStride);
        }

        var bgra = new byte[Pixels * BytesPerPixel];
        for (var i = 0; i < Warmup; i++)
        {
            space.ConvertRow(samples, bgra, Pixels);
        }

        var allocated = AllocationProbe.Minimum(() => space.ConvertRow(samples, bgra, Pixels));

        await Assert.That(allocated).IsEqualTo(0L);
    }

    /// <summary>Gets the largest difference, in 8-bit steps, between a space and the model over the fixed and generated mixes.</summary>
    /// <param name="space">The space.</param>
    /// <param name="model">The ink model.</param>
    /// <returns>The difference.</returns>
    private static double WorstDifference(PdfColorSpace space, in InkModel model)
    {
        var worst = 0.0;
        foreach (var mix in Mixes)
        {
            worst = Math.Max(worst, Difference(space, model, mix));
        }

        for (var i = 0; i < SamplePoints; i++)
        {
            double[] mix = [Unit(i, 0), Unit(i, 1), Unit(i, YellowAxis), Unit(i, BlackAxis)];
            worst = Math.Max(worst, Difference(space, model, mix));
        }

        return worst;
    }

    /// <summary>Gets the largest per-channel difference at one mix.</summary>
    /// <param name="space">The space.</param>
    /// <param name="model">The ink model.</param>
    /// <param name="mix">The ink mix.</param>
    /// <returns>The difference in 8-bit steps.</returns>
    private static double Difference(PdfColorSpace space, in InkModel model, double[] mix)
    {
        var expected = Rgb(model, mix);
        var rgb = Convert(space, mix);
        return Enumerable.Range(0, RgbComponents).Max(i => Math.Abs((rgb[i] - expected[i]) * ByteScale));
    }

    /// <summary>Generates a repeatable number from 0 to 1.</summary>
    /// <param name="index">The point index.</param>
    /// <param name="axis">The axis.</param>
    /// <returns>The number.</returns>
    private static double Unit(int index, int axis) => (uint)(((index * GeneratorStride) + axis) * GeneratorMultiplier) / (double)uint.MaxValue;

    /// <summary>Converts a colour with the space.</summary>
    /// <param name="space">The space.</param>
    /// <param name="input">The components.</param>
    /// <returns>Red, green and blue from 0 to 1.</returns>
    private static float[] Convert(PdfColorSpace space, double[] input)
    {
        var rgb = new float[RgbComponents];
        space.ToRgb([.. input.Select(static v => (float)v)], rgb);
        return rgb;
    }

    /// <summary>Creates an ICCBased colour space from profile bytes.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The space.</returns>
    private static PdfColorSpace IccSpace(byte[] profile)
    {
        var stream = new PdfStream(FixTestHelpers.Dictionary(KnownName.N, PdfValue.FromInteger(Components)), profile);
        return PdfColorSpace.Parse(FixTestHelpers.Array(FixTestHelpers.Name(KnownName.ICCBased), PdfValue.FromStream(stream)), null);
    }
}
