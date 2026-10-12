// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.CMaps;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Finds the fonts text-showing operators select. A font resource counts only when it is a dictionary whose
/// <c>/Subtype</c> is Type1, MMType1, TrueType, Type3 or Type0; anything else selects no font, as in veraPDF.
/// </summary>
internal static class PdfAFontLookup
{
    /// <summary>Finds the font a <c>Tf</c> name selects: in the stream's own resources, then the page's.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="frame">The stream being read.</param>
    /// <param name="name">The font resource name.</param>
    /// <returns>The font, or null.</returns>
    internal static PdfAFontUse? Find(PdfATextWalkState state, PdfATextFrame frame, PdfName name) =>
        FromResources(state, frame.Resources, name) ?? FromResources(state, state.PageResources, name);

    /// <summary>Finds the font a value refers to.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="value">The value, possibly a reference.</param>
    /// <returns>The font, or null when the value is not a font dictionary.</returns>
    internal static PdfAFontUse? FromValue(PdfATextWalkState state, PdfValue value)
    {
        var resolved = StoreReading.Resolve(state.Store, value);
        if (resolved.Kind != PdfKind.Dictionary || resolved.AsDictionary() is not { } dictionary)
        {
            return null;
        }

        if (state.Fonts.TryGetValue(dictionary, out var known))
        {
            return known;
        }

        var use = Create(state, dictionary);
        state.Fonts[dictionary] = use;
        return use;
    }

    /// <summary>Looks a font name up in one resource dictionary.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="resources">The resources, or null.</param>
    /// <param name="name">The font resource name.</param>
    /// <returns>The font, or null.</returns>
    private static PdfAFontUse? FromResources(PdfATextWalkState state, PdfDictionary? resources, PdfName name) =>
        resources?.GetDictionary(KnownName.Font) is { } fonts ? FromValue(state, fonts.GetRaw(name)) : null;

    /// <summary>Makes the use record of a font dictionary.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>The record, or null when the subtype is not a font kind.</returns>
    private static PdfAFontUse? Create(PdfATextWalkState state, PdfDictionary dictionary) => dictionary.GetName(KnownName.Subtype).ToKnownName() switch
    {
        KnownName.Type1 or KnownName.MMType1 => new(dictionary, PdfAFontKind.Type1, null),
        KnownName.TrueType => new(dictionary, PdfAFontKind.TrueType, null),
        KnownName.Type3 => new(dictionary, PdfAFontKind.Type3, null),
        KnownName.Type0 => new(dictionary, PdfAFontKind.Type0, ReadCodes(state, dictionary.Get(KnownName.Encoding))),
        _ => null,
    };

    /// <summary>Reads the CMap that splits a composite font's strings into codes.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="encoding">The resolved <c>/Encoding</c>.</param>
    /// <returns>
    /// The reader, or null when the CMap is predefined other than Identity-H or Identity-V, uses such a CMap, or cannot be
    /// read; the library does not hold the other predefined CMaps, so their codes are not read.
    /// </returns>
    private static PdfACidCodes? ReadCodes(PdfATextWalkState state, PdfValue encoding)
    {
        if (encoding.TryGetName(out var name))
        {
            return IsIdentity(state.Store.Names.GetSpelling(name)) ? PdfACidCodes.IdentityCodes : null;
        }

        if (encoding.AsStream() is not { } stream || !PdfAFontStreams.TryDecode(stream, state.Validation.CancellationToken, out var data))
        {
            return null;
        }

        CMapContent content;
        try
        {
            content = CMapParser.Parse(data);
        }
        catch (Exception ex) when (PdfAFontStreams.IsReadFailure(ex))
        {
            return null;
        }

        return !content.HasUseCMap || IsIdentity(content.UseCMap.Span) ? PdfACidCodes.FromContent(content) : null;
    }

    /// <summary>Determines whether a CMap name is Identity-H or Identity-V.</summary>
    /// <param name="name">The name's bytes.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsIdentity(ReadOnlySpan<byte> name) => name.SequenceEqual("Identity-H"u8) || name.SequenceEqual("Identity-V"u8);
}
