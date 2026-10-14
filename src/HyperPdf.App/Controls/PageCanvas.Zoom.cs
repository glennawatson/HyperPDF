// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Layout;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

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

    /// <summary>Defines the <see cref="WheelZoom"/> property.</summary>
    private static readonly DirectProperty<PageCanvas, WheelZoomEase> WheelZoomProperty =
        AvaloniaProperty.RegisterDirect<PageCanvas, WheelZoomEase>(nameof(WheelZoom), static canvas => canvas.WheelZoom);

    /// <summary>Whether the canvas is moving its own scroll offset to hold a zoom anchor.</summary>
    private bool _anchoring;

    /// <summary>Gets a value indicating whether a wheel zoom is easing, so tiles at passing scales are not requested.</summary>
    internal bool IsZoomEasing => WheelZoom.IsRunning;

    /// <summary>Gets the wheel zoom easing now, or the last one once it has settled; a new notch starts a new ease.</summary>
    internal WheelZoomEase WheelZoom
    {
        get;
        private set => SetAndRaise(WheelZoomProperty, ref field, value);
    }

    /// <summary>Gets the frames of the wheel zoom easing now, each carrying the ease; none once it settles.</summary>
    /// <param name="ease">The ease.</param>
    /// <returns>The frames.</returns>
    private IObservable<WheelZoomEase> ZoomFrames(in WheelZoomEase ease) =>
        ease.IsRunning && TopLevel.GetTopLevel(this) is { } top
            ? top.AnimationFrames.SelectWith(ease, static (running, _) => running)
            : Signal.Never<WheelZoomEase>();

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

        var target = ZoomCalculator.Clamp((IsZoomEasing ? WheelZoom.To : tab.Zoom) * factor);
        if (tab.ReduceMotion || TopLevel.GetTopLevel(this) is null)
        {
            ZoomAround(tab, target, anchor);
            return;
        }

        // The ease starts from the zoom shown now, so a notch while easing carries on smoothly towards the new target.
        WheelZoom = new(true, tab.Zoom, target, Stopwatch.GetTimestamp(), anchor - _scroller.Offset);
    }

    /// <summary>Moves a wheel zoom one frame on, and settles it at its target once its time is up.</summary>
    /// <param name="ease">The ease.</param>
    private void StepZoom(WheelZoomEase ease)
    {
        if (_scroller is null || Tab is not { } tab)
        {
            WheelZoom = ease with { IsRunning = false };
            return;
        }

        var progress = Math.Min(1, Stopwatch.GetElapsedTime(ease.Started) / ZoomDuration);

        // Cubic ease out, applied to the zoom ratio so each frame scales by the same proportion.
        var eased = 1 - Math.Pow(1 - progress, ZoomEasePower);
        var zoom = ease.From * Math.Pow(ease.To / ease.From, eased);
        var done = progress >= 1;
        if (done)
        {
            // Settled before the last step, so the tiles of the final zoom are requested.
            WheelZoom = ease with { IsRunning = false };
        }

        ZoomAround(tab, done ? ease.To : zoom, ease.Anchor + _scroller.Offset);
        if (done)
        {
            InvalidateVisual();
        }
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

    /// <summary>A wheel zoom easing from one zoom to another over the window's frames.</summary>
    /// <param name="IsRunning">Whether it is still easing.</param>
    /// <param name="From">The zoom it started from.</param>
    /// <param name="To">The zoom it ends at.</param>
    /// <param name="Started">When it started, as a <see cref="Stopwatch"/> timestamp.</param>
    /// <param name="Anchor">The point in viewport coordinates that stays still while the zoom eases.</param>
    internal readonly record struct WheelZoomEase(bool IsRunning, double From, double To, long Started, Point Anchor);
}
