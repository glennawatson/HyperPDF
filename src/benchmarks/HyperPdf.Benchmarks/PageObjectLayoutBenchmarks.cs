// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Geometry;
using HyperPdf.ViewModels.Markup;

namespace HyperPdf.Benchmarks;

/// <summary>Measures working out where picked objects move to line up and to be spread evenly.</summary>
public class PageObjectLayoutBenchmarks
{
    /// <summary>How many objects are picked.</summary>
    private const int Count = 32;

    /// <summary>The side of each box.</summary>
    private const float Side = 20;

    /// <summary>The gap between the starts of neighbouring boxes, with some irregularity.</summary>
    private const float Stride = 37;

    /// <summary>How many rows the boxes are staggered over.</summary>
    private const int Rows = 5;

    /// <summary>The boxes of the picked objects.</summary>
    private readonly PageRect[] _boxes = new PageRect[Count];

    /// <summary>Where each box moves to.</summary>
    private readonly PagePoint[] _moves = new PagePoint[Count];

    /// <summary>Lays the boxes out unevenly along the page.</summary>
    [GlobalSetup]
    public void Setup()
    {
        for (var i = 0; i < Count; i++)
        {
            _boxes[i] = new(i * Stride, (i % Rows) * Side, Side, Side);
        }
    }

    /// <summary>Gets the box around the boxes.</summary>
    /// <returns>The box.</returns>
    [Benchmark(Baseline = true)]
    public PageRect Union() => PageObjectLayout.Union(_boxes);

    /// <summary>Lines the boxes up on their left edges.</summary>
    /// <returns>The first move.</returns>
    [Benchmark]
    public PagePoint AlignLeft()
    {
        PageObjectLayout.Align(_boxes, PageObjectAlignment.Left, _moves);
        return _moves[0];
    }

    /// <summary>Spreads the boxes evenly from left to right.</summary>
    /// <returns>The first move.</returns>
    [Benchmark]
    public PagePoint SpreadAcross()
    {
        PageObjectLayout.Spread(_boxes, true, _moves);
        return _moves[1];
    }
}
