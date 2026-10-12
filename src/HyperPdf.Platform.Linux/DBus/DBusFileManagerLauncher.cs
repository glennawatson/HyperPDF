// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Platform;
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
    public async Task<bool> ShowItemAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        using var connection = await SessionBus.TryConnectAsync().ConfigureAwait(false);
        if (connection is null)
        {
            return false;
        }

        MessageBuffer message;
        using (var writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(FileManagerName, FileManagerPath, FileManagerName, "ShowItems", "ass");
            writer.WriteArray(new[] { new Uri(Path.GetFullPath(filePath)).AbsoluteUri });
            writer.WriteString(string.Empty);
            message = writer.CreateMessage();
        }

        try
        {
            await connection.CallMethodAsync(message).WaitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DBusErrorReplyException ex)
        {
            Debug.WriteLine($"ShowItems failed: {ex.Message}");
            return false;
        }
    }
}
