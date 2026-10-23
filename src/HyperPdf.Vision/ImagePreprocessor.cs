// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdf.Vision;

/// <summary>
/// Prepares a picture for the ViT encoder the way its image processor does: a bilinear resize with anti-aliasing to 224 by
/// 224 pixels (the whole picture is stretched), each channel scaled to 0 to 1 and then to -1 to 1, laid out as red, green
/// and blue planes. Transparent pixels are placed over white.
/// </summary>
internal static class ImagePreprocessor
{
    /// <summary>The width and height of the model's input.</summary>
    internal const int Side = 224;

    /// <summary>The channels of the model's input.</summary>
    internal const int Channels = 3;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The position of the green plane, after red.</summary>
    private const int GreenChannel = 1;

    /// <summary>The position of the blue plane, after green.</summary>
    private const int BlueChannel = 2;

    /// <summary>The offset of the alpha byte in a BGRA pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The value of a full 8 bit channel.</summary>
    private const float Full = 255F;

    /// <summary>The mean and standard deviation the model was trained with, for every channel; also the rounding offset.</summary>
    private const float Half = 0.5F;

    /// <summary>The pixels in one plane of the input.</summary>
    private const int PlanePixels = Side * Side;

    /// <summary>The widths of the triangle filter on each side of its centre, in target pixels.</summary>
    private const float FilterRadius = 1F;

    /// <summary>The extra room kept for the weights of a target pixel, so the edges of the filter always fit.</summary>
    private const int WeightSlack = 1;

    /// <summary>The number of sides of the filter, which doubles its radius to give its width.</summary>
    private const float FilterSides = 2F;

    /// <summary>Gets the number of floats in the prepared input.</summary>
    internal static int InputLength => Channels * PlanePixels;

    /// <summary>Prepares a picture.</summary>
    /// <param name="bgra">The pixels, four bytes each: blue, green, red and premultiplied alpha, with no gaps between rows.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="input">Receives <see cref="InputLength"/> floats: the red plane, then green, then blue.</param>
    /// <exception cref="ArgumentException">The pixels or the destination are too short, or a size is not positive.</exception>
    internal static void Prepare(ReadOnlySpan<byte> bgra, int width, int height, Span<float> input)
    {
        if (width <= 0 || height <= 0 || bgra.Length < width * height * BytesPerPixel || input.Length < InputLength)
        {
            throw new ArgumentException("The picture or the destination is the wrong size.", nameof(bgra));
        }

        var across = BuildWeights(width);
        var down = BuildWeights(height);
        var wide = ArrayPool<float>.Shared.Rent(height * Side * Channels);
        try
        {
            ResizeAcross(bgra, width, height, in across, wide);
            ResizeDown(wide, in down, input);
        }
        finally
        {
            ArrayPool<float>.Shared.Return(wide);
        }
    }

    /// <summary>Works out which source pixels, and how much of each, make every target pixel along one axis.</summary>
    /// <param name="sourceSize">The source length in pixels.</param>
    /// <returns>The weights for each of the <see cref="Side"/> target pixels.</returns>
    private static AxisWeights BuildWeights(int sourceSize)
    {
        var scale = (float)sourceSize / Side;
        var filterScale = MathF.Max(scale, 1F);
        var support = FilterRadius * filterScale;
        var span = (int)MathF.Ceiling(support * FilterSides) + WeightSlack;
        var starts = new int[Side];
        var counts = new int[Side];
        var weights = new float[Side * span];
        for (var target = 0; target < Side; target++)
        {
            var centre = (target + Half) * scale;
            var first = Math.Max(0, (int)(centre - support + Half));
            var last = Math.Min(sourceSize, (int)(centre + support + Half));
            var total = 0F;
            for (var source = first; source < last; source++)
            {
                var weight = MathF.Max(0F, 1F - MathF.Abs((source - centre + Half) / filterScale));
                weights[(target * span) + (source - first)] = weight;
                total += weight;
            }

            for (var source = first; source < last && total > 0F; source++)
            {
                weights[(target * span) + (source - first)] /= total;
            }

            starts[target] = first;
            counts[target] = last - first;
        }

        return new(starts, counts, weights, span);
    }

    /// <summary>Resizes every row to the target width, as red, green and blue floats from 0 to 1.</summary>
    /// <param name="bgra">The pixels.</param>
    /// <param name="width">The source width.</param>
    /// <param name="height">The source height.</param>
    /// <param name="across">The weights along a row.</param>
    /// <param name="wide">Receives <paramref name="height"/> rows of <see cref="Side"/> pixels, three floats each.</param>
    private static void ResizeAcross(ReadOnlySpan<byte> bgra, int width, int height, in AxisWeights across, float[] wide)
    {
        for (var y = 0; y < height; y++)
        {
            var row = bgra.Slice(y * width * BytesPerPixel, width * BytesPerPixel);
            for (var x = 0; x < Side; x++)
            {
                var red = 0F;
                var green = 0F;
                var blue = 0F;
                var start = across.Starts[x];
                for (var k = 0; k < across.Counts[x]; k++)
                {
                    var weight = across.Weights[(x * across.Span) + k];
                    var pixel = row.Slice((start + k) * BytesPerPixel, BytesPerPixel);

                    // The pixels are premultiplied, so what shows through the transparent part is white added back.
                    var white = Full - pixel[AlphaOffset];
                    blue += weight * (pixel[0] + white);
                    green += weight * (pixel[1] + white);
                    red += weight * (pixel[2] + white);
                }

                var at = ((y * Side) + x) * Channels;
                wide[at] = red / Full;
                wide[at + GreenChannel] = green / Full;
                wide[at + BlueChannel] = blue / Full;
            }
        }
    }

    /// <summary>Resizes every column to the target height and normalises the values into planes.</summary>
    /// <param name="wide">The rows already resized across.</param>
    /// <param name="down">The weights along a column.</param>
    /// <param name="input">Receives the three planes.</param>
    private static void ResizeDown(float[] wide, in AxisWeights down, Span<float> input)
    {
        for (var y = 0; y < Side; y++)
        {
            var start = down.Starts[y];
            for (var x = 0; x < Side; x++)
            {
                for (var channel = 0; channel < Channels; channel++)
                {
                    var value = 0F;
                    for (var k = 0; k < down.Counts[y]; k++)
                    {
                        value += down.Weights[(y * down.Span) + k] * wide[((((start + k) * Side) + x) * Channels) + channel];
                    }

                    input[(channel * PlanePixels) + (y * Side) + x] = (Math.Clamp(value, 0F, 1F) - Half) / Half;
                }
            }
        }
    }

    /// <summary>The source pixels and weights that make each target pixel along one axis.</summary>
    /// <param name="Starts">The first source pixel of each target pixel.</param>
    /// <param name="Counts">How many source pixels each target pixel takes.</param>
    /// <param name="Weights">The weights, <paramref name="Span"/> for each target pixel.</param>
    /// <param name="Span">The room kept for the weights of each target pixel.</param>
    private readonly record struct AxisWeights(int[] Starts, int[] Counts, float[] Weights, int Span);
}
