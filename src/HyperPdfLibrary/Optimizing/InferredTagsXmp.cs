// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Xml;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Marks a document's XMP to say its structure tree was inferred by the optimiser rather than authored, with the
/// <c>hyperpdf:InferredTags</c> property. An existing packet is edited in place; a document without one gets a new,
/// minimal packet written with a streaming XML writer.
/// </summary>
internal static class InferredTagsXmp
{
    /// <summary>The namespace of the optimiser's XMP properties.</summary>
    internal const string Namespace = "https://github.com/glennawatson/HyperPDF/ns/hyperpdf/1.0/";

    /// <summary>The prefix of the optimiser's XMP properties.</summary>
    internal const string Prefix = "hyperpdf";

    /// <summary>The property's local name.</summary>
    internal const string Property = "InferredTags";

    /// <summary>The property's value: an XMP Boolean, which is spelled True or False.</summary>
    internal const string Marked = "True";

    /// <summary>The RDF namespace.</summary>
    private const string Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

    /// <summary>The XMP envelope namespace.</summary>
    private const string Meta = "adobe:ns:meta/";

    /// <summary>The entries of a metadata stream dictionary.</summary>
    private const int MetadataEntries = 2;

    /// <summary>Gets the PDF/A extension schema description of the property.</summary>
    private static XmpExtensionProperty Extension => new(
        "HyperPdf optimiser",
        Namespace,
        Prefix,
        Property,
        "Boolean",
        "Whether the structure tree was inferred from the page content rather than authored.");

    /// <summary>Marks the document.</summary>
    /// <param name="document">The working copy.</param>
    /// <param name="report">Receives a warning when the packet cannot be edited.</param>
    internal static void Mark(PdfDocument document, OptimizeReportBuilder report)
    {
        var store = document.Objects;
        var raw = document.Catalog.GetRaw(KnownName.Metadata);
        if (StoreReading.Resolve(store, raw).AsStream() is { } stream)
        {
            var change = new XmpChange(new(Prefix, Namespace, Property, XmpShape.Simple), Marked);
            if (!raw.IsReference || XmpEditor.Apply(stream.DecodeToArray(), [change]) is not { } edited || Describe(store, edited) is not { } packet)
            {
                report.Warn("The XMP packet could not be edited, so it does not say the tags were inferred.");
                return;
            }

            var dictionary = stream.Dictionary.Clone();
            _ = dictionary.Remove(KnownName.Filter);
            _ = dictionary.Remove(KnownName.DecodeParms);
            _ = dictionary.Remove(KnownName.Length);
            StoreEditing.Replace(store, raw.AsReference(), PdfValue.FromStream(new(dictionary, packet)));
            return;
        }

        var metadata = new PdfDictionary(store, MetadataEntries);
        metadata.Set(KnownName.Type, PdfValue.FromName(KnownName.Metadata));
        metadata.Set(KnownName.Subtype, PdfValue.FromName(store.Names.Intern("XML"u8)));
        var reference = StoreEditing.Add(store, PdfValue.FromStream(new(metadata, NewPacket())));
        var catalogRef = store.Trailer.GetRaw(KnownName.Root);
        var catalog = StoreReading.Resolve(store, catalogRef).AsDictionary()!.Clone();
        catalog.Set(KnownName.Metadata, PdfValue.FromReference(reference));
        StoreEditing.Replace(store, catalogRef.AsReference(), PdfValue.FromDictionary(catalog));
    }

    /// <summary>
    /// Adds the property's extension schema description when the document claims PDF/A parts 1 to 3, which require one
    /// for every custom property (ISO 19005-1 §6.7.8 as replaced by Cor.1).
    /// </summary>
    /// <param name="store">The working store.</param>
    /// <param name="packet">The edited packet.</param>
    /// <returns>The packet to write, or <see langword="null"/> when the claim needs a description that cannot be added.</returns>
    private static byte[]? Describe(PdfObjectStore store, byte[] packet) =>
        PdfAClaimReading.ReadPart(store) is >= PdfAClaim.Part1 and <= PdfAClaim.Part3 ? XmpExtensionSchemas.Describe(packet, Extension) : packet;

    /// <summary>Writes a minimal XMP packet holding only the inferred-tags property.</summary>
    /// <returns>The packet as UTF-8 bytes.</returns>
    private static byte[] NewPacket()
    {
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), OmitXmlDeclaration = true, };
        using var memory = new MemoryStream();
        using (var writer = XmlWriter.Create(memory, settings))
        {
            writer.WriteProcessingInstruction("xpacket", "begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"");
            writer.WriteStartElement("x", "xmpmeta", Meta);
            writer.WriteStartElement("rdf", "RDF", Rdf);
            writer.WriteStartElement("rdf", "Description", Rdf);
            writer.WriteAttributeString("rdf", "about", Rdf, string.Empty);
            writer.WriteElementString(Prefix, Property, Namespace, Marked);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteProcessingInstruction("xpacket", "end=\"w\"");
        }

        return memory.ToArray();
    }
}
