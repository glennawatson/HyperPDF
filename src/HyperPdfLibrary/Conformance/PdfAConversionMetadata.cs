// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Writes a converted file's metadata: a new XMP packet with the PDF/A identification, and an /Info dictionary that
/// matches it. Parts 1 to 3 keep /Info with each entry equal to its XMP property (ISO 19005-1 §6.7.3 with Cor.1 Table 1;
/// ISO 19005-2 and -3 §6.6.2.3.1); part 4 keeps descriptive metadata in XMP only, with /Info holding at most /ModDate when
/// the catalog has /PieceInfo (ISO 19005-4:2020 §6.1.3). An /Info value is the source of truth, as readers see it first.
/// </summary>
internal static class PdfAConversionMetadata
{
    /// <summary>The bytes reserved for a formatted PDF date.</summary>
    private const int DateBufferLength = 32;

    /// <summary>The entries of a metadata stream dictionary.</summary>
    private const int MetadataEntries = 2;

    /// <summary>The XMP properties the new packet carries, as namespace-qualified keys.</summary>
    private static readonly FrozenSet<string> KeptProperties = FrozenSet.ToFrozenSet(
    [
        XmpMetadata.Key(XmpMetadata.DublinCoreNamespace, "format"),
        XmpMetadata.Key(XmpMetadata.DublinCoreNamespace, "title"),
        XmpMetadata.Key(XmpMetadata.DublinCoreNamespace, "creator"),
        XmpMetadata.Key(XmpMetadata.DublinCoreNamespace, "description"),
        XmpMetadata.Key(XmpMetadata.DublinCoreNamespace, "subject"),
        XmpMetadata.Key(XmpMetadata.PdfNamespace, "Keywords"),
        XmpMetadata.Key(XmpMetadata.PdfNamespace, "Producer"),
        XmpMetadata.Key(XmpMetadata.PdfNamespace, "Trapped"),
        XmpMetadata.Key(XmpMetadata.XmpNamespace, "CreatorTool"),
        XmpMetadata.Key(XmpMetadata.XmpNamespace, "CreateDate"),
        XmpMetadata.Key(XmpMetadata.XmpNamespace, "ModifyDate"),
        XmpMetadata.Key(XmpMetadata.XmpNamespace, "MetadataDate"),
        XmpMetadata.Key(XmpMetadata.PdfAIdNamespace, "part"),
        XmpMetadata.Key(XmpMetadata.PdfAIdNamespace, "conformance"),
        XmpMetadata.Key(XmpMetadata.PdfAIdNamespace, "rev"),
        XmpMetadata.Key(XmpMetadata.PdfAIdNamespace, "amd"),
        XmpMetadata.Key(XmpMetadata.PdfAIdNamespace, "corr"),
        XmpMetadata.Key(XmpMetadata.PdfUaIdNamespace, "part"),
    ], StringComparer.Ordinal);

    /// <summary>Writes the metadata.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Write(PdfAConversionSession session)
    {
        var store = session.Store;
        var xmp = PdfDocumentMetadata.GetXmp(session.Document);
        var infoRef = store.Trailer.GetRaw(KnownName.Info);
        var info = StoreReading.Resolve(store, infoRef).AsDictionary()?.Clone();
        if (info is not null)
        {
            CleanInfo(session, info);
        }

        var properties = Merge(info, xmp);
        if (PdfAConversionXmp.Write(properties, session.Level) is not { } packet)
        {
            session.Refuse("The XMP metadata could not be written.", PdfARule.CatalogXmpIdentification);
            return;
        }

        WritePacket(session, packet);
        session.Changed("Added the PDF/A identification to the XMP metadata.", PdfARule.CatalogXmpIdentification, PdfARule.IdentificationPart2);
        ReportDropped(session, xmp);
        if (session.Part == PdfAClaim.Part4)
        {
            WritePdfA4Info(session, info, infoRef);
        }
        else if (info is not null)
        {
            PutInfo(store, infoRef, info);
        }
    }

