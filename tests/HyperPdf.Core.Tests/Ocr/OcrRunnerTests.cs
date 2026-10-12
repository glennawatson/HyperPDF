// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Ocr;

namespace HyperPdf.Core.Tests.Ocr;

/// <summary>Tests for <see cref="OcrRunner"/>'s greyscale conversion and confidence.</summary>
public sealed class OcrRunnerTests
{
    /// <summary>A pixel count that is not a multiple of the vector step, so the scalar tail runs too.</summary>
    private const int PixelCount = 1_000;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>A prime stepping the test bytes through every value.</summary>
    private const int Step = 37;

    /// <summary>The red byte of a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The red weight, out of 256.</summary>
    private const int RedWeight = 77;

    /// <summary>The green weight, out of 256.</summary>
    private const int GreenWeight = 150;

    /// <summary>The blue weight, out of 256.</summary>
    private const int BlueWeight = 29;

    /// <summary>The shift dividing by 256.</summary>
    private const int WeightShift = 8;

    /// <summary>Verifies every pixel, vector or scalar, gets the Rec. 601 luma.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VectorMatchesScalar()
    {
        var bgra = new byte[PixelCount * BytesPerPixel];
        for (var i = 0; i < bgra.Length; i++)
        {
            bgra[i] = (byte)((i * Step) + (i >> WeightShift));
        }

        var grey = new byte[PixelCount];
        OcrRunner.ToGrey(bgra, grey);
        var mismatches = 0;
        for (var i = 0; i < PixelCount; i++)
        {
            var b = bgra[i * BytesPerPixel];
            var g = bgra[(i * BytesPerPixel) + 1];
            var r = bgra[(i * BytesPerPixel) + RedOffset];
            var expected = (byte)(((b * BlueWeight) + (g * GreenWeight) + (r * RedWeight)) >> WeightShift);
            if (grey[i] != expected)
            {
                mismatches++;
            }
        }

        await Assert.That(mismatches).IsEqualTo(0);
    }

    /// <summary>Verifies a short destination limits the conversion.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StopsAtShortDestination()
    {
        var bgra = new byte[PixelCount * BytesPerPixel];
        bgra.AsSpan().Fill(byte.MaxValue);
        var grey = new byte[PixelCount / RedOffset];
        OcrRunner.ToGrey(bgra, grey);

        await Assert.That(grey[^1]).IsEqualTo(byte.MaxValue);
    }

    /// <summary>Verifies the mean word confidence, which decides whether to ask for the document's language.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AveragesWordConfidence()
    {
        const float clear = 90;
        const float poor = 30;
        const float mean = 60;
        List<OcrWord> words = [new("clear", default, clear), new("poor", default, poor)];

        await Assert.That(OcrRunner.AverageConfidence(words)).IsEqualTo(mean);
        await Assert.That(OcrRunner.AverageConfidence([])).IsEqualTo(0F);
    }
}
