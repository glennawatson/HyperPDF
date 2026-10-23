// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Tests;

/// <summary>A canvas host with no toolkit: a scrolling area the tests set, and a count of the redraws asked for.</summary>
internal sealed class FakeCanvasHost : ICanvasHost
{
    /// <inheritdoc/>
    public bool HasScroller { get; set; } = true;

    /// <inheritdoc/>
    public bool IsInWindow { get; set; }

    /// <inheritdoc/>
    public CanvasPoint ScrollOffset { get; set; }

    /// <inheritdoc/>
    public CanvasSize ViewportSize { get; set; }

    /// <inheritdoc/>
    public CanvasSize ExtentSize { get; set; }

    /// <inheritdoc/>
    public CanvasSize ScrollerSize { get; set; }

    /// <summary>Gets how many times a new measure was asked for.</summary>
    public int Measures { get; private set; }

    /// <summary>Gets how many times a redraw was asked for.</summary>
    public int Redraws { get; private set; }

    /// <summary>Gets a value indicating whether a text selection drag is under way.</summary>
    public bool Dragging { get; private set; }

    /// <summary>Gets how many times the wheel zoom ease changed.</summary>
    public int EaseChanges { get; private set; }

    /// <inheritdoc/>
    public void InvalidateMeasure() => Measures++;

    /// <inheritdoc/>
    public void InvalidateVisual() => Redraws++;

    /// <inheritdoc/>
    public void UpdateLayout()
    {
    }

    /// <inheritdoc/>
    public void PageScaleChanged(double before, double after)
    {
    }

    /// <inheritdoc/>
    public void WheelZoomChanged(WheelZoomEase before, WheelZoomEase after) => EaseChanges++;

    /// <inheritdoc/>
    public void SelectionDragChanged(bool dragging) => Dragging = dragging;
}
