// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Adds PDF Declarations (PDF Association, 2019-09) to a document's XMP metadata. A declaration says a file meets a
/// third-party standard or profile; it is a claim, not proof, so adding one does not check the file. The edit is one undo
/// step, refuses values the specification does not allow, and leaves a PDF/A file alone unless its XMP already carries the
/// extension schema PDF/A needs for the declarations namespace (clause 9).
/// </summary>
public static class PdfDocumentDeclarations
{
    /// <summary>The last PDF/A part that needs an extension schema for a custom XMP property; part 4 does not.</summary>
    private const int MaxPartNeedingSchema = 3;

    /// <summary>Gets the packet written when a file has no XMP metadata.</summary>
    private static ReadOnlySpan<byte> EmptyPacket =>
        "<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n"u8
        + "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"></rdf:RDF></x:xmpmeta>\n"u8
        + "<?xpacket end=\"w\"?>"u8;

    /// <summary>Adds a declaration at document scope.</summary>
    /// <param name="document">The document.</param>
    /// <param name="declaration">The declaration; its profile must be an absolute address.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A field is missing or not allowed by the specification.</exception>
    /// <exception cref="InvalidOperationException">The XMP cannot take the declaration safely, or the file claims PDF/A without the extension schema.</exception>
    public static void Add(PdfDocument document, PdfDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(document);
        PdfDeclarationWriter.Validate(declaration);
        var state = new AddState(document, declaration);
        PdfDocumentEditing.RunEdit(
            document,
            "Add PDF declaration",
            PdfChangeKinds.Metadata,
            PdfCacheScopes.None,
            state,
            static (transaction, run) => Write(run.Document, transaction, run.Declaration));
    }

    /// <summary>Removes the declaration that names a profile, for example when a claim is no longer true. Other declarations stay.</summary>
    /// <param name="document">The document.</param>
    /// <param name="conformsTo">The address of the profile the declaration names.</param>
    /// <returns><see langword="true"/> when a declaration was removed; <see langword="false"/> when the file does not declare the profile.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The XMP cannot be edited safely: it is not UTF-8, or the declaration is laid out in a way this editor will not cut.</exception>
    public static bool Remove(PdfDocument document, string conformsTo)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(conformsTo);
        var raw = document.Catalog.GetRaw(KnownName.Metadata);
        if (!raw.IsReference || StoreReading.Resolve(document.Objects, raw).AsStream() is not { } stream)
        {
            return false;
        }

        var original = stream.DecodeToArray();
        if (!PdfDeclarationWriter.Holds(original, conformsTo))
        {
            return false;
        }

        if (XmpTranscoder.Detect(original, out _) != XmpEncoding.Utf8)
        {
            throw new InvalidOperationException("The document's XMP metadata is not UTF-8, so the declaration was not removed.");
        }

