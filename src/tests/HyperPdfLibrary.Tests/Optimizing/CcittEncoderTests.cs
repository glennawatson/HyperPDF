// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Round trips through <see cref="CcittEncoder"/> and <see cref="CcittFaxDecoder"/>.</summary>
public sealed partial class CcittEncoderTests
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The /K value of Group 4.</summary>
    private const int Group4 = -1;

    /// <summary>A width longer than the longest make-up code, so runs need several codes.</summary>
    private const int WideWidth = 6001;

    /// <summary>The seed of the random images.</summary>
    private const uint Seed = 77;

    /// <summary>The percent of black pixels in the sparse random image.</summary>
    private const uint SparsePercent = 3;

    /// <summary>One hundred percent.</summary>
    private const uint Hundred = 100;

    /// <summary>A prime that scatters x into the noise hash.</summary>
    private const uint NoiseX = 73_856_093;

    /// <summary>A prime that scatters y into the noise hash.</summary>
    private const uint NoiseY = 19_349_663;

    /// <summary>The shift that mixes the noise hash's high bits down.</summary>
    private const int NoiseShift = 13;

    /// <summary>The width of a stroke in the stroke pattern.</summary>
    private const int StrokeWidth = 3;

    /// <summary>The spacing of the stroke pattern.</summary>
    private const int StrokeSpacing = 11;

    /// <summary>A byte with every bit set.</summary>
    private const int FullByte = 0xFF;

    /// <summary>The kinds of image tried.</summary>
    public enum Pattern
    {
        /// <summary>Every pixel white.</summary>
        White = 0,

        /// <summary>Every pixel black.</summary>
        Black = 1,

        /// <summary>Alternating pixels.</summary>
        Checker = 2,

        /// <summary>Random pixels, mostly white.</summary>
        Sparse = 3,

        /// <summary>Random pixels, half black.</summary>
        Noise = 4,

        /// <summary>Diagonal strokes like text.</summary>
        Strokes = 5,
    }

    /// <summary>Encoding then decoding gives back the same bits, for any width, either polarity and every kind of image.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="pattern">The image.</param>
    /// <param name="blackIs1">Whether 1 bits are black.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, 1, Pattern.White, false)]
    [Arguments(7, 3, Pattern.Black, false)]
    [Arguments(13, 9, Pattern.Checker, true)]
    [Arguments(64, 40, Pattern.Sparse, false)]
    [Arguments(203, 57, Pattern.Noise, true)]
    [Arguments(257, 64, Pattern.Strokes, false)]
    [Arguments(WideWidth, 4, Pattern.Black, false)]
    [Arguments(WideWidth, 4, Pattern.Sparse, true)]
    public async Task RoundTripsThroughTheDecoder(int width, int height, Pattern pattern, bool blackIs1)
    {
        var rows = Make(width, height, pattern);
        var decoded = RoundTrip(rows, width, height, blackIs1);

        await Assert.That(decoded).IsEquivalentTo(rows);
    }

    /// <summary>Text-like images compress far below their packed size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompressesStrokes()
    {
        const int Width = 800;
        const int Height = 600;
        const int Ratio = 3;
        var rows = Make(Width, Height, Pattern.Strokes);
        var encoded = Encode(rows, Width, Height, false);

        await Assert.That(encoded.Length * Ratio).IsLessThan(rows.Length);
    }

    /// <summary>Makes packed rows of a pattern.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="pattern">The pattern.</param>
    /// <returns>The rows, with padding bits clear.</returns>
    private static byte[] Make(int width, int height, Pattern pattern)
    {
        var stride = (width + ByteBits - 1) / ByteBits;
        var rows = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!IsSet(pattern, x, y))
                {
                    continue;
                }

                rows[(y * stride) + (x / ByteBits)] |= (byte)(1 << (ByteBits - 1 - (x % ByteBits)));
            }
        }

        return rows;
    }

    /// <summary>Decides one pixel of a pattern.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns><see langword="true"/> for a 1 bit.</returns>
    private static bool IsSet(Pattern pattern, int x, int y) => pattern switch
    {
        Pattern.Black => true,
        Pattern.Checker => ((x + y) & 1) == 0,
        Pattern.Sparse => Noise(x, y) % Hundred < SparsePercent,
        Pattern.Noise => (Noise(x, y) & 1) == 0,
        Pattern.Strokes => (x + (y / StrokeWidth)) % StrokeSpacing < StrokeWidth,
        _ => false,
    };

    /// <summary>Gets a repeatable pseudo-random value for a pixel.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The value.</returns>
    private static uint Noise(int x, int y)
    {
        var value = ((uint)x * NoiseX) ^ ((uint)y * NoiseY) ^ Seed;
        value ^= value >> NoiseShift;
        return value * NoiseX;
    }

    /// <summary>Encodes rows.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="blackIs1">Whether 1 bits are black.</param>
    /// <returns>The Group 4 data.</returns>
    private static byte[] Encode(byte[] rows, int width, int height, bool blackIs1)
    {
        var output = default(PooledBuffer);
        try
        {
            CcittEncoder.EncodeGroup4(rows, width, height, blackIs1, ref output, new(), CancellationToken.None);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Encodes and decodes rows.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="blackIs1">Whether 1 bits are black.</param>
    /// <returns>The decoded rows.</returns>
    private static byte[] RoundTrip(byte[] rows, int width, int height, bool blackIs1)
    {
        var encoded = Encode(rows, width, height, blackIs1);
        var decoded = new byte[rows.Length];
        CcittFaxDecoder.Decode(encoded, new(Group4, width, height, blackIs1, false, false, true), width, height, decoded);
        var stride = (width + ByteBits - 1) / ByteBits;
        var padding = (stride * ByteBits) - width;
        if (padding > 0)
        {
            // The decoder fills a row's padding bits with white; the source left them clear.
            var mask = (byte)(FullByte << padding);
            for (var y = 0; y < height; y++)
            {
                decoded[(y * stride) + stride - 1] &= mask;
            }
        }

        return decoded;
    }
}
