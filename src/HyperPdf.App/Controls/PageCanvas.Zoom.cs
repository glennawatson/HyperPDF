// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Layout;

namespace HyperPdf.App.Controls;

/// <summary>
/// Wheel zoom: each notch eases to its new zoom over a short time, keeping the point under the pointer still. While
/// the zoom moves, pages show the last sharp tiles stretched to fit, and new tiles are requested once it settles.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>The power of the ease out curve.</summary>
    private const double ZoomEasePower = 3;

    /// <summary>How long one wheel zoom takes to ease to its target.</summary>
    private static readonly TimeSpan ZoomDuration = TimeSpan.FromMilliseconds(140);

    /// <summary>The zoom when the current easing started.</summary>
    private double _zoomFrom;

    /// <summary>The zoom the current easing ends at.</summary>
    private double _zoomTo;

    /// <summary>When the current easing started.</summary>
    private long _zoomStarted;

    /// <summary>The anchor in viewport coordinates, which stays still while the zoom eases.</summary>
    private Point _zoomAnchor;

    /// <summary>1 while a wheel zoom is easing, so tiles at passing scales are not requested.</summary>
    private int _zoomEasing;

    /// <summary>Whether the canvas is moving its own scroll offset to hold a zoom anchor.</summary>
    private bool _anchoring;

    /// <summary>Gets a value indicating whether a wheel zoom is easing.</summary>
    internal bool IsZoomEasing => Volatile.Read(ref _zoomEasing) != 0;

    /// <summary>Starts or extends a wheel zoom towards a new factor.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="factor">The zoom multiplier.</param>
    /// <param name="anchor">The canvas point under the pointer.</param>
    private void ZoomByWheel(DocumentTabViewModel tab, double factor, Point anchor)
    {
        if (_scroller is null)
        {
            return;
        }

        var target = ZoomCalculator.Clamp((IsZoomEasing ? _zoomTo : tab.Zoom) * factor);
        if (tab.ReduceMotion || TopLevel.GetTopLevel(this) is not { } top)
        {
            ZoomAround(tab, target, anchor);
            return;
        }

        _zoomFrom = tab.Zoom;
        _zoomTo = target;
        _zoomStarted = Stopwatch.GetTimestamp();
        _zoomAnchor = anchor - _scroller.Offset;

        // A running easing picks up the new target on its next frame.
        if (Interlocked.Exchange(ref _zoomEasing, 1) == 0)
        {
            top.RequestAnimationFrame(OnZoomFrame);
        }
    }

    /// <summary>Moves the easing zoom one frame on.</summary>
    /// <param name="time">The frame time, unused because the easing measures its own start.</param>
    private void OnZoomFrame(TimeSpan time)
    {
        if (_scroller is null || Tab is not { } tab)
        {
            _ = Interlocked.Exchange(ref _zoomEasing, 0);
            return;
        }

        var progress = Math.Min(1, Stopwatch.GetElapsedTime(_zoomStarted) / ZoomDuration);

        // Cubic ease out, applied to the zoom ratio so each frame scales by the same proportion.
        var eased = 1 - Math.Pow(1 - progress, ZoomEasePower);
        var zoom = _zoomFrom * Math.Pow(_zoomTo / _zoomFrom, eased);
        var done = progress >= 1;
        if (done)
        {
            _ = Interlocked.Exchange(ref _zoomEasing, 0);
        }

        ZoomAround(tab, done ? _zoomTo : zoom, _zoomAnchor + _scroller.Offset);
        if (!done)
        {
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnZoomFrame);
            return;
        }

        InvalidateVisual();
    }

    /// <summary>Zooms keeping the point under the pointer fixed, with the new layout and offset in the same frame.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="zoom">The new zoom.</param>
    /// <param name="anchor">The canvas point to keep fixed.</param>
    private void ZoomAround(DocumentTabViewModel tab, double zoom, Point anchor)
    {
        if (_scroller is null)
        {
            return;
        }

        var page = _layout.HitTest(anchor.X, anchor.Y);
        if (page < 0)
        {
            page = _layout.GetPageNearest(anchor.Y);
        }

        if (page < 0)
        {
            tab.SetZoom(zoom);
            return;
        }

        var pagePoint = new PageTransform(_layout.GetPageBounds(page), _sizes[page], tab.Rotation, _layout.Options.Scale).ToPage(anchor);
        var screenOffset = anchor - _scroller.Offset;
        _anchoring = true;
        try
        {
            tab.SetZoom(zoom);

            // The layout was rebuilt by the zoom change; measuring now lets the offset follow before the next frame,
            // so no frame shows the new size at the old offset.
            _scroller.UpdateLayout();
            var target = new PageTransform(_layout.GetPageBounds(page), _sizes[page], tab.Rotation, _layout.Options.Scale).ToCanvas(pagePoint);
            _scroller.Offset = new(target.X - screenOffset.X, target.Y - screenOffset.Y);
        }
        finally
        {
            _anchoring = false;
        }

        ReportPosition();
    }
}
