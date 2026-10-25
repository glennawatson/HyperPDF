// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Accessibility;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>One request from a screen reader to one element, with the helpers that write the replies.</summary>
internal readonly ref struct AtSpiCall
{
    /// <summary>Initializes a new instance of the <see cref="AtSpiCall"/> struct.</summary>
    /// <param name="session">The server the request came to.</param>
    /// <param name="node">The element the request is for.</param>
    /// <param name="context">The request's reply handle.</param>
    internal AtSpiCall(AtSpiSession session, AccessibleNode node, MethodContext context)
    {
        Session = session;
        Node = node;
        Context = context;
    }

    /// <summary>Gets the server the request came to.</summary>
    internal AtSpiSession Session { get; }

    /// <summary>Gets the element the request is for.</summary>
    internal AccessibleNode Node { get; }

    /// <summary>Gets the request's reply handle.</summary>
    internal MethodContext Context { get; }

    /// <summary>Replies with a string.</summary>
    /// <param name="value">The string.</param>
    internal void ReplyString(string value)
    {
        using var writer = Context.CreateReplyWriter("s");
        writer.WriteString(value);
        Context.Reply(writer.CreateMessage());
    }

    /// <summary>Replies with a 32 bit number.</summary>
    /// <param name="value">The number.</param>
    internal void ReplyInt32(int value)
    {
        using var writer = Context.CreateReplyWriter("i");
        writer.WriteInt32(value);
        Context.Reply(writer.CreateMessage());
    }

    /// <summary>Replies with an unsigned 32 bit number.</summary>
    /// <param name="value">The number.</param>
    internal void ReplyUInt32(uint value)
    {
        using var writer = Context.CreateReplyWriter("u");
        writer.WriteUInt32(value);
        Context.Reply(writer.CreateMessage());
    }

    /// <summary>Replies with a boolean.</summary>
    /// <param name="value">The value.</param>
    internal void ReplyBool(bool value)
    {
        using var writer = Context.CreateReplyWriter("b");
        writer.WriteBool(value);
        Context.Reply(writer.CreateMessage());
    }

    /// <summary>Replies with a reference to an element.</summary>
    /// <param name="id">The element's number, or -1 for no element.</param>
    internal void ReplyReference(int id)
    {
        var writer = Context.CreateReplyWriter("(so)");
        try
        {
            AtSpiWire.WriteReference(ref writer, Session, id);
            Context.Reply(writer.CreateMessage());
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Replies with a string to string map.</summary>
    /// <param name="map">The map.</param>
    internal void ReplyStringMap(IReadOnlyDictionary<string, string> map)
    {
        var writer = Context.CreateReplyWriter("a{ss}");
        try
        {
            AtSpiWire.WriteStringMap(ref writer, map);
            Context.Reply(writer.CreateMessage());
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Replies with an error that says the request is not understood.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReplyUnknownMethod() => Context.ReplyUnknownMethodError();
}
