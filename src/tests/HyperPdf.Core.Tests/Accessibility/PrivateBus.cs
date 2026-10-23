// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using Tmds.DBus.Protocol;

namespace HyperPdf.Core.Tests.Accessibility;

/// <summary>A private D-Bus daemon for a test, so a test can talk D-Bus without touching the desktop's session.</summary>
[DebuggerDisplay("PrivateBus: {Address}")]
internal sealed class PrivateBus : IAsyncDisposable
{
    /// <summary>How long the daemon may take to print its address.</summary>
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(15);

    /// <summary>The daemon process.</summary>
    private readonly Process _daemon;

    /// <summary>Initializes a new instance of the <see cref="PrivateBus"/> class.</summary>
    /// <param name="daemon">The daemon process.</param>
    /// <param name="address">The address clients connect to.</param>
    private PrivateBus(Process daemon, string address)
    {
        _daemon = daemon;
        Address = address;
    }

    /// <summary>Gets the address clients connect to.</summary>
    internal string Address { get; }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!_daemon.HasExited)
        {
            _daemon.Kill(true);
        }

        await _daemon.WaitForExitAsync().ConfigureAwait(false);
        _daemon.Dispose();
    }

    /// <summary>Starts a daemon.</summary>
    /// <returns>The bus, or <see langword="null"/> when <c>dbus-daemon</c> is not installed.</returns>
    internal static async Task<PrivateBus?> TryStartAsync()
    {
        var info = new ProcessStartInfo("dbus-daemon", ["--session", "--nofork", "--print-address=1"]) { RedirectStandardOutput = true, UseShellExecute = false };
        Process? daemon;
        try
        {
            daemon = Process.Start(info);
        }
        catch (Win32Exception)
        {
            return null;
        }

        if (daemon is null)
        {
            return null;
        }

        using var timeout = new CancellationTokenSource(StartTimeout);
        var address = await daemon.StandardOutput.ReadLineAsync(timeout.Token).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(address))
        {
            daemon.Kill(true);
            daemon.Dispose();
            return null;
        }

        return new(daemon, address.Trim());
    }

    /// <summary>Connects a client to the bus.</summary>
    /// <returns>The connection, owned by the caller.</returns>
    internal async Task<DBusConnection> ConnectAsync()
    {
        var connection = new DBusConnection(Address);
        await connection.ConnectAsync().ConfigureAwait(false);
        return connection;
    }
}
