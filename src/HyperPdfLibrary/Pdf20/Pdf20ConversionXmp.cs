// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>
/// Reads and writes the catalog's XMP packet for the PDF 2.0 copy (ISO 32000-2 14.3.2). A missing packet is created
/// empty; an existing one is edited in place by the XMP editor, so its other properties and its encoding stay as they were.
/// </summary>
internal static class Pdf20ConversionXmp
{
    /// <summary>The entries of a metadata stream dictionary.</summary>
    private const int MetadataEntries = 2;

    /// <summary>Gets an empty packet: the envelope and one description, ready for properties.</summary>
    private static ReadOnlySpan<byte> EmptyPacket =>
        "<?xpacket begin=\""u8 + "﻿"u8 + "\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n"u8
        + "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">\n"u8
        + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">\n"u8
        + "<rdf:Description rdf:about=\"\"></rdf:Description>\n"u8
        + "</rdf:RDF>\n"u8
        + "</x:xmpmeta>\n"u8
        + "<?xpacket end=\"w\"?>"u8;

    /// <summary>Reads the catalog's XMP, or an empty set of properties when there is none.</summary>
    /// <param name="document">The working copy.</param>
    /// <returns>The XMP.</returns>
    internal static XmpMetadata Read(PdfDocument document) =>
        PdfDocumentMetadata.GetXmp(document) ?? XmpMetadata.Empty;

    /// <summary>Applies changes to the catalog's XMP packet and stores the result.</summary>
    /// <param name="document">The working copy.</param>
    /// <param name="changes">The changes.</param>
    /// <returns>What happened to the packet.</returns>
    internal static XmpWriteResult Write(PdfDocument document, List<XmpChange> changes)
    {
        var store = document.Objects;
        var existing = store.Catalog.GetStream(KnownName.Metadata).DecodeOrEmpty();
        var created = existing.Length == 0;
        var packet = created ? EmptyPacket.ToArray() : existing;
        if (!XmpEditor.TryApply(packet, [.. changes], out var edited))
        {
            return XmpWriteResult.Failed;
        }

        if (edited is null)
        {
            return XmpWriteResult.Unchanged;
        }

        Store(store, edited);
        return created ? XmpWriteResult.Created : XmpWriteResult.Edited;
    }

    /// <summary>Puts the packet in the catalog's metadata stream, unfiltered so XMP readers can find it.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="packet">The packet.</param>
    private static void Store(PdfObjectStore store, byte[] packet)
    {
        var dictionary = new PdfDictionary(store, MetadataEntries);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.Metadata));
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(store.Names.Intern("XML"u8)));
        var stream = PdfValue.FromStream(new(dictionary, packet));
        var raw = store.Catalog.GetRaw(KnownName.Metadata);
        if (raw.IsReference)
        {
            StoreEditing.Replace(store, raw.AsReference(), stream);
            return;
        }

        var catalog = store.Catalog.Clone();
        catalog.Set(KnownName.Metadata, PdfValue.FromReference(StoreEditing.Add(store, stream)));
        PdfAConversionRewriter.ReplaceCatalog(store, catalog);
    }
}
