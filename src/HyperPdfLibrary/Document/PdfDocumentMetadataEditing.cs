// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Edits document information and XMP metadata.</summary>
public static class PdfDocumentMetadataEditing
{
    /// <summary>The XMP date format: ISO 8601 with the offset.</summary>
    private const string XmpDateFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

    /// <summary>The bytes reserved for a formatted PDF date; at least <see cref="PdfDate.MaxFormattedLength"/>.</summary>
    private const int DateBufferLength = 32;

    /// <summary>
    /// Changes the document's metadata in a transaction (the open one, or its own). The /Info dictionary is replaced, or
    /// created and linked from the trailer; matching XMP properties are updated and the rest of the packet is kept.
    /// A document that claims PDF/A keeps its claim valid: for parts 1 to 3 every /Info entry is matched in the XMP, and
    /// for part 4 descriptive metadata goes to the XMP only, with /Info holding at most /ModDate.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="edit">The changes; <see langword="null"/> properties are left as they are, empty strings remove entries.</param>
    /// <exception cref="ArgumentNullException"><paramref name="edit"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The document claims PDF/A and its XMP packet cannot take the changes; nothing is changed.</exception>
    public static void SetMetadata(PdfDocument document, PdfMetadataEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        PdfDocumentEditing.RunEdit(
        document,
        "Edit document properties",
        PdfChangeKinds.Metadata,
        new MetadataEditState(
        document,
        edit),
        static (
        transaction,
        state) =>
        PdfDocumentMetadataEditing.SetMetadataCore(
        state.Document,
        transaction,
        state.Edit));
    }

    /// <summary>Formats a date as XMP writes it.</summary>
    /// <param name="value">The date.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string FormatXmpDate(DateTimeOffset value) => value.ToString(PdfDocumentMetadataEditing.XmpDateFormat, CultureInfo.InvariantCulture);

    /// <summary>Sets a text entry, or removes it for an empty string.</summary>
    /// <param name="info">The information dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The text, or <see langword="null"/> to leave it.</param>
    /// <param name="version">The document's header version.</param>
    private static void SetInfoText(PdfDictionary info, KnownName key, string? value, string version)
    {
        if (value is null)
        {
            return;
        }

        if (value.Length == 0)
        {
            _ = info.Remove(key);
            return;
        }

        info.Set(key, PdfValue.FromString(PdfText.Encode(value, info.GetStringBytes(key), version)));
    }

    /// <summary>Sets a date entry.</summary>
    /// <param name="info">The information dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The date, or <see langword="null"/> to leave it.</param>
    private static void SetInfoDate(PdfDictionary info, KnownName key, DateTimeOffset? value)
    {
        if (value is not { } date)
        {
            return;
        }

        Span<byte> buffer = stackalloc byte[PdfDocumentMetadataEditing.DateBufferLength];
        var length = PdfDate.Format(date, buffer);
        info.Set(key, PdfValue.FromString(buffer[..length].ToArray()));
    }

    /// <summary>Lists the XMP changes an edit makes.</summary>
    /// <param name="edit">The edit.</param>
    /// <returns>The changes.</returns>
    private static List<XmpChange> GetXmpChanges(PdfMetadataEdit edit)
    {
        var changes = new List<XmpChange>();
        PdfDocumentMetadataEditing.AddXmpChange(changes, XmpProperty.Title, edit.Title);
        PdfDocumentMetadataEditing.AddXmpChange(changes, XmpProperty.Author, edit.Author);
        PdfDocumentMetadataEditing.AddXmpChange(changes, XmpProperty.Subject, edit.Subject);
        PdfDocumentMetadataEditing.AddXmpChange(changes, XmpProperty.Keywords, edit.Keywords);
        PdfDocumentMetadataEditing.AddXmpChange(changes, XmpProperty.Creator, edit.Creator);
        PdfDocumentMetadataEditing.AddXmpChange(changes, XmpProperty.Producer, edit.Producer);
        PdfDocumentMetadataEditing.AddXmpChange(changes, XmpProperty.Created, edit.Created is { } created ? PdfDocumentMetadataEditing.FormatXmpDate(created) : null);
        if (edit.Modified is { } modified)
        {
            PdfDocumentMetadataEditing.AddXmpChange(changes, XmpProperty.Modified, PdfDocumentMetadataEditing.FormatXmpDate(modified));
            PdfDocumentMetadataEditing.AddXmpChange(changes, XmpProperty.MetadataDate, PdfDocumentMetadataEditing.FormatXmpDate(modified));
        }

        return changes;
    }

    /// <summary>Adds a change when the value is set.</summary>
    /// <param name="changes">The changes.</param>
    /// <param name="property">The property.</param>
    /// <param name="value">The value, or <see langword="null"/> to leave it.</param>
    private static void AddXmpChange(List<XmpChange> changes, XmpProperty property, string? value)
    {
        if (value is not null)
        {
            changes.Add(new(property, value));
        }
    }

    /// <summary>
    /// Writes the metadata. The XMP is written first, so an edit that would break the document's PDF/A claim stops before
    /// anything changes.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="edit">The changes.</param>
    private static void SetMetadataCore(PdfDocument document, PdfEditTransaction transaction, PdfMetadataEdit edit)
    {
        // One parse gives both the claim and the values the crosswalk compares.
        var xmp = PdfDocumentMetadata.GetXmp(document);
        var part = xmp?.PdfAPart ?? 0;
        var changes = PdfDocumentMetadataEditing.GetXmpChanges(edit);
        if (xmp is not null && part is >= PdfAClaim.Part1 and <= PdfAClaim.Part3)
        {
            var info = StoreReading.Resolve(document.Objects, document.Objects.Trailer.GetRaw(KnownName.Info)).AsDictionary();
            PdfAMetadataCrosswalk.AddInfoSync(changes, info, xmp, edit);
        }

        PdfDocumentMetadataEditing.WriteXmp(document, transaction, changes, part);
        if (part == PdfAClaim.Part4)
        {
            PdfDocumentMetadataEditing.WritePdfA4Info(document, transaction, edit);
        }
        else
        {
            PdfDocumentMetadataEditing.WriteInfo(document, transaction, edit);
        }
    }

