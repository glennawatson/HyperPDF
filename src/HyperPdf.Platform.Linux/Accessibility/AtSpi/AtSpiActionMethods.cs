// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>Answers the methods and properties of <c>org.a11y.atspi.Action</c>: what an element does when it is activated.</summary>
internal static class AtSpiActionMethods
{
    /// <summary>Gets the methods by name.</summary>
    internal static Dictionary<string, AtSpiMethod> Methods { get; } = new(StringComparer.Ordinal)
    {
        ["GetActions"] = static (in AtSpiCall call, ref Reader reader) => ReplyActions(call),
        ["DoAction"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyBool(call.Session.Tree.DoAction(call.Node.Id, reader.ReadInt32())),
        ["GetName"] = static (in AtSpiCall call, ref Reader reader) => ReplyName(call, reader.ReadInt32()),
        ["GetDescription"] = static (in AtSpiCall call, ref Reader reader) => ReplyName(call, reader.ReadInt32()),
        ["GetLocalizedName"] = static (in AtSpiCall call, ref Reader reader) => ReplyName(call, reader.ReadInt32()),
        ["GetKeyBinding"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyString(string.Empty),
    };

    /// <summary>Gets the properties by name.</summary>
    internal static Dictionary<string, AtSpiProperty> Properties { get; } = new(StringComparer.Ordinal)
    {
        ["NActions"] = static (in AtSpiCall call, ref MessageWriter writer) => writer.WriteVariantInt32(call.Node.Actions.Count),
    };

    /// <summary>Replies with the name of an action.</summary>
    /// <param name="call">The request.</param>
    /// <param name="index">The action's index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReplyName(in AtSpiCall call, int index) =>
        call.ReplyString(index >= 0 && index < call.Node.Actions.Count ? call.Node.Actions[index] : string.Empty);

    /// <summary>Replies with every action as a name, a description and a key binding.</summary>
    /// <param name="call">The request.</param>
    private static void ReplyActions(in AtSpiCall call)
    {
        using var writer = call.Context.CreateReplyWriter("a(sss)");
        var start = writer.WriteArrayStart(DBusType.Struct);
        foreach (var action in call.Node.Actions)
        {
            writer.WriteStructureStart();
            writer.WriteString(action);
            writer.WriteString(action);
            writer.WriteString(string.Empty);
        }

        writer.WriteArrayEnd(start);
        call.Context.Reply(writer.CreateMessage());
    }
}
