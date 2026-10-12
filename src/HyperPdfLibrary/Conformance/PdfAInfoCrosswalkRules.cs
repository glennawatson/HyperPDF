// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The PDF/A-1 document information crosswalk (ISO 19005-1, 6.7.3): each entry of the document information
/// dictionary has the same value as its XMP property in the catalog metadata.
/// </summary>
internal static class PdfAInfoCrosswalkRules
{
    /// <summary>Gets the rules these checks record.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.InfoTitleMatchesXmp,
        PdfARule.InfoAuthorMatchesXmp,
        PdfARule.InfoSubjectMatchesXmp,
        PdfARule.InfoKeywordsMatchXmp,
        PdfARule.InfoCreatorMatchesXmp,
        PdfARule.InfoProducerMatchesXmp,
        PdfARule.InfoCreationDateMatchesXmp,
        PdfARule.InfoModificationDateMatchesXmp,
    ];

    /// <summary>Checks the document information dictionary against the catalog packet.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="packet">The catalog packet, or <see langword="null"/> when the catalog has no metadata stream.</param>
    internal static void Check(PdfAValidationContext context, PdfAXmpPacket? packet)
    {
        if (context.Document.Objects.Trailer.Get(KnownName.Info).AsDictionary() is not { } info)
        {
            return;
        }

        var xmp = packet ?? PdfAXmpPacket.Malformed;
        const string where = PdfAContexts.Info;
        context.Require(Matches(info, KnownName.Title, PdfAXmpValues.DefaultText(xmp, "title")), PdfARule.InfoTitleMatchesXmp, where);
        context.Require(Matches(info, KnownName.Subject, PdfAXmpValues.DefaultText(xmp, "description")), PdfARule.InfoSubjectMatchesXmp, where);
        context.Require(Matches(info, KnownName.Keywords, PdfAXmpValues.Simple(xmp, PdfAXmpNames.AdobePdf, "Keywords")), PdfARule.InfoKeywordsMatchXmp, where);
        context.Require(Matches(info, KnownName.Producer, PdfAXmpValues.Simple(xmp, PdfAXmpNames.AdobePdf, "Producer")), PdfARule.InfoProducerMatchesXmp, where);
        context.Require(Matches(info, KnownName.Creator, PdfAXmpValues.Simple(xmp, PdfAXmpNames.XmpBasic, "CreatorTool")), PdfARule.InfoCreatorMatchesXmp, where);
        context.Require(AuthorMatches(info, PdfAXmpValues.Creators(xmp)), PdfARule.InfoAuthorMatchesXmp, where);
        context.Require(DateMatches(info, KnownName.CreationDate, PdfAXmpValues.Simple(xmp, PdfAXmpNames.XmpBasic, "CreateDate")), PdfARule.InfoCreationDateMatchesXmp, where);
        context.Require(DateMatches(info, KnownName.ModDate, PdfAXmpValues.Simple(xmp, PdfAXmpNames.XmpBasic, "ModifyDate")), PdfARule.InfoModificationDateMatchesXmp, where);
    }

    /// <summary>Determines whether an entry is absent or equals its XMP value.</summary>
    /// <param name="info">The document information dictionary.</param>
    /// <param name="key">The entry.</param>
    /// <param name="xmp">The XMP value, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool Matches(PdfDictionary info, KnownName key, string? xmp) =>
        Text(info, key) is not { } text || string.Equals(text, xmp, StringComparison.Ordinal);

    /// <summary>Determines whether <c>/Author</c> is absent, or <c>dc:creator</c> has one entry equal to it.</summary>
    /// <param name="info">The document information dictionary.</param>
    /// <param name="creators">The <c>dc:creator</c> entries, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool AuthorMatches(PdfDictionary info, List<string>? creators) =>
        Text(info, KnownName.Author) is not { } author || (creators is [var only] && string.Equals(author, only, StringComparison.Ordinal));

    /// <summary>Determines whether a date entry is absent or not a string, or names the same instant as its XMP date.</summary>
    /// <param name="info">The document information dictionary.</param>
    /// <param name="key">The entry.</param>
    /// <param name="xmp">The XMP date text, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool DateMatches(PdfDictionary info, KnownName key, string? xmp)
    {
        var value = info.Get(key);
        if (value.Kind != PdfKind.String)
        {
            return true;
        }

        if (xmp is null || !PdfAXmpDates.TryParse(xmp, out var date) || PdfAXmpDates.Instant(date) is not { } xmpInstant)
        {
            return false;
        }

        return PdfAXmpDates.ParsePdfDate(PdfAXmpValues.DecodeText(value.AsStringBytes())) is { } infoInstant && infoInstant == xmpInstant;
    }

    /// <summary>Reads an entry as veraPDF's crosswalk does: a string decoded without one trailing NUL, or other values as text.</summary>
    /// <param name="info">The document information dictionary.</param>
    /// <param name="key">The entry.</param>
    /// <returns>The text, or <see langword="null"/> when the entry is absent or null.</returns>
    private static string? Text(PdfDictionary info, KnownName key)
    {
        var value = info.Get(key);
        return value.Kind switch
        {
            PdfKind.Null => null,
            PdfKind.String => WithoutTrailingNul(PdfAXmpValues.DecodeText(value.AsStringBytes())),
            PdfKind.Integer => value.AsInteger().ToString(CultureInfo.InvariantCulture),
            PdfKind.Real => value.AsNumber().ToString(CultureInfo.InvariantCulture),
            _ => value.Kind.ToString(),
        };
    }

    /// <summary>Removes one trailing NUL character.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without it.</returns>
    private static string WithoutTrailingNul(string text) => text.EndsWith('\0') ? text[..^1] : text;
}
