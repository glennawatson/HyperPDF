// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.ImageFiles;

namespace HyperPdfLibrary.Tests.ImageFiles;

/// <summary>Checks the PNG row filters by filtering known rows the way a writer does and undoing them.</summary>
public sealed class PngUnfilterTests
{
    /// <summary>The bytes in each test row, which is not a multiple of the vector width so the tail runs.</summary>
    private const int RowBytes = 211;

    /// <summary>The multiplier of the generator that makes the rows.</summary>
    private const int Spread = 37;

    /// <summary>The offset that makes the rows differ.</summary>
    private const int RowOffset = 11;

    /// <summary>The filter type that adds the byte to the left.</summary>
    private const int SubFilter = 1;

    /// <summary>The filter type that adds the byte above.</summary>
    private const int UpFilter = 2;

    /// <summary>The filter type that adds the average of the left and upper bytes.</summary>
    private const int AverageFilter = 3;

    /// <summary>The filter type that adds the Paeth predictor.</summary>
    private const int PaethFilter = 4;

    /// <summary>The seed of the row above.</summary>
    private const int AboveSeed = 0;

    /// <summary>The seed of the row being filtered.</summary>
    private const int RowSeed = 1;

    /// <summary>The seed of the first row of a picture.</summary>
    private const int FirstRowSeed = 2;

    /// <summary>The bytes in an RGBA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The distance between the red and blue bytes of a pixel.</summary>
    private const int RedBlueGap = 2;

    /// <summary>The bytes to the pixel on the left to try.</summary>
    private static readonly int[] PixelSizes = [1, 3, 4];

    /// <summary>The pixel counts to try, which cover a single vector, a vector and a tail, and several vectors.</summary>
    private static readonly int[] PixelCounts = [1, 3, 8, 9, 37];

    /// <summary>Every filter undoes to the row it filtered, for one, three and four bytes a pixel.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryFilterUndoesToThePlainRow()
    {
        var prior = MakeRow(AboveSeed);
        var plain = MakeRow(RowSeed);
        foreach (var size in PixelSizes)
        {
            for (var filter = 0; filter <= PaethFilter; filter++)
            {
                var line = Filter(filter, plain, prior, size);

                PngUnfilter.Apply(filter, line, prior, size);

                await Assert.That(line).IsEquivalentTo(plain);
            }
        }
    }

    /// <summary>The first row, which has zeros above it, undoes too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheFirstRowUndoesAgainstZeros()
    {
        var zeros = new byte[RowBytes];
        var plain = MakeRow(FirstRowSeed);
        foreach (var size in PixelSizes)
        {
            for (var filter = 0; filter <= PaethFilter; filter++)
            {
                var line = Filter(filter, plain, zeros, size);

                PngUnfilter.Apply(filter, line, zeros, size);

                await Assert.That(line).IsEquivalentTo(plain);
            }
        }
    }

    /// <summary>Swapping red and blue turns RGBA bytes into BGRA bytes for any pixel count.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RedAndBlueSwap()
    {
        foreach (var pixels in PixelCounts)
        {
            var rgba = new byte[pixels * PixelBytes];
            for (var i = 0; i < rgba.Length; i++)
            {
                rgba[i] = (byte)((i * Spread) + RowOffset);
            }

            var expected = (byte[])rgba.Clone();
            for (var i = 0; i < expected.Length; i += PixelBytes)
            {
                (expected[i], expected[i + RedBlueGap]) = (expected[i + RedBlueGap], expected[i]);
            }

            PngEightBit.SwapRedAndBlue(rgba);

            await Assert.That(rgba).IsEquivalentTo(expected);
        }
    }

    /// <summary>Makes a row of bytes that differs from every other seed's row.</summary>
    /// <param name="seed">Which row.</param>
    /// <returns>The row.</returns>
    private static byte[] MakeRow(int seed)
    {
        var row = new byte[RowBytes];
        for (var i = 0; i < row.Length; i++)
        {
            row[i] = (byte)((i * Spread) + (seed * RowOffset) + (i / (seed + FirstRowSeed)));
        }

        return row;
    }

    /// <summary>Filters a row the way the PNG specification says a writer does.</summary>
    /// <param name="filter">The filter type.</param>
    /// <param name="plain">The row.</param>
    /// <param name="prior">The plain row above.</param>
    /// <param name="size">The bytes to the pixel on the left.</param>
    /// <returns>The filtered row.</returns>
    private static byte[] Filter(int filter, byte[] plain, byte[] prior, int size)
    {
        var result = new byte[plain.Length];
        for (var i = 0; i < plain.Length; i++)
        {
            var left = i >= size ? (int)plain[i - size] : 0;
            var upper = (int)prior[i];
            var upperLeft = i >= size ? (int)prior[i - size] : 0;
            var predicted = filter switch
            {
                SubFilter => left,
                UpFilter => upper,
                AverageFilter => (left + upper) / UpFilter,
                PaethFilter => Paeth(left, upper, upperLeft),
                _ => 0,
            };
            result[i] = (byte)(plain[i] - predicted);
        }

        return result;
    }

    /// <summary>Computes the Paeth predictor as the PNG specification defines it.</summary>
    /// <param name="a">The byte to the left.</param>
    /// <param name="b">The byte above.</param>
    /// <param name="c">The byte above and to the left.</param>
    /// <returns>The prediction.</returns>
    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc)
        {
            return a;
        }

        return pb <= pc ? b : c;
    }
}
