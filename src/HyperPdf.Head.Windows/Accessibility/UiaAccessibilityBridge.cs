// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using HyperPdf.Core.Accessibility;
using HyperPdf.Platform.Windows.Accessibility;

namespace HyperPdf.Head.Windows.Accessibility;

/// <summary>
/// Publishes the viewer's accessible tree to UI Automation, which JAWS, NVDA and ZoomText read. The work, including the Text pattern
/// a screen reader reads the page with and the focus events a magnifier follows, is done by <see cref="WindowsAccessibilityServer"/>.
/// The server answers the window's UI Automation requests from a subclass of the window, so the window procedure never sees them.
/// </summary>
[SupportedOSPlatform("windows")]
[DebuggerDisplay("UiaAccessibilityBridge: window {_window}")]
public sealed class UiaAccessibilityBridge : IWin32AccessibilityBridge
{
    /// <summary>Guards the server and the window.</summary>
    private readonly Lock _gate = new();

    /// <summary>The server, once a tree is attached.</summary>
    private WindowsAccessibilityServer? _server;

    /// <summary>The window, once the head created it; zero before that.</summary>
    private long _window;

    /// <inheritdoc/>
    public bool IsAssistiveTechnologyActive => WindowsAccessibilityServer.IsAssistiveTechnologyListening();

    /// <inheritdoc/>
    public void Attach(IAccessibleTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        WindowsAccessibilityServer? old;
        lock (_gate)
        {
            old = _server;
            _server = WindowsAccessibilityServer.Start(tree);
            if (_window != 0)
            {
                _server.AttachWindow(_window);
            }
        }

        old?.Dispose();
    }

    /// <inheritdoc/>
    public void AttachWindow(long window)
    {
        lock (_gate)
        {
            _window = window;
            _server?.AttachWindow(window);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAnswerGetObject(long window, nuint primary, nint secondary, out nint result)
    {
        // The server's subclass of the window answers before the window procedure runs, so a request never gets here unanswered.
        result = 0;
        return false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        WindowsAccessibilityServer? server;
        lock (_gate)
        {
            server = _server;
            _server = null;
        }

        server?.Dispose();
    }
}
