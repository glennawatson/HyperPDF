// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Records what an inline image needs from a PDF/A conversion: its device colour space, and whether its filters or its
/// interpolation flag would need the content rewritten (ISO 19005-2 §6.1.10 and §6.2.8.3).
/// </summary>
internal static class PdfAConversionInlineImages
{
    /// <summary>Records an inline image's facts.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="image">The image dictionary.</param>
    /// <param name="defaults">The default colour spaces in force.</param>
    /// <param name="facts">The facts.</param>
    internal static void Mark(PdfObjectStore store, PdfDictionary image, PdfAConversionDefaultSpaces defaults, PdfAConversionFacts facts)
    {
        var space = image.GetRaw(KnownName.CS);
        if (space.IsNull)
        {
            space = image.GetRaw(KnownName.ColorSpace);
        }

        if (space.Kind == PdfKind.Name)
        {
            PdfAConversionSpaceColours.MarkName(space.AsName().ToKnownName(), defaults, facts);
        }
        else if (space.AsArray() is { } array)
        {
            PdfAConversionSpaceColours.Mark(store, PdfValue.FromArray(array), facts, 0);
        }

        var filter = image.GetRaw(KnownName.F);
        if (filter.IsNull)
        {
            filter = image.GetRaw(KnownName.Filter);
        }

        if (HasForbiddenFilter(filter))
        {
            facts.InlineForbiddenFilters++;
        }

        var interpolate = image.GetRaw(KnownName.I);
        if (interpolate.IsNull)
        {
            interpolate = image.GetRaw(KnownName.Interpolate);
        }

        facts.InlineInterpolated += interpolate.AsBoolean() ? 1 : 0;
    }

    /// <summary>Determines whether a filter value includes LZW or a crypt filter.</summary>
    /// <param name="filter">A name or an array of names.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasForbiddenFilter(PdfValue filter)
    {
        if (filter.AsArray() is { } chain)
        {
            for (var i = 0; i < chain.Count; i++)
            {
                if (IsForbidden(chain.GetRaw(i)))
                {
                    return true;
                }
            }

            return false;
        }

        return IsForbidden(filter);
    }

    /// <summary>Determines whether a filter name is LZW or Crypt.</summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsForbidden(PdfValue name) => name.AsName().ToKnownName() is KnownName.LZWDecode or KnownName.LZW or KnownName.Crypt;
}
