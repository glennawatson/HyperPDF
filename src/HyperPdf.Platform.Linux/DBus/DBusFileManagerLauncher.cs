// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Diagnostics;
using HyperPdf.Core.Platform;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.DBus;

/// <summary>Shows files in the desktop file manager (Dolphin, Nautilus, ...) through <c>org.freedesktop.FileManager1</c>.</summary>
[DebuggerDisplay("DBusFileManagerLauncher: FileManager1")]
public sealed class DBusFileManagerLauncher : IFileManagerLauncher
{
    /// <summary>The file manager service and interface name.</summary>
    private const string FileManagerName = "org.freedesktop.FileManager1";

    /// <summary>The file manager object path.</summary>
    private const string FileManagerPath = "/org/freedesktop/FileManager1";

    /// <inheritdoc/>
    public IObservable<bool> ShowItem(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        // The connection is closed when the request ends or the caller unsubscribes.
        return SessionBus.TryConnect()
            .SwitchMap(connection => connection is null
                ? Signal.Return(false)
                : Signal.Using(() => connection, open => Show(open, filePath)));
    }

    /// <summary>Asks the file manager to show the file.</summary>
    /// <param name="connection">The open session bus.</param>
    /// <param name="filePath">The file.</param>
    /// <returns>A cold signal giving once <see langword="true"/> when the file manager accepted the request.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<bool> Show(DBusConnection connection, string filePath) =>
        Signal.Defer(() =>
            {
                MessageBuffer message;
                using (var writer = connection.GetMessageWriter())
                {
                    writer.WriteMethodCallHeader(FileManagerName, FileManagerPath, FileManagerName, "ShowItems", "ass");
                    writer.WriteArray(new[] { new Uri(Path.GetFullPath(filePath)).AbsoluteUri });
                    writer.WriteString(string.Empty);
                    message = writer.CreateMessage();
                }

                // The D-Bus library's own async call, awaited once here; unsubscribing stops waiting for the answer.
                return Signal.FromAsync(async token =>
                {
                    await connection.CallMethodAsync(message).WaitAsync(token).ConfigureAwait(false);
                    return true;
                });
            })
            .Catch<bool, DBusErrorReplyException>(static ex =>
            {
                AppLog.Warning(ex, "ShowItems failed.");
                return Signal.Return(false);
            });
}
