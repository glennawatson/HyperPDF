// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>
/// Tests for the DeviceCMYK polynomial: it matches a regeneration from the Neugebauer model, keeps paper white exact, stays
/// close to the model and monotonic per ink, and every vector width gives the scalar bytes.
/// </summary>
public sealed class CmykConverterTests
{
    /// <summary>The components of a CMYK pixel.</summary>
    private const int CmykComponents = 4;

    /// <summary>The index of yellow in a CMYK pixel.</summary>
    private const int YellowIndex = 2;

    /// <summary>The index of black in a CMYK pixel.</summary>
    private const int BlackIndex = 3;

    /// <summary>The index of red in a BGRA pixel.</summary>
    private const int RedByte = 2;

    /// <summary>The index of alpha in a BGRA pixel.</summary>
    private const int AlphaByte = 3;

    /// <summary>The index of blue in an RGB pixel.</summary>
    private const int BlueRgbByte = 2;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The bytes of an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The longest row of the width tests.</summary>
    private const int LongestRow = 70;

    /// <summary>The largest offset into the buffers of the width tests.</summary>
    private const int LargestOffset = 3;

    /// <summary>The pixels of the row test.</summary>
    private const int RowPixels = 150;

    /// <summary>The largest allowed difference between the polynomial in the library and a regeneration, as a fraction of full scale.</summary>
    private const double RegenerationTolerance = 0.001;

    /// <summary>The largest mean CIEDE2000 difference between the polynomial and the model on the check grid.</summary>
    private const double MeanDifferenceLimit = 0.5;

    /// <summary>The largest CIEDE2000 difference between the polynomial and the model on the check grid.</summary>
    private const double MaxDifferenceLimit = 2;

    /// <summary>The largest channel error against the model, in bytes.</summary>
    private const double ChannelErrorLimit = 2.5;

    /// <summary>The check grid points per ink.</summary>
    private const int CheckGrid = 9;

    /// <summary>The check grid points in all.</summary>
    private const int CheckPoints = CheckGrid * CheckGrid * CheckGrid * CheckGrid;

    /// <summary>The byte scale.</summary>
    private const double ByteScale = 255;

    /// <summary>The seed of the pixel generator.</summary>
    private const uint Seed = 7;

    /// <summary>The multiplier of the pixel generator.</summary>
    private const uint Multiplier = 1_664_525;

    /// <summary>The increment of the pixel generator.</summary>
    private const uint Increment = 1_013_904_223;

    /// <summary>The shift that takes well-mixed bits from the generator.</summary>
    private const int ByteShift = 16;

    /// <summary>The tolerance of the monotonic check in bytes: the polynomial may rise this much between grid points.</summary>
    private const double MonotonicSlack = 0.5;

    /// <summary>The lane widths whose output must equal the scalar output.</summary>
    private static readonly CmykLaneWidth[] Widths = [CmykLaneWidth.Vector128, CmykLaneWidth.Vector256, CmykLaneWidth.Vector512, CmykLaneWidth.Widest];

    /// <summary>The committed polynomial matches a fresh fit of the model.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CommittedPolynomialMatchesRegeneration() =>
        await Assert.That(WorstRegenerationError()).IsLessThan(RegenerationTolerance);

    /// <summary>The polynomial stays close to the model it was fitted to, in channel values and in CIEDE2000.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PolynomialStaysCloseToTheModel()
    {
        var error = ModelError();
        await Assert.That(error.Mean).IsLessThan(MeanDifferenceLimit);
        await Assert.That(error.Max).IsLessThan(MaxDifferenceLimit);
        await Assert.That(error.Channel).IsLessThan(ChannelErrorLimit);
    }

    /// <summary>No ink gives paper white to the byte, and solid black and the primaries land near the model colours.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PaperWhiteIsExactAndPrimariesAreNearTheirColours()
    {
        await Assert.That(CmykConverter.ToBgra(0, 0, 0, 0)).IsEqualTo(0xFFFF_FFFFU);
        AssertNear(CmykConverter.ToBgra(byte.MaxValue, 0, 0, 0), CmykNeugebauerModel.ToSrgb(1, 0, 0, 0));
        AssertNear(CmykConverter.ToBgra(0, byte.MaxValue, 0, 0), CmykNeugebauerModel.ToSrgb(0, 1, 0, 0));
        AssertNear(CmykConverter.ToBgra(0, 0, byte.MaxValue, 0), CmykNeugebauerModel.ToSrgb(0, 0, 1, 0));
        AssertNear(CmykConverter.ToBgra(0, 0, 0, byte.MaxValue), CmykNeugebauerModel.ToSrgb(0, 0, 0, 1));
        await Assert.That(CmykConverter.ToBgra(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue) & 0x00FF_FFFF).IsLessThanOrEqualTo(0x0004_0404U);
    }

