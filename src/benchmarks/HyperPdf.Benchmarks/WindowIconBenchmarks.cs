// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Viewer.Icons;
using HyperPdf.Viewer.Text;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the window's own icons. A frame asks for an icon it has already drawn, which is the path that must cost next to nothing
/// and allocate nothing. Stroking an icon for the first time happens once for each size and colour.
/// </summary>
public class WindowIconBenchmarks
{
    /// <summary>The side of an icon beside text at a scale of one.</summary>
    private const int IconSize = 20;

    /// <summary>The side of an icon on a display at twice the scale.</summary>
    private const int LargeIconSize = 40;

    /// <summary>The colour of the icons.</summary>
    private const uint IconColor = 0xFF8FA8B8;

    /// <summary>The coverage buffer the strokes are added to.</summary>
    private readonly CoverageBuffer _coverage = new();

    /// <summary>The backend whose surfaces hold the drawn icons.</summary>
    private CpuRenderBackend _backend = null!;

    /// <summary>The drawn icons.</summary>
    private IconRuns _icons = null!;

    /// <summary>Draws the icon the frame path asks for.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _backend = new(static (_, _, _) => { });
        _icons = new(_backend.Surfaces);
        _ = _icons.Get(ChromeIcon.Save, IconSize, IconColor);
    }

    /// <summary>Frees the icons.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _icons.Dispose();
        _backend.Dispose();
    }

    /// <summary>Gets an icon that is already drawn, as every frame does.</summary>
    /// <returns>The icon's width.</returns>
    [Benchmark(Baseline = true)]
    public int GetDrawnIcon() => _icons.Get(ChromeIcon.Save, IconSize, IconColor)?.Width ?? 0;

    /// <summary>Finds what kind of action an icon stands for, which picks its tint for each frame.</summary>
    /// <returns>The tone.</returns>
    [Benchmark]
    public IconTone FindTone() => IconPaths.ToneOf(ChromeIcon.Save);

    /// <summary>Strokes an icon of straight lines into the coverage buffer, as the first use of an icon does.</summary>
    /// <returns>The buffer's width.</returns>
    [Benchmark]
    public int StrokeLineIcon()
    {
        IconRuns.Cover(_coverage, ChromeIcon.Save, IconSize);
        return _coverage.Width;
    }

    /// <summary>Strokes an icon made of arcs and curves, which are cut into short lines.</summary>
    /// <returns>The buffer's width.</returns>
    [Benchmark]
    public int StrokeCurvedIcon()
    {
        IconRuns.Cover(_coverage, ChromeIcon.Attachment, IconSize);
        return _coverage.Width;
    }

    /// <summary>Strokes an icon for a display at twice the scale.</summary>
    /// <returns>The buffer's width.</returns>
    [Benchmark]
    public int StrokeLargeIcon()
    {
        IconRuns.Cover(_coverage, ChromeIcon.Save, LargeIconSize);
        return _coverage.Width;
    }
}