    /// <summary>Replaces the /Info dictionary, or creates it and links it from the trailer.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="edit">The changes.</param>
    private static void WriteInfo(PdfDocument document, PdfEditTransaction transaction, PdfMetadataEdit edit)
    {
        var raw = document.Objects.Trailer.GetRaw(KnownName.Info);
        var info = StoreReading.Resolve(document.Objects, raw).AsDictionary()?.Clone() ?? new PdfDictionary(document.Objects);
        PdfDocumentMetadataEditing.SetInfoText(info, KnownName.Title, edit.Title, document.Objects.Version);
        PdfDocumentMetadataEditing.SetInfoText(info, KnownName.Author, edit.Author, document.Objects.Version);
        PdfDocumentMetadataEditing.SetInfoText(info, KnownName.Subject, edit.Subject, document.Objects.Version);
        PdfDocumentMetadataEditing.SetInfoText(info, KnownName.Keywords, edit.Keywords, document.Objects.Version);
        PdfDocumentMetadataEditing.SetInfoText(info, KnownName.Creator, edit.Creator, document.Objects.Version);
        PdfDocumentMetadataEditing.SetInfoText(info, KnownName.Producer, edit.Producer, document.Objects.Version);
        PdfDocumentMetadataEditing.SetInfoDate(info, KnownName.CreationDate, edit.Created);
        PdfDocumentMetadataEditing.SetInfoDate(info, KnownName.ModDate, edit.Modified);
        PdfDocumentMetadataEditing.SetInfo(transaction, raw, info);
    }

    /// <summary>
    /// Writes /Info for a PDF/A-4 document, which keeps descriptive metadata in XMP only: /Info may exist only when the
    /// catalog has /PieceInfo, and then holds only /ModDate (ISO 19005-4:2020 §6.1.3).
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="edit">The changes.</param>
    private static void WritePdfA4Info(PdfDocument document, PdfEditTransaction transaction, PdfMetadataEdit edit)
    {
        var raw = document.Objects.Trailer.GetRaw(KnownName.Info);
        if (!document.Catalog.ContainsKey(document.Objects.Names.Intern("PieceInfo"u8)))
        {
            if (!raw.IsNull)
            {
                transaction.SetTrailerEntry(KnownName.Info, default);
            }

            return;
        }

        if (edit.Modified is null)
        {
            return;
        }

        var info = new PdfDictionary(document.Objects);
        PdfDocumentMetadataEditing.SetInfoDate(info, KnownName.ModDate, edit.Modified);
        PdfDocumentMetadataEditing.SetInfo(transaction, raw, info);
    }

    /// <summary>Puts an /Info dictionary in place of the old one, or adds it and links it from the trailer.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="raw">The trailer's /Info value.</param>
    /// <param name="info">The new dictionary.</param>
    private static void SetInfo(PdfEditTransaction transaction, PdfValue raw, PdfDictionary info)
    {
        if (raw.IsReference)
        {
            transaction.Replace(raw.AsReference(), PdfValue.FromDictionary(info));
        }
        else
        {
            transaction.SetTrailerEntry(KnownName.Info, PdfValue.FromReference(transaction.Add(PdfValue.FromDictionary(info))));
        }
    }

    /// <summary>
    /// Updates the catalog's XMP stream when it has one and the packet can be edited safely. A document that claims PDF/A
    /// must take every change, so its claim stays true; otherwise the edit is refused.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="changes">The changes.</param>
    /// <param name="part">The PDF/A part the document claims, or 0.</param>
    /// <exception cref="PdfException">The document claims PDF/A and its XMP cannot take the changes.</exception>
    private static void WriteXmp(PdfDocument document, PdfEditTransaction transaction, List<XmpChange> changes, int part)
    {
        var raw = document.Catalog.GetRaw(KnownName.Metadata);
        if (changes.Count == 0 || !raw.IsReference || StoreReading.Resolve(document.Objects, raw).AsStream() is not { } stream)
        {
            return;
        }

        var original = stream.DecodeToArray();
        var edited = XmpEditor.TryApply(original, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(changes), out var packet);
        if (part > 0 && (!edited || !PdfAMetadataCrosswalk.Holds(XmpParser.Parse(packet ?? original), changes)))
        {
            throw new PdfException(
                "The document claims PDF/A-" + part.ToString(CultureInfo.InvariantCulture)
                + ", and its XMP metadata could not be updated to match the new document properties. "
                + "The edit was not made, as saving it would leave a false PDF/A claim.");
        }

        if (packet is null)
        {
            return;
        }

        // The packet is written uncompressed, as XMP is meant to be readable by tools that do not parse PDF.
        var dictionary = stream.Dictionary.Clone();
        _ = dictionary.Remove(KnownName.Filter);
        _ = dictionary.Remove(KnownName.DecodeParms);
        _ = dictionary.Remove(KnownName.Length);
        _ = dictionary.Remove(KnownName.DL);
        transaction.Replace(raw.AsReference(), PdfValue.FromStream(new(dictionary, packet)));
    }

    /// <summary>The arguments of a metadata edit.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Edit">The changes.</param>
    [DebuggerDisplay("MetadataEditState: {Edit}")]
    private readonly record struct MetadataEditState(PdfDocument Document, PdfMetadataEdit Edit);
}
