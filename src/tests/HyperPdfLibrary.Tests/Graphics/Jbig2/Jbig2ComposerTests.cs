// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Tests.Graphics.Jpeg;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>Tests the bitmap combination operators against a pixel-by-pixel reference.</summary>
public sealed class Jbig2ComposerTests
{
    /// <summary>The random cases per operator.</summary>
    private const int Cases = 60;

    /// <summary>The widest random bitmap, wide enough for the vector paths.</summary>
    private const int MaxWidth = 700;

    /// <summary>The tallest random bitmap.</summary>
    private const int MaxHeight = 9;

    /// <summary>How far outside the target an offset may reach.</summary>
    private const int Margin = 40;

    /// <summary>The seed of the random cases.</summary>
    private const int Seed = 4242;

    /// <summary>The number of operators.</summary>
    private const int Operators = 5;

    /// <summary>The bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The outcomes of a coin toss.</summary>
    private const int Coin = 2;

    /// <summary>Every operator gives the reference result at every alignment, including offsets outside the target.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OperatorsMatchReference()
    {
        var random = new JpegTestRandom(Seed);
        var failures = 0;
        for (var op = 0; op < Operators; op++)
        {
            for (var i = 0; i < Cases; i++)
            {
                failures += ComposeMatches(random, (Jbig2ComposeOperator)op) ? 0 : 1;
            }
        }

        await Assert.That(failures).IsEqualTo(0);
    }

    /// <summary>Inverting a copy flips every bit, through the vector paths and the tail.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopyInvertedFlipsEveryBit()
    {
        var random = new JpegTestRandom(Seed);
        var source = new byte[MaxWidth + 1];
        for (var i = 0; i < source.Length; i++)
        {
            source[i] = (byte)random.Next(0, byte.MaxValue + 1);
        }

        var destination = new byte[source.Length];

        Jbig2Composer.CopyInverted(source, destination);

        var flipped = true;
        for (var i = 0; i < source.Length; i++)
        {
            flipped &= destination[i] == (byte)~source[i];
        }

        await Assert.That(flipped).IsTrue();
    }

    /// <summary>Composes random bitmaps and compares the target with a pixel-by-pixel reference.</summary>
    /// <param name="random">The source of the case.</param>
    /// <param name="operation">The operator.</param>
    /// <returns><see langword="true"/> when every pixel matches.</returns>
    private static bool ComposeMatches(JpegTestRandom random, Jbig2ComposeOperator operation)
    {
        using var target = RandomBitmap(random);
        using var source = RandomBitmap(random);
        var x = random.Next(-source.Width - Margin, target.Width + Margin);
        var y = random.Next(-source.Height - 1, target.Height + 1);
        var expected = new int[target.Width * target.Height];
        for (var row = 0; row < target.Height; row++)
        {
            for (var column = 0; column < target.Width; column++)
            {
                var inside = column >= x && column < x + source.Width && row >= y && row < y + source.Height;
                var before = target.GetPixel(column, row);
                expected[(row * target.Width) + column] = inside ? Combine(operation, source.GetPixel(column - x, row - y), before) : before;
            }
        }

        _ = Jbig2Composer.Compose(target, source.View, x, y, operation);
        return Matches(target, expected);
    }

    /// <summary>Checks a bitmap's pixels, and that its padding bits stay clear.</summary>
    /// <param name="bitmap">The bitmap.</param>
    /// <param name="expected">The expected pixels.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool Matches(Jbig2Bitmap bitmap, int[] expected)
    {
        for (var row = 0; row < bitmap.Height; row++)
        {
            for (var column = 0; column < bitmap.Stride * BitsPerByte; column++)
            {
                var pixel = (bitmap.Row(row)[column >> Jbig2Bits.ByteShift] >> (Jbig2Bits.BitMask - (column & Jbig2Bits.BitMask))) & 1;
                var want = column < bitmap.Width ? expected[(row * bitmap.Width) + column] : 0;
                if (pixel != want)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Applies an operator to one pixel.</summary>
    /// <param name="operation">The operator.</param>
    /// <param name="source">The source pixel.</param>
    /// <param name="target">The target pixel.</param>
    /// <returns>The result.</returns>
    private static int Combine(Jbig2ComposeOperator operation, int source, int target) => operation switch
    {
        Jbig2ComposeOperator.Or => source | target,
        Jbig2ComposeOperator.And => source & target,
        Jbig2ComposeOperator.Xor => source ^ target,
        Jbig2ComposeOperator.Xnor => 1 - (source ^ target),
        _ => source,
    };

    /// <summary>Creates a bitmap of random size and pixels, with clear padding bits.</summary>
    /// <param name="random">The source of the bitmap.</param>
    /// <returns>The bitmap.</returns>
    /// <exception cref="InvalidOperationException">The bitmap could not be created, which a valid size rules out.</exception>
    private static Jbig2Bitmap RandomBitmap(JpegTestRandom random)
    {
        var bitmap = Jbig2Bitmap.Create(random.Next(1, MaxWidth), random.Next(1, MaxHeight)) ?? throw new InvalidOperationException("The bitmap size is valid.");
        for (var row = 0; row < bitmap.Height; row++)
        {
            var bytes = bitmap.Row(row);
            for (var column = 0; column < bitmap.Width; column++)
            {
                if (random.Next(0, Coin) == 1)
                {
                    Jbig2Bits.SetBlack(bytes, column);
                }
            }
        }

        return bitmap;
    }
}
