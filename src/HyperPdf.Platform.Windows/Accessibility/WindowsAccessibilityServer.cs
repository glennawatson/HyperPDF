// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Accessibility;
using HyperPdf.Platform.Windows.Accessibility.Uia;

namespace HyperPdf.Platform.Windows.Accessibility;

/// <summary>
/// Serves the app's elements to screen readers on Windows through UI Automation, so a screen reader gets heading levels, places in
/// a list and the page text that the UI framework's own elements lack. Each window the server is given answers UI Automation
/// with the elements of the tree; a window it was not given keeps the UI framework's.
/// </summary>
[DebuggerDisplay("WindowsAccessibilityServer: {_hooks.Count} windows")]
public sealed class WindowsAccessibilityServer : IDisposable
{
    /// <summary>The Windows setting that says a screen reader is running (<c>SPI_GETSCREENREADER</c>).</summary>
    private const uint GetScreenReader = 0x0046;

    /// <summary>The elements served.</summary>
    private readonly UiaElements _elements;

    /// <summary>Raises events as the tree changes.</summary>
    private readonly UiaEvents _events;

    /// <summary>The hook on each window, by the window's number.</summary>
    private readonly Dictionary<long, UiaWindowHook> _hooks = [];

    /// <summary>Guards <see cref="_hooks"/>.</summary>
    private readonly Lock _gate = new();

    /// <summary>Initializes a new instance of the <see cref="WindowsAccessibilityServer"/> class.</summary>
    /// <param name="tree">The elements to serve.</param>
    private WindowsAccessibilityServer(IAccessibleTree tree)
    {
        _elements = new(tree);
        _events = UiaEvents.Start(_elements);
    }

    /// <summary>Checks whether a screen reader or another UI Automation client has asked for events, or Windows says a screen reader is running.</summary>
    /// <returns>Whether one is; <see langword="false"/> on a system that is not Windows.</returns>
    public static bool IsAssistiveTechnologyListening() =>
        OperatingSystem.IsWindows() && (NativeMethods.UiaClientsAreListening() != 0 || IsScreenReaderFlagSet());

    /// <summary>Starts serving the app's elements. Windows are given to it with <see cref="AttachWindow"/>.</summary>
    /// <param name="tree">The elements to serve.</param>
    /// <returns>The server, to dispose when the app no longer serves elements.</returns>
    public static WindowsAccessibilityServer Start(IAccessibleTree tree) => new(tree);

    /// <summary>Makes a window answer UI Automation with the app's elements. Call it on the window's own thread; giving the same window again does nothing.</summary>
    /// <param name="window">The window's handle, as a number.</param>
    public void AttachWindow(long window)
    {
        lock (_gate)
        {
            if (_hooks.ContainsKey(window))
            {
                return;
            }

            if (UiaWindowHook.Install(window, _elements, Forget) is { } hook)
            {
                _hooks[window] = hook;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _events.Dispose();
        UiaWindowHook[] hooks;
        lock (_gate)
        {
            hooks = [.. _hooks.Values];
            _hooks.Clear();
        }

        foreach (var hook in hooks)
        {
            hook.Dispose();
        }
    }

    /// <summary>Reads Windows's setting that says a screen reader is running, which screen readers switch on as they start.</summary>
    /// <returns>Whether the setting is on.</returns>
    private static bool IsScreenReaderFlagSet() => NativeMethods.SystemParametersInfo(GetScreenReader, 0, out var running, 0) != 0 && running != 0;

    /// <summary>Forgets the hook of a window that has been destroyed.</summary>
    /// <param name="window">The window's number.</param>
    private void Forget(long window)
    {
        lock (_gate)
        {
            _ = _hooks.Remove(window);
        }
    }
}
