// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Render.Managed.Raster;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that the vector path of the managed rasteriser's blend gives the bytes the one-pixel formula gives.</summary>
public sealed class ManagedSpanBlenderTests
{
    /// <summary>The shift that moves alpha to the lowest byte of a pixel.</summary>
    private const int AlphaShift = 24;

    /// <summary>The largest byte, which stands for one.</summary>
    private const uint Full = 255;

    /// <summary>The rounding term of a coverage product.</summary>
    private const uint Rounding = 128;

    /// <summary>The bits of one byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Moves the test colours away from the test pixels.</summary>
    private const int ColourOffset = 7;

    /// <summary>The number of pixels in the repeating pattern of zero, full and partial coverage.</summary>
    private const int CoveragePattern = 5;

    /// <summary>The step of the partial coverage values.</summary>
    private const int CoverageStep = 37;

    /// <summary>The step of the shape coverage values.</summary>
    private const int ShapeStep = 91;

    /// <summary>The first shape coverage value.</summary>
    private const int ShapeStart = 3;

    /// <summary>The step of the clip coverage values.</summary>
    private const int MaskStep = 53;

    /// <summary>Every this many bytes the clip is made fully covering.</summary>
    private const int FullMaskEvery = 3;

    /// <summary>The factor that spreads the test pixels over all four bytes.</summary>
    private const uint Spread = 2_654_435_761U;

    /// <summary>Putting a colour over runs of every length up to a few vectors matches the scalar result, tail included.</summary>
    /// <param name="length">The number of pixels.</param>
    /// <param name="color">The premultiplied colour.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0, 0x80402010U)]
    [Arguments(1, 0x80402010U)]
    [Arguments(3, 0x80402010U)]
    [Arguments(4, 0x80402010U)]
    [Arguments(7, 0x80402010U)]
    [Arguments(8, 0xFF00FF00U)]
    [Arguments(9, 0x01010101U)]
    [Arguments(31, 0xC0FFFFFFU)]
    [Arguments(100, 0x00000000U)]
    public async Task OverMatchesTheOnePixelFormula(int length, uint color)
    {
        var pixels = new uint[length];
        for (var i = 0; i < length; i++)
        {
            pixels[i] = ((uint)(i + 1) * Spread) | 0xFF000000U;
        }

        var expected = new uint[length];
        var keep = Full - (color >> AlphaShift);
        for (var i = 0; i < length; i++)
        {
            expected[i] = color + SpanBlender.Scale(pixels[i], keep);
        }

        SpanBlender.Over(pixels, color);

        await Assert.That(pixels.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    /// <summary>Blending a line of colours through coverage matches the one pixel formula, for every kind of coverage.</summary>
    /// <param name="length">The number of pixels.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(64)]
    [Arguments(257)]
    public async Task ShadedMatchesTheOnePixelFormula(int length)
    {
        var pixels = new uint[length];
        var source = new uint[length];
        var coverage = new byte[length];
        for (var i = 0; i < length; i++)
        {
            pixels[i] = ((uint)(i + 1) * Spread) | 0xFF000000U;
            var color = (uint)(i + ColourOffset) * Spread;
            var alpha = color >> AlphaShift;
            source[i] = SpanBlender.Scale(color, alpha) | (alpha << AlphaShift);

            // Zero, full and in between all occur.
            coverage[i] = (i % CoveragePattern) switch { 0 => 0, 1 => (byte)Full, _ => (byte)(i * CoverageStep) };
        }

        var expected = (uint[])pixels.Clone();
        for (var i = 0; i < length; i++)
        {
            if (coverage[i] == 0)
            {
                continue;
            }

            var color = coverage[i] == Full ? source[i] : SpanBlender.Scale(source[i], coverage[i]);
            expected[i] = color >> AlphaShift == Full ? color : color + SpanBlender.Scale(pixels[i], Full - (color >> AlphaShift));
        }

        SpanBlender.Shaded(pixels, coverage, source);

        await Assert.That(pixels.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    /// <summary>Multiplying two lines of coverage matches the one byte formula.</summary>
    /// <param name="length">The number of bytes.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(15)]
    [Arguments(16)]
    [Arguments(17)]
    [Arguments(300)]
    public async Task MultiplyMatchesTheOneByteFormula(int length)
    {
        var shape = new byte[length];
        var mask = new byte[length];
        for (var i = 0; i < length; i++)
        {
            shape[i] = (byte)((i * ShapeStep) + ShapeStart);
            mask[i] = (byte)((i * MaskStep) + (i % FullMaskEvery == 0 ? Full : 0));
        }

        var expected = new byte[length];
        for (var i = 0; i < length; i++)
        {
            var product = (shape[i] * (uint)mask[i]) + Rounding;
            expected[i] = (byte)((product + (product >> ByteBits)) >> ByteBits);
        }

        SpanBlender.Multiply(shape, mask);

        await Assert.That(shape.AsSpan().SequenceEqual(expected)).IsTrue();
    }
}
