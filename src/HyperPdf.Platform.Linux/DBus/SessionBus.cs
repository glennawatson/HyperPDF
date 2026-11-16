// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.DBus;

/// <summary>Connects to the session bus.</summary>
internal static class SessionBus
{
    /// <summary>The environment variable that holds the session bus address.</summary>
    private const string SessionAddressVariable = "DBUS_SESSION_BUS_ADDRESS";

    /// <summary>Connects to the session bus, returning <see langword="null"/> when none is available.</summary>
    /// <returns>The connection, owned by the caller.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IObservable<DBusConnection?> TryConnect() =>
        Signal.Defer(static () =>
        {
            // The library reads the variable once for the whole process, so a session that is started or changed later would not be seen.
            var address = Environment.GetEnvironmentVariable(SessionAddressVariable);
            if (string.IsNullOrEmpty(address))
            {
                address = DBusAddress.Session;
            }

            if (string.IsNullOrEmpty(address))
            {
                return Signal.Return<DBusConnection?>(null);
            }

            var connection = new DBusConnection(address);

            // The D-Bus library's own async connect, awaited once here.
            return Signal.FromAsync(async () =>
                {
                    await connection.ConnectAsync().ConfigureAwait(false);
                    return (DBusConnection?)connection;
                })
                .Catch<DBusConnection?, DBusConnectionException>(_ =>
                {
                    connection.Dispose();
                    return Signal.Return<DBusConnection?>(null);
                });
        });

    /// <summary>Connects to the session bus for callers that still await it.</summary>
    /// <returns>The connection, owned by the caller, or <see langword="null"/> when none is available.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Task<DBusConnection?> TryConnectAsync() => TryConnect().ToTask();
}
