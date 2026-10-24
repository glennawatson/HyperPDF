// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Ocr;
using HyperPdf.Core.Tests.Fakes;

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

    /// <summary>A cancelled token stops the recognition before the page is rendered or the recogniser runs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledTokenStopsRecognition()
    {
        using var document = new FakeDocument("scan.pdf", FakeEngine.A4) { PageText = string.Empty };
        using var engine = new CountingEngine();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        List<OcrWord> words = [];

        await Assert.That(() => OcrRunner.RecognizeWords(document, engine, 0, words, cancelled.Token)).Throws<OperationCanceledException>();
        await Assert.That(document.RenderCount).IsEqualTo(0);
        await Assert.That(engine.Calls).IsEqualTo(0);
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

    /// <summary>A recogniser that counts the images it is given.</summary>
    private sealed class CountingEngine : IOcrEngine
    {
        /// <inheritdoc/>
        public bool IsAvailable => true;

        /// <inheritdoc/>
        public OcrEngineStatus Status => OcrEngineStatus.Ready;

        /// <summary>Gets the images recognised.</summary>
        internal int Calls { get; private set; }

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <inheritdoc/>
        public void Recognize(ReadOnlySpan<byte> image, int width, int height, float pixelsPerPoint, List<OcrWord> output) => Calls++;
    }
}
