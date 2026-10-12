// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Xml;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Embeds the description of a custom XMP property in a packet's PDF/A extension schema container, which PDF/A parts 1
/// to 3 require for every property outside the XMP Specification (ISO 19005-1 §6.7.8 as replaced by Cor.1, with the
/// required fields of Cor.2). A packet that already describes the namespace is left as it is. The packet bytes outside
/// the inserted description are kept.
/// </summary>
internal static class XmpExtensionSchemas
{
    /// <summary>The PDF/A extension schema container namespace.</summary>
    private const string ExtensionNamespace = "http://www.aiim.org/pdfa/ns/extension/";

    /// <summary>The required prefix of the container property.</summary>
    private const string ExtensionPrefix = "pdfaExtension";

    /// <summary>The prefix of a namespace declaration attribute.</summary>
    private const string XmlnsPrefix = "xmlns";

    /// <summary>The PDF/A schema value type namespace.</summary>
    private const string SchemaNamespace = "http://www.aiim.org/pdfa/ns/schema#";

    /// <summary>The required prefix of the schema value type's fields.</summary>
    private const string SchemaPrefix = "pdfaSchema";

    /// <summary>The PDF/A property value type namespace.</summary>
    private const string PropertyNamespace = "http://www.aiim.org/pdfa/ns/property#";

    /// <summary>The required prefix of the property value type's fields.</summary>
    private const string PropertyPrefix = "pdfaProperty";

    /// <summary>The RDF namespace.</summary>
    private const string RdfNamespace = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

    /// <summary>The elements a schema description leaves open: its item, the property field, the sequence and the property item.</summary>
    private const int SchemaDepth = 4;

    /// <summary>The elements a new container leaves open: the description, the schemas property and its bag.</summary>
    private const int ContainerDepth = 3;

    /// <summary>Gets the RDF namespace's bytes.</summary>
    private static ReadOnlySpan<byte> RdfNamespaceBytes => "http://www.w3.org/1999/02/22-rdf-syntax-ns#"u8;

    /// <summary>Adds a property's extension schema description to a UTF-8 packet.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="extension">The property to describe.</param>
    /// <returns>The packet with the description, the packet itself when it already describes the namespace, or <see langword="null"/> when it cannot be added safely.</returns>
    internal static byte[]? Describe(byte[] packet, XmpExtensionProperty extension)
    {
        if (XmpTranscoder.Detect(packet, out _) != XmpEncoding.Utf8)
        {
            return null;
        }

        if (IsDescribed(packet, extension.Namespace))
        {
            return packet;
        }

        var rdf = Encoding.UTF8.GetString(XmpEditor.FindPrefix(packet, RdfNamespaceBytes) ?? "rdf"u8.ToArray());
        var result = AddToContainer(packet, rdf, extension) ?? AddContainer(packet, rdf, extension);
        return result is not null && XmpEditor.IsWellFormed(result) ? result : null;
    }

