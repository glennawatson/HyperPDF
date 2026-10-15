// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Keeps the document information dictionary and the XMP packet equivalent in a document that claims PDF/A parts 1 to 3:
/// each Info entry present needs the same value in its XMP property (ISO 19005-1 §6.7.3, crosswalk in Cor.1 Table 1,
/// single-entry dc:creator and empty strings in Cor.2).
/// </summary>
internal static class PdfAMetadataCrosswalk
{
    /// <summary>Adds a change for each Info entry the edit leaves alone whose XMP equivalent is missing or different.</summary>
    /// <param name="changes">The edit's XMP changes, added to.</param>
    /// <param name="info">The document information dictionary, or <see langword="null"/>.</param>
    /// <param name="current">The catalog's XMP.</param>
    /// <param name="edit">The edit.</param>
    internal static void AddInfoSync(List<XmpChange> changes, PdfDictionary? info, XmpMetadata current, PdfMetadataEdit edit)
    {
        if (info is null)
        {
            return;
        }

        AddText(changes, info.GetText(KnownName.Title), edit.Title, XmpProperty.Title, current.Title);
        AddText(changes, info.GetText(KnownName.Author), edit.Author, XmpProperty.Author, current.Creators is [var creator] ? creator : null);
        AddText(changes, info.GetText(KnownName.Subject), edit.Subject, XmpProperty.Subject, current.Description);
        AddText(changes, info.GetText(KnownName.Keywords), edit.Keywords, XmpProperty.Keywords, current.Keywords);
        AddText(changes, info.GetText(KnownName.Creator), edit.Creator, XmpProperty.Creator, current.CreatorTool);
        AddText(changes, info.GetText(KnownName.Producer), edit.Producer, XmpProperty.Producer, current.Producer);
        AddDate(changes, PdfDate.Parse(info.GetStringBytes(KnownName.CreationDate)), edit.Created, XmpProperty.Created, current.CreateDate);
        AddDate(changes, PdfDate.Parse(info.GetStringBytes(KnownName.ModDate)), edit.Modified, XmpProperty.Modified, current.ModifyDate);
    }

    /// <summary>Determines whether a packet holds every change: the value for a set property and nothing for a removed one.</summary>
    /// <param name="xmp">The packet after the edit.</param>
    /// <param name="changes">The changes.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool Holds(XmpMetadata xmp, List<XmpChange> changes)
    {
        foreach (var change in changes)
        {
            if (!Holds(xmp, change))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether a packet holds one change.</summary>
    /// <param name="xmp">The packet after the edit.</param>
    /// <param name="change">The change.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool Holds(XmpMetadata xmp, XmpChange change)
    {
        var property = change.Property;
        var values = xmp.GetValues(property.Namespace, property.LocalName);
        if (change.Value.Length == 0)
        {
            return values.IsEmpty;
        }

        return property.Shape == XmpShape.Sequence
            ? values is [var single] && IsSameText(single, change.Value)
            : IsSameText(xmp.GetValue(property.Namespace, property.LocalName), change.Value);
    }

    /// <summary>
    /// Compares an XMP value with a document information value. The XMP reader trims the text it reads, so the ends are
    /// compared without their white space; the rest is compared code point by code point (Cor.2 §6.7.3).
    /// </summary>
    /// <param name="xmpValue">The XMP value, or <see langword="null"/>.</param>
    /// <param name="infoValue">The other value.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool IsSameText(string? xmpValue, string infoValue) =>
        xmpValue is not null && xmpValue.AsSpan().Trim().SequenceEqual(infoValue.AsSpan().Trim());

    /// <summary>Adds a text change when the edit leaves the entry alone and the XMP value differs.</summary>
    /// <param name="changes">The changes.</param>
    /// <param name="infoValue">The Info entry, or <see langword="null"/>.</param>
    /// <param name="edited">The edit's value, or <see langword="null"/> when the edit leaves the entry.</param>
    /// <param name="property">The XMP property.</param>
    /// <param name="xmpValue">The XMP value, or <see langword="null"/>.</param>
    private static void AddText(List<XmpChange> changes, string? infoValue, string? edited, XmpProperty property, string? xmpValue)
    {
        // An empty Info string would need an empty XMP value, which the editor treats as a removal; a valid source
        // already pairs it, so it is left as it is.
        if (edited is null && infoValue is { Length: > 0 } && !IsSameText(xmpValue, infoValue))
        {
            changes.Add(new(property, infoValue));
        }
    }

    /// <summary>Adds a date change when the edit leaves the entry alone and the XMP date differs.</summary>
    /// <param name="changes">The changes.</param>
    /// <param name="infoValue">The Info date, or <see langword="null"/>.</param>
    /// <param name="edited">The edit's date, or <see langword="null"/> when the edit leaves the entry.</param>
    /// <param name="property">The XMP property.</param>
    /// <param name="xmpValue">The XMP date, or <see langword="null"/>.</param>
    private static void AddDate(List<XmpChange> changes, DateTimeOffset? infoValue, DateTimeOffset? edited, XmpProperty property, DateTimeOffset? xmpValue)
    {
        if (edited is null && infoValue is { } date && date != xmpValue)
        {
            changes.Add(new(property, PdfDocumentMetadataEditing.FormatXmpDate(date)));
        }
    }
}
