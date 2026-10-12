// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Platform;
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
    /// <returns><see langword="true"/> when a running instance accepted the request.</returns>
    public static async Task<bool> TryForwardAsync(IReadOnlyList<string> uris, string? activationToken)
    {
        ArgumentNullException.ThrowIfNull(uris);
        using var connection = await SessionBus.TryConnectAsync().ConfigureAwait(false);
        if (connection is null)
        {
            return false;
        }

        MessageBuffer message;
        using (var writer = connection.GetMessageWriter())
        {
            var method = uris.Count == 0 ? ApplicationInterface.ActivateMethod : ApplicationInterface.OpenMethod;
            writer.WriteMethodCallHeader(AppIdentity.ApplicationId, AppIdentity.ObjectPath, ApplicationInterface.Name, method, uris.Count == 0 ? "a{sv}" : "asa{sv}", MessageFlags.NoAutoStart);
            if (uris.Count > 0)
            {
                writer.WriteArray(uris);
            }

            writer.WriteDictionary(ApplicationInterface.CreatePlatformData(activationToken));
            message = writer.CreateMessage();
        }

        try
        {
            await connection.CallMethodAsync(message).WaitAsync(ForwardTimeout).ConfigureAwait(false);
            return true;
        }
        catch (DBusErrorReplyException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (DBusConnectionException)
        {
            return false;
        }
    }

    /// <summary>Claims the application's D-Bus name.</summary>
    /// <returns>The host, or <see langword="null"/> when there is no session bus or another instance owns the name.</returns>
    public static async Task<SingleInstanceHost?> TryStartAsync()
    {
        var connection = await SessionBus.TryConnectAsync().ConfigureAwait(false);
        if (connection is null)
        {
            return null;
        }

        var host = new SingleInstanceHost(connection);
        connection.AddMethodHandler(new ApplicationMethodHandler(host._openRequests.OnNext));
        try
        {
            if (await connection.TryRequestNameAsync(AppIdentity.ApplicationId, RequestNameOptions.None).ConfigureAwait(false))
            {
                return host;
            }
        }
        catch (DBusErrorReplyException ex)
        {
            Debug.WriteLine($"Could not claim {AppIdentity.ApplicationId}: {ex.Message}");
        }

        host.Dispose();
        return null;
    }

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
}
