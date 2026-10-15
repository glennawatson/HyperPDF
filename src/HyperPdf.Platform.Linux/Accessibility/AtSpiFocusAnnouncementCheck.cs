// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Platform;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.Accessibility;

/// <summary>
/// Watches the AT-SPI bus for the focus announcements Avalonia's bridge sends for this process. The bridge announces
/// focus only on objects a screen reader has already read; reading the tree here creates them, so a repeated focus
/// change is announced.
/// </summary>
[DebuggerDisplay("AtSpiFocusAnnouncementCheck: {IsActive}")]
public sealed class AtSpiFocusAnnouncementCheck : IFocusAnnouncementCheck
{
    /// <summary>The bus name that hands out the accessibility bus address.</summary>
    private const string A11yBusName = "org.a11y.Bus";

    /// <summary>The object path of <see cref="A11yBusName"/>.</summary>
    private const string A11yBusPath = "/org/a11y/bus";

    /// <summary>The AT-SPI registry's bus name and interface.</summary>
    private const string Registry = "org.a11y.atspi.Registry";

    /// <summary>The AT-SPI registry's object path.</summary>
    private const string RegistryPath = "/org/a11y/atspi/registry";

    /// <summary>The message bus's own name, path and interface.</summary>
    private const string MessageBus = "org.freedesktop.DBus";

    /// <summary>The message bus's object path.</summary>
    private const string MessageBusPath = "/org/freedesktop/DBus";

    /// <summary>The properties interface.</summary>
    private const string Properties = "org.freedesktop.DBus.Properties";

    /// <summary>The accessible object interface.</summary>
    private const string Accessible = "org.a11y.atspi.Accessible";

    /// <summary>The object event interface.</summary>
    private const string ObjectEvents = "org.a11y.atspi.Event.Object";

    /// <summary>The root object of an application.</summary>
    private const string RootPath = "/org/a11y/atspi/accessible/root";

    /// <summary>The state an announced focus change sets.</summary>
    private const string Focused = "focused";

    /// <summary>The most objects read in one walk, so a runaway tree cannot stall the check.</summary>
    private const int MaxObjects = 20_000;

    /// <summary>The pause between looks for the bridge's connection while the app starts.</summary>
    private static readonly TimeSpan FindBridgeInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>How long to look for the bridge's connection.</summary>
    private static readonly TimeSpan FindBridgeTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Event registrations that make the bridge send object events, as bus name and event.</summary>
    private readonly HashSet<(string Bus, string Event)> _listeners = [];

    /// <summary>Guards the event registrations.</summary>
    private readonly Lock _listenersGate = new();

    /// <summary>Stops the background start.</summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>Signals each time the check becomes active.</summary>
    private readonly Signal<RxVoid> _activations = new();

    /// <summary>Guards <see cref="_wasActive"/>.</summary>
    private readonly Lock _activeGate = new();

    /// <summary>Subscriptions to the bus, released on dispose.</summary>
    private readonly List<IDisposable> _subscriptions = [];

    /// <summary>Guards the subscriptions.</summary>
    private readonly Lock _subscriptionsGate = new();

    /// <summary>The accessibility bus connection.</summary>
    private DBusConnection? _connection;

    /// <summary>The unique bus name of the bridge's connection in this process.</summary>
    private volatile string? _bridge;

    /// <summary>Whether an assistive technology is registered for object events.</summary>
    private volatile bool _hasListeners;

    /// <summary>The focus announcements seen from the bridge.</summary>
    private long _announcements;

    /// <summary>Whether the check was active when last looked at, so each activation is signalled once.</summary>
    private bool _wasActive;

    /// <summary>Initializes a new instance of the <see cref="AtSpiFocusAnnouncementCheck"/> class and starts watching.</summary>
    public AtSpiFocusAnnouncementCheck()
    {
        Activations = new(_activations);
        _ = StartAsync(_stop.Token);
    }

    /// <inheritdoc/>
    public bool IsActive => _hasListeners && _bridge is not null;

    /// <inheritdoc/>
    public AsObservableSignal<RxVoid> Activations { get; }

    /// <inheritdoc/>
    public long Announcements => Interlocked.Read(ref _announcements);

