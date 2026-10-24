// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.Intrinsics;
using HyperPdf.Codecs.Video;

namespace HyperPdf.Codecs.Tests;

/// <summary>Checks the conversion of Y, Cb and Cr pictures to blue, green, red and alpha bytes.</summary>
public sealed class YuvConverterTests
{
    /// <summary>The widest row tested; it takes whole 16 pixel steps, whole 8 pixel steps and a tail.</summary>
    private const int WidestRow = 70;

    /// <summary>The offset of the seed of the Cb samples from the seed of the luma samples.</summary>
    private const int CbSeedStep = 1_000;

    /// <summary>The offset of the seed of the Cr samples from the seed of the luma samples.</summary>
    private const int CrSeedStep = 2_000;

    /// <summary>The bytes in a pixel.</summary>
    private const int BytesPerPixel = sizeof(int);

    /// <summary>The sample of white in limited range video.</summary>
    private const byte LimitedWhite = 235;

    /// <summary>The sample of black in limited range video.</summary>
    private const byte LimitedBlack = 16;

    /// <summary>The sample that has no colour.</summary>
    private const byte NoColour = 128;

    /// <summary>The value of an opaque pixel's alpha byte.</summary>
    private const byte Opaque = 255;

    /// <summary>The side of the small picture used to check white and black.</summary>
    private const int TinySide = 2;

    /// <summary>The width of the picture used for the same size check.</summary>
    private const int SameWidth = 36;

    /// <summary>The height of the picture used for the same size check.</summary>
    private const int SameHeight = 20;

    /// <summary>The width of the flat picture that is scaled.</summary>
    private const int FlatWidth = 50;

    /// <summary>The height of the flat picture that is scaled.</summary>
    private const int FlatHeight = 30;

    /// <summary>The luma sample of the flat picture.</summary>
    private const byte FlatLuma = 120;

    /// <summary>The Cb sample of the flat picture.</summary>
    private const byte FlatBlue = 100;

    /// <summary>The Cr sample of the flat picture.</summary>
    private const byte FlatRed = 150;

    /// <summary>The width of the ramp picture.</summary>
    private const int RampWidth = 64;

    /// <summary>The height of the ramp picture.</summary>
    private const int RampHeight = 16;

    /// <summary>The width the ramp picture is scaled to.</summary>
    private const int RampTarget = 16;

    /// <summary>The luma step between two columns of the ramp.</summary>
    private const int RampStep = 3;

    /// <summary>The bytes of an opaque black pixel.</summary>
    private static readonly byte[] BlackPixel = [0, 0, 0, Opaque];

    /// <summary>The bytes of an opaque white pixel.</summary>
    private static readonly byte[] WhitePixel = [Opaque, Opaque, Opaque, Opaque];

    /// <summary>Checks that rows of every width give the same pixels as the whole number code, for each set of weights.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Rows_MatchTheScalarCode()
    {
        ColorMatrix[] matrices = [ColorMatrix.Bt601Limited, ColorMatrix.Bt709Limited, ColorMatrix.Bt601Full, ColorMatrix.Bt709Full];
        for (var width = 1; width <= WidestRow; width++)
        {
            var y = RandomData.Bytes(width, width);
            var u = RandomData.Bytes(width + CbSeedStep, (width + 1) >> 1);
            var v = RandomData.Bytes(width + CrSeedStep, (width + 1) >> 1);
            foreach (var matrix in matrices)
            {
                var pixels = new int[width];
                YuvConverter.ConvertRow(y, u, v, matrix, pixels);
                for (var x = 0; x < width; x++)
                {
                    await Assert.That(pixels[x]).IsEqualTo(YuvConverter.Pixel(y[x], u[x >> 1], v[x >> 1], matrix));
                }
            }
        }
    }

    /// <summary>Checks that each vector width, when the computer has it, gives the same pixels as the whole number code for the pixels it converts.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Kernels_MatchTheScalarCode()
    {
        var y = RandomData.Bytes(1, WidestRow);
        var u = RandomData.Bytes(1 + CbSeedStep, WidestRow >> 1);
        var v = RandomData.Bytes(1 + CrSeedStep, WidestRow >> 1);
        var narrow = new int[WidestRow];
        var wide = new int[WidestRow];
        var narrowDone = Vector128.IsHardwareAccelerated ? YuvKernel128.Convert(y, u, v, ColorMatrix.Bt709Limited, narrow) : 0;
        var wideDone = Vector256.IsHardwareAccelerated ? YuvKernel256.Convert(y, u, v, ColorMatrix.Bt709Limited, wide) : 0;

        for (var x = 0; x < narrowDone; x++)
        {
            await Assert.That(narrow[x]).IsEqualTo(YuvConverter.Pixel(y[x], u[x >> 1], v[x >> 1], ColorMatrix.Bt709Limited));
        }

        for (var x = 0; x < wideDone; x++)
        {
            await Assert.That(wide[x]).IsEqualTo(YuvConverter.Pixel(y[x], u[x >> 1], v[x >> 1], ColorMatrix.Bt709Limited));
        }
    }

