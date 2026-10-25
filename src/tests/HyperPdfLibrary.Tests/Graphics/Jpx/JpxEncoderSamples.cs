// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Tests.Graphics.Jpeg;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>Makes test images for the JPEG 2000 encoder and reads their samples back out of the decoder's planes.</summary>
internal static class JpxEncoderSamples
{
    /// <summary>The seed of the sample generator.</summary>
    private const uint Seed = 4242;

    /// <summary>The weight of the horizontal gradient.</summary>
    private const int GradientX = 5;

    /// <summary>The weight of the vertical gradient.</summary>
    private const int GradientY = 3;

    /// <summary>The weight of the component in the gradient.</summary>
    private const int GradientComponent = 37;

    /// <summary>The steps of the gradient before it starts again.</summary>
    private const int GradientPeriod = 256;

    /// <summary>The spread of the noise added to the gradient, as a fraction of the sample range: one in this many.</summary>
    private const int NoiseDivisor = 8;

    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bytes of a sample of more than eight bits.</summary>
    private const int WideBytes = 2;

    /// <summary>Makes an image of a smooth gradient with noise, so it has both flat and busy parts.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="components">The channels.</param>
    /// <param name="bits">The bits of a sample.</param>
    /// <returns>The image with no colour description beyond the defaults.</returns>
    internal static PdfJpxImage Gradient(int width, int height, int components, int bits)
    {
        var random = new JpegTestRandom(Seed);
        var range = 1 << bits;
        var wide = bits > ByteBits;
        var bytes = new byte[width * height * components * (wide ? WideBytes : 1)];
        var position = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                for (var c = 0; c < components; c++)
                {
                    var ramp = (((x * GradientX) + (y * GradientY) + (c * GradientComponent)) % GradientPeriod) * range / GradientPeriod;
                    var value = (ramp + random.Next(0, Math.Max(1, range / NoiseDivisor))) % range;
                    position = Store(bytes, position, value, wide);
                }
            }
        }

        return new(width, height, components, bits, bytes);
    }

    /// <summary>Reads one component's samples out of an image.</summary>
    /// <param name="image">The image.</param>
    /// <param name="component">The component.</param>
    /// <returns>The samples, row by row.</returns>
    internal static int[] Component(PdfJpxImage image, int component)
    {
        var samples = new int[image.Width * image.Height];
        var span = image.Samples.Span;
        var bytes = image.BytesPerSample;
        for (var i = 0; i < samples.Length; i++)
        {
            var at = ((i * image.Components) + component) * bytes;
            samples[i] = bytes == 1 ? span[at] : (span[at] << ByteBits) | span[at + 1];
        }

        return samples;
    }

    /// <summary>Stores a sample.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="position">Where to store it.</param>
    /// <param name="value">The sample.</param>
    /// <param name="wide">Whether the sample takes two bytes.</param>
    /// <returns>The position after the sample.</returns>
    private static int Store(byte[] bytes, int position, int value, bool wide)
    {
        if (!wide)
        {
            bytes[position] = (byte)value;
            return position + 1;
        }

        bytes[position] = (byte)(value >> ByteBits);
        bytes[position + 1] = (byte)value;
        return position + WideBytes;
    }
}
