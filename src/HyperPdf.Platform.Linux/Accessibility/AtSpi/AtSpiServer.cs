// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Accessibility;
using HyperPdf.Core.Diagnostics;
using HyperPdf.Core.Reactive;
using HyperPdf.Platform.Linux.DBus;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;
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

    /// <summary>Holds the attempts to attach to the registry, so disposing the server stops them.</summary>
    private readonly MultipleDisposable _embedding = new();

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

        _embedding.Dispose();
        _changes.Dispose();
        _bus.Dispose();
    }

    /// <summary>Connects to the accessibility bus, answers requests and attaches to the registry, trying again if the registry is not there yet.</summary>
    /// <param name="tree">The elements to serve.</param>
    /// <returns>A cold signal giving once the server, or <see langword="null"/> when there is no accessibility bus.</returns>
    internal static IObservable<AtSpiServer?> TryStart(IAccessibleTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return GetBusAddress()
            .SwitchMap(address => string.IsNullOrEmpty(address)
                ? Signal.Return<AtSpiServer?>(null)
                : Connect(address, tree));
    }

    /// <summary>Asks the session bus for the address of the accessibility bus.</summary>
    /// <returns>A cold signal giving once the address, or <see langword="null"/> when the session has no accessibility bus.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<string?> GetBusAddress() =>
        SessionBus.TryConnect()
            .SwitchMap(static session => session is null
                ? Signal.Return<string?>(null)
                : Signal.Using(() => session, ReadBusAddress));

    /// <summary>Reads the accessibility bus address from the session bus.</summary>
    /// <param name="session">The open session bus.</param>
    /// <returns>A cold signal giving once the address, or <see langword="null"/> when the session has no accessibility bus.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<string?> ReadBusAddress(DBusConnection session) =>
        Signal.Defer(() =>
            {
                MessageBuffer message;
                using (var writer = session.GetMessageWriter())
                {
                    writer.WriteMethodCallHeader(AtSpiNames.BusService, AtSpiNames.BusPath, AtSpiNames.BusInterface, AtSpiNames.GetAddressMethod);
                    message = writer.CreateMessage();
                }

                // The D-Bus library's own async call, awaited once here.
                return Signal.FromAsync(async () => (string?)await session.CallMethodAsync(message, static (reply, _) => reply.GetBodyReader().ReadString()).ConfigureAwait(false));
            })
            .Catch<string?, DBusErrorReplyException>(static ex =>
            {
                AppLog.Warning(ex, "The session has no accessibility bus.");
                return Signal.Return<string?>(null);
            });

    /// <summary>Connects to the accessibility bus and starts serving the tree on it.</summary>
    /// <param name="address">The accessibility bus address.</param>
    /// <param name="tree">The elements to serve.</param>
    /// <returns>A cold signal giving once the server, or <see langword="null"/> when the bus could not be reached.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<AtSpiServer?> Connect(string address, IAccessibleTree tree) =>
        Signal.Defer(() =>
            {
                var bus = new DBusConnection(address);

                // The D-Bus library's own async connect, awaited once here.
                return Signal.FromAsync(async () =>
                    {
                        await bus.ConnectAsync().ConfigureAwait(false);
                        return (AtSpiServer?)Serve(bus, tree);
                    })
                    .Catch<AtSpiServer?, DBusConnectionException>(ex => ConnectFailed(ex, bus))
                    .Catch<AtSpiServer?, IOException>(ex => ConnectFailed(ex, bus));
            });

    /// <summary>Records that the accessibility bus could not be reached.</summary>
    /// <param name="exception">The failure.</param>
    /// <param name="bus">The connection to close.</param>
    /// <returns>A signal giving <see langword="null"/>.</returns>
    private static IObservable<AtSpiServer?> ConnectFailed(Exception exception, DBusConnection bus)
    {
        AppLog.Warning(exception, "Could not connect to the accessibility bus.");
        bus.Dispose();
        return Signal.Return<AtSpiServer?>(null);
    }

    /// <summary>Answers requests on a connected bus and starts attaching to the registry.</summary>
    /// <param name="bus">The connected bus.</param>
    /// <param name="tree">The elements to serve.</param>
    /// <returns>The server.</returns>
    private static AtSpiServer Serve(DBusConnection bus, IAccessibleTree tree)
    {
        var session = new AtSpiSession(tree, bus.UniqueName ?? string.Empty);
        bus.AddMethodHandler(new AtSpiObjectHandler(session));
        var server = new AtSpiServer(bus, session);

        // Attempts and the waits between them run one after another until the registry accepts the application; disposing the server ends them.
        server._embedding.Add(LazySequence.Concat(Attempts(bus))
            .TakeWhile(static accepted => !accepted)
            .SubscribeSafe(static _ => { }, static error => AppLog.Error(error, "Attaching to the AT-SPI registry ended with an unexpected failure.")));
        return server;
    }

    /// <summary>Makes an attempt to attach to the registry, then a wait, for as long as the sequence is read.</summary>
    /// <param name="bus">The connection to the accessibility bus.</param>
    /// <returns>Signals giving whether the registry accepted the application; the waits give <see langword="false"/>.</returns>
    private static IEnumerable<IObservable<bool>> Attempts(DBusConnection bus)
    {
        while (true)
        {
            yield return TryEmbed(bus);
            yield return Signal.Timer(EmbedRetryPeriod, TaskPoolSequencer.Default).Select(static _ => false);
        }
    }

    /// <summary>Makes one attempt to attach to the registry, which is what makes screen readers see the application.</summary>
    /// <param name="bus">The connection to the accessibility bus.</param>
    /// <returns>A cold signal giving once whether the registry accepted the application.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<bool> TryEmbed(DBusConnection bus) =>
        Signal.Defer(() =>
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

                // The D-Bus library's own async call, awaited once here; unsubscribing stops waiting for the answer.
                return Signal.FromAsync(async token =>
                {
                    await bus.CallMethodAsync(message).WaitAsync(token).ConfigureAwait(false);
                    return true;
                });
            })
            .Timeout(EmbedTimeout)
            .Catch<bool, DBusErrorReplyException>(NotAcceptedYet)
            .Catch<bool, DBusConnectionException>(NotAcceptedYet)
            .Catch<bool, TimeoutException>(NotAcceptedYet)
            .Catch<bool, IOException>(NotAcceptedYet);

    /// <summary>Records that the registry did not accept the application yet.</summary>
    /// <param name="exception">The failure.</param>
    /// <returns>A signal giving <see langword="false"/>.</returns>
    private static IObservable<bool> NotAcceptedYet(Exception exception)
    {
        AppLog.Warning(exception, "The AT-SPI registry did not accept the application yet.");
        return Signal.Return(false);
    }
}
