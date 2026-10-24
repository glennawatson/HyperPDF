// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Skia;
using SkiaSharp;

namespace HyperPdf.App.Tests;

/// <summary>Checks the native gradient bridge against SkiaSharp's shader factory.</summary>
public sealed class NativeMethodsTests
{
    /// <summary>The TranslationX used by the test workload.</summary>
    private const int TranslationX = 2;

    /// <summary>The TranslationY used by the test workload.</summary>
    private const int TranslationY = 3;

    /// <summary>Verifies that span-based stops produce identical raster pixels.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Linear_SpanStops_MatchesSkiaFactory()
    {
        const int size = 32;
        SKColor[] colors = [SKColors.Red, SKColors.Green, SKColors.Blue];
        float[] offsets = [0, 0.4F, 1];
        var start = new SKPoint(0, 0);
        var end = new SKPoint(size, size);
        var matrix = SKMatrix.CreateTranslation(TranslationX, TranslationY);
        using var expectedShader = SKShader.CreateLinearGradient(start, end, colors, offsets, SKShaderTileMode.Mirror, matrix);
        using var actual = new SKBitmap(size, size);
        using var expected = new SKBitmap(size, size);
        using var actualCanvas = new SKCanvas(actual);
        using var expectedCanvas = new SKCanvas(expected);
        using var paint = new SKPaint();
        NativeMethods.AssignShader(paint, NativeMethods.Linear(start, end, colors, offsets, SKShaderTileMode.Mirror, ref matrix));
        actualCanvas.DrawRect(new(0, 0, size, size), paint);
        paint.Shader = expectedShader;
        expectedCanvas.DrawRect(new(0, 0, size, size), paint);
        await Assert.That(actual.GetPixelSpan().SequenceEqual(expected.GetPixelSpan())).IsTrue();
    }
}
