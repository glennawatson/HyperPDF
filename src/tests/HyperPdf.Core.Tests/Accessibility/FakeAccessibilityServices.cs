// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Tmds.DBus.Protocol;

namespace HyperPdf.Core.Tests.Accessibility;

/// <summary>
/// Stands in for the desktop's accessibility service on a private bus: <c>org.a11y.Bus</c>, which says where the accessibility bus is and
/// whether assistive technology is on, and the registry, which an application attaches itself to.
/// </summary>
[DebuggerDisplay("FakeAccessibilityServices: {BusAddress}")]
internal sealed class FakeAccessibilityServices : IPathMethodHandler
{
    /// <summary>The path of the accessibility service on the session bus.</summary>
    private const string BusPath = "/org/a11y/bus";

    /// <summary>The path of the registry's root object.</summary>
    private const string RootPath = "/org/a11y/atspi/accessible/root";

    /// <summary>The address handed out as the accessibility bus.</summary>
    private readonly string _busAddress;

    /// <summary>The value of the switch that says assistive technology is on.</summary>
    private readonly bool _enabled;

    /// <summary>The paths this handler serves, which is the one object it was made for.</summary>
    private readonly string _path;

    /// <summary>The request received by the registry, once an application has attached.</summary>
    private readonly TaskCompletionSource<string> _embedded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Initializes a new instance of the <see cref="FakeAccessibilityServices"/> class.</summary>
    /// <param name="busAddress">The address handed out as the accessibility bus.</param>
    /// <param name="enabled">Whether assistive technology is on.</param>
    /// <param name="path">The one path this handler serves.</param>
    private FakeAccessibilityServices(string busAddress, bool enabled, string path)
    {
        _busAddress = busAddress;
        _enabled = enabled;
        _path = path;
    }

    /// <inheritdoc/>
    public string Path => _path;

    /// <inheritdoc/>
    public bool HandlesChildPaths => false;

    /// <summary>Gets the address that was handed out.</summary>
    internal string BusAddress => _busAddress;

    /// <summary>Gets a task that completes with the unique name of the application that attached to the registry.</summary>
    internal Task<string> Embedded => _embedded.Task;

    /// <inheritdoc/>
    public ValueTask HandleMethodAsync(MethodContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.IsDBusIntrospectRequest)
        {
            context.ReplyIntrospectXml([], Array.Empty<string>());
            return ValueTask.CompletedTask;
        }

        var request = context.Request;
        var reader = request.GetBodyReader();
        switch (request.MemberAsString)
        {
            case "GetAddress":
            {
                using var writer = context.CreateReplyWriter("s");
                writer.WriteString(_busAddress);
                context.Reply(writer.CreateMessage());
                break;
            }

            case "Get":
            {
                using var writer = context.CreateReplyWriter("v");
                writer.WriteVariantBool(_enabled);
                context.Reply(writer.CreateMessage());
                break;
            }

            case "Embed":
            {
                reader.AlignStruct();
                var name = reader.ReadString();
                _ = _embedded.TrySetResult(name);
                using var writer = context.CreateReplyWriter("(so)");
                writer.WriteStructureStart();
                writer.WriteString(string.Empty);
                writer.WriteObjectPath(RootPath);
                context.Reply(writer.CreateMessage());
                break;
            }

            default:
            {
                context.ReplyUnknownMethodError();
                break;
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Starts the <c>org.a11y.Bus</c> service.</summary>
    /// <param name="connection">The connection that owns the name.</param>
    /// <param name="busAddress">The address handed out as the accessibility bus.</param>
    /// <param name="enabled">Whether assistive technology is on.</param>
    /// <returns>The service.</returns>
    internal static async Task<FakeAccessibilityServices> StartBusServiceAsync(DBusConnection connection, string busAddress, bool enabled)
    {
        var service = new FakeAccessibilityServices(busAddress, enabled, BusPath);
        connection.AddMethodHandler(service);
        await connection.RequestNameAsync("org.a11y.Bus", RequestNameOptions.None).ConfigureAwait(false);
        return service;
    }

    /// <summary>Starts the registry.</summary>
    /// <param name="connection">The connection that owns the name.</param>
    /// <returns>The registry.</returns>
    internal static async Task<FakeAccessibilityServices> StartRegistryAsync(DBusConnection connection)
    {
        var registry = new FakeAccessibilityServices(string.Empty, false, RootPath);
        connection.AddMethodHandler(registry);
        await connection.RequestNameAsync("org.a11y.atspi.Registry", RequestNameOptions.None).ConfigureAwait(false);
        return registry;
    }
}