    /// <summary>Removes /Info entries XMP cannot match and writes dates in one form, so /Info and XMP agree.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="info">The copy of /Info, changed in place.</param>
    private static void CleanInfo(PdfAConversionSession session, PdfDictionary info)
    {
        ReadOnlySpan<KnownName> texts = [KnownName.Title, KnownName.Author, KnownName.Subject, KnownName.Keywords, KnownName.Creator, KnownName.Producer];
        foreach (var key in texts)
        {
            CleanText(session, info, key);
        }

        CleanDate(session, info, KnownName.CreationDate);
        CleanDate(session, info, KnownName.ModDate);
        if (!info.ContainsKey(KnownName.Trapped) || TrappedText(info) is not null)
        {
            return;
        }

        _ = info.Remove(KnownName.Trapped);
        session.Changed("Removed a document information Trapped entry that was not True, False or Unknown.", PdfARule.InfoTitleMatchesXmp);
    }

    /// <summary>Removes a text entry XMP cannot hold, and re-encodes a UTF-8 string for parts 1 to 3, which predate it.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="info">The /Info copy.</param>
    /// <param name="key">The key.</param>
    private static void CleanText(PdfAConversionSession session, PdfDictionary info, KnownName key)
    {
        if (!info.ContainsKey(key))
        {
            return;
        }

        var text = info.GetText(key);
        if (text is null || text.Length == 0 || !PdfAConversionXmp.CanHold(text))
        {
            _ = info.Remove(key);
            session.Changed("Removed an empty or unreadable document information entry, as XMP cannot match it.", PdfARule.InfoTitleMatchesXmp);
            return;
        }

        if (session.Part < PdfAClaim.Part4 && info.GetStringBytes(key).StartsWith("﻿"u8))
        {
            info.Set(key, PdfValue.FromString(PdfText.Encode(text, false)));
        }
    }

    /// <summary>Writes a date entry in full form, or removes one that cannot be read.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="info">The /Info copy.</param>
    /// <param name="key">The key.</param>
    private static void CleanDate(PdfAConversionSession session, PdfDictionary info, KnownName key)
    {
        if (!info.ContainsKey(key))
        {
            return;
        }

        if (PdfDate.Parse(info.GetStringBytes(key)) is not { } date)
        {
            _ = info.Remove(key);
            session.Changed("Removed a document information date that could not be read, as XMP cannot match it.", PdfARule.InfoCreationDateMatchesXmp);
            return;
        }

        Span<byte> buffer = stackalloc byte[DateBufferLength];
        var length = PdfDate.Format(date, buffer);
        info.Set(key, PdfValue.FromString(buffer[..length].ToArray()));
    }

    /// <summary>Reads /Trapped as XMP spells it.</summary>
    /// <param name="info">The /Info dictionary.</param>
    /// <returns>True, False or Unknown, or <see langword="null"/>.</returns>
    private static string? TrappedText(PdfDictionary info)
    {
        var value = info.Get(KnownName.Trapped);
        if (value.Kind == PdfKind.Boolean)
        {
            return value.AsBoolean() ? "True" : "False";
        }

        return info.Owner?.Names.GetString(value.AsName()) is ("True" or "False" or "Unknown") and var name ? name : null;
    }

    /// <summary>Takes each property from /Info, else from the old XMP.</summary>
    /// <param name="info">The cleaned /Info, or <see langword="null"/>.</param>
    /// <param name="xmp">The old XMP, or <see langword="null"/>.</param>
    /// <returns>The properties.</returns>
    private static PdfAConversionProperties Merge(PdfDictionary? info, XmpMetadata? xmp)
    {
        var source = info ?? new PdfDictionary(null);
        var old = xmp ?? XmpMetadata.Empty;
        var author = source.GetText(KnownName.Author);
        return new(
            Pick(source, KnownName.Title, old.Title),
            author is null ? old.Creators : [author],
            Pick(source, KnownName.Subject, old.Description),
            old.Subjects,
            Pick(source, KnownName.Keywords, old.Keywords),
            Pick(source, KnownName.Creator, old.CreatorTool),
            Pick(source, KnownName.Producer, old.Producer),
            PickDate(source, KnownName.CreationDate, old.CreateDate),
            PickDate(source, KnownName.ModDate, old.ModifyDate),
            source.ContainsKey(KnownName.Trapped) ? TrappedText(source) : null,
            old.PdfUaPart);
    }

