// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Accessibility;
using HyperPdf.Core.Diagnostics;
using HyperPdf.Platform.Linux.DBus;
using ReactiveUI.Primitives;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>
/// Serves the app's elements to screen readers over AT-SPI2, on the accessibility bus. It works under X11 and Wayland, because
/// AT-SPI is carried by D-Bus and does not depend on the display server. The server connects, attaches itself to the registry,
/// answers requests about elements and sends events when the tree changes.
/// </summary>
[DebuggerDisplay("AtSpiServer: {_bus.UniqueName}")]
internal sealed class AtSpiServer : IDisposable
{
    /// <summary>How long the registry may take to accept the application.</summary>
    private static readonly TimeSpan EmbedTimeout = TimeSpan.FromSeconds(5);

    /// <summary>How often attaching to the registry is tried again while the registry is not there.</summary>
    private static readonly TimeSpan EmbedRetryPeriod = TimeSpan.FromSeconds(3);

    /// <summary>The connection to the accessibility bus.</summary>
    private readonly DBusConnection _bus;

    /// <summary>The subscription to changes of the tree.</summary>
    private readonly IDisposable _changes;

    /// <summary>Stops the attempts to attach to the registry.</summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="AtSpiServer"/> class.</summary>
    /// <param name="bus">The connection to the accessibility bus.</param>
    /// <param name="session">The server's state.</param>
    private AtSpiServer(DBusConnection bus, AtSpiSession session)
    {
        _bus = bus;
        var emitter = new AtSpiEmitter(bus, session);

        // The step reads the tree and emitter held here; Primitives has no state-passing SubscribeSafe yet.
        _changes = session.Tree.Changes.SubscribeSafe(change => AtSpiChanges.Apply(change, session.Tree, emitter), AppLog.Error);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stop.Cancel();
        _changes.Dispose();
        _bus.Dispose();
        _stop.Dispose();
    }

    /// <summary>Connects to the accessibility bus, answers requests and attaches to the registry, trying again if the registry is not there yet.</summary>
    /// <param name="tree">The elements to serve.</param>
    /// <returns>The server, or <see langword="null"/> when there is no accessibility bus.</returns>
    internal static async Task<AtSpiServer?> TryStartAsync(IAccessibleTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var address = await GetBusAddressAsync().ConfigureAwait(false);
        if (string.IsNullOrEmpty(address))
        {
            return null;
        }

        var bus = new DBusConnection(address);
        try
        {
            await bus.ConnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DBusConnectionException or IOException)
        {
            AppLog.Warning(ex, "Could not connect to the accessibility bus.");
            bus.Dispose();
            return null;
        }

        var session = new AtSpiSession(tree, bus.UniqueName ?? string.Empty);
        bus.AddMethodHandler(new AtSpiObjectHandler(session));
        var server = new AtSpiServer(bus, session);
        _ = server.EmbedUntilAcceptedAsync(server._stop.Token);
        return server;
    }

    /// <summary>Asks the session bus for the address of the accessibility bus.</summary>
    /// <returns>The address, or <see langword="null"/> when the session has no accessibility bus.</returns>
    private static async Task<string?> GetBusAddressAsync()
    {
        using var session = await SessionBus.TryConnectAsync().ConfigureAwait(false);
        if (session is null)
        {
            return null;
        }

        MessageBuffer message;
        using (var writer = session.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(AtSpiNames.BusService, AtSpiNames.BusPath, AtSpiNames.BusInterface, AtSpiNames.GetAddressMethod);
            message = writer.CreateMessage();
        }

        try
        {
            return await session.CallMethodAsync(message, static (reply, _) => reply.GetBodyReader().ReadString()).ConfigureAwait(false);
        }
        catch (DBusErrorReplyException ex)
        {
            AppLog.Warning(ex, "The session has no accessibility bus.");
            return null;
        }
    }

    /// <summary>Attaches the application to the registry's tree, which is what makes screen readers see it.</summary>
    /// <param name="bus">The connection to the accessibility bus.</param>
    /// <returns>A task that finishes when the registry has accepted the application.</returns>
    private static async Task EmbedAsync(DBusConnection bus)
    {
        MessageBuffer message;
        using (var writer = bus.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(AtSpiNames.RegistryService, AtSpiNames.RootPath, AtSpiNames.SocketInterface, AtSpiNames.EmbedMethod, "(so)");
            writer.WriteStructureStart();
            writer.WriteString(bus.UniqueName ?? string.Empty);
            writer.WriteObjectPath(AtSpiNames.RootPath);
            message = writer.CreateMessage();
        }

        await bus.CallMethodAsync(message).WaitAsync(EmbedTimeout).ConfigureAwait(false);
    }

    /// <summary>Tries to attach to the registry until it accepts the application. The server answers requests in the meantime.</summary>
    /// <param name="cancellationToken">Stops the attempts.</param>
    /// <returns>A task that ends when the registry has accepted the application or the server is disposed.</returns>
    private async Task EmbedUntilAcceptedAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(EmbedRetryPeriod);
            do
            {
                if (await TryEmbedAsync().ConfigureAwait(false))
                {
                    return;
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // The server was disposed while waiting for the registry.
        }
    }

    /// <summary>Makes one attempt to attach to the registry.</summary>
    /// <returns>Whether the registry accepted the application.</returns>
    private async Task<bool> TryEmbedAsync()
    {
        try
        {
            await EmbedAsync(_bus).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is DBusErrorReplyException or DBusConnectionException or TimeoutException or IOException)
        {
            AppLog.Warning(ex, "The AT-SPI registry did not accept the application yet.");
            return false;
        }
    }
}
