// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Diagnostics;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>Receives every request to an element of the tree, under <c>/org/a11y/atspi/accessible</c>, and hands it to the dispatcher.</summary>
[DebuggerDisplay("AtSpiObjectHandler: {Path}")]
internal sealed class AtSpiObjectHandler : IPathMethodHandler
{
    /// <summary>The server the requests are for.</summary>
    private readonly AtSpiSession _session;

    /// <summary>Initializes a new instance of the <see cref="AtSpiObjectHandler"/> class.</summary>
    /// <param name="session">The server the requests are for.</param>
    internal AtSpiObjectHandler(AtSpiSession session) => _session = session;

    /// <inheritdoc/>
    public string Path => AtSpiNames.ObjectPrefix;

    /// <inheritdoc/>
    public bool HandlesChildPaths => true;

    /// <inheritdoc/>
    public ValueTask HandleMethodAsync(MethodContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.IsDBusIntrospectRequest)
        {
            ReplyIntrospection(context);
            return ValueTask.CompletedTask;
        }

        try
        {
            AtSpiDispatcher.Handle(_session, context);
        }
        catch (Exception ex)
        {
            // One request that goes wrong must not close the connection, which would silence the app for every screen reader.
            AppLog.Error(ex, $"An AT-SPI request failed: {context.Request.InterfaceAsString}.{context.Request.MemberAsString}");
            if (!context.ReplySent)
            {
                context.ReplyError("org.freedesktop.DBus.Error.Failed", ex.Message);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Describes the interfaces of the element a request is for.</summary>
    /// <param name="context">The request.</param>
    private void ReplyIntrospection(MethodContext context)
    {
        if (!AtSpiPaths.TryParse(context.Request.PathAsString ?? string.Empty, _session.RootId, out var id) || _session.Tree.Get(id) is not { } node)
        {
            context.ReplyError("org.freedesktop.DBus.Error.UnknownObject", "There is no such element.");
            return;
        }

        var names = AtSpiAccessibleMethods.InterfacesOf(node, _session.RootId);
        names.Add(AtSpiNames.PropertiesInterface);
        var described = new ReadOnlyMemory<byte>[names.Count];
        for (var index = 0; index < described.Length; index++)
        {
            described[index] = AtSpiIntrospection.Describe(names[index]);
        }

        context.ReplyIntrospectXml(described, Array.Empty<string>());
    }
}
