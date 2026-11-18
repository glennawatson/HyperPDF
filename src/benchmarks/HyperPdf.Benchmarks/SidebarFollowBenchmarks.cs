// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.ViewModels.Rendering;
using HyperPdf.Viewer.Chrome;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the choice of where the list of pages in the side panel starts, against the calculation the Avalonia list made for the
/// same pages. The choice runs each time the page being read changes, so it must cost next to nothing and allocate nothing.
/// </summary>
public class SidebarFollowBenchmarks
{
    /// <summary>The rows that fit the panel.</summary>
    private const int Visible = 6;

    /// <summary>The rows in the list.</summary>
    private const int Count = 2000;

    /// <summary>The first row shown.</summary>
    private const int First = 100;

    /// <summary>The height of a row.</summary>
    private const double RowHeight = 100;

    /// <summary>A page in view, which leaves the list where it is.</summary>
    private const int InView = First + 2;

    /// <summary>The page just past the bottom edge.</summary>
    private const int PastBottom = First + Visible;

    /// <summary>A page far from the rows shown.</summary>
    private const int Far = 1500;

    /// <summary>Gets the offset the Avalonia list chose for a page that is in view.</summary>
    /// <returns>The offset.</returns>
    [Benchmark(Baseline = true)]
    public double AvaloniaInView() => Avalonia(InView);

    /// <summary>Gets the first row for a page that is in view.</summary>
    /// <returns>The first row.</returns>
    [Benchmark]
    public int InViewStaysStill() => SidebarFollow.First(First, Visible, Count, InView);

    /// <summary>Gets the offset the Avalonia list chose for the page just past the bottom edge.</summary>
    /// <returns>The offset.</returns>
    [Benchmark]
    public double AvaloniaPastBottom() => Avalonia(PastBottom);

    /// <summary>Gets the first row for the page just past the bottom edge.</summary>
    /// <returns>The first row.</returns>
    [Benchmark]
    public int EdgeMovesOneRow() => SidebarFollow.First(First, Visible, Count, PastBottom);

    /// <summary>Gets the offset the Avalonia list chose for a page far away.</summary>
    /// <returns>The offset.</returns>
    [Benchmark]
    public double AvaloniaFar() => Avalonia(Far);

    /// <summary>Gets the first row for a page far away.</summary>
    /// <returns>The first row.</returns>
    [Benchmark]
    public int FarJumpMovesToThePage() => SidebarFollow.First(First, Visible, Count, Far);

    /// <summary>Makes the Avalonia list's choice for a page.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The offset.</returns>
    private static double Avalonia(int page) =>
        ThumbnailViewport.GetOffset(First * RowHeight, Visible * RowHeight, page * RowHeight, (page + 1) * RowHeight, Count * RowHeight);
}
