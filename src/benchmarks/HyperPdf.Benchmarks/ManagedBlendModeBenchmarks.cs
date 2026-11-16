// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Render.Managed.Raster;

namespace HyperPdf.Benchmarks;

/// <summary>Measures blending a line of colours onto a line with the managed rasteriser's blend modes.</summary>
public class ManagedBlendModeBenchmarks
{
    /// <summary>The pixels in a line: a US letter page at twice its size.</summary>
    private const int Pixels = 1224;

    /// <summary>The multiplier that fills the test lines with varied bytes.</summary>
    private const uint Spread = 2_654_435_761U;

    /// <summary>The shift that moves alpha to the lowest byte of a pixel.</summary>
    private const int AlphaShift = 24;

    /// <summary>The line the benchmarks blend onto.</summary>
    private readonly uint[] _line = new uint[Pixels];

    /// <summary>The colour of each pixel.</summary>
    private readonly uint[] _colors = new uint[Pixels];

    /// <summary>The coverage of each pixel.</summary>
    private readonly byte[] _coverage = new byte[Pixels];

    /// <summary>Full coverage for every pixel.</summary>
    private readonly byte[] _full = new byte[Pixels];

    /// <summary>Opaque colours.</summary>
    private readonly uint[] _opaque = new uint[Pixels];

    /// <summary>Fills the lines with varied bytes.</summary>
    [GlobalSetup]
    public void Setup()
    {
        for (var i = 0; i < Pixels; i++)
        {
            var color = (uint)(i + 1) * Spread;
            _line[i] = color | 0xFF000000U;
            _colors[i] = SpanBlender.Scale(color, color >> AlphaShift) | (color & 0xFF000000U);
            _coverage[i] = (byte)((color >> 8) | 1);
            _full[i] = byte.MaxValue;
            _opaque[i] = color | 0xFF000000U;
        }
    }

    /// <summary>Blends opaque colours with overlay, the usual case of a drawing over a page.</summary>
    /// <returns>A pixel, so the work is not optimised away.</returns>
    [Benchmark]
    public uint OverlayOpaque()
    {
        BlendModes.Apply(PdfBlendMode.Overlay, _line, _full, _opaque);
        return _line[0];
    }

    /// <summary>Blends opaque colours with multiply.</summary>
    /// <returns>A pixel, so the work is not optimised away.</returns>
    [Benchmark]
    public uint MultiplyOpaque()
    {
        BlendModes.Apply(PdfBlendMode.Multiply, _line, _full, _opaque);
        return _line[0];
    }

    /// <summary>Blends with multiply.</summary>
    /// <returns>A pixel, so the work is not optimised away.</returns>
    [Benchmark]
    public uint Multiply() => Run(PdfBlendMode.Multiply);

    /// <summary>Blends with overlay, which has a case for each channel.</summary>
    /// <returns>A pixel, so the work is not optimised away.</returns>
    [Benchmark]
    public uint Overlay() => Run(PdfBlendMode.Overlay);

    /// <summary>Blends with hue, which works on the whole colour.</summary>
    /// <returns>A pixel, so the work is not optimised away.</returns>
    [Benchmark]
    public uint Hue() => Run(PdfBlendMode.Hue);

    /// <summary>Blends the colours onto the line with a mode.</summary>
    /// <param name="mode">The blend mode.</param>
    /// <returns>A pixel.</returns>
    private uint Run(PdfBlendMode mode)
    {
        BlendModes.Apply(mode, _line, _coverage, _colors);
        return _line[0];
    }
}
