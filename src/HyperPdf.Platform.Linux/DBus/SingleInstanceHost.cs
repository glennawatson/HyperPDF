// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Diagnostics;
using HyperPdf.Core.Platform;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.DBus;

/// <summary>
/// Keeps one viewer process per session. The first process owns the application's D-Bus name and serves
/// <c>org.freedesktop.Application</c>; later launches (for example from Dolphin) forward their files to it and exit,
/// so every document opens as a tab in the existing window.
/// </summary>
[DebuggerDisplay("SingleInstanceHost: {AppIdentity.ApplicationId}")]
public sealed class SingleInstanceHost : ISingleInstance
{
    /// <summary>How long a forwarding launch waits for the primary instance.</summary>
    private static readonly TimeSpan ForwardTimeout = TimeSpan.FromSeconds(3);

    /// <summary>The connection that owns the name.</summary>
    private readonly DBusConnection _connection;

    /// <summary>Requests received from other processes.</summary>
    private readonly Signal<OpenRequest> _openRequests = new();

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="SingleInstanceHost"/> class.</summary>
    /// <param name="connection">The connection that owns the name.</param>
    private SingleInstanceHost(DBusConnection connection)
    {
        _connection = connection;
        OpenRequests = new(_openRequests);
    }

    /// <summary>Gets the requests from other processes to open documents or activate the window, emitted on a D-Bus thread.</summary>
    public AsObservableSignal<OpenRequest> OpenRequests { get; }

    /// <summary>Asks a running instance to open documents.</summary>
    /// <param name="uris">Paths or URIs to open; empty to just raise the window.</param>
    /// <param name="activationToken">The activation token from the environment, if any.</param>
    /// <returns>A cold signal giving once <see langword="true"/> when a running instance accepted the request; unsubscribing stops waiting.</returns>
    public static IObservable<bool> TryForward(string[] uris, string? activationToken)
    {
        // The connection is closed when the request ends or the caller unsubscribes.
        return SessionBus.TryConnect()
            .SwitchMap(connection => connection is null
                ? Signal.Return(false)
                : Signal.Using(() => connection, open => Forward(open, uris, activationToken)));
    }

    /// <summary>Claims the application's D-Bus name.</summary>
    /// <returns>A cold signal giving once the host, or <see langword="null"/> when there is no session bus or another instance owns the name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<SingleInstanceHost?> TryStart() =>
        SessionBus.TryConnect()
            .SwitchMap(static connection => connection is null
                ? Signal.Return<SingleInstanceHost?>(null)
                : Claim(connection));

    /// <summary>Gets the activation token handed to this process by the launcher, if any.</summary>
    /// <returns>The token.</returns>
    public static string? GetLaunchActivationToken()
    {
        var token = Environment.GetEnvironmentVariable("XDG_ACTIVATION_TOKEN");
        return !string.IsNullOrEmpty(token) ? token : Environment.GetEnvironmentVariable("DESKTOP_STARTUP_ID");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _connection.Dispose();
        _openRequests.OnCompleted();
        _openRequests.Dispose();
    }

    /// <summary>Sends the open or activate call to a running instance.</summary>
    /// <param name="connection">The open session bus.</param>
    /// <param name="uris">Paths or URIs to open; empty to just raise the window.</param>
    /// <param name="activationToken">The activation token from the environment, if any.</param>
    /// <returns>A cold signal giving once whether the running instance accepted the request.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<bool> Forward(DBusConnection connection, string[] uris, string? activationToken) =>
        Signal.Defer(() =>
            {
                MessageBuffer message;
                using (var writer = connection.GetMessageWriter())
                {
                    var forwards = uris.Length > 0;
                    var method = forwards ? ApplicationInterface.OpenMethod : ApplicationInterface.ActivateMethod;
                    writer.WriteMethodCallHeader(AppIdentity.ApplicationId, AppIdentity.ObjectPath, ApplicationInterface.Name, method, forwards ? "asa{sv}" : "a{sv}", MessageFlags.NoAutoStart);
                    if (forwards)
                    {
                        writer.WriteArray(uris);
                    }

                    writer.WriteDictionary(ApplicationInterface.CreatePlatformData(activationToken));
                    message = writer.CreateMessage();
                }

                // The D-Bus library's own async call, awaited once here; unsubscribing stops waiting for the answer.
                return Signal.FromAsync(async token =>
                {
                    await connection.CallMethodAsync(message).WaitAsync(token).ConfigureAwait(false);
                    return true;
                });
            })
            .Timeout(ForwardTimeout)
            .Catch<bool, DBusErrorReplyException>(static _ => Signal.Return(false))
            .Catch<bool, TimeoutException>(static _ => Signal.Return(false))
            .Catch<bool, DBusConnectionException>(static _ => Signal.Return(false));

    /// <summary>Asks the bus for the application's name on a connection.</summary>
    /// <param name="connection">The open session bus; owned by the host once the name is claimed, closed otherwise.</param>
    /// <returns>A cold signal giving once the host, or <see langword="null"/> when another instance owns the name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<SingleInstanceHost?> Claim(DBusConnection connection) =>
        Signal.Defer(() =>
        {
            var host = new SingleInstanceHost(connection);
            connection.AddMethodHandler(new ApplicationMethodHandler(host._openRequests.OnNext));

            // The D-Bus library's own async call, awaited once here.
            return Signal.FromAsync(() => connection.TryRequestNameAsync(AppIdentity.ApplicationId, RequestNameOptions.None))
                .Catch<bool, DBusErrorReplyException>(static ex =>
                {
                    AppLog.Warning(ex, $"Could not claim {AppIdentity.ApplicationId}.");
                    return Signal.Return(false);
                })
                .Select(claimed =>
                {
                    if (claimed)
                    {
                        return host;
                    }

                    host.Dispose();
                    return null;
                });
        });
}
