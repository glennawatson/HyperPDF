// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Presentation.Input;
using HyperPdf.Windowing.Accessibility;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Windowing.Windows;

/// <summary>A native window owned by one platform head. The head draws the chrome natively and hosts the page canvas.</summary>
public interface IPlatformWindow : IDisposable
{
    /// <summary>Gets the size of the drawing area in device pixels.</summary>
    SurfaceSize ClientSize { get; }

    /// <summary>Gets the device pixels per logical pixel, so 2.0 on a doubled display.</summary>
    double Scale { get; }

    /// <summary>Gets the backend drawing into this window.</summary>
    IRenderBackend Backend { get; }

    /// <summary>Gets the bridge that exposes this window to screen readers and magnifiers.</summary>
    IAccessibilityBridge Accessibility { get; }

    /// <summary>Sets the window title.</summary>
    /// <param name="title">The title.</param>
    void SetTitle(string title);

    /// <summary>Sets the pointer shape over the canvas.</summary>
    /// <param name="cursor">The shape.</param>
    void SetCursor(ViewerCursor cursor);

    /// <summary>Asks for the canvas to be drawn on the next frame.</summary>
    void RequestFrame();

    /// <summary>Shows the window.</summary>
    void Show();

    /// <summary>Closes the window.</summary>
    void Close();
}
