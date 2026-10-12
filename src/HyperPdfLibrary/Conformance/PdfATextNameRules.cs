// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Unicode;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The PDF/A rules on names and replacement text (ISO 19005-2 and -3, 6.1.8; ISO 19005-4, 6.1.7 and 6.2.10.8): font
/// names, colorant names and structure type names are valid UTF-8, and /ActualText holds no private use characters.
/// </summary>
internal static class PdfATextNameRules
{
    /// <summary>The first character a used font's name check rejects, as veraPDF reads that name.</summary>
    private const char LatinStart = '\u0080';

    /// <summary>The last character a used font's name check rejects.</summary>
    private const char LatinEnd = 'ÿ';

    /// <summary>Gets the rules this class checks.</summary>
    internal static PdfARule[] Rules => [PdfARule.NameUtf8, PdfARule.ActualTextPrivateUse];

    /// <summary>Checks the names and replacement text the walk and the structure tree reach.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="walk">The text walk.</param>
    internal static void Check(PdfAValidationContext context, PdfATextWalkState walk)
    {
        var store = context.Document.Objects;
        foreach (var font in walk.Used)
        {
            var primary = PdfAFontFacts.Primary(font);
            context.Require(IsFontObjectName(store, primary), PdfARule.NameUtf8, PdfAFontFacts.Context(store, primary));
        }

        foreach (var resources in walk.Resources)
        {
            CheckResources(context, store, resources);
        }

        context.Require(!walk.PrivateUseActualText, PdfARule.ActualTextPrivateUse, "marked content");
        CheckStructure(context, store);
    }

    /// <summary>
    /// Checks a used font's name as veraPDF checks it: it decodes the name as UTF-8 and tests the result's Latin-1
    /// bytes, so a name that encodes U+0080 to U+00FF fails and an invalid sequence passes.
    /// </summary>
    /// <param name="store">The objects.</param>
    /// <param name="font">The font's primary dictionary.</param>
    /// <returns><see langword="true"/> when the name passes.</returns>
    private static bool IsFontObjectName(PdfObjectStore store, PdfDictionary font)
    {
        if (!font.Get(KnownName.BaseFont).TryGetName(out var name))
        {
            return true;
        }

        foreach (var character in Encoding.UTF8.GetString(store.Names.GetSpelling(name)))
        {
            if (character is >= LatinStart and <= LatinEnd)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Checks the font names and colorant names of a resource dictionary.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="store">The objects.</param>
    /// <param name="resources">The resources.</param>
    private static void CheckResources(PdfAValidationContext context, PdfObjectStore store, PdfDictionary resources)
    {
        var fonts = resources.GetDictionary(KnownName.Font);
        for (var i = 0; fonts is not null && i < fonts.Count; i++)
        {
            RequireUtf8(context, store, ResourceFontName(StoreReading.Resolve(store, fonts.GetValueAt(i))), "font resources");
        }

        var spaces = resources.GetDictionary(KnownName.ColorSpace);
        for (var i = 0; spaces is not null && i < spaces.Count; i++)
        {
            CheckColorants(context, store, StoreReading.Resolve(store, spaces.GetValueAt(i)).AsArray());
        }
    }

    /// <summary>Gets the /BaseFont of a font resource veraPDF reads as a font; a composite font's comes from its CIDFont.</summary>
    /// <param name="value">The resolved resource.</param>
    /// <returns>The name, or null when the resource is not a font.</returns>
    private static PdfValue ResourceFontName(PdfValue value)
    {
        var font = PdfAFontFacts.Dictionary(value);
        var kind = font?.GetName(KnownName.Subtype).ToKnownName() ?? KnownName.None;
        if (font is null || !PdfAFontFacts.IsFontKind(kind))
        {
            return default;
        }

        var named = kind == KnownName.Type0 ? PdfAFontFacts.Descendant(font) : font;
        return named?.Get(KnownName.BaseFont) ?? default;
    }

    /// <summary>Checks the colorant names of a Separation or DeviceN colour space array.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="store">The objects.</param>
    /// <param name="space">The colour space array, or null.</param>
    private static void CheckColorants(PdfAValidationContext context, PdfObjectStore store, PdfArray? space)
    {
        var family = space?.Get(0).AsName().ToKnownName();
        if (family == KnownName.Separation)
        {
            RequireUtf8(context, store, space!.Get(1), "colour space resources");
            return;
        }

        var names = family == KnownName.DeviceN ? space!.GetArray(1) : null;
        for (var i = 0; names is not null && i < names.Count; i++)
        {
            RequireUtf8(context, store, names.Get(i), "colour space resources");
        }
    }

    /// <summary>Checks the role map and the structure element types, and the structure elements' /ActualText.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="store">The objects.</param>
    private static void CheckStructure(PdfAValidationContext context, PdfObjectStore store)
    {
        var root = PdfAFontFacts.Dictionary(context.Document.Catalog.Get(KnownName.StructTreeRoot));
        var roles = root?.GetDictionary(KnownName.RoleMap);
        for (var i = 0; roles is not null && i < roles.Count; i++)
        {
            if (!StoreReading.Resolve(store, roles.GetValueAt(i)).TryGetName(out var role))
            {
                continue;
            }

            RequireUtf8(context, store, PdfValue.FromName(roles.GetKeyAt(i)), "role map");
            RequireUtf8(context, store, PdfValue.FromName(role), "role map");
        }

        foreach (var element in PdfAStructureWalk.Elements(context))
        {
            RequireUtf8(context, store, element.Get(KnownName.S), "structure element");
            var actualText = element.Get(KnownName.ActualText).Kind == PdfKind.String ? element.GetText(KnownName.ActualText) : null;
            context.Require(!PdfAPrivateUse.Contains(actualText), PdfARule.ActualTextPrivateUse, "structure element");
        }
    }

    /// <summary>Records a failure when a value is a name whose bytes are not valid UTF-8.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="store">The objects.</param>
    /// <param name="value">The resolved value.</param>
    /// <param name="where">The report context.</param>
    private static void RequireUtf8(PdfAValidationContext context, PdfObjectStore store, PdfValue value, string where)
    {
        if (value.TryGetName(out var name))
        {
            context.Require(Utf8.IsValid(store.Names.GetSpelling(name)), PdfARule.NameUtf8, where);
        }
    }
}
