// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using System.Xml;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Metadata;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Writes the XMP packet of a converted file: the descriptive properties from the XMP Specification's predefined schemas
/// and the PDF/A identification schema (ISO 19005-1 §6.7.11, ISO 19005-2 and -3 §6.6.4, ISO 19005-4 §6.7.3). Only
/// predefined properties are written, so parts 1 to 3 need no extension schema; a PDF/UA claim is kept with its
/// extension schema description. The packet is UTF-8 with no bytes or encoding attribute in its header.
/// </summary>
internal static class PdfAConversionXmp
{
    /// <summary>The RDF namespace.</summary>
    private const string Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

    /// <summary>The XMP envelope namespace.</summary>
    private const string Meta = "adobe:ns:meta/";

    /// <summary>The XML namespace, for <c>xml:lang</c>.</summary>
    private const string Xml = "http://www.w3.org/XML/1998/namespace";

    /// <summary>The prefix of the PDF/A identification schema.</summary>
    private const string PdfAIdPrefix = "pdfaid";

    /// <summary>The prefix of the PDF/UA identification schema.</summary>
    private const string PdfUaIdPrefix = "pdfuaid";

    /// <summary>The identification schemas' part property.</summary>
    private const string PartProperty = "part";

    /// <summary>The value of <c>pdfaid:rev</c> for PDF/A-4 (ISO 19005-4:2020 §6.7.3).</summary>
    private const string Pdf4Revision = "2020";

    /// <summary>The language of a language alternative's default entry.</summary>
    private const string DefaultLanguage = "x-default";

    /// <summary>The RDF prefix.</summary>
    private const string RdfPrefix = "rdf";

    /// <summary>The list item element.</summary>
    private const string Item = "li";

    /// <summary>Gets the extension schema description of <c>pdfuaid:part</c>, for parts 1 to 3.</summary>
    private static XmpExtensionProperty PdfUaPart => new(
        "PDF/UA identification schema",
        XmpMetadata.PdfUaIdNamespace,
        PdfUaIdPrefix,
        PartProperty,
        "Integer",
        "The part of ISO 14289 the file conforms to.");

    /// <summary>Writes the packet.</summary>
    /// <param name="properties">The descriptive properties.</param>
    /// <param name="level">The level the file claims.</param>
    /// <returns>The packet as UTF-8 bytes, or <see langword="null"/> when the PDF/UA description cannot be added.</returns>
    internal static byte[]? Write(PdfAConversionProperties properties, PdfALevel level)
    {
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = true, Indent = true };
        using var memory = new MemoryStream();
        using (var writer = XmlWriter.Create(memory, settings))
        {
            writer.WriteProcessingInstruction("xpacket", "begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"");
            writer.WriteStartElement("x", "xmpmeta", Meta);
            writer.WriteStartElement(RdfPrefix, "RDF", Rdf);
            writer.WriteStartElement(RdfPrefix, "Description", Rdf);
            writer.WriteAttributeString(RdfPrefix, "about", Rdf, string.Empty);
            WriteDublinCore(writer, properties);
            WriteDocumentInfo(writer, properties);
            WriteIdentification(writer, properties, level);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteProcessingInstruction("xpacket", "end=\"w\"");
        }

        var packet = memory.ToArray();
        return properties.PdfUaPart is not null && PdfALevels.Part(level) < PdfAClaim.Part4 ? XmpExtensionSchemas.Describe(packet, PdfUaPart) : packet;
    }

