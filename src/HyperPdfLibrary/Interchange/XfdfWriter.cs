// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Xml;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Interchange;

/// <summary>Writes XFDF (ISO 19444-1) files with a forward-only <see cref="XmlWriter"/>.</summary>
public static class XfdfWriter
{
    /// <summary>The writer settings, shared because the writer only reads them; XML readers normalize line endings, including indentation inside rich-text fragments.</summary>
    private static readonly XmlWriterSettings WriterSettings = new()
    {
        Encoding = new UTF8Encoding(false),
        Indent = true,
        NewLineHandling = NewLineHandling.Entitize,
        NewLineChars = "\n",
        CloseOutput = false,
    };

    /// <summary>Writes the data as an XFDF file.</summary>
    /// <param name="data">The data.</param>
    /// <returns>The file's UTF-8 bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
    public static byte[] Write(PdfInterchangeData data)
    {
        using PooledWriteStream stream = new();
        Write(data, stream);
        return stream.ToArray();
    }

    /// <summary>Writes the data as an XFDF file; the stream is left open.</summary>
    /// <param name="data">The data.</param>
    /// <param name="destination">The stream receiving UTF-8 bytes.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void Write(PdfInterchangeData data, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(destination);

        using var writer = XmlWriter.Create(destination, WriterSettings);
        writer.WriteStartDocument();
        writer.WriteStartElement(XfdfNames.Root, XfdfNames.Namespace);
        writer.WriteAttributeString("xml", "space", null, "preserve");
        WriteFile(writer, data);
        WriteFields(writer, data);
        WriteAnnotations(writer, data);
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    /// <summary>Writes an attribute when it has a value.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="name">The attribute's name.</param>
    /// <param name="value">The value, or <see langword="null"/> to write nothing.</param>
    internal static void WriteAttribute(XmlWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteAttributeString(name, InterchangeValues.MakeXmlSafe(value));
        }
    }

    /// <summary>Writes an element holding XHTML. Text that is not well formed XML is left out.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="element">The element's name.</param>
    /// <param name="fragment">The XHTML, or <see langword="null"/>.</param>
    internal static void WriteRichText(XmlWriter writer, string element, string? fragment)
    {
        if (string.IsNullOrEmpty(fragment) || !IsWellFormed(fragment))
        {
            return;
        }

        writer.WriteStartElement(element);
        using var reader = XmlReader.Create(new StringReader(fragment), FragmentSettings());
        _ = reader.Read();
        while (!reader.EOF)
        {
            // An XML declaration belongs to a document of its own, so the writer would refuse it here.
            if (reader.NodeType == XmlNodeType.XmlDeclaration)
            {
                _ = reader.Read();
            }
            else
            {
                writer.WriteNode(reader, true);
            }
        }

        writer.WriteEndElement();
    }

    /// <summary>Writes the <c>f</c> and <c>ids</c> elements.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="data">The data.</param>
    private static void WriteFile(XmlWriter writer, PdfInterchangeData data)
    {
        if (data.FileHref is not null)
        {
            writer.WriteStartElement(XfdfNames.File);
            writer.WriteAttributeString("href", InterchangeValues.MakeXmlSafe(data.FileHref));
            writer.WriteEndElement();
        }

        if (data.OriginalId is null && data.ModifiedId is null)
        {
            return;
        }

        writer.WriteStartElement(XfdfNames.Ids);
        WriteAttribute(writer, "original", data.OriginalId);
        WriteAttribute(writer, "modified", data.ModifiedId);
        writer.WriteEndElement();
    }

    /// <summary>Writes the <c>fields</c> element.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="data">The data.</param>
    private static void WriteFields(XmlWriter writer, PdfInterchangeData data)
    {
        if (data.Fields.Count == 0)
        {
            return;
        }

        writer.WriteStartElement(XfdfNames.Fields);
        foreach (var child in InterchangeFieldTree.Build(data.Fields).Children)
        {
            WriteField(writer, child);
        }

        writer.WriteEndElement();
    }

    /// <summary>Writes one field and its children.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="node">The field.</param>
    private static void WriteField(XmlWriter writer, InterchangeFieldTree node)
    {
        writer.WriteStartElement(XfdfNames.Field);
        writer.WriteAttributeString("name", InterchangeValues.MakeXmlSafe(node.PartialName));
        if (node.Field is { } field)
        {
            foreach (var value in field.Values)
            {
                writer.WriteElementString(XfdfNames.Value, InterchangeValues.MakeXmlSafe(value));
            }

            WriteRichText(writer, XfdfNames.ValueRichText, field.RichText);
        }

        foreach (var child in node.Children)
        {
            WriteField(writer, child);
        }

        writer.WriteEndElement();
    }

    /// <summary>Writes the <c>annots</c> element.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="data">The data.</param>
    private static void WriteAnnotations(XmlWriter writer, PdfInterchangeData data)
    {
        if (data.Annotations.Count == 0)
        {
            return;
        }

        writer.WriteStartElement(XfdfNames.Annots);
        foreach (var annotation in data.Annotations)
        {
            XfdfAnnotationWriter.Write(writer, annotation);
        }

        writer.WriteEndElement();
    }

    /// <summary>Checks that a fragment is well formed, so writing it cannot fail halfway.</summary>
    /// <param name="fragment">The XHTML.</param>
    /// <returns><see langword="true"/> when well formed.</returns>
    private static bool IsWellFormed(string fragment)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(fragment), FragmentSettings());

            // Skip does nothing before the first read.
            _ = reader.Read();
            while (!reader.EOF)
            {
                reader.Skip();
            }

            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    /// <summary>Creates the settings for reading an XHTML fragment: no DTD, no resolver, no declaration problems.</summary>
    /// <returns>The settings.</returns>
    private static XmlReaderSettings FragmentSettings() => new()
    {
        ConformanceLevel = ConformanceLevel.Fragment,
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        MaxCharactersInDocument = PdfLimits.MaxDecodedLength,
    };
}
