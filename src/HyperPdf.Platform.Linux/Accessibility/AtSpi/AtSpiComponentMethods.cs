// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>Answers the methods of <c>org.a11y.atspi.Component</c>: where an element is and how to move focus to it.</summary>
internal static class AtSpiComponentMethods
{
    /// <summary>The layer of ordinary controls, which AT-SPI calls the widget layer.</summary>
    private const uint WidgetLayer = 3;

    /// <summary>Gets the methods by name.</summary>
    internal static Dictionary<string, AtSpiMethod> Methods { get; } = new(StringComparer.Ordinal)
    {
        ["Contains"] = static (in AtSpiCall call, ref Reader reader) => ReplyContains(call, reader.ReadInt32(), reader.ReadInt32(), reader.ReadUInt32()),
        ["GetAccessibleAtPoint"] = static (in AtSpiCall call, ref Reader reader) => ReplyAtPoint(call, reader.ReadInt32(), reader.ReadInt32(), reader.ReadUInt32()),
        ["GetExtents"] = static (in AtSpiCall call, ref Reader reader) => ReplyExtents(call, reader.ReadUInt32()),
        ["GetPosition"] = static (in AtSpiCall call, ref Reader reader) => ReplyPosition(call, reader.ReadUInt32()),
        ["GetSize"] = static (in AtSpiCall call, ref Reader reader) => ReplySize(call),
        ["GetLayer"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyUInt32(WidgetLayer),
        ["GetMDIZOrder"] = static (in AtSpiCall call, ref Reader reader) => ReplyOrder(call),
        ["GrabFocus"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyBool(call.Session.Tree.Focus(call.Node.Id)),
        ["GetAlpha"] = static (in AtSpiCall call, ref Reader reader) => ReplyAlpha(call),
        ["SetExtents"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyBool(false),
        ["SetPosition"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyBool(false),
        ["SetSize"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyBool(false),
        ["ScrollTo"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyBool(false),
        ["ScrollToPoint"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyBool(false),
    };

    /// <summary>Replies whether a point is inside the element.</summary>
    /// <param name="call">The request.</param>
    /// <param name="x">Pixels from the left of the coordinate space.</param>
    /// <param name="y">Pixels from the top of the coordinate space.</param>
    /// <param name="coordinateType">The coordinate type.</param>
    private static void ReplyContains(in AtSpiCall call, int x, int y, uint coordinateType)
    {
        var bounds = AtSpiGeometry.Extents(call.Session.Tree, call.Node, coordinateType);
        call.ReplyBool(x >= bounds.X && y >= bounds.Y && x < bounds.X + bounds.Width && y < bounds.Y + bounds.Height);
    }

    /// <summary>Replies with the deepest element at a point.</summary>
    /// <param name="call">The request.</param>
    /// <param name="x">Pixels from the left of the coordinate space.</param>
    /// <param name="y">Pixels from the top of the coordinate space.</param>
    /// <param name="coordinateType">The coordinate type.</param>
    private static void ReplyAtPoint(in AtSpiCall call, int x, int y, uint coordinateType)
    {
        var origin = AtSpiGeometry.OriginOf(call.Session.Tree, call.Node, coordinateType);
        call.ReplyReference(call.Session.Tree.HitTest(origin.X + x, origin.Y + y));
    }

    /// <summary>Replies with the element's position and size.</summary>
    /// <param name="call">The request.</param>
    /// <param name="coordinateType">The coordinate type.</param>
    private static void ReplyExtents(in AtSpiCall call, uint coordinateType)
    {
        var bounds = AtSpiGeometry.Extents(call.Session.Tree, call.Node, coordinateType);
        var writer = call.Context.CreateReplyWriter("(iiii)");
        try
        {
            AtSpiWire.WriteRectangle(ref writer, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            call.Context.Reply(writer.CreateMessage());
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Replies with the element's position.</summary>
    /// <param name="call">The request.</param>
    /// <param name="coordinateType">The coordinate type.</param>
    private static void ReplyPosition(in AtSpiCall call, uint coordinateType)
    {
        var bounds = AtSpiGeometry.Extents(call.Session.Tree, call.Node, coordinateType);
        using var writer = call.Context.CreateReplyWriter("ii");
        writer.WriteInt32(bounds.X);
        writer.WriteInt32(bounds.Y);
        call.Context.Reply(writer.CreateMessage());
    }

    /// <summary>Replies with the element's size.</summary>
    /// <param name="call">The request.</param>
    private static void ReplySize(in AtSpiCall call)
    {
        using var writer = call.Context.CreateReplyWriter("ii");
        writer.WriteInt32(call.Node.Bounds.Width);
        writer.WriteInt32(call.Node.Bounds.Height);
        call.Context.Reply(writer.CreateMessage());
    }

    /// <summary>Replies with the stacking order, which the app does not use.</summary>
    /// <param name="call">The request.</param>
    private static void ReplyOrder(in AtSpiCall call)
    {
        using var writer = call.Context.CreateReplyWriter("n");
        writer.WriteInt16(0);
        call.Context.Reply(writer.CreateMessage());
    }

    /// <summary>Replies with full opacity.</summary>
    /// <param name="call">The request.</param>
    private static void ReplyAlpha(in AtSpiCall call)
    {
        using var writer = call.Context.CreateReplyWriter("d");
        writer.WriteDouble(1D);
        call.Context.Reply(writer.CreateMessage());
    }
}
