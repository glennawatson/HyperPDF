// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using System.Xml;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>Writes a created document's information dictionary and XMP packet (ISO 32000-2 14.3).</summary>
internal static class CreatedMetadata
{
    /// <summary>The number of PDF/UA part 2.</summary>
    private const int PartTwo = 2;

    /// <summary>The <c>pdfuaid:rev</c> of ISO 14289-2:2024.</summary>
    private const string PartTwoRevision = "2024";

    /// <summary>The RDF namespace.</summary>
    private const string Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

    /// <summary>The XMP envelope namespace.</summary>
    private const string Meta = "adobe:ns:meta/";

    /// <summary>The XML namespace, for <c>xml:lang</c>.</summary>
    private const string Xml = "http://www.w3.org/XML/1998/namespace";

    /// <summary>The RDF prefix.</summary>
    private const string RdfPrefix = "rdf";

    /// <summary>The language of a language alternative's default entry.</summary>
    private const string DefaultLanguage = "x-default";

    /// <summary>The byte order mark the XMP packet header names its encoding with.</summary>
    private const char ByteOrderMark = (char)0xFEFF;

    /// <summary>The bytes reserved for a formatted PDF date, which is at most <see cref="PdfDate.MaxFormattedLength"/>.</summary>
    private const int DateBufferLength = 32;

    /// <summary>The entries of the information dictionary.</summary>
    private const int InfoEntries = 8;

    /// <summary>The entries of a metadata stream dictionary.</summary>
    private const int MetadataEntries = 3;

    /// <summary>The keys of the information dictionary that the document options set, which custom entries cannot use.</summary>
    private static readonly string[] StandardInfoKeys = ["Title", "Author", "Subject", "Keywords", "Creator", "Producer", "CreationDate", "ModDate", "Trapped"];

    /// <summary>The writer settings, shared because a writer only reads them.</summary>
    private static readonly XmlWriterSettings WriterSettings = new() { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = true, Indent = true };

    /// <summary>Adds the information dictionary.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="options">The options.</param>
    /// <param name="created">The creation date.</param>
    /// <returns>The dictionary's object.</returns>
    internal static PdfObjectId AddInfo(IPdfObjectSink sink, PdfDocumentCreatorOptions options, DateTimeOffset created)
    {
        var info = new PdfDictionary(sink.Owner, InfoEntries);
        SetText(info, KnownName.Title, options.Title);
        SetText(info, KnownName.Author, options.Author);
        SetText(info, KnownName.Subject, options.Subject);
        SetText(info, KnownName.Keywords, options.Keywords);
        SetText(info, KnownName.Creator, options.CreatorTool);
        SetText(info, KnownName.Producer, options.Producer);
        Span<byte> date = stackalloc byte[DateBufferLength];
        var length = PdfDate.Format(created, date);
        info.Set(KnownName.CreationDate, PdfValue.FromString(date[..length].ToArray()));
        AddCustomInfo(sink, info, options.CustomInfo);
        return sink.Add(PdfValue.FromDictionary(info));
    }