    /// <summary>Reads the subject of the packet's first description, which every description in a packet shares.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The <c>rdf:about</c> value, or empty when there is none.</returns>
    private static string ReadSubject(byte[] packet)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = packet.Length };
        using var stream = new MemoryStream(packet, false);
        using var reader = XmlReader.Create(stream, settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Description" && reader.NamespaceURI == RdfNamespace)
            {
                return reader.GetAttribute("about", RdfNamespace) ?? string.Empty;
            }
        }

        return string.Empty;
    }

    /// <summary>Determines whether a packet already describes a namespace, in element or attribute form.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="ns">The namespace URI.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsDescribed(byte[] packet, string ns) =>
        packet.AsSpan().IndexOf(Encoding.UTF8.GetBytes($"{SchemaPrefix}:namespaceURI>{ns}<")) >= 0
        || packet.AsSpan().IndexOf(Encoding.UTF8.GetBytes($"{SchemaPrefix}:namespaceURI=\"{ns}\"")) >= 0;

    /// <summary>Adds the description to the bag of an existing <c>pdfaExtension:schemas</c> property.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="rdf">The packet's RDF prefix.</param>
    /// <param name="extension">The property.</param>
    /// <returns>The new packet, or <see langword="null"/> when the packet has no container with an open bag.</returns>
    private static byte[]? AddToContainer(byte[] packet, string rdf, XmpExtensionProperty extension)
    {
        var container = XmpEditor.FindName(packet, "<pdfaExtension:schemas"u8.ToArray(), 0);
        var bag = container < 0 ? -1 : XmpEditor.FindName(packet, Encoding.UTF8.GetBytes($"<{rdf}:Bag"), container);
        var close = bag < 0 ? -1 : packet.AsSpan(bag).IndexOf((byte)'>');
        return close < 0 || packet[bag + close - 1] == (byte)'/'
            ? null
            : XmpEditor.Splice(packet, bag + close + 1, bag + close + 1, Write(rdf, null, extension));
    }

    /// <summary>Adds a new description holding a <c>pdfaExtension:schemas</c> container before the end of the RDF body.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="rdf">The packet's RDF prefix.</param>
    /// <param name="extension">The property.</param>
    /// <returns>The new packet, or <see langword="null"/> when the packet has no RDF body.</returns>
    private static byte[]? AddContainer(byte[] packet, string rdf, XmpExtensionProperty extension)
    {
        var end = packet.AsSpan().IndexOf(Encoding.UTF8.GetBytes($"</{rdf}:RDF>"));
        return end < 0 ? null : XmpEditor.Splice(packet, end, end, Write(rdf, ReadSubject(packet), extension));
    }

    /// <summary>Writes the schema description, alone or inside a new container description.</summary>
    /// <param name="rdf">The RDF prefix.</param>
    /// <param name="subject">The <c>rdf:about</c> of a new container description, or <see langword="null"/> to write the description alone.</param>
    /// <param name="extension">The property.</param>
    /// <returns>The UTF-8 bytes.</returns>
    private static byte[] Write(string rdf, string? subject, XmpExtensionProperty extension)
    {
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment };
        using var memory = new MemoryStream();
        using (var writer = XmlWriter.Create(memory, settings))
        {
            if (subject is not null)
            {
                writer.WriteStartElement(rdf, "Description", RdfNamespace);
                writer.WriteAttributeString(rdf, "about", RdfNamespace, subject);
                writer.WriteAttributeString(XmlnsPrefix, ExtensionPrefix, null, ExtensionNamespace);
                writer.WriteStartElement(ExtensionPrefix, "schemas", ExtensionNamespace);
                writer.WriteStartElement(rdf, "Bag", RdfNamespace);
            }

            // The fragment declares each prefix once on its outer element, so the fields read as plain elements.
            writer.WriteStartElement(rdf, "li", RdfNamespace);
            writer.WriteAttributeString(XmlnsPrefix, SchemaPrefix, null, SchemaNamespace);
            writer.WriteAttributeString(XmlnsPrefix, PropertyPrefix, null, PropertyNamespace);
            writer.WriteAttributeString(rdf, "parseType", RdfNamespace, "Resource");
            writer.WriteElementString(SchemaPrefix, "schema", SchemaNamespace, extension.SchemaName);
            writer.WriteElementString(SchemaPrefix, "namespaceURI", SchemaNamespace, extension.Namespace);
            writer.WriteElementString(SchemaPrefix, "prefix", SchemaNamespace, extension.Prefix);
            writer.WriteStartElement(SchemaPrefix, "property", SchemaNamespace);
            writer.WriteStartElement(rdf, "Seq", RdfNamespace);
            writer.WriteStartElement(rdf, "li", RdfNamespace);
            writer.WriteAttributeString(rdf, "parseType", RdfNamespace, "Resource");
            writer.WriteElementString(PropertyPrefix, "name", PropertyNamespace, extension.Name);
            writer.WriteElementString(PropertyPrefix, "valueType", PropertyNamespace, extension.ValueType);
            writer.WriteElementString(PropertyPrefix, "category", PropertyNamespace, "external");
            writer.WriteElementString(PropertyPrefix, "description", PropertyNamespace, extension.Description);

            // Close the property item, its sequence, the schema property and the schema item, then any container.
            for (var open = SchemaDepth + (subject is null ? 0 : ContainerDepth); open > 0; open--)
            {
                writer.WriteEndElement();
            }
        }

        return memory.ToArray();
    }
}
