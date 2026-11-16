// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Diagnostics;
using ReactiveUI.Primitives;
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

        // The connection and the file handle are released when the request ends or the caller unsubscribes.
        return SessionBus.TryConnect()
            .SwitchMap(connection => connection is null
                ? Signal.Return(false)
                : Signal.Using(() => connection, open => Signal.Using(() => File.OpenHandle(filePath), file => Send(open, file, title))));
    }

    /// <summary>Hands the open PDF to the portal and waits for its answer.</summary>
    /// <param name="connection">The open session bus.</param>
    /// <param name="file">The open PDF.</param>
    /// <param name="title">The job title.</param>
    /// <returns>A cold signal giving once <see langword="true"/> when the portal accepted the file.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<bool> Send(DBusConnection connection, Microsoft.Win32.SafeHandles.SafeFileHandle file, string title) =>
        Signal.Defer(() =>
            {
                MessageBuffer message;
                using (var writer = connection.GetMessageWriter())
                {
                    var w = writer;
                    WritePrintCall(ref w, title, file);
                    message = w.CreateMessage();
                }

                // The D-Bus library's own async call, awaited once here; unsubscribing stops waiting for the answer.
                return Signal.FromAsync(async token =>
                {
                    await connection.CallMethodAsync(message).WaitAsync(token).ConfigureAwait(false);
                    return true;
                });
            })
            .Catch<bool, DBusErrorReplyException>(PortalFailed)
            .Catch<bool, DBusConnectionException>(PortalFailed);

    /// <summary>Records that the portal could not print.</summary>
    /// <param name="exception">The failure.</param>
    /// <returns>A signal giving <see langword="false"/>.</returns>
    private static IObservable<bool> PortalFailed(Exception exception)
    {
        AppLog.Warning(exception, "Print portal failed.");
        return Signal.Return(false);
    }
}
