// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>
/// Describes each AT-SPI interface the server offers, with the signature of every method and property. Screen readers do not ask for
/// this, but D-Bus tools such as <c>busctl</c> and <c>d-feet</c> do, and they cannot call a method they cannot find in it.
/// </summary>
internal static class AtSpiIntrospection
{
    /// <summary>The signature of a reference to an element.</summary>
    private const string Reference = "(so)";

    /// <summary>The signature of a text stretch result.</summary>
    private const string Stretch = "sii";

    /// <summary>The signature of a box result.</summary>
    private const string Box = "(iiii)";

    /// <summary>The signature of an attribute run result.</summary>
    private const string AttributeRun = "a{ss}ii";

    /// <summary>The members of each interface, by interface name.</summary>
    private static readonly Dictionary<string, AtSpiMember[]> Members = new(StringComparer.Ordinal)
    {
        [AtSpiNames.AccessibleInterface] =
        [
            Property("Name", "s"),
            Property("Description", "s"),
            Property("Parent", Reference),
            Property("ChildCount", "i"),
            Property("Locale", "s"),
            Property("AccessibleId", "s"),
            Method("GetChildAtIndex", "i", Reference),
            Method("GetChildren", string.Empty, "a(so)"),
            Method("GetIndexInParent", string.Empty, "i"),
            Method("GetRelationSet", string.Empty, "a(ua(so))"),
            Method("GetRole", string.Empty, "u"),
            Method("GetRoleName", string.Empty, "s"),
            Method("GetLocalizedRoleName", string.Empty, "s"),
            Method("GetState", string.Empty, "au"),
            Method("GetAttributes", string.Empty, "a{ss}"),
            Method("GetApplication", string.Empty, Reference),
            Method("GetInterfaces", string.Empty, "as"),
        ],
        [AtSpiNames.ComponentInterface] =
        [
            Method("Contains", "iiu", "b"),
            Method("GetAccessibleAtPoint", "iiu", Reference),
            Method("GetExtents", "u", Box),
            Method("GetPosition", "u", "ii"),
            Method("GetSize", string.Empty, "ii"),
            Method("GetLayer", string.Empty, "u"),
            Method("GetMDIZOrder", string.Empty, "n"),
            Method("GrabFocus", string.Empty, "b"),
            Method("GetAlpha", string.Empty, "d"),
            Method("SetExtents", "iiiiu", "b"),
            Method("SetPosition", "iiu", "b"),
            Method("SetSize", "ii", "b"),
            Method("ScrollTo", "u", "b"),
            Method("ScrollToPoint", "uii", "b"),
        ],
        [AtSpiNames.ActionInterface] =
        [
            Property("NActions", "i"),
            Method("GetActions", string.Empty, "a(sss)"),
            Method("DoAction", "i", "b"),
            Method("GetName", "i", "s"),
            Method("GetDescription", "i", "s"),
            Method("GetLocalizedName", "i", "s"),
            Method("GetKeyBinding", "i", "s"),
        ],
        [AtSpiNames.TextInterface] =
        [
            Property("CharacterCount", "i"),
            Property("CaretOffset", "i"),
            Method("GetText", "ii", "s"),
            Method("SetCaretOffset", "i", "b"),
            Method("GetTextAtOffset", "iu", Stretch),
            Method("GetTextBeforeOffset", "iu", Stretch),
            Method("GetTextAfterOffset", "iu", Stretch),
            Method("GetStringAtOffset", "iu", Stretch),
            Method("GetCharacterAtOffset", "i", "i"),
            Method("GetCharacterExtents", "iu", Box),
            Method("GetRangeExtents", "iiu", Box),
            Method("GetOffsetAtPoint", "iiu", "i"),
            Method("GetNSelections", string.Empty, "i"),
            Method("GetSelection", "i", "ii"),
            Method("AddSelection", "ii", "b"),
            Method("RemoveSelection", "i", "b"),
            Method("SetSelection", "iii", "b"),
            Method("GetAttributes", "i", AttributeRun),
            Method("GetAttributeRun", "ib", AttributeRun),
            Method("GetAttributeValue", "is", "s"),
            Method("GetDefaultAttributes", string.Empty, "a{ss}"),
            Method("GetDefaultAttributeSet", string.Empty, "a{ss}"),
        ],
        [AtSpiNames.DocumentInterface] =
        [
            Method("GetLocale", string.Empty, "s"),
            Method("GetAttributeValue", "s", "s"),
            Method("GetAttributes", string.Empty, "a{ss}"),
        ],
        [AtSpiNames.PropertiesInterface] =
        [
            Method("Get", "ss", "v"),
            Method("GetAll", "s", "a{sv}"),
        ],
        [AtSpiNames.ApplicationInterface] =
        [
            Property("ToolkitName", "s"),
            Property(AtSpiNames.VersionProperty, "s"),
            Property("AtspiVersion", "s"),
            Property("Id", "i"),
            Method("GetLocale", "u", "s"),
            Method("GetApplicationBusAddress", string.Empty, "s"),
        ],
    };