    /// <summary>Adds the XMP metadata stream.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="options">The options.</param>
    /// <param name="created">The creation date.</param>
    /// <param name="claimedPart">The PDF/UA part to claim, or 0 for none.</param>
    /// <returns>The stream's object.</returns>
    internal static PdfObjectId AddXmp(IPdfObjectSink sink, PdfDocumentCreatorOptions options, DateTimeOffset created, int claimedPart)
    {
        var dictionary = new PdfDictionary(sink.Owner, MetadataEntries);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.Metadata));
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(sink.Names.Intern("XML")));
        return sink.Add(PdfValue.FromStream(new(dictionary, WritePacket(options, created, claimedPart))));
    }

    /// <summary>Adds the custom entries of the information dictionary.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="info">The information dictionary.</param>
    /// <param name="custom">The custom entries, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">A key is empty, has a character a name cannot hold, or is one of the standard keys.</exception>
    private static void AddCustomInfo(IPdfObjectSink sink, PdfDictionary info, IReadOnlyDictionary<string, string>? custom)
    {
        if (custom is null)
        {
            return;
        }

        foreach (var (key, value) in custom)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            if (StandardInfoKeys.Contains(key, StringComparer.Ordinal) || key.AsSpan().ContainsAnyExceptInRange('!', '~') || key.AsSpan().ContainsAny("()<>[]{}/%#"))
            {
                throw new ArgumentException($"'{key}' cannot be used as a custom document information key.", nameof(custom));
            }

            SetText(info, sink.Names.Intern(key), value);
        }
    }

    /// <summary>Sets a text string entry when there is text.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    private static void SetText(PdfDictionary dictionary, PdfName key, string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            dictionary.Set(key, PdfValue.FromString(PdfText.Encode(text)));
        }
    }

    /// <summary>Writes the XMP packet as UTF-8.</summary>
    /// <param name="options">The options.</param>
    /// <param name="created">The creation date.</param>
    /// <param name="claimedPart">The PDF/UA part to claim, or 0 for none.</param>
    /// <returns>The packet.</returns>
    private static byte[] WritePacket(PdfDocumentCreatorOptions options, DateTimeOffset created, int claimedPart)
    {
        using var memory = new PooledWriteStream();
        using (var writer = XmlWriter.Create(memory, WriterSettings))
        {
            writer.WriteProcessingInstruction("xpacket", $"begin=\"{ByteOrderMark}\" id=\"W5M0MpCehiHzreSzNTczkc9d\"");
            writer.WriteStartElement("x", "xmpmeta", Meta);
            writer.WriteStartElement(RdfPrefix, "RDF", Rdf);
            writer.WriteStartElement(RdfPrefix, "Description", Rdf);
            writer.WriteAttributeString(RdfPrefix, "about", Rdf, string.Empty);
            writer.WriteElementString("dc", "format", XmpMetadata.DublinCoreNamespace, "application/pdf");
            WriteAlternative(writer, "title", options.Title);
            WriteAuthor(writer, options.Author);
            WriteAlternative(writer, "description", options.Subject);
            WriteSimple(writer, "pdf", "Keywords", XmpMetadata.PdfNamespace, options.Keywords);
            WriteSimple(writer, "pdf", "Producer", XmpMetadata.PdfNamespace, options.Producer);
            WriteSimple(writer, "xmp", "CreatorTool", XmpMetadata.XmpNamespace, options.CreatorTool);
            var date = PdfDocumentMetadataEditing.FormatXmpDate(created);
            writer.WriteElementString("xmp", "CreateDate", XmpMetadata.XmpNamespace, date);
            writer.WriteElementString("xmp", "MetadataDate", XmpMetadata.XmpNamespace, date);
            if (claimedPart > 0)
            {
                writer.WriteElementString("pdfuaid", "part", XmpMetadata.PdfUaIdNamespace, claimedPart.ToString(CultureInfo.InvariantCulture));
            }

            if (claimedPart == PartTwo)
            {
                // ISO 14289-2 clause 5 names the revision of the part.
                writer.WriteElementString("pdfuaid", "rev", XmpMetadata.PdfUaIdNamespace, PartTwoRevision);
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteProcessingInstruction("xpacket", "end=\"w\"");
        }

        return memory.ToArray();
    }

    /// <summary>Writes a simple property when it has a value.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="prefix">The prefix.</param>
    /// <param name="name">The local name.</param>
    /// <param name="ns">The namespace.</param>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    private static void WriteSimple(XmlWriter writer, string prefix, string name, string ns, string? value)
    {
        if (!string.IsNullOrEmpty(value))
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
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        writer.WriteStartElement("dc", name, XmpMetadata.DublinCoreNamespace);
        writer.WriteStartElement(RdfPrefix, "Alt", Rdf);
        writer.WriteStartElement(RdfPrefix, "li", Rdf);
        writer.WriteAttributeString("xml", "lang", Xml, DefaultLanguage);
        writer.WriteString(value);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    /// <summary>Writes the Dublin Core creator list.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="author">The author, or <see langword="null"/>.</param>
    private static void WriteAuthor(XmlWriter writer, string? author)
    {
        if (string.IsNullOrEmpty(author))
        {
            return;
        }

        writer.WriteStartElement("dc", "creator", XmpMetadata.DublinCoreNamespace);
        writer.WriteStartElement(RdfPrefix, "Seq", Rdf);
        writer.WriteElementString(RdfPrefix, "li", Rdf, author);
        writer.WriteEndElement();
        writer.WriteEndElement();
    }
}