    /// <summary>Gets whether a registered event makes Avalonia's bridge send object events, as its own tracker decides.</summary>
    /// <param name="eventName">The registered event, such as <c>object:state-changed:focused</c>.</param>
    /// <returns><see langword="true"/> for <c>*</c> and the object, window and focus classes.</returns>
    public static bool IsObjectEvent(string eventName) =>
        !string.IsNullOrWhiteSpace(eventName)
        && (eventName == "*"
            || eventName.StartsWith("object:", StringComparison.OrdinalIgnoreCase)
            || eventName.StartsWith("window:", StringComparison.OrdinalIgnoreCase)
            || eventName.StartsWith("focus:", StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc/>
    public async Task<bool> PrepareRepeatAsync(long announcementsBefore, CancellationToken cancellationToken)
    {
        if (_connection is not { } connection || _bridge is not { } bridge)
        {
            return false;
        }

        // The bridge answers on the UI thread after the focus change, and sends its announcement on the same
        // connection first, so once the answer arrives any announcement has too.
        await CallAsync(connection, PropertyCall(connection, bridge, RootPath, Accessible, "ChildCount"), cancellationToken).ConfigureAwait(false);
        if (Announcements != announcementsBefore)
        {
            return false;
        }

        await ReadTreeAsync(connection, bridge, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _stop.Cancel();
        lock (_subscriptionsGate)
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
        }

        _connection?.Dispose();
        _activations.Dispose();
        _stop.Dispose();
    }

    /// <summary>Reads every object of the bridge's tree, which makes the bridge create the ones nobody has read.</summary>
    /// <param name="connection">The accessibility bus.</param>
    /// <param name="bridge">The bridge's bus name.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>A task that completes once the tree is read.</returns>
    private static async Task ReadTreeAsync(DBusConnection connection, string bridge, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(RootPath);
        var read = 0;
        while (read < MaxObjects && pending.TryPop(out var path))
        {
            read++;
            var children = await connection.CallMethodAsync(
                MethodCall(connection, bridge, path, Accessible, "GetChildren"),
                static (message, _) => ReadChildPaths(message)).WaitAsync(cancellationToken).ConfigureAwait(false);
            foreach (var child in children)
            {
                pending.Push(child);
            }
        }
    }

    /// <summary>Reads the object paths from a <c>GetChildren</c> reply, an array of <c>(so)</c>.</summary>
    /// <param name="message">The reply.</param>
    /// <returns>The child paths.</returns>
    private static List<string> ReadChildPaths(Message message)
    {
        var reader = message.GetBodyReader();
        var paths = new List<string>();
        var end = reader.ReadArrayStart(DBusType.Struct);
        while (reader.HasNext(end))
        {
            reader.AlignStruct();
            _ = reader.ReadStringAsSpan();
            paths.Add(reader.ReadObjectPathAsString());
        }

        return paths;
    }

    /// <summary>Writes a method call with no arguments.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="destination">The bus name.</param>
    /// <param name="path">The object path.</param>
    /// <param name="interfaceName">The interface.</param>
    /// <param name="member">The method.</param>
    /// <returns>The message.</returns>
    private static MessageBuffer MethodCall(DBusConnection connection, string destination, string path, string interfaceName, string member)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination, path, interfaceName, member);
        return writer.CreateMessage();
    }

