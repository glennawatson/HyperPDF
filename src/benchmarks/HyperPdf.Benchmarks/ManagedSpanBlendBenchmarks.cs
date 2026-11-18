// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Render.Managed.Raster;

namespace HyperPdf.Benchmarks;

/// <summary>Measures putting a translucent colour over a line of pixels with the managed rasteriser, against the one pixel at a time loop it replaced.</summary>
public class ManagedSpanBlendBenchmarks
{
    /// <summary>The pixels in a line: a US letter page at twice its size.</summary>
    private const int Pixels = 1224;

    /// <summary>The pixels in a run of coverage, about the width of a letter.</summary>
    private const int ShortRun = 12;

    /// <summary>The translucent premultiplied colour.</summary>
    private const uint Color = 0x80402010U;

    /// <summary>What the colour leaves of the pixels under it: 255 less the colour's alpha of 128.</summary>
    private const uint Keep = 127;

    /// <summary>The coverage that means fully covered, and the alpha of an opaque pixel.</summary>
    private const uint FullCoverage = 255;

    /// <summary>The shift that moves alpha to the lowest byte of a pixel.</summary>
    private const int AlphaShift = 24;

    /// <summary>The multiplier that fills the test lines with varied bytes.</summary>
    private const uint Spread = 2_654_435_761U;

    /// <summary>The line the benchmarks blend onto.</summary>
    private readonly uint[] _line = new uint[Pixels];

    /// <summary>The colour of each pixel for the shaded benchmarks.</summary>
    private readonly uint[] _colors = new uint[Pixels];

    /// <summary>The coverage of each pixel.</summary>
    private readonly byte[] _coverage = new byte[Pixels];

    /// <summary>The clip's coverage.</summary>
    private readonly byte[] _mask = new byte[Pixels];

    /// <summary>Fills the lines with varied bytes.</summary>
    [GlobalSetup]
    public void Setup()
    {
        for (var i = 0; i < Pixels; i++)
        {
            var color = (uint)(i + 1) * Spread;
            _line[i] = color | 0xFF000000U;
            _colors[i] = SpanBlender.Scale(color, color >> AlphaShift) | (color & 0xFF000000U);
            _coverage[i] = (byte)(color >> 8);
            _mask[i] = (byte)(color >> 16);
        }
    }

    /// <summary>Blends one pixel at a time, as the rasteriser did before it used vectors.</summary>
    /// <returns>A pixel, so the work is not optimised away.</returns>
    [Benchmark(Baseline = true)]
    public uint OnePixelAtATime()
    {
        foreach (ref var pixel in _line.AsSpan())
        {
            pixel = Color + SpanBlender.Scale(pixel, Keep);
        }

        return _line[0];
    }

    /// <summary>Blends a line of colours through coverage, as the rasteriser does for gradients and pictures.</summary>
    /// <returns>A pixel, so the work is not optimised away.</returns>
    [Benchmark]
    public uint ShadedWithVectors()
    {
        SpanBlender.Shaded(_line, _coverage, _colors);
        return _line[0];
    }

    /// <summary>Blends a line of colours through coverage, one pixel at a time.</summary>
    /// <returns>A pixel, so the work is not optimised away.</returns>
    [Benchmark]
    public uint ShadedOnePixelAtATime()
    {
        for (var i = 0; i < _line.Length; i++)
        {
            var value = _coverage[i];
            if (value == 0)
            {
                continue;
            }

            var color = value == FullCoverage ? _colors[i] : SpanBlender.Scale(_colors[i], value);
            _line[i] = (color >> AlphaShift) == FullCoverage ? color : color + SpanBlender.Scale(_line[i], FullCoverage - (color >> AlphaShift));
        }

        return _line[0];
    }

    /// <summary>Blends one colour through the coverage of short runs, the length of a row of a glyph, which is how text edges are drawn.</summary>
    /// <returns>A pixel, so the work is not optimised away.</returns>
    [Benchmark]
    public uint SolidInShortRuns()
    {
        for (var start = 0; start + ShortRun <= Pixels; start += ShortRun)
        {
            SpanBlender.SolidEach(_line.AsSpan(start, ShortRun), _coverage.AsSpan(start, ShortRun), Color);
        }

        return _line[0];
    }

    /// <summary>Multiplies a shape's coverage by a clip's with vectors.</summary>
    /// <returns>A byte, so the work is not optimised away.</returns>
    [Benchmark]
    public byte MultiplyWithVectors()
    {
        SpanBlender.Multiply(_coverage, _mask);
        return _coverage[0];
    }

    /// <summary>Blends with vectors.</summary>
    /// <returns>A pixel, so the work is not optimised away.</returns>
    [Benchmark]
    public uint Vectors()
    {
        SpanBlender.Over(_line, Color);
        return _line[0];
    }
}