    /// <summary>Takes a text entry from /Info, else the XMP value.</summary>
    /// <param name="info">The /Info dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The XMP value.</param>
    /// <returns>The value.</returns>
    private static string? Pick(PdfDictionary info, KnownName key, string? fallback) => info.GetText(key) ?? fallback;

    /// <summary>Takes a date entry from /Info, else the XMP value.</summary>
    /// <param name="info">The /Info dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The XMP value.</param>
    /// <returns>The value.</returns>
    private static DateTimeOffset? PickDate(PdfDictionary info, KnownName key, DateTimeOffset? fallback) =>
        info.ContainsKey(key) ? PdfDate.Parse(info.GetStringBytes(key)) ?? fallback : fallback;

    /// <summary>Puts the packet in the catalog's metadata stream, unfiltered.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="packet">The packet.</param>
    private static void WritePacket(PdfAConversionSession session, byte[] packet)
    {
        var store = session.Store;
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

    /// <summary>Notes the old XMP properties the new packet does not carry.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="xmp">The old XMP, or <see langword="null"/>.</param>
    private static void ReportDropped(PdfAConversionSession session, XmpMetadata? xmp)
    {
        var dropped = 0;
        foreach (var key in xmp?.Properties.Keys ?? [])
        {
            dropped += KeptProperties.Contains(key) ? 0 : 1;
        }

        if (dropped > 0)
        {
            session.Changed(
                string.Create(CultureInfo.InvariantCulture, $"Left out {dropped} other XMP properties, so every property in the file is one PDF/A defines."),
                PdfARule.XmpPropertyDefined,
                PdfARule.XmpPropertyDefinedPdfA1);
        }
    }

    /// <summary>Writes /Info for PDF/A-4: removed, or only /ModDate when the catalog has /PieceInfo.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="info">The cleaned /Info copy, or <see langword="null"/>.</param>
    /// <param name="infoRef">The trailer's /Info value.</param>
    private static void WritePdfA4Info(PdfAConversionSession session, PdfDictionary? info, PdfValue infoRef)
    {
        var store = session.Store;
        if (info is null)
        {
            return;
        }

        var modified = info.GetRaw(KnownName.ModDate);
        if (store.Catalog.ContainsKey(store.Names.Intern("PieceInfo"u8)) && !modified.IsNull)
        {
            var kept = new PdfDictionary(store, 1);
            kept.Set(KnownName.ModDate, modified);
            PutInfo(store, infoRef, kept);
            session.Changed("Moved the document properties to XMP; the document information keeps only the modification date.", PdfARule.InfoOnlyModDate);
            return;
        }

        _ = store.Trailer.Remove(KnownName.Info);
        session.Changed("Moved the document properties to XMP and removed the document information dictionary.", PdfARule.InfoNeedsPieceInfo);
    }

    /// <summary>Replaces /Info, as an indirect object when it was one.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="infoRef">The trailer's /Info value.</param>
    /// <param name="info">The new dictionary.</param>
    private static void PutInfo(PdfObjectStore store, PdfValue infoRef, PdfDictionary info)
    {
        if (infoRef.IsReference)
        {
            StoreEditing.Replace(store, infoRef.AsReference(), PdfValue.FromDictionary(info));
        }
        else
        {
            store.Trailer.Set(KnownName.Info, PdfValue.FromReference(StoreEditing.Add(store, PdfValue.FromDictionary(info))));
        }
    }
}
