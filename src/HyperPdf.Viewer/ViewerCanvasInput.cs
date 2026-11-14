// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;
using HyperPdf.Presentation.Canvas;
using HyperPdf.Presentation.Input;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Viewer;

/// <summary>Turns window events into the canvas state's input: presses, moves, keys and the wheel.</summary>
internal static class ViewerCanvasInput
{
    /// <summary>How far one arrow key press or one line of the wheel scrolls, in canvas units.</summary>
    private const double LineStep = 48;

    /// <summary>How far one wheel notch scrolls, in canvas units.</summary>
    private const double NotchStep = 120;

    /// <summary>The zoom of one wheel notch with Control held.</summary>
    private const double WheelZoomFactor = 1.1;

    /// <summary>The part of the window a Page Up or Page Down key scrolls, so a line or two stays in view.</summary>
    private const double PageFraction = 0.9;

    /// <summary>Routes a pointer or key event to the canvas state.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="clicks">The click counter.</param>
    /// <param name="windowEvent">The event.</param>
    /// <returns>What the canvas state asked for, or <see langword="null"/> when the event is not input.</returns>
    internal static InputEffects? Route(ViewerCanvas canvas, ref ClickCounter clicks, in WindowEvent windowEvent)
    {
        var inWindow = canvas.ToWindowPoint(windowEvent.X, windowEvent.Y);
        var position = new CanvasPoint(inWindow.X + canvas.Offset.X, inWindow.Y + canvas.Offset.Y);
        switch (windowEvent.Kind)
        {
            case WindowEventKind.PointerDown:
                {
                    clicks = clicks.Press(windowEvent.X, windowEvent.Y, Stopwatch.GetTimestamp());
                    return canvas.State.Press(new(position, inWindow, windowEvent.Buttons, windowEvent.Held, clicks.Count, Handled: false));
                }

            case WindowEventKind.PointerMoved:
                {
                    return canvas.State.Move(new(position, inWindow, (windowEvent.Buttons & ViewerButtons.Left) != 0));
                }

            case WindowEventKind.PointerUp:
                {
                    return canvas.State.Release(position, handled: false);
                }

            case WindowEventKind.KeyDown:
                {
                    return PressKey(canvas, windowEvent.Key, windowEvent.Held);
                }

            case WindowEventKind.KeyUp:
                {
                    return canvas.State.ReleaseKey(windowEvent.Key, windowEvent.Held);
                }

            default:
                {
                    return null;
                }
        }
    }

    /// <summary>Handles the wheel: Control zooms around the pointer, Shift scrolls sideways and a plain wheel scrolls or turns the page.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="windowEvent">The scroll event.</param>
    internal static void Scroll(ViewerCanvas canvas, in WindowEvent windowEvent)
    {
        var notches = (double)windowEvent.Y / WindowEvent.ScrollNotch;
        var sideways = (double)windowEvent.X / WindowEvent.ScrollNotch;
        if ((windowEvent.Held & ViewerModifiers.Control) != 0)
        {
            if (canvas.State.Tab is { } tab && notches != 0)
            {
                var inWindow = canvas.ToWindowPoint(windowEvent.X, windowEvent.Y);
                canvas.State.ZoomByWheel(tab, Math.Pow(WheelZoomFactor, notches), new(inWindow.X + canvas.Offset.X, inWindow.Y + canvas.Offset.Y));
            }

            return;
        }

        if ((windowEvent.Held & ViewerModifiers.Shift) != 0)
        {
            sideways += notches;
            notches = 0;
        }

        if (notches != 0 && !canvas.State.StepPageByPage(-notches * LineStep))
        {
            canvas.ScrollBy(0, -notches * NotchStep);
        }

        if (sideways != 0 && !canvas.State.StepSideways(-sideways * LineStep))
        {
            canvas.ScrollBy(-sideways * NotchStep, 0);
        }
    }

    /// <summary>Gives a key to the canvas state and, when it has no use for it, scrolls or zooms with it.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held.</param>
    /// <returns>What the canvas state asked for.</returns>
    private static InputEffects PressKey(ViewerCanvas canvas, ViewerKey key, ViewerModifiers modifiers)
    {
        var effects = canvas.State.PressKey(key, modifiers);
        if (!effects.Handled)
        {
            effects.Handled = ZoomWithKey(canvas, key, modifiers) || ScrollWithKey(canvas, key);
        }

        return effects;
    }

    /// <summary>Zooms in or out with Control and the plus or minus key.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held.</param>
    /// <returns><see langword="true"/> when the key zoomed.</returns>
    private static bool ZoomWithKey(ViewerCanvas canvas, ViewerKey key, ViewerModifiers modifiers)
    {
        if ((modifiers & ViewerModifiers.Control) == 0 || canvas.State.Tab is not { } tab || key is not (ViewerKey.Plus or ViewerKey.Minus))
        {
            return false;
        }

        tab.SetZoom(key == ViewerKey.Plus ? ZoomCalculator.ZoomIn(tab.Zoom) : ZoomCalculator.ZoomOut(tab.Zoom));
        return true;
    }

    /// <summary>Scrolls with the arrow, page, Home and End keys.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the key scrolled.</returns>
    private static bool ScrollWithKey(ViewerCanvas canvas, ViewerKey key)
    {
        var page = canvas.ViewportSize.Height * PageFraction;
        switch (key)
        {
            case ViewerKey.Up:
                {
                    canvas.ScrollBy(0, -LineStep);
                    return true;
                }

            case ViewerKey.Down:
                {
                    canvas.ScrollBy(0, LineStep);
                    return true;
                }

            case ViewerKey.Left:
                {
                    canvas.ScrollBy(-LineStep, 0);
                    return true;
                }

            case ViewerKey.Right:
                {
                    canvas.ScrollBy(LineStep, 0);
                    return true;
                }

            case ViewerKey.PageUp:
                {
                    canvas.ScrollBy(0, -page);
                    return true;
                }

            case ViewerKey.PageDown or ViewerKey.Space:
                {
                    canvas.ScrollBy(0, page);
                    return true;
                }

            case ViewerKey.Home:
                {
                    canvas.ScrollBy(0, -canvas.ExtentSize.Height);
                    return true;
                }

            case ViewerKey.End:
                {
                    canvas.ScrollBy(0, canvas.ExtentSize.Height);
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }
}
