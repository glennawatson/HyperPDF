// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>
/// Checks the generic refinement region decoding procedure against figures 12 to 16 of the JBIG2 standard. The test codes a
/// bitmap with a context gathered from the pixel positions the figures list, written here independently of the decoder, and
/// decodes it with the library.
/// </summary>
public sealed class Jbig2RefinementSpecTests
{
    /// <summary>The size of the test bitmaps.</summary>
    private const int Side = 24;

    /// <summary>The first size guess for the coded data.</summary>
    private const int InitialOutput = 256;

    /// <summary>The step through a list of column and row offsets.</summary>
    private const int PairStep = 2;

    /// <summary>The context bits of template 0.</summary>
    private const int Template0ContextBits = 13;

    /// <summary>The context bits of template 1.</summary>
    private const int Template1ContextBits = 10;

    /// <summary>The seed of the random bitmaps.</summary>
    private const int Seed = 1234;

    /// <summary>The share of pixels, out of ten, that are black.</summary>
    private const int BlackTenths = 4;

    /// <summary>The ten values a random pixel is drawn from.</summary>
    private const int Tenths = 10;

    /// <summary>The share of pixels, out of this many, that the target changes from the shifted reference.</summary>
    private const int NoiseRate = 7;

    /// <summary>The offset of the reference bitmap's column used by the offset tests.</summary>
    private const int OffsetX = 2;

    /// <summary>The offset of the reference bitmap's row used by the offset tests.</summary>
    private const int OffsetY = -1;

    /// <summary>The rows that are copied from the reference in the typical prediction test: every third row.</summary>
    private const int CopyEvery = 3;

    /// <summary>The target pixels of figure 12 as column and row offsets, with RA1 first.</summary>
    private static readonly int[] Template0Target = [-1, -1, 0, -1, 1, -1, -1, 0];

    /// <summary>The reference pixels of figure 12 as column and row offsets, with RA2 first.</summary>
    private static readonly int[] Template0Reference = [-1, -1, 0, -1, 1, -1, -1, 0, 0, 0, 1, 0, -1, 1, 0, 1, 1, 1];

    /// <summary>The target pixels of figure 13.</summary>
    private static readonly int[] Template1Target = [-1, -1, 0, -1, 1, -1, -1, 0];

    /// <summary>The reference pixels of figure 13.</summary>
    private static readonly int[] Template1Reference = [0, -1, -1, 0, 0, 0, 1, 0, 0, 1, 1, 1];

    /// <summary>The offsets that, with the target pixels, make up the 3 by 3 area of figure 16.</summary>
    private static readonly int[] Around = [-1, 0, 1];

    /// <summary>Both templates decode what a coder using the pixels of figures 12 and 13 wrote, with and without reference offsets.</summary>
    /// <param name="template">The template.</param>
    /// <param name="offsets">Whether the reference is offset.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0, false)]
    [Arguments(0, true)]
    [Arguments(1, false)]
    [Arguments(1, true)]
    public async Task ContextsFollowFiguresTwelveAndThirteen(int template, bool offsets)
    {
        var random = new Generator(Seed);
        var reference = RandomPixels(random);
        var dx = offsets ? OffsetX : 0;
        var dy = offsets ? OffsetY : 0;
        var target = Shifted(reference, dx, dy, random);

        var coded = Encode(template, false, reference, target, dx, dy);
        var decoded = Decode(template, false, reference, coded, dx, dy);

        await Assert.That(decoded.SequenceEqual(target)).IsTrue();
    }

    /// <summary>Typical prediction (figures 14 to 16) decodes what a coder following 6.3.5.6 wrote.</summary>
    /// <param name="template">The template.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task TypicalPredictionFollowsTheText(int template)
    {
        var random = new Generator(Seed);
        var reference = RandomPixels(random);
        var target = Shifted(reference, 0, 0, random);
        for (var y = 0; y < Side; y += CopyEvery)
        {
            Array.Copy(reference, y * Side, target, y * Side, Side);
        }

        var coded = Encode(template, true, reference, target, 0, 0);
        var decoded = Decode(template, true, reference, coded, 0, 0);

        await Assert.That(decoded.SequenceEqual(target)).IsTrue();
    }

    /// <summary>Gets a pixel, 0 outside the bitmap.</summary>
    /// <param name="pixels">The pixels, one int per pixel.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The pixel.</returns>
    private static int Pixel(int[] pixels, int x, int y) => (uint)x < Side && (uint)y < Side ? pixels[(y * Side) + x] : 0;

    /// <summary>Makes a random bitmap.</summary>
    /// <param name="random">The generator.</param>
    /// <returns>The pixels.</returns>
    private static int[] RandomPixels(Generator random)
    {
        var pixels = new int[Side * Side];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = random.Next(Tenths) < BlackTenths ? 1 : 0;
        }

