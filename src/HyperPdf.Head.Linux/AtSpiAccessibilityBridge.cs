// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Accessibility;
using HyperPdf.Platform.Linux.Accessibility;
using HyperPdf.Windowing.Accessibility;

namespace HyperPdf.Head.Linux;

/// <summary>Serves the accessible tree to Orca and other screen readers over AT-SPI, whichever window system the window uses.</summary>
[DebuggerDisplay("AtSpiAccessibilityBridge: active {IsAssistiveTechnologyActive}")]
public sealed class AtSpiAccessibilityBridge : IAccessibilityBridge
{
    /// <summary>The server that is starting or running, or <see langword="null"/> before <see cref="Attach"/> and after <see cref="Dispose"/>.</summary>
    private Task<IDisposable?>? _server;

    /// <summary>One when the AT-SPI server is running.</summary>
    private int _running;

    /// <inheritdoc/>
    public bool IsAssistiveTechnologyActive => Volatile.Read(ref _running) != 0;

    /// <inheritdoc/>
    public void Attach(IAccessibleTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        Volatile.Write(ref _server, StartAsync(tree));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _server, null) is not { } server)
        {
            return;
        }

        Volatile.Write(ref _running, 0);
        _ = StopAsync(server);
    }

    /// <summary>Stops a server once it has finished starting, without making the caller wait.</summary>
    /// <param name="server">The server task.</param>
    /// <returns>A task that completes when the server has stopped.</returns>
    private static async Task StopAsync(Task<IDisposable?> server) => (await server.ConfigureAwait(false))?.Dispose();

    /// <summary>Starts the server and records whether the session had an accessibility bus to serve.</summary>
    /// <param name="tree">The tree to serve.</param>
    /// <returns>The running server, or <see langword="null"/> when the session has no accessibility bus.</returns>
    private async Task<IDisposable?> StartAsync(IAccessibleTree tree)
    {
        var server = await LinuxAccessibility.StartAsync(tree).ConfigureAwait(false);
        Volatile.Write(ref _running, server is null ? 0 : 1);
        return server;
    }
}
