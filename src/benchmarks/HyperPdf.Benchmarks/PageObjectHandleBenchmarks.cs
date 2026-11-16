// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Geometry;
using HyperPdf.ViewModels.Markup;

namespace HyperPdf.Benchmarks;

/// <summary>Measures what the pointer does on every move while a picked object is resized: finding the handle under the pointer, dragging it and fitting the change.</summary>
public class PageObjectHandleBenchmarks
{
    /// <summary>The left edge of the picked object's box.</summary>
    private const float Left = 100;

    /// <summary>The top edge of the picked object's box.</summary>
    private const float Top = 200;

    /// <summary>The width of the picked object's box.</summary>
    private const float Width = 180;

    /// <summary>The height of the picked object's box.</summary>
    private const float Height = 90;

    /// <summary>How far from a handle still counts as on it, in points.</summary>
    private const float Reach = 6;

    /// <summary>How far past the corner the pointer is dragged, in points.</summary>
    private const float Drag = 40;

    /// <summary>The horizontal middle of the box, measured from its left edge.</summary>
    private const float MiddleX = 90;

    /// <summary>A point below the middle of the box that is clear of every handle.</summary>
    private const float MiddleY = 57;

    /// <summary>The box of the picked object.</summary>
    private readonly PageRect _box = new(Left, Top, Width, Height);

    /// <summary>Finds the handle under a point on the bottom right corner.</summary>
    /// <returns>The handle.</returns>
    [Benchmark(Baseline = true)]
    public PageObjectHandle FindHandle() => PageObjectHandles.HitTest(_box, new(Left + Width, Top + Height), Reach);

    /// <summary>Finds no handle for a point in the middle of the box, the common case while the pointer moves over the object.</summary>
    /// <returns>The handle.</returns>
    [Benchmark]
    public PageObjectHandle FindNoHandle() => PageObjectHandles.HitTest(_box, new(Left + MiddleX, Top + MiddleY), Reach);

    /// <summary>Finds no handle for a point far outside the box, the most common case while the pointer moves over the page.</summary>
    /// <returns>The handle.</returns>
    [Benchmark]
    public PageObjectHandle FindOutside() => PageObjectHandles.HitTest(_box, new(Left - Drag, Top - Drag), Reach);

    /// <summary>Drags the bottom right corner with the proportions kept.</summary>
    /// <returns>The new box.</returns>
    [Benchmark]
    public PageRect DragCorner() => PageObjectHandles.Drag(_box, PageObjectHandle.BottomRight, new(Left + Width + Drag, Top + Height + Drag), keepShape: true);

    /// <summary>Works out the change that carries the box onto the dragged one, and the box around the result.</summary>
    /// <returns>The box around the carried corners.</returns>
    [Benchmark]
    public PageRect FitAndCarry()
    {
        var dragged = PageObjectHandles.Drag(_box, PageObjectHandle.Right, new(Left + Width + Drag, Top), keepShape: false);
        var change =PageObjectHandles.Fit(_box, dragged);
        return PageObjectHandles.Carry(_box, change);
    }
}
