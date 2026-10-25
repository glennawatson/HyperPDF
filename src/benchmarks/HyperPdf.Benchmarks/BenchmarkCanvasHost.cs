// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.Presentation.Canvas;

namespace HyperPdf.Benchmarks;

/// <summary>A scrolling area with no toolkit behind it.</summary>
internal sealed class BenchmarkCanvasHost : ICanvasHost
{
    /// <summary>Gets or sets the scroll offset.</summary>
    public CanvasPoint Offset { get; set; }

    /// <summary>Gets or sets the viewport size.</summary>
    public CanvasSize Viewport { get; set; }

    /// <summary>Gets or sets the extent size.</summary>
    public CanvasSize Extent { get; set; }

    /// <inheritdoc/>
    public bool HasScroller => true;

    /// <inheritdoc/>
    public bool IsInWindow => false;

    /// <inheritdoc/>
    public CanvasPoint ScrollOffset
    {
        get => Offset;
        set => Offset = value;
    }

    /// <inheritdoc/>
    public CanvasSize ViewportSize => Viewport;

    /// <inheritdoc/>
    public CanvasSize ExtentSize => Extent;

    /// <inheritdoc/>
    public CanvasSize ScrollerSize => Viewport;

    /// <inheritdoc/>
    public void InvalidateMeasure()
    {
    }

    /// <inheritdoc/>
    public void InvalidateVisual()
    {
    }

    /// <inheritdoc/>
    public void UpdateLayout()
    {
    }

    /// <inheritdoc/>
    public void PageScaleChanged(double before, double after)
    {
    }

    /// <inheritdoc/>
    public void WheelZoomChanged(WheelZoomEase before, WheelZoomEase after)
    {
    }
}
