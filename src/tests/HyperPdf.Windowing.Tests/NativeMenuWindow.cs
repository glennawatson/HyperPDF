// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Presentation.Input;
using HyperPdf.Windowing.Accessibility;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Windowing.Tests;

/// <summary>A window with no screen whose operating system would draw the menu bar, as macOS does; it keeps the menus it is handed.</summary>
internal sealed class NativeMenuWindow : IPlatformWindow, INativeMenuBar
{
    /// <summary>Initializes a new instance of the <see cref="NativeMenuWindow"/> class.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    internal NativeMenuWindow(int width, int height) => Inner = new(width, height);

    /// <inheritdoc/>
    public SurfaceSize ClientSize => Inner.ClientSize;

    /// <inheritdoc/>
    public ScreenPoint ScreenOrigin => Inner.ScreenOrigin;

    /// <inheritdoc/>
    public long NativeHandle => Inner.NativeHandle;

    /// <inheritdoc/>
    public double Scale => Inner.Scale;

    /// <inheritdoc/>
    public IRenderBackend Backend => Inner.Backend;

    /// <inheritdoc/>
    public IAccessibilityBridge Accessibility => Inner.Accessibility;

    /// <inheritdoc/>
    public IObservable<WindowEvent> Events => Inner.Events;

    /// <summary>Gets the window with no screen that does the work.</summary>
    internal HeadlessWindow Inner { get; }

    /// <summary>Gets the menus' names the viewer handed over, or <see langword="null"/> before it did.</summary>
    internal IReadOnlyList<string>? Titles { get; private set; }

    /// <summary>Gets what makes each menu's entries.</summary>
    internal Func<int, NativeMenuItem[]>? Build { get; private set; }

    /// <summary>Gets the words of the application menu.</summary>
    internal NativeAppMenu? Application { get; private set; }

    /// <summary>Gets how many times the menus were handed over.</summary>
    internal int Handed { get; private set; }

    /// <inheritdoc/>
    public void SetMenus(IReadOnlyList<string> titles, Func<int, NativeMenuItem[]> build, NativeAppMenu application)
    {
        Titles = titles;
        Build = build;
        Application = application;
        Handed++;
    }

    /// <inheritdoc/>
    public void SetTitle(string title) => Inner.SetTitle(title);

    /// <inheritdoc/>
    public void SetFullScreen(bool on) => Inner.SetFullScreen(on);

    /// <inheritdoc/>
    public void SetCursor(ViewerCursor cursor) => Inner.SetCursor(cursor);

    /// <inheritdoc/>
    public void SetClipboardText(string text) => Inner.SetClipboardText(text);

    /// <inheritdoc/>
    public string? GetClipboardText() => Inner.GetClipboardText();

    /// <inheritdoc/>
    public void RequestFrame() => Inner.RequestFrame();

    /// <inheritdoc/>
    public void Wake() => Inner.Wake();

    /// <inheritdoc/>
    public void PumpEvents(int timeoutMilliseconds) => Inner.PumpEvents(timeoutMilliseconds);

    /// <inheritdoc/>
    public void Show() => Inner.Show();

    /// <inheritdoc/>
    public void Close() => Inner.Close();

    /// <inheritdoc/>
    public void Dispose() => Inner.Dispose();
}