        return pixels;
    }

    /// <summary>Makes a bitmap that resembles a reference shifted by an offset, with some pixels changed.</summary>
    /// <param name="reference">The reference.</param>
    /// <param name="dx">The column offset of the reference.</param>
    /// <param name="dy">The row offset of the reference.</param>
    /// <param name="random">The generator.</param>
    /// <returns>The pixels.</returns>
    private static int[] Shifted(int[] reference, int dx, int dy, Generator random)
    {
        var pixels = new int[Side * Side];
        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var pixel = Pixel(reference, x - dx, y - dy);
                pixels[(y * Side) + x] = random.Next(NoiseRate) == 0 ? 1 - pixel : pixel;
            }
        }

        return pixels;
    }

    /// <summary>Gathers the template pixels of a pixel into a context, in list order.</summary>
    /// <param name="template">The template.</param>
    /// <param name="reference">The reference.</param>
    /// <param name="target">The pixels decoded so far; later pixels are not read.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="dx">The column offset of the reference.</param>
    /// <param name="dy">The row offset of the reference.</param>
    /// <returns>The context.</returns>
    private static int Context(int template, int[] reference, int[] target, int x, int y, int dx, int dy)
    {
        var targetList = template == 0 ? Template0Target : Template1Target;
        var referenceList = template == 0 ? Template0Reference : Template1Reference;
        var context = 0;
        for (var i = 0; i < targetList.Length; i += PairStep)
        {
            context = (context << 1) | Pixel(target, x + targetList[i], y + targetList[i + 1]);
        }

        for (var i = 0; i < referenceList.Length; i += PairStep)
        {
            context = (context << 1) | Pixel(reference, x - dx + referenceList[i], y - dy + referenceList[i + 1]);
        }

        return context;
    }

    /// <summary>Gets the SLTP context of figures 14 and 15: every pixel 0 except the reference pixel under the current one.</summary>
    /// <param name="template">The template.</param>
    /// <returns>The context, in the list order of <see cref="Context"/>.</returns>
    private static int TypicalContext(int template)
    {
        var referenceList = template == 0 ? Template0Reference : Template1Reference;
        var context = 0;
        for (var i = 0; i < referenceList.Length; i += PairStep)
        {
            context = (context << 1) | (referenceList[i] == 0 && referenceList[i + 1] == 0 ? 1 : 0);
        }

        return context;
    }

    /// <summary>Determines whether the 3 by 3 area of the reference at a pixel is of one value (figure 16).</summary>
    /// <param name="reference">The reference.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns><see langword="true"/> when all nine pixels are equal.</returns>
    private static bool Uniform(int[] reference, int x, int y)
    {
        var first = Pixel(reference, x, y);
        return Array.TrueForAll(Around, row => Array.TrueForAll(Around, column => Pixel(reference, x + column, y + row) == first));
    }

    /// <summary>Codes a bitmap with the context rule written from the figures.</summary>
    /// <param name="template">The template.</param>
    /// <param name="typical">Whether typical prediction is on.</param>
    /// <param name="reference">The reference.</param>
    /// <param name="target">The bitmap to code.</param>
    /// <param name="dx">The column offset of the reference.</param>
    /// <param name="dy">The row offset of the reference.</param>
    /// <returns>The coded bytes.</returns>
    private static byte[] Encode(int template, bool typical, int[] reference, int[] target, int dx, int dy)
    {
        var encoder = new Jbig2MqEncoder(InitialOutput);
        var contexts = new byte[1 << (template == 0 ? Template0ContextBits : Template1ContextBits)];
        var previousRow = false;
        for (var y = 0; y < Side; y++)
        {
            var typicalRow = typical && IsTypicalRow(reference, target, y);
            if (typical)
            {
                encoder.Encode(ref contexts[TypicalContext(template)], typicalRow == previousRow ? 0 : 1);
                previousRow = typicalRow;
            }

            for (var x = 0; x < Side; x++)
            {
                if (typicalRow && Uniform(reference, x, y))
                {
                    continue;
                }

                encoder.Encode(ref contexts[Context(template, reference, target, x, y, dx, dy)], target[(y * Side) + x]);
            }
        }

        var output = default(PooledBuffer);
        encoder.Finish(ref output);
        var data = output.ToArray();
        output.Dispose();
        return data;
    }

    /// <summary>Determines whether every predictable pixel of a row equals its predicted value (6.3.5.6 step 3 d).</summary>
    /// <param name="reference">The reference.</param>
    /// <param name="target">The bitmap.</param>
    /// <param name="y">The row.</param>
    /// <returns><see langword="true"/> when the row can be coded as typical.</returns>
    private static bool IsTypicalRow(int[] reference, int[] target, int y)
    {
        for (var x = 0; x < Side; x++)
        {
            if (Uniform(reference, x, y) && Pixel(reference, x, y) != target[(y * Side) + x])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Decodes coded bytes with the library.</summary>
    /// <param name="template">The template.</param>
    /// <param name="typical">Whether typical prediction is on.</param>
    /// <param name="reference">The reference.</param>
    /// <param name="coded">The coded bytes.</param>
    /// <param name="dx">The column offset of the reference.</param>
    /// <param name="dy">The row offset of the reference.</param>
    /// <returns>The decoded pixels.</returns>
    private static int[] Decode(int template, bool typical, int[] reference, byte[] coded, int dx, int dy)
    {
        var stride = Jbig2Decoder.GetRowBytes(Side);
        var rows = new byte[stride * Side];
        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                if (reference[(y * Side) + x] != 0)
                {
                    rows[(y * stride) + (x >> Jbig2Bits.ByteShift)] |= (byte)(Jbig2Bits.FirstPixelBit >> (x & Jbig2Bits.BitMask));
                }
            }
        }

        var parameters = new Jbig2RefinementParameters(template, typical, dx, dy, Jbig2AtPixels.Refinement);
        var contexts = new byte[parameters.ContextCount];
        var decoder = new Jbig2ArithmeticDecoder(coded, 0);
        using var bitmap = Jbig2Bitmap.Create(Side, Side)!;
        _ = Jbig2RefinementRegion.Decode(ref decoder, contexts, parameters, new(rows, Side, Side), bitmap);
        var result = new int[Side * Side];
        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                result[(y * Side) + x] = bitmap.GetPixel(x, y);
            }
        }

        return result;
    }
}