    /// <summary>Writes a method call with one string argument.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="destination">The bus name.</param>
    /// <param name="path">The object path.</param>
    /// <param name="interfaceName">The interface.</param>
    /// <param name="member">The method.</param>
    /// <param name="argument">The argument.</param>
    /// <returns>The message.</returns>
    private static MessageBuffer MethodCall(DBusConnection connection, string destination, string path, string interfaceName, string member, string argument)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination, path, interfaceName, member, "s");
        writer.WriteString(argument);
        return writer.CreateMessage();
    }

    /// <summary>Writes a property read.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="destination">The bus name.</param>
    /// <param name="path">The object path.</param>
    /// <param name="interfaceName">The property's interface.</param>
    /// <param name="property">The property.</param>
    /// <returns>The message.</returns>
    private static MessageBuffer PropertyCall(DBusConnection connection, string destination, string path, string interfaceName, string property)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination, path, Properties, "Get", "ss");
        writer.WriteString(interfaceName);
        writer.WriteString(property);
        return writer.CreateMessage();
    }

    /// <summary>Sends a call and waits for its reply.</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="message">The call.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes with the reply.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task CallAsync(DBusConnection connection, MessageBuffer message, CancellationToken cancellationToken) =>
        connection.CallMethodAsync(message).WaitAsync(cancellationToken);

    /// <summary>Gets the accessibility bus address from the session bus.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The address, or <see langword="null"/> when there is no accessibility bus.</returns>
    private static async Task<string?> GetBusAddressAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(DBusAddress.Session))
        {
            return null;
        }

        using var session = new DBusConnection(DBusAddress.Session);
        await session.ConnectAsync().ConfigureAwait(false);
        return await session.CallMethodAsync(
            MethodCall(session, A11yBusName, A11yBusPath, A11yBusName, "GetAddress"),
            static (message, _) => message.GetBodyReader().ReadString()).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads who sent a <c>focused</c> state change that sets the state.</summary>
    /// <param name="message">The signal, whose body is <c>siiva{sv}</c>.</param>
    /// <returns>The sender, or an empty string when the state was cleared.</returns>
    private static string ReadFocusSender(Message message)
    {
        var reader = message.GetBodyReader();
        _ = reader.ReadStringAsSpan();
        return reader.ReadInt32() == 1 ? message.SenderAsString ?? string.Empty : string.Empty;
    }

    /// <summary>Reads the bus name and event from a registry signal.</summary>
    /// <param name="message">The signal.</param>
    /// <returns>The registration.</returns>
    private static (string Bus, string Event) ReadRegistration(Message message)
    {
        var reader = message.GetBodyReader();
        return (reader.ReadString(), reader.ReadString());
    }

    /// <summary>Reads the registry's current registrations, an array of <c>(ss)</c>.</summary>
    /// <param name="message">The reply.</param>
    /// <returns>The registrations.</returns>
    private static List<(string Bus, string Event)> ReadRegistrations(Message message)
    {
        var reader = message.GetBodyReader();
        var registrations = new List<(string Bus, string Event)>();
        var end = reader.ReadArrayStart(DBusType.Struct);
        while (reader.HasNext(end))
        {
            reader.AlignStruct();
            registrations.Add((reader.ReadString(), reader.ReadString()));
        }

        return registrations;
    }

    /// <summary>Finds the bridge's connection: the other connection this process holds on the accessibility bus.</summary>
    /// <param name="connection">The accessibility bus.</param>
    /// <param name="cancellationToken">Stops looking.</param>
    /// <returns>Its unique bus name, or <see langword="null"/> when the bridge never connected.</returns>
    private static async Task<string?> FindBridgeAsync(DBusConnection connection, CancellationToken cancellationToken)
    {
        var processId = (uint)Environment.ProcessId;
        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < FindBridgeTimeout)
        {
            foreach (var name in await connection.ListServicesAsync().WaitAsync(cancellationToken).ConfigureAwait(false))
            {
                if (name.StartsWith(':') && name != connection.UniqueName && await GetProcessIdAsync(connection, name, cancellationToken).ConfigureAwait(false) == processId)
                {
                    return name;
                }
            }

            await Task.Delay(FindBridgeInterval, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>Asks the bus which process owns a name.</summary>
    /// <param name="connection">The bus.</param>
    /// <param name="name">The unique name.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The process id, or zero when the name has gone.</returns>
    private static async Task<uint> GetProcessIdAsync(DBusConnection connection, string name, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.CallMethodAsync(
                MethodCall(connection, MessageBus, MessageBusPath, MessageBus, "GetConnectionUnixProcessID", name),
                static (message, _) => message.GetBodyReader().ReadUInt32()).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DBusErrorReplyException)
        {
            return 0;
        }
    }

    /// <summary>Connects, tracks screen reader registrations and finds the bridge's connection.</summary>
    /// <param name="cancellationToken">Stops starting.</param>
    /// <returns>A task that completes once watching has started or failed.</returns>
    private async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (await GetBusAddressAsync(cancellationToken).ConfigureAwait(false) is not { Length: > 0 } address)
            {
                return;
            }

            var connection = new DBusConnection(address);
            _connection = connection;
            await connection.ConnectAsync().ConfigureAwait(false);
            await WatchRegistryAsync(connection, cancellationToken).ConfigureAwait(false);
            Track(await connection.AddMatchAsync(
                new MatchRule { Type = MessageType.Signal, Interface = ObjectEvents, Member = "StateChanged", Arg0 = Focused },
                static (message, _) => ReadFocusSender(message),
                static notification =>
                {
                    if (notification.HasValue && notification.State is AtSpiFocusAnnouncementCheck check)
                    {
                        check.OnFocusAnnounced(notification.Value);
                    }
                },
                false,
                ObserverFlags.None,
                this).ConfigureAwait(false));
            _bridge = await FindBridgeAsync(connection, cancellationToken).ConfigureAwait(false);
            SignalIfActivated();
        }
        catch (Exception ex) when (ex is DBusConnectionException or DBusErrorReplyException or OperationCanceledException or ObjectDisposedException)
        {
            // No accessibility bus, or the app is closing: focus changes are then left as the bridge sends them.
            Trace.TraceInformation($"Focus announcement check unavailable: {ex.Message}");
        }
    }

    /// <summary>Counts a focus announcement from the bridge.</summary>
    /// <param name="sender">The signal's sender.</param>
    private void OnFocusAnnounced(string sender)
    {
        if (sender.Length > 0 && sender == _bridge)
        {
            _ = Interlocked.Increment(ref _announcements);
        }
    }

    /// <summary>Tracks which assistive technologies are registered for object events.</summary>
    /// <param name="connection">The accessibility bus.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>A task that completes once the current registrations are known.</returns>
    private async Task WatchRegistryAsync(DBusConnection connection, CancellationToken cancellationToken)
    {
        Track(await connection.AddMatchAsync(
            new MatchRule { Type = MessageType.Signal, Interface = Registry, Member = "EventListenerRegistered" },
            static (message, _) => ReadRegistration(message),
            static notification =>
            {
                if (notification.HasValue && notification.State is AtSpiFocusAnnouncementCheck check)
                {
                    check.UpdateListeners(notification.Value, true);
                }
            },
            false,
            ObserverFlags.None,
            this).ConfigureAwait(false));
        Track(await connection.AddMatchAsync(
            new MatchRule { Type = MessageType.Signal, Interface = Registry, Member = "EventListenerDeregistered" },
            static (message, _) => ReadRegistration(message),
            static notification =>
            {
                if (notification.HasValue && notification.State is AtSpiFocusAnnouncementCheck check)
                {
                    check.UpdateListeners(notification.Value, false);
                }
            },
            false,
            ObserverFlags.None,
            this).ConfigureAwait(false));
        var registered = await connection.CallMethodAsync(
            MethodCall(connection, Registry, RegistryPath, Registry, "GetRegisteredEvents"),
            static (message, _) => ReadRegistrations(message)).WaitAsync(cancellationToken).ConfigureAwait(false);
        foreach (var registration in registered)
        {
            UpdateListeners(registration, true);
        }
    }

    /// <summary>Records a registration change.</summary>
    /// <param name="registration">The bus name and event.</param>
    /// <param name="registered">Whether it was added or removed.</param>
    private void UpdateListeners((string Bus, string Event) registration, bool registered)
    {
        if (!IsObjectEvent(registration.Event))
        {
            return;
        }

        lock (_listenersGate)
        {
            _ = registered ? _listeners.Add(registration) : _listeners.Remove(registration);
            _hasListeners = _listeners.Count > 0;
        }

        SignalIfActivated();
    }

    /// <summary>Signals once each time the check goes from inactive to active.</summary>
    private void SignalIfActivated()
    {
        bool activated;
        lock (_activeGate)
        {
            var active = IsActive;
            activated = active && !_wasActive;
            _wasActive = active;
        }

        if (!activated || _stop.IsCancellationRequested)
        {
            return;
        }

        try
        {
            _activations.OnNext(RxVoid.Default);
        }
        catch (ObjectDisposedException)
        {
            // Disposed while a bus signal was being handled; nobody is listening any more.
        }
    }

    /// <summary>Keeps a bus subscription until dispose.</summary>
    /// <param name="subscription">The subscription.</param>
    private void Track(IDisposable subscription)
    {
        lock (_subscriptionsGate)
        {
            _subscriptions.Add(subscription);
        }
    }
}
