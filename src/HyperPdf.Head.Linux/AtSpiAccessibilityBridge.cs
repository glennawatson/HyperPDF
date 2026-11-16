// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Accessibility;
using HyperPdf.Core.Diagnostics;
using HyperPdf.Platform.Linux.Accessibility;
using HyperPdf.Windowing.Accessibility;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.Head.Linux;

/// <summary>Serves the accessible tree to Orca and other screen readers over AT-SPI, whichever window system the window uses.</summary>
[DebuggerDisplay("AtSpiAccessibilityBridge: active {IsAssistiveTechnologyActive}")]
public sealed class AtSpiAccessibilityBridge : IAccessibilityBridge
{
    /// <summary>Holds the server once it has started, so disposing the bridge stops it; a server that arrives after disposal is stopped at once.</summary>
    private readonly MultipleDisposable _server = new();

    /// <summary>One when the AT-SPI server is running.</summary>
    private int _running;

    /// <summary>One once the bridge has been disposed.</summary>
    private int _disposed;

    /// <inheritdoc/>
    public bool IsAssistiveTechnologyActive => Volatile.Read(ref _running) != 0;

    /// <inheritdoc/>
    public void Attach(IAccessibleTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        // The signal ends by itself once the server has started, so nothing needs to hold the subscription.
        _ = LinuxAccessibility.Start(tree).SubscribeSafe(Started, static error => AppLog.Error(error, "The AT-SPI server could not start."));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Volatile.Write(ref _running, 0);
        _server.Dispose();
    }

    /// <summary>Records the started server, or stops it when the bridge has been disposed in the meantime.</summary>
    /// <param name="server">The running server, or <see langword="null"/> when the session has no accessibility bus.</param>
    private void Started(IDisposable? server)
    {
        if (server is null)
        {
            return;
        }

        if (Volatile.Read(ref _disposed) != 0)
        {
            server.Dispose();
            return;
        }

        _server.Add(server);
        Volatile.Write(ref _running, 1);

        // Disposal that raced with the line above has already emptied the holder, which stops what was just added.
        if (Volatile.Read(ref _disposed) != 0)
        {
            Volatile.Write(ref _running, 0);
        }
    }
}
