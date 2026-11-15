// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using BenchmarkDotNet.Attributes;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Viewer.Text;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the window's own text: tab names, button labels, menu rows. A label is drawn once from the font's outlines into a
/// surface and kept, so the first use and every later use are measured apart.
/// </summary>
public class WindowTextBenchmarks
{
    /// <summary>The labels drawn for the first time, more than the cache keeps, so each one is drawn from its outlines.</summary>
    private const int Labels = 2048;

    /// <summary>The text size in pixels.</summary>
    private const int TextSize = 14;

    /// <summary>The text colour.</summary>
    private const uint TextColor = 0xFFE0E0E0;

    /// <summary>The width a long name is cut to, in pixels.</summary>
    private const int FitWidth = 180;

    /// <summary>A label of the length of a toolbar button's.</summary>
    private const string Label = "Fit width";

    /// <summary>The style of the text.</summary>
    private static readonly TextStyle Style = new(TextSize, false, TextColor);

    /// <summary>The labels that are drawn for the first time.</summary>
    private readonly string[] _labels = new string[Labels];

    /// <summary>The backend whose surfaces hold the text.</summary>
    private CpuRenderBackend _backend = null!;

    /// <summary>The drawn text.</summary>
    private TextRuns _runs = null!;

    /// <summary>The next label to draw for the first time.</summary>
    private int _next;

    /// <summary>Makes the text cache and the labels, and draws the cached label once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _backend = new(static (_, _, _) => { });
        _runs = new(_backend.Surfaces);
        for (var i = 0; i < _labels.Length; i++)
        {
            _labels[i] = string.Create(CultureInfo.InvariantCulture, $"Page {i} of {Labels}");
        }

        _ = _runs.Get(Label, Style);
    }

    /// <summary>Frees the text and the backend.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _runs.Dispose();
        _backend.Dispose();
    }

    /// <summary>Gets a label that was drawn before, as every frame does for every control.</summary>
    /// <returns>The width of the label.</returns>
    [Benchmark(Baseline = true)]
    public int GetDrawnLabel() => _runs.Get(Label, Style).Width;

    /// <summary>Measures a label without drawing it, as laying out a row of buttons does.</summary>
    /// <returns>The width of the label.</returns>
    [Benchmark]
    public int MeasureLabel() => _runs.Measure(Label, Style);

    /// <summary>Draws a label for the first time: its outlines are turned into coverage and written into a new surface.</summary>
    /// <returns>The width of the label.</returns>
    [Benchmark]
    public int DrawLabelFirstTime()
    {
        _next = (_next + 1) % _labels.Length;
        return _runs.Get(_labels[_next], Style).Width;
    }

    /// <summary>Measures how much of a long name fits a tab, which cuts it and adds an ellipsis.</summary>
    /// <returns>The width of the whole name.</returns>
    [Benchmark]
    public int MeasureLongName() => Math.Min(FitWidth, _runs.Measure("A long document name that does not fit its tab.pdf", Style));
}
