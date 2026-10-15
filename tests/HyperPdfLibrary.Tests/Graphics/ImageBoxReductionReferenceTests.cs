// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>
/// Checks the area average against a plain per-pixel version of the same arithmetic over varied sizes, levels and pixel formats,
/// and checks that decoding with the average built in gives the pixels of decoding in full and then averaging.
/// </summary>
public sealed class ImageBoxReductionReferenceTests
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int Bgra = 4;

    /// <summary>The components of an RGB pixel.</summary>
    private const int Rgb = 3;

    /// <summary>The largest 8-bit sample.</summary>
    private const int ByteMax = 255;

    /// <summary>The largest 15-bit linear value.</summary>
    private const int LinearMax = 32_767;

    /// <summary>The bits dropped from a weighted sample.</summary>
    private const int WeightShift = 4;

    /// <summary>Two, the divisor of a rounding offset and of the reversed-decode cycle.</summary>
    private const int Half = 2;

    /// <summary>The multiplier that scatters sample values.</summary>
    private const int Scatter = 73;

    /// <summary>The multiplier that varies alpha between pixels.</summary>
    private const int AlphaScatter = 29;

    /// <summary>The number of pixels between fully clear pixels.</summary>
    private const int ClearEvery = 7;

    /// <summary>The number of pixels between fully opaque pixels.</summary>
    private const int OpaqueEvery = 3;

    /// <summary>The most halvings the reducer tries.</summary>
    private const int MostLevels = 5;

    /// <summary>The most halvings the decoder tries to build in.</summary>
    private const int MostDecodedLevels = 4;

    /// <summary>The bits of a byte sample.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits of a wide sample.</summary>
    private const int WideBits = 16;

    /// <summary>The side of the image the colour-key test decodes.</summary>
    private const int KeyedSide = 16;

    /// <summary>The halvings the colour-key test asks for.</summary>
    private const int KeyedLevels = 2;

    /// <summary>The highest colour-key sample.</summary>
    private const int KeyHigh = 10;

    /// <summary>The sRGB transfer function's linear segment limit for encoded values.</summary>
    private const double SrgbKnee = 0.04045;

    /// <summary>The sRGB transfer function's linear segment slope.</summary>
    private const double SrgbSlope = 12.92;

    /// <summary>The sRGB transfer function's offset.</summary>
    private const double SrgbOffset = 0.055;

    /// <summary>The sRGB transfer function's gamma.</summary>
    private const double SrgbGamma = 2.4;

    /// <summary>The sRGB transfer function's divisor for the power segment.</summary>
    private const double SrgbScale = 1.055;

    /// <summary>The linear light limit of the sRGB linear segment.</summary>
    private const double LinearKnee = 0.0031308;

    /// <summary>The sizes tried: single pixels, odd edges, wide, tall and exact multiples.</summary>
    private static readonly Dimensions[] Sizes =
    [
        new(1, 1),
        new(2, 2),
        new(5, 3),
        new(17, 9),
        new(64, 64),
        new(300, 7),
        new(33, 300),
        new(1, 40),
        new(40, 1),
        new(96, 96),
    ];

    /// <summary>The sample depths tried.</summary>
    private static readonly int[] Depths = [ByteBits, WideBits];

    /// <summary>The sRGB to linear table, built as the reducer builds it.</summary>
    private static readonly int[] ToLinear = BuildToLinear();

    /// <summary>The linear to sRGB table.</summary>
    private static readonly byte[] ToSrgb = BuildToSrgb();

    /// <summary>Premultiplied BGRA images with opaque, clear and partly transparent pixels average as the reference does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ColorMatchesTheReference()
    {
        foreach (var (width, height) in Sizes)
        {
            for (var levels = 1; levels <= MostLevels; levels++)
            {
                var source = new PdfImageData(width, height, Premultiplied(width, height), false, false, PdfImageCodec.None);

                await Assert.That(ImageBoxReduction.Reduce(source, levels).Pixels.AsSpan().SequenceEqual(ReferenceColor(source, levels))).IsTrue();
            }
        }
    }

    /// <summary>Opaque BGRA images average as the reference does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpaqueColorMatchesTheReference()
    {
        foreach (var (width, height) in Sizes)
        {
            for (var levels = 1; levels <= MostLevels; levels++)
            {
                var pixels = Premultiplied(width, height);
                for (var i = Rgb; i < pixels.Length; i += Bgra)
                {
                    pixels[i] = ByteMax;
                }

                var source = new PdfImageData(width, height, pixels, false, false, PdfImageCodec.None);

                await Assert.That(ImageBoxReduction.Reduce(source, levels).Pixels.AsSpan().SequenceEqual(ReferenceColor(source, levels))).IsTrue();
            }
        }
    }

    /// <summary>Gray and coverage images average as the reference does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GrayAndCoverageMatchTheReference()
    {
        foreach (var (width, height) in Sizes)
        {
            for (var levels = 1; levels <= MostLevels; levels++)
            {
                var samples = Samples(width * height);
                var gray = new PdfImageData(width, height, samples, false, false, PdfImageCodec.None) { IsGray = true };
                var stencil = new PdfImageData(width, height, samples, true, false, PdfImageCodec.None);

                await Assert.That(ImageBoxReduction.Reduce(gray, levels).Pixels.AsSpan().SequenceEqual(ReferenceGray(gray, levels, false))).IsTrue();
                await Assert.That(ImageBoxReduction.Reduce(stencil, levels).Pixels.AsSpan().SequenceEqual(ReferenceGray(stencil, levels, true))).IsTrue();
            }
        }
    }

    /// <summary>Decoding with the average built in gives the pixels of a full decode followed by the average, for 8 and 16 bit samples and a reversed /Decode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodingWithTheAverageBuiltInMatchesDecodeThenReduce()
    {
        foreach (var (width, height) in Sizes)
        {
            foreach (var bits in Depths)
            {
                for (var levels = 1; levels <= MostDecodedLevels; levels++)
                {
                    var stream = RgbImage(width, height, bits, levels % Half == 0);
                    var full = PdfImageDecoder.DecodeCompact(stream, new ImageLevels(0, 0), default, CancellationToken.None);
                    var fused = PdfImageDecoder.DecodeCompact(stream, new ImageLevels(0, levels), default, CancellationToken.None);
                    var expected = ImageBoxReduction.Reduce(full!, levels);

                    await Assert.That(fused!.IsBoxReduced).IsTrue();
                    await Assert.That(full!.IsBoxReduced).IsFalse();
                    await Assert.That(fused.Width).IsEqualTo(expected.Width);
                    await Assert.That(fused.Height).IsEqualTo(expected.Height);
                    await Assert.That(fused.Pixels.AsSpan().SequenceEqual(expected.Pixels)).IsTrue();
                }
            }
        }
    }

    /// <summary>An image with a colour-key mask is decoded in full, because the key applies to the source pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ColorKeyedImagesAreNotReducedWhileDecoding()
    {
        var stream = RgbImage(KeyedSide, KeyedSide, ByteBits, false);
        stream.Dictionary.Add(KnownName.Mask, PdfValue.FromArray(PdfArray.FromNumbers(null, [0, KeyHigh, 0, KeyHigh, 0, KeyHigh])));

        var decoded = PdfImageDecoder.DecodeCompact(stream, new ImageLevels(0, KeyedLevels), default, CancellationToken.None);

        await Assert.That(decoded!.IsBoxReduced).IsFalse();
        await Assert.That(decoded.Width).IsEqualTo(KeyedSide);
    }

    /// <summary>Builds an RGB image stream of scattered samples.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="bits">The bits per component.</param>
    /// <param name="reversed">Whether /Decode reverses the samples.</param>
    /// <returns>The stream.</returns>
    private static PdfStream RgbImage(int width, int height, int bits, bool reversed)
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.Width, PdfValue.FromInteger(width));
        dictionary.Add(KnownName.Height, PdfValue.FromInteger(height));
        dictionary.Add(KnownName.BitsPerComponent, PdfValue.FromInteger(bits));
        dictionary.Add(KnownName.ColorSpace, PdfValue.FromName(KnownName.DeviceRGB));
        if (reversed)
        {
            dictionary.Add(KnownName.Decode, PdfValue.FromArray(PdfArray.FromNumbers(null, [1F, 0F, 1F, 0F, 1F, 0F])));
        }

        return new(dictionary, Samples(width * height * Rgb * (bits / ByteBits)));
    }

    /// <summary>Creates scattered sample bytes.</summary>
    /// <param name="length">The number of bytes.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Samples(int length)
    {
        var samples = new byte[length];
        for (var i = 0; i < length; i++)
        {
            samples[i] = (byte)((i * Scatter) + (i / Rgb));
        }

        return samples;
    }

    /// <summary>Creates premultiplied BGRA pixels with a mix of clear, opaque and partly transparent pixels.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The pixels.</returns>
    private static byte[] Premultiplied(int width, int height)
    {
        var pixels = new byte[width * height * Bgra];
        for (var i = 0; i < width * height; i++)
        {
            var alpha = PixelAlpha(i);
            for (var c = 0; c < Rgb; c++)
            {
                var straight = (byte)((i * Scatter) + (c * AlphaScatter) + (i / width));
                pixels[(i * Bgra) + c] = (byte)(straight * alpha / ByteMax);
            }

            pixels[(i * Bgra) + Rgb] = (byte)alpha;
        }

        return pixels;
    }

    /// <summary>Gets the alpha of a pixel: every few pixels are clear or opaque and the rest vary.</summary>
    /// <param name="index">The pixel's index.</param>
    /// <returns>The alpha.</returns>
    private static int PixelAlpha(int index)
    {
        if (index % ClearEvery == 0)
        {
            return 0;
        }

        return index % OpaqueEvery == 0 ? ByteMax : ((index * AlphaScatter) % (ByteMax - 1)) + 1;
    }

    /// <summary>Averages a BGRA image one pixel at a time.</summary>
    /// <param name="source">The source image.</param>
    /// <param name="levels">The halvings.</param>
    /// <returns>The reduced pixels.</returns>
    private static byte[] ReferenceColor(PdfImageData source, int levels)
    {
        var step = 1 << levels;
        var width = ImageBoxReduction.ReducedSize(source.Width, levels);
        var height = ImageBoxReduction.ReducedSize(source.Height, levels);
        var output = new byte[width * height * Bgra];
        for (var oy = 0; oy < height; oy++)
        {
            for (var ox = 0; ox < width; ox++)
            {
                AverageBlock(source, step, ox, oy, output.AsSpan(((oy * width) + ox) * Bgra, Bgra));
            }
        }

        return output;
    }

    /// <summary>Averages one block of a BGRA image into one output pixel.</summary>
    /// <param name="source">The source image.</param>
    /// <param name="step">The source pixels across one output pixel.</param>
    /// <param name="ox">The output column.</param>
    /// <param name="oy">The output row.</param>
    /// <param name="pixel">Receives the output pixel; left clear when the block is transparent.</param>
    private static void AverageBlock(PdfImageData source, int step, int ox, int oy, Span<byte> pixel)
    {
        var sums = new int[Rgb];
        var alphaSum = 0;
        var count = 0;
        for (var y = oy * step; y < Math.Min(source.Height, (oy + 1) * step); y++)
        {
            for (var x = ox * step; x < Math.Min(source.Width, (ox + 1) * step); x++)
            {
                count++;
                var p = ((y * source.Width) + x) * Bgra;
                int alpha = source.Pixels[p + Rgb];
                alphaSum += alpha;
                for (var c = 0; c < Rgb && alpha != 0; c++)
                {
                    var straight = alpha == ByteMax ? source.Pixels[p + c] : Math.Min(ByteMax, ((source.Pixels[p + c] * ByteMax) + (alpha / Half)) / alpha);
                    sums[c] += (ToLinear[straight] * alpha) >> WeightShift;
                }
            }
        }

        if (alphaSum == 0)
        {
            return;
        }

        var averageAlpha = (alphaSum + (count / Half)) / count;
        for (var c = 0; c < Rgb; c++)
        {
            var linear = (int)Math.Min(LinearMax, ((long)sums[c] << WeightShift) / alphaSum);
            pixel[c] = (byte)(((ToSrgb[linear] * averageAlpha) + (ByteMax / Half)) / ByteMax);
        }

        pixel[Rgb] = (byte)averageAlpha;
    }

    /// <summary>Averages a gray or coverage image one pixel at a time.</summary>
    /// <param name="source">The source image.</param>
    /// <param name="levels">The halvings.</param>
    /// <param name="coverage">Whether the samples are coverage, averaged without the sRGB curve.</param>
    /// <returns>The reduced pixels.</returns>
    private static byte[] ReferenceGray(PdfImageData source, int levels, bool coverage)
    {
        var step = 1 << levels;
        var width = ImageBoxReduction.ReducedSize(source.Width, levels);
        var height = ImageBoxReduction.ReducedSize(source.Height, levels);
        var output = new byte[width * height];
        for (var oy = 0; oy < height; oy++)
        {
            for (var ox = 0; ox < width; ox++)
            {
                output[(oy * width) + ox] = GrayBlock(source, step, ox, oy, coverage);
            }
        }

        return output;
    }

    /// <summary>Averages one block of a gray or coverage image.</summary>
    /// <param name="source">The source image.</param>
    /// <param name="step">The source pixels across one output pixel.</param>
    /// <param name="ox">The output column.</param>
    /// <param name="oy">The output row.</param>
    /// <param name="coverage">Whether the samples are coverage.</param>
    /// <returns>The output sample.</returns>
    private static byte GrayBlock(PdfImageData source, int step, int ox, int oy, bool coverage)
    {
        var sum = 0;
        var count = 0;
        for (var y = oy * step; y < Math.Min(source.Height, (oy + 1) * step); y++)
        {
            for (var x = ox * step; x < Math.Min(source.Width, (ox + 1) * step); x++)
            {
                count++;
                var value = source.Pixels[(y * source.Width) + x];
                sum += coverage ? (int)Math.Round(value * (double)LinearMax / ByteMax) : ToLinear[value];
            }
        }

        var mean = sum / count;
        return coverage ? (byte)Math.Round(mean * (double)ByteMax / LinearMax) : ToSrgb[mean];
    }

    /// <summary>Builds the sRGB to 15 bit linear table.</summary>
    /// <returns>256 values.</returns>
    private static int[] BuildToLinear()
    {
        var table = new int[ByteMax + 1];
        for (var i = 0; i < table.Length; i++)
        {
            var encoded = i / (double)ByteMax;
            var linear = encoded <= SrgbKnee ? encoded / SrgbSlope : Math.Pow((encoded + SrgbOffset) / SrgbScale, SrgbGamma);
            table[i] = (short)Math.Round(linear * LinearMax);
        }

        return table;
    }

    /// <summary>Builds the 15 bit linear to sRGB table.</summary>
    /// <returns>One value for each linear step.</returns>
    private static byte[] BuildToSrgb()
    {
        var table = new byte[LinearMax + 1];
        for (var i = 0; i < table.Length; i++)
        {
            var linear = i / (double)LinearMax;
            var encoded = linear <= LinearKnee ? linear * SrgbSlope : (SrgbScale * Math.Pow(linear, 1 / SrgbGamma)) - SrgbOffset;
            table[i] = (byte)Math.Clamp((int)Math.Round(encoded * ByteMax), 0, ByteMax);
        }

        return table;
    }

    /// <summary>A test image size.</summary>
    /// <param name="Width">The width in pixels.</param>
    /// <param name="Height">The height in pixels.</param>
    private readonly record struct Dimensions(int Width, int Height);
}
