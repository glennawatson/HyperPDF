// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Automation.Peers;

namespace Avalonia.FreeDesktop.AtSpi;

/// <summary>
/// Stands in for Avalonia's AT-SPI server. The X11 backend starts one at launch and hands it each window, and this one ignores them,
/// because HyperPDF serves the same windows to screen readers through its own server.
/// </summary>
[DebuggerDisplay("AtSpiServer: off")]
public sealed class AtSpiServer : IAsyncDisposable
{
    /// <summary>Starts the server, which here means doing nothing.</summary>
    /// <returns>A finished task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task StartAsync() => Task.CompletedTask;

    /// <summary>Adds a window to the tree, which here is ignored.</summary>
    /// <param name="peer">The window's automation peer.</param>
    public void AddWindow(AutomationPeer peer)
    {
        // The app's own server reads the windows itself.
    }

    /// <summary>Removes a window from the tree, which here is ignored.</summary>
    /// <param name="peer">The window's automation peer.</param>
    public void RemoveWindow(AutomationPeer peer)
    {
        // The app's own server reads the windows itself.
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