    /// <summary>Checks that limited range white and black come out as full white and black.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LimitedRange_WhiteAndBlack_AreFullRange()
    {
        var frame = new VideoFrame(TinySide, TinySide, 0);
        frame.Y.AsSpan().Fill(LimitedWhite);
        frame.Y[0] = LimitedBlack;
        frame.U.AsSpan().Fill(NoColour);
        frame.V.AsSpan().Fill(NoColour);
        var pixels = new byte[TinySide * TinySide * BytesPerPixel];

        YuvConverter.ToBgra(frame, ColorMatrix.Bt601Limited, pixels);

        await Assert.That(pixels.AsSpan(0, BytesPerPixel).ToArray()).IsEquivalentTo(BlackPixel);
        await Assert.That(pixels.AsSpan(BytesPerPixel, BytesPerPixel).ToArray()).IsEquivalentTo(WhitePixel);
    }

    /// <summary>Checks that a destination that is too short is refused.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ShortDestination_IsRefused()
    {
        var frame = new VideoFrame(SameWidth, SameHeight, 0);

        await Assert.That(() => YuvConverter.ToBgra(frame, ColorMatrix.Bt601Limited, new byte[(SameWidth * SameHeight * BytesPerPixel) - 1])).Throws<ArgumentException>();
    }

    /// <summary>Checks that a picture at the target size is converted as it is.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Scaler_SameSize_MatchesTheConverter()
    {
        var frame = RandomFrame(SameWidth, SameHeight, 1);
        var expected = new byte[SameWidth * SameHeight * BytesPerPixel];
        var actual = new byte[expected.Length];

        YuvConverter.ToBgra(frame, ColorMatrix.For(frame.Height), expected);
        new VideoScaler(SameWidth, SameHeight).Scale(frame, actual);

        await Assert.That(actual).IsEquivalentTo(expected);
    }

    /// <summary>Checks that a flat picture stays flat when it is made smaller or larger.</summary>
    /// <param name="width">The width of the converted picture.</param>
    /// <param name="height">The height of the converted picture.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(8, 6)]
    [Arguments(100, 60)]
    [Arguments(34, 18)]
    public async Task Scaler_FlatPicture_StaysFlat(int width, int height)
    {
        var frame = new VideoFrame(FlatWidth, FlatHeight, 0);
        frame.Y.AsSpan().Fill(FlatLuma);
        frame.U.AsSpan().Fill(FlatBlue);
        frame.V.AsSpan().Fill(FlatRed);
        var expected = YuvConverter.Pixel(FlatLuma, FlatBlue, FlatRed, ColorMatrix.Bt601Limited);
        var pixels = new byte[width * height * BytesPerPixel];

        new VideoScaler(width, height).Scale(frame, pixels);

        for (var i = 0; i < pixels.Length; i += BytesPerPixel)
        {
            await Assert.That(BitConverter.ToInt32(pixels, i)).IsEqualTo(expected);
        }
    }

    /// <summary>Checks that a left to right ramp stays a ramp when the picture is made smaller.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Scaler_Ramp_StaysIncreasing()
    {
        var frame = new VideoFrame(RampWidth, RampHeight, 0);
        for (var row = 0; row < frame.Height; row++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                frame.Y[(row * frame.Width) + x] = (byte)(LimitedBlack + (x * RampStep));
            }
        }

        frame.U.AsSpan().Fill(NoColour);
        frame.V.AsSpan().Fill(NoColour);
        var pixels = new byte[RampTarget * TinySide * BytesPerPixel];

        new VideoScaler(RampTarget, TinySide).Scale(frame, pixels);

        for (var x = 1; x < RampTarget; x++)
        {
            await Assert.That(pixels[x * BytesPerPixel]).IsGreaterThanOrEqualTo(pixels[(x - 1) * BytesPerPixel]);
        }
    }

    /// <summary>Makes a picture of random samples.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="seed">The seed.</param>
    /// <returns>The picture.</returns>
    private static VideoFrame RandomFrame(int width, int height, int seed)
    {
        var frame = new VideoFrame(width, height, 0);
        RandomData.Bytes(seed, frame.Y.Length).CopyTo(frame.Y, 0);
        RandomData.Bytes(seed + CbSeedStep, frame.U.Length).CopyTo(frame.U, 0);
        RandomData.Bytes(seed + CrSeedStep, frame.V.Length).CopyTo(frame.V, 0);
        return frame;
    }
}
