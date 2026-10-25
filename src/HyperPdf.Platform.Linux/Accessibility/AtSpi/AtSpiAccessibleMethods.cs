// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>Answers the methods and properties of <c>org.a11y.atspi.Accessible</c>, which every element has.</summary>
internal static class AtSpiAccessibleMethods
{
    /// <summary>The empty answer to a relation set request.</summary>
    private const string RelationSetSignature = "a(ua(so))";

    /// <summary>Gets the methods by name.</summary>
    internal static Dictionary<string, AtSpiMethod> Methods { get; } = new(StringComparer.Ordinal)
    {
        ["GetRole"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyUInt32(AtSpiRoles.GetNumber(call.Node.Role)),
        ["GetRoleName"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyString(AtSpiRoles.GetName(call.Node.Role)),
        ["GetLocalizedRoleName"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyString(AtSpiRoles.GetName(call.Node.Role)),
        ["GetState"] = static (in AtSpiCall call, ref Reader reader) => ReplyState(call),
        ["GetAttributes"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyStringMap(AtSpiAttributes.For(call.Node)),
        ["GetApplication"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyReference(call.Session.RootId),
        ["GetChildAtIndex"] = static (in AtSpiCall call, ref Reader reader) => ReplyChildAt(call, reader.ReadInt32()),
        ["GetChildren"] = static (in AtSpiCall call, ref Reader reader) => ReplyChildren(call),
        ["GetIndexInParent"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyInt32(IndexInParent(call)),
        ["GetRelationSet"] = static (in AtSpiCall call, ref Reader reader) => ReplyNoRelations(call),
        ["GetInterfaces"] = static (in AtSpiCall call, ref Reader reader) => ReplyInterfaces(call),
    };

    /// <summary>Gets the properties by name.</summary>
    internal static Dictionary<string, AtSpiProperty> Properties { get; } = new(StringComparer.Ordinal)
    {
        ["Name"] = static (in AtSpiCall call, ref MessageWriter writer) => writer.WriteVariantString(call.Node.Name),
        ["Description"] = static (in AtSpiCall call, ref MessageWriter writer) => writer.WriteVariantString(call.Node.Description),
        ["Parent"] = static (in AtSpiCall call, ref MessageWriter writer) => AtSpiWire.WriteReferenceVariant(ref writer, call.Session, call.Node.ParentId),
        ["ChildCount"] = static (in AtSpiCall call, ref MessageWriter writer) => writer.WriteVariantInt32(call.Node.Children.Count),
        ["Locale"] = static (in AtSpiCall call, ref MessageWriter writer) => writer.WriteVariantString(string.Empty),
        ["AccessibleId"] = static (in AtSpiCall call, ref MessageWriter writer) => writer.WriteVariantString(call.Node.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
    };

    /// <summary>Lists the interfaces an element offers.</summary>
    /// <param name="node">The element.</param>
    /// <param name="rootId">The number of the application element.</param>
    /// <returns>The interface names.</returns>
    internal static List<string> InterfacesOf(AccessibleNode node, int rootId)
    {
        var interfaces = new List<string> { AtSpiNames.AccessibleInterface, AtSpiNames.ComponentInterface };
        if (node.Id == rootId)
        {
            interfaces.Add(AtSpiNames.ApplicationInterface);
        }

        if (node.Actions.Count > 0)
        {
            interfaces.Add(AtSpiNames.ActionInterface);
        }

        if (node.Text is not null)
        {
            interfaces.Add(AtSpiNames.TextInterface);
        }

        if (node.Role == AccessibleRole.Document)
        {
            interfaces.Add(AtSpiNames.DocumentInterface);
        }

        return interfaces;
    }

    /// <summary>Replies with the element's state set.</summary>
    /// <param name="call">The request.</param>
    private static void ReplyState(in AtSpiCall call)
    {
        using var writer = call.Context.CreateReplyWriter("au");
        writer.WriteArray(AtSpiStateMap.Split(AtSpiStateMap.ToMask(call.Node.States, call.Node.Role)));
        call.Context.Reply(writer.CreateMessage());
    }

    /// <summary>Replies with one child.</summary>
    /// <param name="call">The request.</param>
    /// <param name="index">The child's index.</param>
    private static void ReplyChildAt(in AtSpiCall call, int index)
    {
        if (index < 0 || index >= call.Node.Children.Count)
        {
            call.Context.ReplyError("org.freedesktop.DBus.Error.InvalidArgs", "There is no child at that index.");
            return;
        }

        call.ReplyReference(call.Node.Children[index]);
    }

    /// <summary>Replies with all the children.</summary>
    /// <param name="call">The request.</param>
    private static void ReplyChildren(in AtSpiCall call)
    {
        var writer = call.Context.CreateReplyWriter("a(so)");
        try
        {
            var start = writer.WriteArrayStart(DBusType.Struct);
            foreach (var child in call.Node.Children)
            {
                AtSpiWire.WriteReference(ref writer, call.Session, child);
            }

            writer.WriteArrayEnd(start);
            call.Context.Reply(writer.CreateMessage());
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Finds the element's index among its parent's children.</summary>
    /// <param name="call">The request.</param>
    /// <returns>The index, or -1 for the application.</returns>
    private static int IndexInParent(in AtSpiCall call)
    {
        var parent = call.Session.Tree.Get(call.Node.ParentId);
        if (parent is null)
        {
            return -1;
        }

        for (var index = 0; index < parent.Children.Count; index++)
        {
            if (parent.Children[index] == call.Node.Id)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Replies with no relations, because the app does not describe any.</summary>
    /// <param name="call">The request.</param>
    private static void ReplyNoRelations(in AtSpiCall call)
    {
        using var writer = call.Context.CreateReplyWriter(RelationSetSignature);
        var start = writer.WriteArrayStart(DBusType.Struct);
        writer.WriteArrayEnd(start);
        call.Context.Reply(writer.CreateMessage());
    }

    /// <summary>Replies with the interface names.</summary>
    /// <param name="call">The request.</param>
    private static void ReplyInterfaces(in AtSpiCall call)
    {
        using var writer = call.Context.CreateReplyWriter("as");
        writer.WriteArray(InterfacesOf(call.Node, call.Session.RootId));
        call.Context.Reply(writer.CreateMessage());
    }
}
