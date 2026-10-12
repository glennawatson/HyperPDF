// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.CMaps;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The PDF/A rules on composite fonts and their CMaps (ISO 19005-1, 6.3.3; ISO 19005-2 and -3, 6.2.11.3; ISO 19005-4,
/// 6.2.10.3): compatible character collections, the CIDToGIDMap of TrueType CIDFonts, and embedded or predefined CMaps.
/// </summary>
internal static class PdfACompositeFontRules
{
    /// <summary>The largest CID a CMap may use before PDF/A-4.</summary>
    private const long MaxCid = ushort.MaxValue;

    /// <summary>The rendering modes other than 3, as bits.</summary>
    private const int RenderedModes = 0xF7;

    /// <summary>Gets the rules this class checks.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.Type0CMapOrdering,
        PdfARule.Type0CMapOrderingPdfA1,
        PdfARule.CidFontToGidMapPdfA1,
        PdfARule.CMapNamePredefined,
        PdfARule.CMapNamePdfA1,
        PdfARule.CMapReferencedName,
        PdfARule.CMapWModeMatchesDictionary,
        PdfARule.CMapMaximumCid,
    ];

    /// <summary>Checks a used composite font.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="font">The font.</param>
    internal static void Check(PdfAValidationContext context, PdfAFontUse font)
    {
        var store = context.Document.Objects;
        var cmap = font.Dictionary.Get(KnownName.Encoding);
        var descendant = PdfAFontFacts.Descendant(font.Dictionary);
        var where = PdfAFontFacts.Context(store, descendant ?? font.Dictionary);
        CheckCollections(context, store, cmap, descendant, where);
        if (descendant is not null && descendant.IsName(KnownName.Subtype, KnownName.CIDFontType2) && (font.Modes & RenderedModes) != 0)
        {
            context.Require(HasCidToGidMap(descendant), PdfARule.CidFontToGidMapPdfA1, where);
        }

        CheckCMap(context, store, cmap, where);
    }

    /// <summary>Determines whether a CIDFont's <c>/CIDToGIDMap</c> is a stream or the name Identity.</summary>
    /// <param name="descendant">The CIDFont.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal static bool HasCidToGidMap(PdfDictionary descendant)
    {
        var map = descendant.Get(KnownName.CIDToGIDMap);
        return map.AsStream() is not null || map.IsName(KnownName.Identity);
    }

    /// <summary>Checks that the CIDFont and CMap character collections agree, unless the CMap is Identity-H or Identity-V.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="store">The objects.</param>
    /// <param name="cmap">The resolved <c>/Encoding</c>.</param>
    /// <param name="descendant">The CIDFont, or null.</param>
    /// <param name="where">The report context.</param>
    private static void CheckCollections(PdfAValidationContext context, PdfObjectStore store, PdfValue cmap, PdfDictionary? descendant, string where)
    {
        var name = CMapName(store, cmap);
        if (name is "Identity-H" or "Identity-V")
        {
            return;
        }

        var font = PdfACharacterCollection.FromDictionary(descendant?.Get(KnownName.CIDSystemInfo) ?? default);
        var map = PdfACharacterCollection.FromCMap(store, cmap, context.Part);
        var same = font.Ordering is not null && font.Ordering == map.Ordering && font.Registry is not null && font.Registry == map.Registry;
        context.Require(same, PdfARule.Type0CMapOrderingPdfA1, where);
        context.Require(same && font.Supplement <= map.Supplement, PdfARule.Type0CMapOrdering, where);
    }

    /// <summary>Checks the CMap: predefined or embedded, its base CMap, and an embedded CMap's writing mode and CIDs.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="store">The objects.</param>
    /// <param name="cmap">The resolved <c>/Encoding</c>.</param>
    /// <param name="where">The report context.</param>
    private static void CheckCMap(PdfAValidationContext context, PdfObjectStore store, PdfValue cmap, string where)
    {
        if (cmap.IsNull)
        {
            return;
        }

        var name = CMapName(store, cmap);
        var embedded = cmap.AsStream();
        context.Require(embedded is not null || PdfAPredefinedCMaps.IsPredefined(name), PdfARule.CMapNamePredefined, where);
        context.Require(embedded is not null || name is "Identity-H" or "Identity-V", PdfARule.CMapNamePdfA1, where);
        if (embedded is null)
        {
            return;
        }

        var parent = embedded.Dictionary.Get(KnownName.UseCMap);
        if (!parent.IsNull)
        {
            context.Require(PdfAPredefinedCMaps.IsPredefined(CMapName(store, parent)), PdfARule.CMapReferencedName, where);
        }

        if (!PdfAFontStreams.TryDecode(embedded, context.CancellationToken, out var data))
        {
            return;
        }

        CMapContent content;
        try
        {
            content = CMapParser.Parse(data);
        }
        catch (Exception ex) when (PdfAFontStreams.IsReadFailure(ex))
        {
            return;
        }

        _ = PdfAFontFacts.TryGetInteger(embedded.Dictionary, KnownName.WMode, out var dictionaryMode);
        context.Require(content.WritingMode == dictionaryMode, PdfARule.CMapWModeMatchesDictionary, where);
        context.Require(content.Cids.MaxValue <= MaxCid, PdfARule.CMapMaximumCid, where);
    }

    /// <summary>Gets a CMap's name: a name value, or the <c>/CMapName</c> of an embedded CMap.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="cmap">The resolved CMap value.</param>
    /// <returns>The name, or empty.</returns>
    private static string CMapName(PdfObjectStore store, PdfValue cmap)
    {
        if (cmap.TryGetName(out var name))
        {
            return store.Names.GetString(name);
        }

        if (cmap.AsStream() is not { } stream)
        {
            return string.Empty;
        }

        var value = stream.Dictionary.Get(KnownName.CMapName);
        return value.TryGetName(out var streamName) ? store.Names.GetString(streamName) : stream.Dictionary.GetText(KnownName.CMapName) ?? string.Empty;
    }
}
