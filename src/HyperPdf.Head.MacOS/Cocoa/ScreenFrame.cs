// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Accessibility;

namespace HyperPdf.Head.MacOS.Cocoa;

/// <summary>What is needed to turn the viewer's screen pixels, measured from the top left, into Cocoa's screen points, measured from the bottom left.</summary>
/// <param name="Scale">Device pixels per point.</param>
/// <param name="ScreenHeight">The height of the screen in points.</param>
[DebuggerDisplay("ScreenFrame scale {Scale}, height {ScreenHeight}")]
internal readonly record struct ScreenFrame(double Scale, double ScreenHeight)
{
    /// <summary>Reads the scale and screen height of the window a view is in.</summary>
    /// <param name="view">The view.</param>
    /// <returns>The frame; a scale of 1 and no height when the view is in no window.</returns>
    internal static ScreenFrame Of(nint view)
    {
        var window = NativeMethods.Send(view, NativeMethods.Selector("window"));
        if (window == 0)
        {
            return new(1, 0);
        }

        var screen = NativeMethods.Send(window, Selectors.Screen);
        var height = screen != 0 ? Cocoa.Rect(screen, Selectors.Frame).Height : 0;
        return new(Math.Max(1, NativeMethods.SendDouble(window, Selectors.BackingScaleFactor)), height);
    }

    /// <summary>Turns bounds in screen pixels into a Cocoa rectangle in screen points.</summary>
    /// <param name="bounds">The bounds, measured from the top left of the screen.</param>
    /// <returns>The rectangle, measured from the bottom left of the screen.</returns>
    internal CocoaRect ToCocoa(in AccessibleBounds bounds) => new(
        bounds.X / Scale,
        ScreenHeight - ((bounds.Y + bounds.Height) / Scale),
        bounds.Width / Scale,
        bounds.Height / Scale);
}
