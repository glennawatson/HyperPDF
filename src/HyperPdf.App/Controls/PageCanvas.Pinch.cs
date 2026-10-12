// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Input;
using HyperPdf.Core.Layout;

namespace HyperPdf.App.Controls;

/// <summary>
/// Pinch zoom. On a touch screen the pages follow the fingers directly, zooming around the point between them. A
/// touchpad pinch on macOS arrives as magnify steps, which zoom around the pointer like Ctrl+wheel: easing when
/// motion is allowed and jumping when it is reduced. Other desktops send a touchpad pinch as Ctrl+wheel itself.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>The zoom when the current pinch started, or 0 when there is no pinch.</summary>
    private double _pinchStartZoom;

    /// <summary>Zooms with a touch pinch, around the point between the fingers.</summary>
    /// <param name="e">The pinch, whose scale is measured from the start of the gesture.</param>
    private void HandlePinch(PinchEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (Tab is not { } tab || !(e.Scale > 0))
        {
            return;
        }

        if (_pinchStartZoom <= 0)
        {
            _pinchStartZoom = tab.Zoom;
        }

        // The pages follow the fingers, so there is no easing to reduce.
        ZoomAround(tab, ZoomCalculator.Clamp(_pinchStartZoom * e.Scale), e.ScaleOrigin);
        e.Handled = true;
    }

    /// <summary>Ends a touch pinch.</summary>
    /// <param name="e">The end of the pinch.</param>
    private void HandlePinchEnded(PinchEndedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _pinchStartZoom = 0;
        InvalidateVisual();
    }

    /// <summary>Zooms with a touchpad magnify step around the pointer, as Ctrl+wheel does.</summary>
    /// <param name="e">The magnify step; its horizontal delta is the change in size, such as 0.05 for 5% bigger.</param>
    private void HandleMagnify(PointerDeltaEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var factor = 1 + e.Delta.X;
        if (Tab is not { } tab || !(factor > 0))
        {
            return;
        }

        ZoomByWheel(tab, factor, e.GetPosition(this));
        e.Handled = true;
    }
}
