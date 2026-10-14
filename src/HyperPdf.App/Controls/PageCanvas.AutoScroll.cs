// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Navigation;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Controls;

/// <summary>
/// Auto-scroll: the pages move down by themselves at a calm speed. Up and Down, or + and -, change the speed; Escape
/// or any click stops it, and it stops by itself at the end. When movement is reduced the pages step a line at a
/// time at a steady interval instead of gliding.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>Works out how far each frame moves.</summary>
    private readonly AutoScroller _autoScroller = new();

    /// <summary>When the last auto-scroll frame ran.</summary>
    private long _autoScrollLast;

    /// <summary>1 while a glide's frames are subscribed.</summary>
    private int _autoScrollRunning;

    /// <summary>Gets a value indicating whether auto-scroll frames are being requested, for tests.</summary>
    internal bool IsAutoScrollRunning => Volatile.Read(ref _autoScrollRunning) != 0;

    /// <summary>Handles the keys that change the speed or stop while auto-scrolling.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="e">The key press.</param>
    /// <returns><see langword="true"/> when the key was used.</returns>
    private static bool HandleAutoScrollKey(DocumentTabViewModel tab, KeyEventArgs e)
    {
        if (!tab.PageTools.IsAutoScrolling || (e.KeyModifiers & ~KeyModifiers.Shift) != 0)
        {
            return false;
        }

        switch (e.Key)
        {
            case Key.Up or Key.OemPlus or Key.Add:
            {
                _ = tab.PageTools.ChangeAutoScrollSpeed(1);
                return true;
            }

            case Key.Down or Key.OemMinus or Key.Subtract:
            {
                _ = tab.PageTools.ChangeAutoScrollSpeed(-1);
                return true;
            }

            case Key.Escape:
            {
                tab.PageTools.IsAutoScrolling = false;
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Starts a glide: the keyboard comes to the pages so the arrows change the speed and Escape stops, and the window's
    /// frames move the pages until the subscription ends.
    /// </summary>
    /// <returns>The frames, or none when the canvas is not in a window.</returns>
    private IObservable<TimeSpan> StartGlide()
    {
        if (TopLevel.GetTopLevel(this) is not { } top)
        {
            return Signal.Never<TimeSpan>();
        }

        _ = Focus();
        _autoScroller.Reset();
        _autoScrollLast = Stopwatch.GetTimestamp();
        _ = Interlocked.Exchange(ref _autoScrollRunning, 1);

        // Finally has no state-passing form, so the end of the glide reaches the flag through this closure.
        return top.AnimationFrames.Finally(() => Interlocked.Exchange(ref _autoScrollRunning, 0));
    }

    /// <summary>Moves the pages on by the time since the last frame, and ends auto-scroll at the end of the document.</summary>
    private void StepAutoScroll()
    {
        if (_scroller is null || Tab is not { PageTools.IsAutoScrolling: true } tab)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var seconds = Stopwatch.GetElapsedTime(_autoScrollLast, now).TotalSeconds;
        _autoScrollLast = now;
        var distance = _autoScroller.Advance(seconds, tab.PageTools.AutoScrollSpeed, tab.ReduceMotion);
        if (distance > 0 && !AutoScrollBy(distance))
        {
            // Turning auto-scroll off ends the glide's frames.
            tab.PageTools.IsAutoScrolling = false;
        }
    }

    /// <summary>Scrolls down, page by page within the page shown and then to the next page.</summary>
    /// <param name="distance">How far.</param>
    /// <returns><see langword="true"/> when the pages moved; <see langword="false"/> at the end.</returns>
    private bool AutoScrollBy(double distance)
    {
        if (_scroller is null)
        {
            return false;
        }

        var before = _scroller.Offset.Y;
        if (StepPageByPage(distance))
        {
            return _scroller.Offset.Y > before;
        }

        var end = Math.Max(0, _scroller.Extent.Height - _scroller.Viewport.Height);
        if (before >= end - EdgeTolerance)
        {
            return false;
        }

        _scroller.Offset = new(_scroller.Offset.X, Math.Min(end, before + distance));
        ReportPosition();
        return true;
    }

    /// <summary>Handles a press for auto-scroll, the hand and the area tools, before text selection and annotating.</summary>
    /// <param name="e">The press.</param>
    /// <param name="point">The pointer state.</param>
    /// <returns><see langword="true"/> when the press was used.</returns>
    private bool BeginPageToolPress(PointerPressedEventArgs e, PointerPoint point) => StopAutoScrollOnPress(e) || BeginPan(e, point) || BeginArea(e, point);

    /// <summary>Stops auto-scroll on any press, so a click is always a way out.</summary>
    /// <param name="e">The press.</param>
    /// <returns><see langword="true"/> when the press stopped auto-scroll.</returns>
    private bool StopAutoScrollOnPress(PointerPressedEventArgs e)
    {
        if (Tab is not { PageTools.IsAutoScrolling: true } tab)
        {
            return false;
        }

        tab.PageTools.IsAutoScrolling = false;
        _ = Focus();
        e.Handled = true;
        return true;
    }
}
