// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>
/// Writes the shapes AT-SPI uses on the wire that the D-Bus writer has no single call for. The writer is a <c>ref struct</c> that keeps
/// its position inside itself, so every method takes it by reference; a copy would lose what is written.
/// </summary>
internal static class AtSpiWire
{
    /// <summary>Writes a reference to an element: the bus name and the object path, as a structure.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="session">The server, which supplies the bus name.</param>
    /// <param name="id">The element's number, or a negative number for no element.</param>
    internal static void WriteReference(ref MessageWriter writer, AtSpiSession session, int id)
    {
        writer.WriteStructureStart();
        if (id < 0)
        {
            writer.WriteString(string.Empty);
            writer.WriteObjectPath(AtSpiNames.NullPath);
            return;
        }

        writer.WriteString(session.BusName);
        writer.WriteObjectPath(AtSpiPaths.For(id, session.RootId));
    }

    /// <summary>Writes a reference to an element as the value of a variant.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="session">The server, which supplies the bus name.</param>
    /// <param name="id">The element's number.</param>
    internal static void WriteReferenceVariant(ref MessageWriter writer, AtSpiSession session, int id)
    {
        writer.WriteSignature("(so)");
        WriteReference(ref writer, session, id);
    }

    /// <summary>Writes a map of strings to strings.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="map">The map.</param>
    internal static void WriteStringMap(ref MessageWriter writer, IReadOnlyDictionary<string, string> map)
    {
        var start = writer.WriteDictionaryStart();
        foreach (var (key, value) in map)
        {
            writer.WriteDictionaryEntryStart();
            writer.WriteString(key);
            writer.WriteString(value);
        }

        writer.WriteDictionaryEnd(start);
    }

    /// <summary>Writes four numbers as a structure, such as the position and size of an element.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="first">The first number.</param>
    /// <param name="second">The second number.</param>
    /// <param name="third">The third number.</param>
    /// <param name="fourth">The fourth number.</param>
    internal static void WriteRectangle(ref MessageWriter writer, int first, int second, int third, int fourth)
    {
        writer.WriteStructureStart();
        writer.WriteInt32(first);
        writer.WriteInt32(second);
        writer.WriteInt32(third);
        writer.WriteInt32(fourth);
    }
}
