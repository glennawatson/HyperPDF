// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the zoom label over a 30 frame ease from 100% to 130%: rebuilt on every frame against rebuilt only when the rounded percent changes.</summary>
public class ZoomLabelEaseBenchmarks
{
    /// <summary>The frames in the ease.</summary>
    private const int Frames = 30;

    /// <summary>The zoom the ease starts at.</summary>
    private const double StartZoom = 1;

    /// <summary>The zoom the ease ends at.</summary>
    private const double EndZoom = 1.3;

    /// <summary>The zoom of each frame.</summary>
    private readonly double[] _zoomPerFrame = new double[Frames];

    /// <summary>Fills the zoom of each frame.</summary>
    [GlobalSetup]
    public void Setup()
    {
        for (var frame = 0; frame < Frames; frame++)
        {
            _zoomPerFrame[frame] = StartZoom + ((EndZoom - StartZoom) * (frame + 1) / Frames);
        }
    }

    /// <summary>Builds the label text on every frame.</summary>
    /// <returns>The length of the last label.</returns>
    [Benchmark(Baseline = true)]
    public int BuildLabelEveryFrame()
    {
        var length = 0;
        foreach (var zoom in _zoomPerFrame)
        {
            length = ZoomLabel.Format(ZoomLabel.RoundPercent(zoom)).Length;
        }

        return length;
    }

    /// <summary>Builds the label text only when the rounded percent changes.</summary>
    /// <returns>The length of the last label.</returns>
    [Benchmark]
    public int BuildLabelWhenPercentChanges()
    {
        var shown = ZoomLabel.RoundPercent(StartZoom);
        var text = ZoomLabel.Format(shown);
        foreach (var zoom in _zoomPerFrame)
        {
            var percent = ZoomLabel.RoundPercent(zoom);
            if (percent == shown)
            {
                continue;
            }

            shown = percent;
            text = ZoomLabel.Format(percent);
        }

        return text.Length;
    }
}