    /// <summary>Gets the members of an interface.</summary>
    /// <param name="interfaceName">The interface.</param>
    /// <returns>The members, or an empty array for an interface the server does not describe.</returns>
    internal static AtSpiMember[] MembersOf(string interfaceName) => Members.TryGetValue(interfaceName, out var members) ? members : [];

    /// <summary>Writes the introspection XML of one interface as UTF-8.</summary>
    /// <param name="interfaceName">The interface.</param>
    /// <returns>The XML.</returns>
    internal static ReadOnlyMemory<byte> Describe(string interfaceName)
    {
        var xml = new StringBuilder();
        _ = xml.Append("<interface name=\"").Append(interfaceName).Append("\">");
        foreach (var member in MembersOf(interfaceName))
        {
            AppendMember(xml, member);
        }

        _ = xml.Append("</interface>");
        return Encoding.UTF8.GetBytes(xml.ToString());
    }

    /// <summary>Describes a method.</summary>
    /// <param name="name">The method's name.</param>
    /// <param name="input">The signature of the arguments.</param>
    /// <param name="output">The signature of the result.</param>
    /// <returns>The member.</returns>
    private static AtSpiMember Method(string name, string input, string output) => new(name, input, output, false);

    /// <summary>Describes a read only property.</summary>
    /// <param name="name">The property's name.</param>
    /// <param name="type">The signature of the value.</param>
    /// <returns>The member.</returns>
    private static AtSpiMember Property(string name, string type) => new(name, string.Empty, type, true);

    /// <summary>Writes one member into the XML.</summary>
    /// <param name="xml">The XML being built.</param>
    /// <param name="member">The member.</param>
    private static void AppendMember(StringBuilder xml, in AtSpiMember member)
    {
        if (member.IsProperty)
        {
            _ = xml.Append("<property name=\"").Append(member.Name).Append("\" type=\"").Append(member.Output).Append("\" access=\"read\"/>");
            return;
        }

        _ = xml.Append("<method name=\"").Append(member.Name).Append("\">");
        AppendArguments(xml, member.Input, "in");
        AppendArguments(xml, member.Output, "out");
        _ = xml.Append("</method>");
    }

    /// <summary>Writes one argument for each complete type in a signature.</summary>
    /// <param name="xml">The XML being built.</param>
    /// <param name="signature">The signature, which may hold several complete types.</param>
    /// <param name="direction">Whether the arguments go "in" or "out".</param>
    private static void AppendArguments(StringBuilder xml, string signature, string direction)
    {
        var index = 0;
        while (index < signature.Length)
        {
            var end = EndOfType(signature, index);
            _ = xml.Append("<arg direction=\"").Append(direction).Append("\" type=\"").Append(signature, index, end - index).Append("\"/>");
            index = end;
        }
    }

    /// <summary>Finds the end of the complete type that starts at an index.</summary>
    /// <param name="signature">The signature.</param>
    /// <param name="start">The index the type starts at.</param>
    /// <returns>The index just after the type.</returns>
    private static int EndOfType(string signature, int start)
    {
        var index = start;
        while (signature[index] == 'a')
        {
            index++;
        }

        if (signature[index] is not ('(' or '{'))
        {
            return index + 1;
        }

        var depth = 0;
        do
        {
            depth += NestingChange(signature[index]);
            index++;
        }
        while (depth > 0);
        return index;
    }

    /// <summary>Tells how a signature character changes the nesting of containers.</summary>
    /// <param name="c">The character.</param>
    /// <returns>1 for an opening bracket, -1 for a closing one and 0 otherwise.</returns>
    private static int NestingChange(char c)
    {
        if (c is '(' or '{')
        {
            return 1;
        }

        return c is ')' or '}' ? -1 : 0;
    }
}
