// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Platform;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.DBus;

/// <summary>Serves <c>org.freedesktop.Application</c> for the primary instance.</summary>
[DebuggerDisplay("ApplicationMethodHandler: {Path}")]
internal sealed class ApplicationMethodHandler : IPathMethodHandler
{
    /// <summary>The introspection data.</summary>
    private static readonly ReadOnlyMemory<byte> InterfaceXml = """
        <interface name="org.freedesktop.Application">
          <method name="Activate"><arg type="a{sv}" name="platform_data" direction="in"/></method>
          <method name="Open"><arg type="as" name="uris" direction="in"/><arg type="a{sv}" name="platform_data" direction="in"/></method>
          <method name="ActivateAction">
            <arg type="s" name="action_name" direction="in"/><arg type="av" name="parameter" direction="in"/><arg type="a{sv}" name="platform_data" direction="in"/>
          </method>
        </interface>
        """u8.ToArray();

    /// <summary>Receives requests.</summary>
    private readonly Action<OpenRequest> _onRequest;

    /// <summary>Initializes a new instance of the <see cref="ApplicationMethodHandler"/> class.</summary>
    /// <param name="onRequest">Receives requests on a D-Bus thread.</param>
    internal ApplicationMethodHandler(Action<OpenRequest> onRequest) => _onRequest = onRequest;

    /// <inheritdoc/>
    public string Path => AppIdentity.ObjectPath;

    /// <inheritdoc/>
    public bool HandlesChildPaths => false;

    /// <inheritdoc/>
    public ValueTask HandleMethodAsync(MethodContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.IsDBusIntrospectRequest)
        {
            context.ReplyIntrospectXml([InterfaceXml], Array.Empty<string>());
            return ValueTask.CompletedTask;
        }

        var request = context.Request;
        if (request.InterfaceAsString != ApplicationInterface.Name)
        {
            context.ReplyUnknownMethodError();
            return ValueTask.CompletedTask;
        }

        var reader = request.GetBodyReader();
        switch (request.MemberAsString)
        {
            case ApplicationInterface.OpenMethod:
            {
                var uris = reader.ReadArrayOfString();
                var data = reader.ReadDictionaryOfStringToVariantValue();
                _onRequest(new(uris, ApplicationInterface.GetActivationToken(data)));
                break;
            }

            case ApplicationInterface.ActivateMethod:
            {
                var data = reader.ReadDictionaryOfStringToVariantValue();
                _onRequest(new([], ApplicationInterface.GetActivationToken(data)));
                break;
            }

            case ApplicationInterface.ActivateActionMethod:
            {
                _onRequest(new([], null));
                break;
            }

            default:
            {
                context.ReplyUnknownMethodError();
                return ValueTask.CompletedTask;
            }
        }

        if (!context.NoReplyExpected)
        {
            using var writer = context.CreateReplyWriter(null!);
            context.Reply(writer.CreateMessage());
        }

        return ValueTask.CompletedTask;
    }
}
