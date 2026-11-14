// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Windowing.Windows;

/// <summary>The kinds of event a platform window reports to the viewer.</summary>
public enum WindowEventKind
{
    /// <summary>No event, which is what a default <see cref="WindowEvent"/> holds.</summary>
    None = 0,

    /// <summary>The user asked to close the window.</summary>
    CloseRequested = 1,

    /// <summary>The drawing area changed size. The event carries the new size.</summary>
    Resized = 2,

    /// <summary>Part of the window needs drawing again.</summary>
    Exposed = 3,

    /// <summary>A key went down. The event carries the platform key code and modifier bits.</summary>
    KeyDown = 4,

    /// <summary>A key came up.</summary>
    KeyUp = 5,

    /// <summary>A pointer button went down.</summary>
    PointerDown = 6,

    /// <summary>A pointer button came up.</summary>
    PointerUp = 7,

    /// <summary>The pointer moved.</summary>
    PointerMoved = 8,

    /// <summary>The wheel or touchpad scrolled. The event carries the vertical amount in <c>Y</c> and the horizontal amount in <c>X</c>.</summary>
    Scrolled = 9,
}
