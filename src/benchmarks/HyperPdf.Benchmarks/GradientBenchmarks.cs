// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Skia;
using SkiaSharp;

namespace HyperPdf.Benchmarks;

/// <summary>Measures gradient stop conversion and native shader creation.</summary>
public class GradientBenchmarks
{
    /// <summary>The gradient length in pixels.</summary>
    private const int GradientLength = 32;

    /// <summary>The middle stop position.</summary>
    private const float MiddleStop = 0.5F;

    /// <summary>The session paint shared by both cases.</summary>
    private SKPaint _paint = null!;

    /// <summary>Creates the context-owned paint.</summary>
    [GlobalSetup]
    public void Setup() => _paint = new();

    /// <summary>Releases the context-owned paint.</summary>
    [GlobalCleanup]
    public void Cleanup() => _paint.Dispose();

    /// <summary>Creates a shader through SkiaSharp's array API.</summary>
    [Benchmark(Baseline = true)]
    public void ArrayStops()
    {
        SKColor[] colors = [SKColors.Red, SKColors.Green, SKColors.Blue];
        float[] offsets = [0, MiddleStop, 1];
        using var shader = SKShader.CreateLinearGradient(new(0, 0), new(GradientLength, GradientLength), colors, offsets, SKShaderTileMode.Clamp);
        _paint.Shader = shader;
        _paint.Reset();
    }

    /// <summary>Creates an equivalent shader with stack-allocated stops.</summary>
    [Benchmark]
    public void SpanStops()
    {
        Span<SKColor> colors = [SKColors.Red, SKColors.Green, SKColors.Blue];
        Span<float> offsets = [0, MiddleStop, 1];
        var matrix = SKMatrix.Identity;
        NativeMethods.AssignShader(_paint, NativeMethods.Linear(new(0, 0), new(GradientLength, GradientLength), colors, offsets, SKShaderTileMode.Clamp, ref matrix));
        _paint.Reset();
    }
}
