// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Diagnostics;
using ReactiveUI.Primitives.Signals;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.DBus;

/// <summary>
/// Prints through the XDG desktop portal (<c>org.freedesktop.portal.Print</c>), which shows the desktop's own print
/// dialog (KDE's on Plasma) and prints the PDF it is handed. Works inside and outside sandboxes.
/// </summary>
[DebuggerDisplay("PortalPrintService: Print portal")]
public sealed class PortalPrintService
{
    /// <summary>The portal service name.</summary>
    private const string PortalName = "org.freedesktop.portal.Desktop";

    /// <summary>The portal object path.</summary>
    private const string PortalPath = "/org/freedesktop/portal/desktop";

    /// <summary>The print interface.</summary>
    private const string PrintInterface = "org.freedesktop.portal.Print";

    /// <summary>The method that prints a file.</summary>
    private const string PrintMethod = "Print";

    /// <summary>The signature of <see cref="PrintMethod"/>: parent window, title, file descriptor, options.</summary>
    private const string PrintSignature = "ssha{sv}";

    /// <summary>Gets a value indicating whether a session bus, and so the portal, can be reached.</summary>
    public bool IsAvailable => !string.IsNullOrEmpty(DBusAddress.Session);

    /// <summary>Writes the portal's Print call.</summary>
    /// <param name="writer">The message writer.</param>
    /// <param name="title">The job title.</param>
    /// <param name="file">The open PDF.</param>
    public static void WritePrintCall(ref MessageWriter writer, string title, Microsoft.Win32.SafeHandles.SafeFileHandle file)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(file);
        writer.WriteMethodCallHeader(PortalName, PortalPath, PrintInterface, PrintMethod, PrintSignature);
        writer.WriteString(string.Empty);
        writer.WriteString(title);
        writer.WriteHandle(file);
        writer.WriteDictionary(new Dictionary<string, VariantValue>(StringComparer.Ordinal));
    }

    /// <summary>Hands a PDF to the portal, which shows the desktop's print dialog.</summary>
    /// <param name="filePath">The PDF.</param>
    /// <param name="title">The job title.</param>
    /// <returns>A cold signal giving <see langword="true"/> once when the portal accepted the file; unsubscribing stops waiting for the portal.</returns>
    public IObservable<bool> Print(string filePath, string title)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(title);

        // The session bus is an async API from another library, so its call is the one task here.
        return Signal.FromAsync(token => PrintAsync(filePath, title, token));
    }

    /// <summary>Hands a PDF to the portal and waits for its answer.</summary>
    /// <param name="filePath">The PDF.</param>
    /// <param name="title">The job title.</param>
    /// <param name="cancellationToken">Cancels waiting for the portal.</param>
    /// <returns><see langword="true"/> when the portal accepted the file.</returns>
    private static async Task<bool> PrintAsync(string filePath, string title, CancellationToken cancellationToken)
    {
        using var connection = await SessionBus.TryConnectAsync().ConfigureAwait(false);
        if (connection is null)
        {
            return false;
        }

        using var file = File.OpenHandle(filePath);
        MessageBuffer message;
        using (var writer = connection.GetMessageWriter())
        {
            var w = writer;
            WritePrintCall(ref w, title, file);
            message = w.CreateMessage();
        }

        try
        {
            await connection.CallMethodAsync(message).WaitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DBusErrorReplyException ex)
        {
            AppLog.Warning(ex, "Print portal failed.");
            return false;
        }
        catch (DBusConnectionException ex)
        {
            AppLog.Warning(ex, "Print portal failed.");
            return false;
        }
    }
}
