// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jbig2;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>Checks grouped bit counting against a pixel-by-pixel reference at every display reduction level.</summary>
public sealed class Jbig2DownsamplerTests
{
    /// <summary>The multiplier of the deterministic packed data.</summary>
    private const int PatternMultiplier = 73;

    /// <summary>The shift of the deterministic packed data.</summary>
    private const int PatternShift = 3;

    /// <summary>The white sample value.</summary>
    private const int White = 255;

    /// <summary>Odd edges and full bit groups produce the same coverage as the scalar reference.</summary>
    /// <param name="width">The source width.</param>
    /// <param name="height">The source height.</param>
    /// <param name="levels">The number of halvings.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(7, 5, 1)]
    [Arguments(17, 19, 1)]
    [Arguments(17, 19, 2)]
    [Arguments(17, 19, 3)]
    [Arguments(17, 19, 4)]
    [Arguments(64, 65, 4)]
    public async Task GroupedBitsMatchScalar(int width, int height, int levels)
    {
        var stride = Jbig2Bits.Stride(width);
        var rows = new byte[stride * height];
        for (var i = 0; i < rows.Length; i++)
        {
            rows[i] = (byte)((i * PatternMultiplier) ^ (i >> PatternShift));
        }

        var side = 1 << levels;
        var actual = new byte[((width + side - 1) / side) * ((height + side - 1) / side)];
        Jbig2Downsampler.Average(rows, width, height, stride, levels, actual);
        var expected = Reference(rows, width, height, stride, side);

        await Assert.That(actual.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    /// <summary>Averages one pixel at a time.</summary>
    /// <param name="rows">The packed source rows.</param>
    /// <param name="width">The source width.</param>
    /// <param name="height">The source height.</param>
    /// <param name="stride">The packed row length.</param>
    /// <param name="side">The source block side.</param>
    /// <returns>The expected gray samples.</returns>
    private static byte[] Reference(byte[] rows, int width, int height, int stride, int side)
    {
        var reducedWidth = (width + side - 1) / side;
        var reducedHeight = (height + side - 1) / side;
        var output = new byte[reducedWidth * reducedHeight];
        for (var y = 0; y < reducedHeight; y++)
        {
            for (var x = 0; x < reducedWidth; x++)
            {
                output[(y * reducedWidth) + x] = Expected(rows, width, height, stride, side, x, y);
            }
        }

        return output;
    }

    /// <summary>Averages one source block.</summary>
    /// <param name="rows">The packed source rows.</param>
    /// <param name="width">The source width.</param>
    /// <param name="height">The source height.</param>
    /// <param name="stride">The packed row length.</param>
    /// <param name="side">The source block side.</param>
    /// <param name="x">The output column.</param>
    /// <param name="y">The output row.</param>
    /// <returns>The gray sample.</returns>
    private static byte Expected(byte[] rows, int width, int height, int stride, int side, int x, int y)
    {
        var x0 = x * side;
        var x1 = Math.Min(x0 + side, width);
        var y0 = y * side;
        var y1 = Math.Min(y0 + side, height);
        var white = 0;
        for (var sourceY = y0; sourceY < y1; sourceY++)
        {
            for (var sourceX = x0; sourceX < x1; sourceX++)
            {
                white += Jbig2Bits.Get(rows.AsSpan(sourceY * stride, stride), sourceX, width);
            }
        }

        return (byte)((white * White) / ((x1 - x0) * (y1 - y0)));
    }
}
