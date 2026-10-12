// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.DBus;

/// <summary>Connects to the session bus.</summary>
internal static class SessionBus
{
    /// <summary>Connects to the session bus, returning <see langword="null"/> when none is available.</summary>
    /// <returns>The connection, owned by the caller.</returns>
    internal static async Task<DBusConnection?> TryConnectAsync()
    {
        var address = DBusAddress.Session;
        if (string.IsNullOrEmpty(address))
        {
            return null;
        }

        var connection = new DBusConnection(address);
        try
        {
            await connection.ConnectAsync().ConfigureAwait(false);
            return connection;
        }
        catch (DBusConnectionException)
        {
            connection.Dispose();
            return null;
        }
    }
}
