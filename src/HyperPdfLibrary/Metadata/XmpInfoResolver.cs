// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Metadata;

/// <summary>
/// Combines the document information dictionary with the catalog's XMP packet. ISO 32000-2 §14.3.3 deprecates the
/// information dictionary in favour of XMP, so a PDF 2.0 file takes the XMP value when both exist. An earlier file keeps
/// its information value and uses the XMP value only when the dictionary has none.
/// </summary>
internal static class XmpInfoResolver
{
    /// <summary>The separator between names when several creators or subjects become one string.</summary>
    private const string ListSeparator = "; ";

    /// <summary>Determines whether a format version is PDF 2.0 or later.</summary>
    /// <param name="version">The version text, for example "2.0", or null.</param>
    /// <returns><see langword="true"/> for 2.0 and later.</returns>
    internal static bool IsPdf2OrLater(string? version) =>
        version is { Length: > 0 } && char.IsAsciiDigit(version[0]) && version[0] >= '2';

    /// <summary>Resolves the information a viewer shows.</summary>
    /// <param name="info">The information dictionary values.</param>
    /// <param name="xmp">The catalog's XMP.</param>
    /// <returns>The information with the XMP values applied.</returns>
    internal static PdfDocumentInfo Resolve(PdfDocumentInfo info, XmpMetadata xmp)
    {
        var xmpFirst = IsPdf2OrLater(info.Version);
        return info with
        {
            Title = Pick(info.Title, xmp.Title, xmpFirst),
            Author = Pick(info.Author, Join(xmp.Creators), xmpFirst),
            Subject = Pick(info.Subject, xmp.Description, xmpFirst),
            Keywords = Pick(info.Keywords, xmp.Keywords ?? Join(xmp.Subjects), xmpFirst),
            Creator = Pick(info.Creator, xmp.CreatorTool, xmpFirst),
            Producer = Pick(info.Producer, xmp.Producer, xmpFirst),
            Created = xmpFirst ? xmp.CreateDate ?? info.Created : info.Created ?? xmp.CreateDate,
            Modified = xmpFirst ? xmp.ModifyDate ?? info.Modified : info.Modified ?? xmp.ModifyDate,
            Trapped = Pick(info.Trapped, xmp.Trapped, xmpFirst),
        };
    }

    /// <summary>Chooses between an information value and an XMP value, ignoring blank text.</summary>
    /// <param name="infoValue">The information value, or null.</param>
    /// <param name="xmpValue">The XMP value, or null.</param>
    /// <param name="xmpFirst">Whether the XMP value wins when both exist.</param>
    /// <returns>The chosen value, or null.</returns>
    private static string? Pick(string? infoValue, string? xmpValue, bool xmpFirst)
    {
        var info = string.IsNullOrWhiteSpace(infoValue) ? null : infoValue;
        var xmp = string.IsNullOrWhiteSpace(xmpValue) ? null : xmpValue;
        return xmpFirst ? xmp ?? info : info ?? xmp;
    }

    /// <summary>Joins a list into one string.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The joined text, or null for an empty list.</returns>
    private static string? Join(string[] values) => values.Length == 0 ? null : string.Join(ListSeparator, values.AsSpan());
}
