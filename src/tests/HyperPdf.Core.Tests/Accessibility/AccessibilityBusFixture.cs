// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using TUnit.Core.Interfaces;
using Tmds.DBus.Protocol;

namespace HyperPdf.Core.Tests.Accessibility;

/// <summary>
/// One private D-Bus daemon for a whole test class, made the session bus of the test process. The D-Bus library reads the session bus
/// address once and keeps it, so every test in the process has to use the same daemon.
/// </summary>
[DebuggerDisplay("AccessibilityBusFixture: {Bus?.Address}")]
public sealed class AccessibilityBusFixture : IAsyncInitializer, IAsyncDisposable
{
    /// <summary>The environment variable that holds the session bus address.</summary>
    private const string SessionBusAddress = "DBUS_SESSION_BUS_ADDRESS";

    /// <summary>The session bus address before the fixture changed it.</summary>
    private string? _previous;

    /// <summary>Gets the private daemon, or <see langword="null"/> when <c>dbus-daemon</c> is not installed.</summary>
    internal PrivateBus? Bus { get; private set; }

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        Bus = await PrivateBus.TryStartAsync().ConfigureAwait(false);
        if (Bus is null)
        {
            return;
        }

        _previous = Environment.GetEnvironmentVariable(SessionBusAddress);
        Environment.SetEnvironmentVariable(SessionBusAddress, Bus.Address);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Bus is null)
        {
            return;
        }

        Environment.SetEnvironmentVariable(SessionBusAddress, _previous);
        await Bus.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Starts the fake <c>org.a11y.Bus</c> service for one test.</summary>
    /// <param name="enabled">Whether assistive technology is on.</param>
    /// <returns>The service, which releases its name when disposed.</returns>
    internal async Task<RunningService> StartBusServiceAsync(bool enabled)
    {
        var connection = await Bus!.ConnectAsync().ConfigureAwait(false);
        var service = await FakeAccessibilityServices.StartBusServiceAsync(connection, Bus.Address, enabled).ConfigureAwait(false);
        return new(connection, service);
    }

    /// <summary>Starts the fake registry for one test.</summary>
    /// <returns>The registry, which releases its name when disposed.</returns>
    internal async Task<RunningService> StartRegistryAsync()
    {
        var connection = await Bus!.ConnectAsync().ConfigureAwait(false);
        var registry = await FakeAccessibilityServices.StartRegistryAsync(connection).ConfigureAwait(false);
        return new(connection, registry);
    }

    /// <summary>A fake service and the connection that owns its name.</summary>
    /// <param name="Connection">The connection.</param>
    /// <param name="Service">The service.</param>
    [DebuggerDisplay("RunningService")]
    internal sealed record RunningService(DBusConnection Connection, FakeAccessibilityServices Service) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose() => Connection.Dispose();
    }
}
