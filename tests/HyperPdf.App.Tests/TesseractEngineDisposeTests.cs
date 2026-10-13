// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Ocr;
using HyperPdf.Ocr;

namespace HyperPdf.App.Tests;

/// <summary>Tests that disposing the Tesseract engine waits for a recognition in progress.</summary>
public sealed class TesseractEngineDisposeTests
{
    /// <summary>The language the app ships.</summary>
    private const string Language = "eng";

    /// <summary>The edge of the blank test image, in pixels.</summary>
    private const int Edge = 1200;

    /// <summary>The resolution of the test image.</summary>
    private const float PixelsPerPoint = 300F / 72F;

    /// <summary>The engines made and disposed while a recognition runs.</summary>
    private const int Rounds = 8;

    /// <summary>A recognition running when the engine is disposed finishes first, and a recognition after the dispose finds nothing and does not fail.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    /// <exception cref="TUnit.Core.Exceptions.SkipTestException">No Tesseract ships for this runtime.</exception>
    [Test]
    public async Task DisposeWaitsForARecognitionInProgress(CancellationToken cancellationToken)
    {
        var image = new byte[Edge * Edge];
        image.AsSpan().Fill(byte.MaxValue);
        for (var round = 0; round < Rounds; round++)
        {
            var engine = new TesseractEngine(Language);
            if (!engine.IsAvailable)
            {
                engine.Dispose();
                throw new TUnit.Core.Exceptions.SkipTestException("No Tesseract ships for this runtime.");
            }

            var words = new List<OcrWord>();
            var recognise = Task.Run(() => engine.Recognize(image, Edge, Edge, PixelsPerPoint, words), cancellationToken);
            var dispose = Task.Run(engine.Dispose, cancellationToken);
            await Task.WhenAll(recognise, dispose);

            engine.Recognize(image, Edge, Edge, PixelsPerPoint, words);
            await Assert.That(words.Count).IsEqualTo(0);
        }
    }
}
