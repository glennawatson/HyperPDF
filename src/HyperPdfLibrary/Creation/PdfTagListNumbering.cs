// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>Validates and writes ListNumbering attributes (ISO 32000-2 14.8.5.5, Table 382).</summary>
internal static class PdfTagListNumbering
{
    /// <summary>The entries in a list-numbering attribute dictionary.</summary>
    private const int AttributeEntries = 2;

    /// <summary>Rejects undefined, misplaced and version-incompatible list numbering.</summary>
    /// <param name="sink">The document receiving the attributes.</param>
    /// <param name="type">The standard structure type.</param>
    /// <param name="options">The requested attributes.</param>
    /// <exception cref="ArgumentOutOfRangeException">The numbering scheme is undefined.</exception>
    /// <exception cref="ArgumentException">The attribute requires a list or PDF 2.0 output.</exception>
    internal static void Validate(IPdfObjectSink sink, PdfStructureType type, PdfTagOptions options)
    {
        if (options.ListNumbering is not { } numbering)
        {
            return;
        }

        if ((uint)numbering > (uint)PdfListNumbering.Description)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The list numbering scheme is undefined.");
        }

        if (type != PdfStructureType.List)
        {
            throw new ArgumentException("List numbering requires an L structure element.", nameof(options));
        }

        if (numbering >= PdfListNumbering.Unordered && !IsPdf20(sink))
        {
            throw new ArgumentException("This list numbering scheme requires PDF 2.0 output.", nameof(options));
        }
    }

    /// <summary>Creates a List-owned dictionary for an explicitly requested numbering scheme.</summary>
    /// <param name="sink">The document's object and name owner.</param>
    /// <param name="numbering">The validated scheme.</param>
    /// <returns>The attribute dictionary.</returns>
    internal static PdfDictionary Create(IPdfObjectSink sink, PdfListNumbering numbering)
    {
        var attributes = new PdfDictionary(sink.Owner, AttributeEntries);
        attributes.Set(KnownName.O, PdfValue.FromName(sink.Names.Intern("List"u8)));
        attributes.Set(sink.Names.Intern("ListNumbering"u8), PdfValue.FromName(sink.Names.Intern(Name(numbering))));
        return attributes;
    }

    /// <summary>Returns the fixed standard spelling of an ordered scheme.</summary>
    /// <param name="numbering">The validated scheme.</param>
    /// <returns>The attribute name bytes.</returns>
    internal static ReadOnlySpan<byte> Name(PdfListNumbering numbering) => numbering switch
    {
        PdfListNumbering.Ordered => "Ordered"u8,
        PdfListNumbering.Decimal => "Decimal"u8,
        PdfListNumbering.UpperRoman => "UpperRoman"u8,
        PdfListNumbering.LowerRoman => "LowerRoman"u8,
        PdfListNumbering.UpperAlpha => "UpperAlpha"u8,
        PdfListNumbering.LowerAlpha => "LowerAlpha"u8,
        _ => UnorderedName(numbering),
    };

    /// <summary>Checks the output version independently of the element's namespace.</summary>
    /// <param name="sink">The output document.</param>
    /// <returns>Whether its header or catalog permits PDF 2.0 attributes.</returns>
    private static bool IsPdf20(IPdfObjectSink sink)
    {
        if (sink is PdfDocumentBuilder builder)
        {
            return builder.Version == PdfHeaderVersion.Pdf20;
        }

        if (sink.Owner is not { } store)
        {
            return false;
        }

        if (store.Version == "2.0")
        {
            return true;
        }

        var version = store.Catalog.Get(store.Names.Intern("Version"u8));
        return version.Kind == PdfKind.Name && store.Names.NameEquals(version.AsName(), "2.0"u8);
    }

    /// <summary>Returns the fixed standard spelling of an unordered or description scheme.</summary>
    /// <param name="numbering">The validated scheme.</param>
    /// <returns>The attribute name bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The scheme is undefined.</exception>
    private static ReadOnlySpan<byte> UnorderedName(PdfListNumbering numbering) => numbering switch
    {
        PdfListNumbering.None => "None"u8,
        PdfListNumbering.Disc => "Disc"u8,
        PdfListNumbering.Circle => "Circle"u8,
        PdfListNumbering.Square => "Square"u8,
        PdfListNumbering.Unordered => "Unordered"u8,
        PdfListNumbering.Description => "Description"u8,
        _ => throw new ArgumentOutOfRangeException(nameof(numbering)),
    };
}