        var packet = PdfDeclarationWriter.Remove(original, conformsTo)
            ?? throw new InvalidOperationException("The declaration is laid out in a way that cannot be cut out safely, so it was not removed.");
        PdfDocumentEditing.RunEdit(
            document,
            "Remove PDF declaration",
            PdfChangeKinds.Metadata,
            PdfCacheScopes.None,
            new RemoveState(raw.AsReference(), stream, packet),
            Replace);
        return true;
    }

    /// <summary>Replaces the XMP stream with an uncompressed copy that holds the new packet.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="run">The stream and its new bytes.</param>
    private static void Replace(PdfEditTransaction transaction, RemoveState run)
    {
        var dictionary = run.Stream.Dictionary.Clone();
        _ = dictionary.Remove(KnownName.Filter);
        _ = dictionary.Remove(KnownName.DecodeParms);
        _ = dictionary.Remove(KnownName.Length);
        _ = dictionary.Remove(KnownName.DL);
        transaction.Replace(run.Id, PdfValue.FromStream(new(dictionary, run.Packet)));
    }

    /// <summary>Writes the declaration into the catalog's XMP stream, or into a new one.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="declaration">The declaration.</param>
    /// <exception cref="InvalidOperationException">The XMP cannot take the declaration safely, or the file claims PDF/A without the extension schema.</exception>
    private static void Write(PdfDocument document, PdfEditTransaction transaction, PdfDeclaration declaration)
    {
        var raw = document.Catalog.GetRaw(KnownName.Metadata);
        if (!raw.IsReference || StoreReading.Resolve(document.Objects, raw).AsStream() is not { } stream)
        {
            AddNewPacket(document, transaction, declaration);
            return;
        }

        var original = stream.DecodeToArray();
        if (XmpTranscoder.Detect(original, out _) != XmpEncoding.Utf8)
        {
            throw new InvalidOperationException("The document's XMP metadata is not UTF-8, so a declaration was not added.");
        }

        if (PdfDeclarationWriter.Holds(original, declaration.ConformsTo!))
        {
            return;
        }

        var packet = BuildPacket(document, original, declaration);

        // The packet is written uncompressed, as XMP is meant to be readable by tools that do not parse PDF.
        var dictionary = stream.Dictionary.Clone();
        _ = dictionary.Remove(KnownName.Filter);
        _ = dictionary.Remove(KnownName.DecodeParms);
        _ = dictionary.Remove(KnownName.Length);
        _ = dictionary.Remove(KnownName.DL);
        transaction.Replace(raw.AsReference(), PdfValue.FromStream(new(dictionary, packet)));
    }

    /// <summary>Adds the declaration to a packet, and the extension schema a PDF/A file needs to describe it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="original">The current packet.</param>
    /// <param name="declaration">The declaration.</param>
    /// <returns>The new packet.</returns>
    /// <exception cref="InvalidOperationException">The packet cannot take the declaration, or the extension schema a PDF/A file needs.</exception>
    private static byte[] BuildPacket(PdfDocument document, byte[] original, PdfDeclaration declaration)
    {
        var packet = PdfDeclarationWriter.Add(original, declaration)
            ?? throw new InvalidOperationException("The document's XMP metadata could not take the declaration safely, so it was not added.");
        var part = PdfDocumentMetadata.GetXmp(document)?.PdfAPart ?? 0;
        var needsSchema = part is >= 1 and <= MaxPartNeedingSchema && !PdfDeclarationWriter.HasExtensionSchema(original);

        // PDF/A parts 1 to 3 must describe the declarations namespace in an extension schema, or the file stops meeting its claim.
        return needsSchema
            ? XmpExtensionSchemas.Describe(packet, PdfDeclarationWriter.ExtensionSchema)
                ?? throw new InvalidOperationException("This file claims PDF/A and its metadata could not take the extension schema for declarations, so nothing was added.")
            : packet;
    }

    /// <summary>Makes an XMP stream that holds the declaration and links it from the catalog.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="declaration">The declaration.</param>
    /// <exception cref="InvalidOperationException">The new packet could not take the declaration.</exception>
    private static void AddNewPacket(PdfDocument document, PdfEditTransaction transaction, PdfDeclaration declaration)
    {
        var store = document.Objects;
        var packet = PdfDeclarationWriter.Add(EmptyPacket.ToArray(), declaration) ?? throw new InvalidOperationException("A declaration could not be written.");
        var dictionary = new PdfDictionary(store);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.Metadata));
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(store.Names.Intern("XML"u8)));
        var id = transaction.Add(PdfValue.FromStream(new(dictionary, packet)));
        var rootRef = store.Trailer.GetRaw(KnownName.Root);
        var catalog = transaction.CloneDictionary(rootRef.AsReference()) ?? throw new InvalidOperationException("The document's catalog cannot be edited.");
        catalog.Set(KnownName.Metadata, PdfValue.FromReference(id));
        transaction.Replace(rootRef.AsReference(), PdfValue.FromDictionary(catalog));
    }

    /// <summary>The arguments of a removal.</summary>
    /// <param name="Id">The XMP stream's object.</param>
    /// <param name="Stream">The current stream.</param>
    /// <param name="Packet">The packet without the declaration.</param>
    [DebuggerDisplay("RemoveState: {Id}")]
    private readonly record struct RemoveState(PdfObjectId Id, PdfStream Stream, byte[] Packet);

    /// <summary>The arguments of an add.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Declaration">The declaration.</param>
    [DebuggerDisplay("AddState: {Declaration}")]
    private readonly record struct AddState(PdfDocument Document, PdfDeclaration Declaration);
}
