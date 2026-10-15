// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>Where a document information entry goes in XMP, and how to read the value XMP already holds.</summary>
/// <param name="Key">The /Info key.</param>
/// <param name="Property">The XMP property that takes the value.</param>
/// <param name="Existing">Reads the value the XMP already holds for the property.</param>
/// <param name="Label">The entry's name in plain words.</param>
[DebuggerDisplay("Pdf20InfoMapping: {Label}")]
internal sealed record Pdf20InfoMapping(KnownName Key, XmpProperty Property, Func<XmpMetadata, string?> Existing, string Label)
{
    /// <summary>The separator between names when several creators become one string.</summary>
    private const string ListSeparator = "; ";

    /// <summary>Gets the standard entries ISO 32000-2 14.3.3 deprecates and the XMP properties that replace them.</summary>
    internal static Pdf20InfoMapping[] All { get; } =
    [
        new(KnownName.Title, XmpProperty.Title, static xmp => xmp.Title, "title"),
        new(KnownName.Author, XmpProperty.Author, static xmp => xmp.Creators.IsEmpty ? null : string.Join(ListSeparator, xmp.Creators.AsSpan()), "author"),
        new(KnownName.Subject, XmpProperty.Subject, static xmp => xmp.Description, "subject"),
        new(KnownName.Keywords, XmpProperty.Keywords, static xmp => xmp.Keywords, "keywords"),
        new(KnownName.Creator, XmpProperty.Creator, static xmp => xmp.CreatorTool, "creating application"),
        new(KnownName.Producer, XmpProperty.Producer, static xmp => xmp.Producer, "producer"),
        new(KnownName.Trapped, new("pdf", XmpMetadata.PdfNamespace, "Trapped", XmpShape.Simple), static xmp => xmp.Trapped, "trapping state"),
    ];

    /// <summary>Determines whether PDF 2.0 still allows an entry in the document information: the two dates, and the standard entries handled separately.</summary>
    /// <param name="key">The entry's key.</param>
    /// <returns><see langword="true"/> when the entry is not a custom one to move.</returns>
    internal static bool IsKept(PdfName key)
    {
        var known = key.ToKnownName();
        if (known is KnownName.CreationDate or KnownName.ModDate)
        {
            return true;
        }

        foreach (var mapping in All)
        {
            if (mapping.Key == known)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a document information dictionary holds nothing but the two dates PDF 2.0 still allows.</summary>
    /// <param name="info">The dictionary.</param>
    /// <returns><see langword="true"/> when every entry is /CreationDate or /ModDate.</returns>
    internal static bool OnlyDates(PdfDictionary info)
    {
        for (var i = 0; i < info.Count; i++)
        {
            if (info.GetKeyAt(i).ToKnownName() is not (KnownName.CreationDate or KnownName.ModDate))
            {
                return false;
            }
        }

        return true;
    }
}
