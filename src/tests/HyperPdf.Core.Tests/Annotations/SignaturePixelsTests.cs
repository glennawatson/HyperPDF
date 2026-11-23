// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;

namespace HyperPdf.Core.Tests.Annotations;

/// <summary>Tests paper removal without destroying soft signature edges.</summary>
public sealed class SignaturePixelsTests
{
    /// <summary>The values a channel takes.</summary>
    private const int Values = 256;

    /// <summary>The bytes of a pixel.</summary>
    private const int Channels = 4;

    /// <summary>The largest channel value.</summary>
    private const int Opaque = 255;

    /// <summary>The red channel's byte offset.</summary>
    private const int Red = 2;

    /// <summary>The expected transparent paper and black ink pixels.</summary>
    private static readonly byte[] SoftBlackInk = [0, 0, 0, 0, 0, 0, 0, 255, 0, 0, 0, 127];

    /// <summary>Premultiplied pixels as a decoder gives them: half-transparent colour, clear, and solid colour.</summary>
    private static readonly byte[] Premultiplied = [64, 32, 16, 128, 0, 0, 0, 0, 10, 20, 30, 255];

    /// <summary>The same pixels with straight alpha.</summary>
    private static readonly byte[] Straight = [128, 64, 32, 128, 0, 0, 0, 0, 10, 20, 30, 255];

    /// <summary>The expected blue ink pixel after removing white paper.</summary>
    private static readonly byte[] SoftBlueInk = [255, 0, 0, 127];

    /// <summary>Unpremultiplying gives the rounded division for every alpha and channel value, and leaves clear and solid pixels alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnpremultiplyGivesTheRoundedDivisionForEveryAlphaAndValue()
    {
        var pixels = new byte[Values * Values * Channels];
        var expected = new byte[pixels.Length];
        for (var alpha = 0; alpha < Values; alpha++)
        {
            for (var value = 0; value < Values; value++)
            {
                var offset = ((alpha * Values) + value) * Channels;
                pixels.AsSpan(offset, Channels - 1).Fill((byte)value);
                pixels[offset + Channels - 1] = (byte)alpha;
                var straight = alpha is 0 or Opaque ? value : Math.Min(Opaque, ((value * Opaque) + (alpha >> 1)) / alpha);
                expected.AsSpan(offset, Channels - 1).Fill((byte)straight);
                expected[offset + Channels - 1] = (byte)alpha;
            }
        }

        SignaturePixels.Unpremultiply(pixels);

        await Assert.That(pixels.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    /// <summary>Removing paper gives the scaled distance above the paper for every pair of paper and ink values.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovingPaperGivesTheScaledDistanceForEveryPaperAndInk()
    {
        var pixels = new byte[Values * Values * Channels];
        var expected = new byte[pixels.Length];
        for (var paper = 0; paper < Values; paper++)
        {
            for (var ink = paper; ink < Values; ink++)
            {
                // Blue and green are the paper, red is darker ink above it.
                var offset = ((paper * Values) + ink) * Channels;
                pixels[offset] = (byte)paper;
                pixels[offset + 1] = (byte)paper;
                pixels[offset + Red] = (byte)ink;
                pixels[offset + Channels - 1] = Opaque;
                var alpha = Opaque - paper;
                if (alpha == 0)
                {
                    continue;
                }

                expected[offset + Red] = (byte)((ink - paper) * Opaque / alpha);
                expected[offset + Channels - 1] = (byte)alpha;
            }

            for (var ink = 0; ink < paper; ink++)
            {
                // Unused slots stay plain white, which becomes clear.
                pixels.AsSpan(((paper * Values) + ink) * Channels, Channels).Fill(Opaque);
            }
        }

        SignaturePixels.RemoveWhitePaper(pixels);

        await Assert.That(pixels.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    /// <summary>Verifies premultiplied pixels from an image decoder become straight alpha, leaving clear and solid pixels alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnpremultipliesDecodedPixels()
    {
        var pixels = Premultiplied.ToArray();
        SignaturePixels.Unpremultiply(pixels);
        await Assert.That(pixels.SequenceEqual(Straight)).IsTrue();
        await Assert.That(static () => SignaturePixels.Unpremultiply(new byte[3])).Throws<ArgumentException>();
    }

    /// <summary>Verifies white paper disappears while grey edge pixels become translucent black.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovesWhitePaperWithSoftEdges()
    {
        byte[] pixels = [255, 255, 255, 255, 0, 0, 0, 255, 128, 128, 128, 255];
        SignaturePixels.RemoveWhitePaper(pixels);
        await Assert.That(pixels.SequenceEqual(SoftBlackInk)).IsTrue();
    }

    /// <summary>Verifies coloured ink has no white fringe when composited on dark paper.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecoversBlueInkFromWhitePaper()
    {
        byte[] pixels = [255, 128, 128, 255];
        SignaturePixels.RemoveWhitePaper(pixels);
        await Assert.That(pixels.SequenceEqual(SoftBlueInk)).IsTrue();
    }

    /// <summary>Verifies transparent source images keep their alpha and colour values.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreservesExistingTransparency()
    {
        byte[] original = [255, 255, 255, 255, 70, 90, 110, 128, 255, 255, 255, 0];
        var pixels = original.ToArray();
        SignaturePixels.RemoveWhitePaper(pixels);
        await Assert.That(pixels.SequenceEqual(original)).IsTrue();
    }

    /// <summary>Verifies recovered pixels reproduce the scan on white within byte rounding.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreservesAppearanceOnWhite()
    {
        const int opaque = 255;
        const int channels = 4;
        const int colourChannels = 3;
        const int redChannel = 2;
        const int tolerance = 1;
        var original = new byte[(opaque + 1) * channels];
        for (var value = 0; value <= opaque; value++)
        {
            var offset = value * channels;
            original[offset] = (byte)value;
            original[offset + 1] = (byte)(opaque - value);
            original[offset + redChannel] = opaque;
            original[offset + colourChannels] = opaque;
        }

        var pixels = original.ToArray();
        SignaturePixels.RemoveWhitePaper(pixels);
        for (var offset = 0; offset < pixels.Length; offset += channels)
        {
            var alpha = pixels[offset + colourChannels];
            for (var channel = 0; channel < colourChannels; channel++)
            {
                var restored = ((pixels[offset + channel] * alpha) / opaque) + opaque - alpha;
                await Assert.That(Math.Abs(restored - original[offset + channel])).IsLessThanOrEqualTo(tolerance);
            }
        }
    }

    /// <summary>Verifies incomplete BGRA pixels are rejected before changing the image.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsIncompletePixel()
    {
        byte[] pixels = [255];
        await Assert.That(() => SignaturePixels.RemoveWhitePaper(pixels)).Throws<ArgumentException>();
    }

    /// <summary>Verifies empty images require no special buffer.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AcceptsEmptyPixels()
    {
        byte[] pixels = [];
        SignaturePixels.RemoveWhitePaper(pixels);
        await Assert.That(pixels).IsEmpty();
    }
}