    /// <summary>Every ink only darkens: along each ink axis no channel rises by more than a fraction of a byte.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EachInkOnlyDarkens() =>
        await Assert.That(WorstRise()).IsLessThan(MonotonicSlack);

    /// <summary>Every lane width, at every length from 0 to 70 and at unaligned offsets, writes the bytes the scalar path writes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryWidthMatchesScalar() =>
        await Assert.That(CountWidthMismatches()).IsEqualTo(0);

    /// <summary>Row output equals the per-pixel conversion, with opaque alpha, and RGB rows equal the BGRA rows without alpha.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RowsAgreeWithPixelsAndRgbRows() =>
        await Assert.That(CountRowMismatches()).IsEqualTo(0);

    /// <summary>Checks a BGRA word against an expected colour within a few bytes.</summary>
    /// <param name="word">The little-endian BGRA word.</param>
    /// <param name="expected">The colour the model gives.</param>
    /// <exception cref="InvalidOperationException">The word is further from the colour than the slack.</exception>
    private static void AssertNear(uint word, CmykSrgb expected)
    {
        const int Slack = 3;
        var red = (int)Math.Round(expected.Red * ByteScale);
        var green = (int)Math.Round(expected.Green * ByteScale);
        var blue = (int)Math.Round(expected.Blue * ByteScale);
        var actualRed = (int)((word >> ByteShift) & byte.MaxValue);
        var actualGreen = (int)((word >> ByteBits) & byte.MaxValue);
        var actualBlue = (int)(word & byte.MaxValue);
        if (Math.Abs(actualRed - red) > Slack || Math.Abs(actualGreen - green) > Slack || Math.Abs(actualBlue - blue) > Slack)
        {
            throw new InvalidOperationException($"Expected about {red},{green},{blue} but got {actualRed},{actualGreen},{actualBlue}.");
        }
    }

    /// <summary>Gets the largest difference between the library polynomial and a regeneration over the check grid.</summary>
    /// <returns>The largest channel difference, as a fraction of full scale.</returns>
    private static double WorstRegenerationError()
    {
        var fit = CmykPolynomialFit.Fit();
        Span<float> rgb = stackalloc float[RgbBytes];
        var worst = 0.0;
        for (var index = 0; index < CheckPoints; index++)
        {
            var c = CmykPolynomialFit.GridInk(index, 0, CheckGrid);
            var m = CmykPolynomialFit.GridInk(index, 1, CheckGrid);
            var y = CmykPolynomialFit.GridInk(index, CmykPolynomialFit.YellowInk, CheckGrid);
            var k = CmykPolynomialFit.GridInk(index, CmykPolynomialFit.BlackInk, CheckGrid);
            CmykConverter.ToRgb((float)c, (float)m, (float)y, (float)k, rgb);
            for (var channel = 0; channel < RgbBytes; channel++)
            {
                var expected = Math.Clamp(CmykPolynomialFit.Evaluate(fit[channel], c, m, y, k), 0, 1);
                worst = Math.Max(worst, Math.Abs(expected - rgb[channel]));
            }
        }

        return worst;
    }

    /// <summary>Measures the polynomial against the model over the check grid.</summary>
    /// <returns>The mean and largest CIEDE2000 difference and the largest channel error in bytes.</returns>
    private static CmykModelError ModelError()
    {
        Span<float> rgb = stackalloc float[RgbBytes];
        double sumDifference = 0;
        double maxDifference = 0;
        double maxChannel = 0;
        for (var index = 0; index < CheckPoints; index++)
        {
            var c = CmykPolynomialFit.GridInk(index, 0, CheckGrid);
            var m = CmykPolynomialFit.GridInk(index, 1, CheckGrid);
            var y = CmykPolynomialFit.GridInk(index, CmykPolynomialFit.YellowInk, CheckGrid);
            var k = CmykPolynomialFit.GridInk(index, CmykPolynomialFit.BlackInk, CheckGrid);
            CmykConverter.ToRgb((float)c, (float)m, (float)y, (float)k, rgb);
            var model = CmykNeugebauerModel.ToSrgb(c, m, y, k);
            var difference = CmykColorDifference.Between(model, new(rgb[0], rgb[1], rgb[2]));
            sumDifference += difference;
            maxDifference = Math.Max(maxDifference, difference);
            maxChannel = Math.Max(maxChannel, Math.Max(Math.Abs(model.Red - rgb[0]), Math.Max(Math.Abs(model.Green - rgb[1]), Math.Abs(model.Blue - rgb[2]))) * ByteScale);
        }

        return new(sumDifference / CheckPoints, maxDifference, maxChannel);
    }