    /// <summary>Determines whether XML can hold a text value.</summary>
    /// <param name="value">The text.</param>
    /// <returns><see langword="true"/> when every character is allowed in XML 1.0.</returns>
    internal static bool CanHold(string value)
    {
        try
        {
            _ = XmlConvert.VerifyXmlChars(value);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    /// <summary>Writes the Dublin Core properties.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="properties">The properties.</param>
    private static void WriteDublinCore(XmlWriter writer, PdfAConversionProperties properties)
    {
        writer.WriteElementString("dc", "format", XmpMetadata.DublinCoreNamespace, "application/pdf");
        WriteAlternative(writer, "title", properties.Title);
        WriteList(writer, "creator", "Seq", properties.Creators);
        WriteAlternative(writer, "description", properties.Description);
        WriteList(writer, "subject", "Bag", properties.Subjects);
    }

    /// <summary>Writes the Adobe PDF and XMP basic properties.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="properties">The properties.</param>
    private static void WriteDocumentInfo(XmlWriter writer, PdfAConversionProperties properties)
    {
        var pdf = XmpMetadata.PdfNamespace;
        var xmp = XmpMetadata.XmpNamespace;
        WriteSimple(writer, "pdf", "Keywords", pdf, properties.Keywords);
        WriteSimple(writer, "pdf", "Producer", pdf, properties.Producer);
        WriteSimple(writer, "pdf", "Trapped", pdf, properties.Trapped);
        WriteSimple(writer, "xmp", "CreatorTool", xmp, properties.CreatorTool);
        WriteSimple(writer, "xmp", "CreateDate", xmp, Format(properties.Created));
        WriteSimple(writer, "xmp", "ModifyDate", xmp, Format(properties.Modified));
        WriteSimple(writer, "xmp", "MetadataDate", xmp, Format(properties.Modified));
    }

    /// <summary>Writes the PDF/A identification, and the PDF/UA one when the document claims it.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="properties">The properties.</param>
    /// <param name="level">The level.</param>
    private static void WriteIdentification(XmlWriter writer, PdfAConversionProperties properties, PdfALevel level)
    {
        var id = XmpMetadata.PdfAIdNamespace;
        var part = PdfALevels.Part(level);
        writer.WriteElementString(PdfAIdPrefix, PartProperty, id, part.ToString(CultureInfo.InvariantCulture));
        WriteSimple(writer, PdfAIdPrefix, "conformance", id, PdfALevels.Conformance(level));
        if (part == PdfAClaim.Part4)
        {
            writer.WriteElementString(PdfAIdPrefix, "rev", id, Pdf4Revision);
        }

        if (properties.PdfUaPart is { } ua)
        {
            writer.WriteElementString(PdfUaIdPrefix, PartProperty, XmpMetadata.PdfUaIdNamespace, ua.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Formats a date as XMP writes it.</summary>
    /// <param name="value">The date, or <see langword="null"/>.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    private static string? Format(DateTimeOffset? value) => value is { } date ? PdfDocumentMetadataEditing.FormatXmpDate(date) : null;

    /// <summary>Writes a simple property when it has a value.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="prefix">The prefix.</param>
    /// <param name="name">The local name.</param>
    /// <param name="ns">The namespace.</param>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    private static void WriteSimple(XmlWriter writer, string prefix, string name, string ns, string? value)
    {
        if (value is not null)
        {
            writer.WriteElementString(prefix, name, ns, value);
        }
    }

    /// <summary>Writes a Dublin Core language alternative with one default entry.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="name">The local name.</param>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    private static void WriteAlternative(XmlWriter writer, string name, string? value)
    {
        if (value is null)
        {
            return;
        }

        writer.WriteStartElement("dc", name, XmpMetadata.DublinCoreNamespace);
        writer.WriteStartElement(RdfPrefix, "Alt", Rdf);
        writer.WriteStartElement(RdfPrefix, Item, Rdf);
        writer.WriteAttributeString("xml", "lang", Xml, DefaultLanguage);
        writer.WriteString(value);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    /// <summary>Writes a Dublin Core ordered or unordered array.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="name">The local name.</param>
    /// <param name="container">Seq or Bag.</param>
    /// <param name="values">The values; nothing is written when empty.</param>
    private static void WriteList(XmlWriter writer, string name, string container, string[] values)
    {
        if (values.Length == 0)
        {
            return;
        }

        writer.WriteStartElement("dc", name, XmpMetadata.DublinCoreNamespace);
        writer.WriteStartElement(RdfPrefix, container, Rdf);
        foreach (var value in values)
        {
            writer.WriteElementString(RdfPrefix, Item, Rdf, value);
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
    }
}
