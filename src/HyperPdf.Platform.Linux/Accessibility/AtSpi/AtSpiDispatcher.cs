// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Accessibility;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>Routes a request from a screen reader to the interface that answers it.</summary>
internal static class AtSpiDispatcher
{
    /// <summary>The error for an element that no longer exists.</summary>
    private const string UnknownObjectError = "org.freedesktop.DBus.Error.UnknownObject";

    /// <summary>The error for a request whose arguments are wrong.</summary>
    private const string InvalidArgsError = "org.freedesktop.DBus.Error.InvalidArgs";

    /// <summary>The properties method that reads one property.</summary>
    private const string GetMember = "Get";

    /// <summary>The properties method that reads all the properties of an interface.</summary>
    private const string GetAllMember = "GetAll";

    /// <summary>Answers a request.</summary>
    /// <param name="session">The server.</param>
    /// <param name="context">The request.</param>
    internal static void Handle(AtSpiSession session, MethodContext context)
    {
        var request = context.Request;
        if (!AtSpiPaths.TryParse(request.PathAsString ?? string.Empty, session.RootId, out var id) || session.Tree.Get(id) is not { } node)
        {
            context.ReplyError(UnknownObjectError, "There is no such element.");
            return;
        }

        var call = new AtSpiCall(session, node, context);
        var reader = request.GetBodyReader();
        var interfaceName = request.InterfaceAsString ?? string.Empty;
        var member = request.MemberAsString ?? string.Empty;
        if (interfaceName == AtSpiNames.PropertiesInterface)
        {
            HandleProperties(call, member, ref reader);
        }
        else if (AtSpiInterfaces.TryGetMethod(interfaceName, member, out var method) && Offers(node, session.RootId, interfaceName))
        {
            method(call, ref reader);
        }
        else
        {
            context.ReplyUnknownMethodError();
        }
    }

    /// <summary>Checks that an element offers an interface.</summary>
    /// <param name="node">The element.</param>
    /// <param name="rootId">The number of the application element.</param>
    /// <param name="interfaceName">The interface.</param>
    /// <returns>Whether it does.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Offers(AccessibleNode node, int rootId, string interfaceName) =>
        AtSpiAccessibleMethods.InterfacesOf(node, rootId).Contains(interfaceName);

    /// <summary>Answers a request on the standard properties interface.</summary>
    /// <param name="call">The request.</param>
    /// <param name="member">The method name.</param>
    /// <param name="reader">The arguments.</param>
    private static void HandleProperties(in AtSpiCall call, string member, ref Reader reader)
    {
        var interfaceName = reader.ReadString();
        if (member == GetMember)
        {
            ReplyProperty(call, interfaceName, reader.ReadString());
        }
        else if (member == GetAllMember)
        {
            ReplyAllProperties(call, interfaceName);
        }
        else
        {
            call.ReplyUnknownMethod();
        }
    }

    /// <summary>Replies with one property.</summary>
    /// <param name="call">The request.</param>
    /// <param name="interfaceName">The interface the property belongs to.</param>
    /// <param name="name">The property's name.</param>
    private static void ReplyProperty(in AtSpiCall call, string interfaceName, string name)
    {
        if (!AtSpiInterfaces.TryGetProperty(interfaceName, name, out var property))
        {
            call.Context.ReplyError(InvalidArgsError, "There is no such property.");
            return;
        }

        var writer = call.Context.CreateReplyWriter("v");
        try
        {
            property(call, ref writer);
            call.Context.Reply(writer.CreateMessage());
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Replies with every property of an interface.</summary>
    /// <param name="call">The request.</param>
    /// <param name="interfaceName">The interface.</param>
    private static void ReplyAllProperties(in AtSpiCall call, string interfaceName)
    {
        var writer = call.Context.CreateReplyWriter("a{sv}");
        try
        {
            var start = writer.WriteDictionaryStart();
            foreach (var (name, property) in AtSpiInterfaces.GetProperties(interfaceName))
            {
                writer.WriteDictionaryEntryStart();
                writer.WriteString(name);
                property(call, ref writer);
            }

            writer.WriteDictionaryEnd(start);
            call.Context.Reply(writer.CreateMessage());
        }
        finally
        {
            writer.Dispose();
        }
    }
}
