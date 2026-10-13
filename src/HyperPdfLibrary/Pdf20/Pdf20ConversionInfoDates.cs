// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>
/// Makes the two dates PDF 2.0 keeps in the document information match the XMP dates (ISO 32000-2 14.3.3): a date the XMP
/// lacks is copied there, and a date that differs is replaced by the XMP one, as the XMP value wins.
/// </summary>
internal static class Pdf20ConversionInfoDates
{
    /// <summary>The bytes reserved for a formatted PDF date.</summary>
    private const int DateBufferLength = 32;

    /// <summary>Reconciles both dates.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="xmp">The catalog's XMP.</param>
    internal static void Reconcile(Pdf20InfoPlan plan, XmpMetadata xmp)
    {
        Reconcile(plan, KnownName.CreationDate, XmpProperty.Created, xmp.CreateDate, "creation date");
        Reconcile(plan, KnownName.ModDate, XmpProperty.Modified, xmp.ModifyDate, "modification date");
    }

    /// <summary>Reconciles one date.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="key">The /Info key.</param>
    /// <param name="property">The XMP property.</param>
    /// <param name="xmpDate">The XMP date, or <see langword="null"/>.</param>
    /// <param name="label">The date's name in plain words.</param>
    private static void Reconcile(Pdf20InfoPlan plan, KnownName key, XmpProperty property, DateTimeOffset? xmpDate, string label)
    {
        var info = plan.Info;
        if (!info.ContainsKey(key))
        {
            return;
        }

        if (PdfDate.Parse(info.GetStringBytes(key)) is not { } infoDate)
        {
            _ = info.Remove(key);
            plan.Left.Add($"The document information {label} was not a readable date, so it was removed.");
            return;
        }

        if (xmpDate is not { } xmpValue)
        {
            plan.Changes.Add(new(property, PdfDocumentMetadataEditing.FormatXmpDate(infoDate)));
            plan.DatesAdded.Add(label);
            return;
        }

        if (xmpValue == infoDate)
        {
            return;
        }

        Replace(info, key, xmpValue);
        plan.Left.Add(
            $"The XMP {label} ({PdfDocumentMetadataEditing.FormatXmpDate(xmpValue)}) differs from the document information one "
            + $"({PdfDocumentMetadataEditing.FormatXmpDate(infoDate)}), so the XMP date was kept and the document information now matches it.");
    }

    /// <summary>Writes a date into the information dictionary.</summary>
    /// <param name="info">The information dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="date">The date.</param>
    private static void Replace(PdfDictionary info, KnownName key, DateTimeOffset date)
    {
        Span<byte> buffer = stackalloc byte[DateBufferLength];
        var length = PdfDate.Format(date, buffer);
        info.Set(key, PdfValue.FromString(buffer[..length].ToArray()));
    }
}
