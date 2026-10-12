// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Visits the colour space of a 3D annotation's 3D stream, which veraPDF checks like any other used colour space.</summary>
internal static class PdfAThreeDChecks
{
    /// <summary>Visits the colour space of an annotation's 3D stream, once for each stream.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="pageScope">The page's resources, which a named colour space is looked up in.</param>
    internal static void Visit(PdfAColourState state, PdfDictionary annotation, in PdfAColourResourceScope pageScope)
    {
        if (!annotation.IsName(KnownName.Subtype, KnownName.ThreeD))
        {
            return;
        }

        var raw = annotation.GetRaw(KnownName.ThreeDD);
        if (StoreReading.Resolve(state.Store, raw).AsDictionary() is { } reference && reference.Get(KnownName.Type).AsName() == state.Names.ThreeDReference)
        {
            raw = reference.GetRaw(KnownName.ThreeDD);
        }

        if (StoreReading.Resolve(state.Store, raw).AsStream() is not { } stream
            || !stream.Dictionary.IsName(KnownName.Type, KnownName.ThreeD)
            || (raw.IsReference && !state.Visited.Add(new(raw.AsReference(), PdfAVisitKind.ThreeDStream, 0))))
        {
            return;
        }

        PdfAColourChecks.Visit(state, ColourSpace(state, stream.Dictionary.GetRaw(KnownName.ColorSpace), pageScope));
    }

    /// <summary>Reads a 3D stream's colour space: a name, alone or in a one-element array, is a resource lookup.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="raw">The <c>/ColorSpace</c> value.</param>
    /// <param name="pageScope">The page's resources.</param>
    /// <returns>The space, or null.</returns>
    private static PdfAColourSpace? ColourSpace(PdfAColourState state, PdfValue raw, in PdfAColourResourceScope pageScope)
    {
        var value = StoreReading.Resolve(state.Store, raw);
        var single = value.AsArray() is { Count: 1 } array ? StoreReading.Resolve(state.Store, array.GetRaw(0)) : value;
        if (single.TryGetName(out var name))
        {
            return PdfAResources.ColourSpace(state, pageScope, name);
        }

        return value.AsArray() is not null ? PdfAColourSpaceParsing.Parse(state, raw, null, false) : null;
    }
}
