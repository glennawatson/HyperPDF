// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures making and placing a drawn signature: smoothing a stroke, making the mark, scaling its points onto the
/// page, and one keyboard move and resize while placing. Allocations are checked from the EventPipe trace.
/// </summary>
public class SignatureMarkBenchmarks
{
    /// <summary>The pointer samples in the stroke, about a second of drawing.</summary>
    private const int Samples = 240;

    /// <summary>The stroke's width in pad units.</summary>
    private const float StrokeWidth = 300;

    /// <summary>The stroke's wave height in pad units.</summary>
    private const float WaveHeight = 40;

    /// <summary>The waves across the stroke.</summary>
    private const float Waves = 6;

    /// <summary>The coordinates stored for each drawn point.</summary>
    private const int Coordinates = 2;

    /// <summary>A letter page.</summary>
    private static readonly PageSize Page = PageSize.Letter;

    /// <summary>Where the mark is placed.</summary>
    private static readonly PageRect Bounds = new(100, 600, 180, 36);

    /// <summary>The pointer samples.</summary>
    private PagePoint[] _samples = [];

    /// <summary>The stroke lengths: one stroke.</summary>
    private int[] _lengths = [];

    /// <summary>The smoothed stroke, reused.</summary>
    private PagePoint[] _smoothed = [];

    /// <summary>The drawn mark.</summary>
    private SignatureMark _mark = null!;

    /// <summary>The mark's points on the page, reused.</summary>
    private PagePoint[] _mapped = [];

    /// <summary>Creates a wavy stroke and its mark.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _samples = new PagePoint[Samples];
        for (var i = 0; i < Samples; i++)
        {
            var across = (float)i / (Samples - 1);
            _samples[i] = new(across * StrokeWidth, WaveHeight * (1 + MathF.Sin(across * Waves * MathF.Tau)));
        }

        _lengths = [Samples];
        _smoothed = new PagePoint[SignatureStrokes.SmoothedLength(Samples)];
        _mark = SignatureMark.Drawn(SignatureMarkKind.Signature, _samples, _lengths)!;
        _mapped = new PagePoint[_mark.Points.Length / Coordinates];
    }

    /// <summary>Smooths one stroke into a reused buffer.</summary>
    [Benchmark]
    public void SmoothStroke() => SignatureStrokes.Smooth(_samples, _smoothed);

    /// <summary>Makes a drawn mark from one stroke, as each stroke on the pad does.</summary>
    /// <returns>The mark's width.</returns>
    [Benchmark]
    public float MakeDrawnMark() => SignatureMark.Drawn(SignatureMarkKind.Signature, _samples, _lengths)!.Width;

    /// <summary>Scales the mark's points onto the page, as placing it does.</summary>
    [Benchmark]
    public void MapPoints() => SignatureMarkLayout.MapPoints(_mark, Bounds, _mapped);

    /// <summary>One arrow key move and one resize step while placing.</summary>
    /// <returns>The new width.</returns>
    [Benchmark]
    public float MoveAndResize() =>
        SignatureMarkLayout.Resize(SignatureMarkLayout.Move(Bounds, SignatureMarkLayout.MoveStep, SignatureMarkLayout.FineMoveStep, Page), SignatureMarkLayout.ResizeStep, Page).Width;
}