    /// <summary>Gets the largest rise of any channel, in bytes, when one ink is increased by one grid step.</summary>
    /// <returns>The largest rise.</returns>
    private static double WorstRise()
    {
        Span<float> before = stackalloc float[RgbBytes];
        Span<float> after = stackalloc float[RgbBytes];
        var worst = 0.0;
        for (var index = 0; index < CheckPoints; index++)
        {
            float[] ink =
            [
                (float)CmykPolynomialFit.GridInk(index, 0, CheckGrid),
                (float)CmykPolynomialFit.GridInk(index, 1, CheckGrid),
                (float)CmykPolynomialFit.GridInk(index, CmykPolynomialFit.YellowInk, CheckGrid),
                (float)CmykPolynomialFit.GridInk(index, BlackIndex, CheckGrid),
            ];
            CmykConverter.ToRgb(ink[0], ink[1], ink[2], ink[BlackIndex], before);
            for (var axis = 0; axis < CmykComponents; axis++)
            {
                var more = (float[])ink.Clone();
                more[axis] += 1F / (CheckGrid - 1F);
                if (more[axis] > 1)
                {
                    continue;
                }

                CmykConverter.ToRgb(more[0], more[1], more[2], more[BlackIndex], after);
                for (var channel = 0; channel < RgbBytes; channel++)
                {
                    worst = Math.Max(worst, (after[channel] - before[channel]) * ByteScale);
                }
            }
        }

        return worst;
    }

    /// <summary>Counts the rows where a vector width writes different bytes from the scalar path.</summary>
    /// <returns>The mismatch count.</returns>
    private static int CountWidthMismatches()
    {
        var samples = RandomBytes((LongestRow * CmykComponents) + LargestOffset);
        var expected = new byte[(LongestRow * BgraBytes) + LargestOffset];
        var actual = new byte[expected.Length];
        var mismatches = 0;
        for (var offset = 0; offset <= LargestOffset; offset++)
        {
            for (var count = 0; count <= LongestRow; count++)
            {
                var source = samples.AsSpan(offset, count * CmykComponents);
                Array.Clear(expected);
                CmykConverter.ToBgraRow(source, expected.AsSpan(offset, count * BgraBytes), count, CmykLaneWidth.Scalar);
                foreach (var width in Widths)
                {
                    Array.Clear(actual);
                    CmykConverter.ToBgraRow(source, actual.AsSpan(offset, count * BgraBytes), count, width);
                    mismatches += actual.AsSpan().SequenceEqual(expected) ? 0 : 1;
                }
            }
        }

        return mismatches;
    }

    /// <summary>Counts the pixels where the row output disagrees with the per-pixel conversion or the RGB row.</summary>
    /// <returns>The mismatch count.</returns>
    private static int CountRowMismatches()
    {
        var samples = RandomBytes(RowPixels * CmykComponents);
        var bgra = new byte[RowPixels * BgraBytes];
        var rgb = new byte[RowPixels * RgbBytes];
        CmykConverter.ToBgraRow(samples, bgra, RowPixels);
        CmykConverter.ToRgbRow(samples, rgb, RowPixels);
        var mismatches = 0;
        for (var i = 0; i < RowPixels; i++)
        {
            var ink = samples.AsSpan(i * CmykComponents, CmykComponents);
            var outBgra = bgra.AsSpan(i * BgraBytes, BgraBytes);
            var outRgb = rgb.AsSpan(i * RgbBytes, RgbBytes);
            var word = CmykConverter.ToBgra(ink[0], ink[1], ink[YellowIndex], ink[BlackIndex]);
            var agrees = BitConverter.ToUInt32(bgra, i * BgraBytes) == word
                && outBgra[AlphaByte] == byte.MaxValue
                && outRgb[0] == outBgra[RedByte]
                && outRgb[1] == outBgra[1]
                && outRgb[BlueRgbByte] == outBgra[0];
            mismatches += agrees ? 0 : 1;
        }

        return mismatches;
    }

    /// <summary>Makes deterministic pseudo-random bytes.</summary>
    /// <param name="count">The byte count.</param>
    /// <returns>The bytes.</returns>
    private static byte[] RandomBytes(int count)
    {
        var state = Seed;
        var bytes = new byte[count];
        for (var i = 0; i < count; i++)
        {
            state = (state * Multiplier) + Increment;
            bytes[i] = (byte)(state >> ByteShift);
        }

        return bytes;
    }

    /// <summary>How far the polynomial is from the model over the check grid.</summary>
    /// <param name="Mean">The mean CIEDE2000 difference.</param>
    /// <param name="Max">The largest CIEDE2000 difference.</param>
    /// <param name="Channel">The largest channel error in bytes.</param>
    private readonly record struct CmykModelError(double Mean, double Max, double Channel);
}
