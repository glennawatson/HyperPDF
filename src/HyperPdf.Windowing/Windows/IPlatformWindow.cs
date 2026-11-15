// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Presentation.Input;
using HyperPdf.Windowing.Accessibility;
using HyperPdf.Windowing.Rendering;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Windowing.Windows;

/// <summary>A native window owned by one platform head. The head draws the chrome natively and hosts the page canvas.</summary>
public interface IPlatformWindow : IDisposable
{
    /// <summary>Gets the size of the drawing area in device pixels.</summary>
    SurfaceSize ClientSize { get; }

    /// <summary>Gets where the top left of the drawing area is on the screen, or the screen's own corner where the window system does not tell a window its place.</summary>
    ScreenPoint ScreenOrigin { get; }

    /// <summary>Gets the operating system's number for the window, such as a window handle, or 0 when it has none. Accessibility bridges find the window by it.</summary>
    long NativeHandle { get; }

    /// <summary>Gets the device pixels per logical pixel, so 2.0 on a doubled display.</summary>
    double Scale { get; }

    /// <summary>Gets the backend drawing into this window.</summary>
    IRenderBackend Backend { get; }

    /// <summary>Gets the bridge that exposes this window to screen readers and magnifiers.</summary>
    IAccessibilityBridge Accessibility { get; }

    /// <summary>
    /// Gets the window's events: the pointer, the keyboard, typed text, a new size, a frame to draw and the wish to close. They are
    /// published on the thread that calls <see cref="PumpEvents"/>, while it pumps.
    /// </summary>
    IObservable<WindowEvent> Events { get; }

    /// <summary>
    /// Gets the files the user dropped on the window, one list for each drop, published on the thread that calls <see cref="PumpEvents"/>.
    /// A window whose system does not tell it about drops publishes none.
    /// </summary>
    IObservable<IReadOnlyList<string>> FileDrops => Signal.Never<IReadOnlyList<string>>();

    /// <summary>Sets the window title.</summary>
    /// <param name="title">The title.</param>
    void SetTitle(string title);

    /// <summary>Makes the window fill the screen with nothing round it, or puts it back as it was.</summary>
    /// <param name="on"><see langword="true"/> to fill the screen, <see langword="false"/> to go back.</param>
    void SetFullScreen(bool on);

    /// <summary>
    /// Brings the window to the front and asks for the keyboard, as when a second launch hands its files to this window. A system that
    /// does not let a program take the front, such as a Wayland compositor without an activation token, may only mark the window.
    /// </summary>
    void Raise()
    {
        // A window that cannot take the front leaves it to the user.
    }

    /// <summary>Sets the pointer shape over the canvas.</summary>
    /// <param name="cursor">The shape.</param>
    void SetCursor(ViewerCursor cursor);

    /// <summary>Puts text on the system clipboard, in place of what was there, so it can be pasted into another application.</summary>
    /// <param name="text">The text.</param>
    void SetClipboardText(string text);

    /// <summary>Reads the text on the system clipboard, to paste it into a text box.</summary>
    /// <returns>The text, or <see langword="null"/> when the clipboard holds no text or does not answer in time.</returns>
    string? GetClipboardText();

    /// <summary>Asks for the canvas to be drawn on the next frame.</summary>
    void RequestFrame();

    /// <summary>Makes a waiting <see cref="PumpEvents"/> return, so work queued from another thread runs. This is the only member that is safe to call from any thread.</summary>
    void Wake();

    /// <summary>Reads the queued window events and publishes each one on <see cref="Events"/>.</summary>
    /// <param name="timeoutMilliseconds">The longest to wait for the first event, or 0 to only read what is queued. A head may return sooner.</param>
    void PumpEvents(int timeoutMilliseconds);

    /// <summary>Shows the window.</summary>
    void Show();

    /// <summary>Closes the window.</summary>
    void Close();
}
