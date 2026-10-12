// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Visits patterns and shadings: a tiling pattern's content and the colour space of a shading.</summary>
internal static class PdfAPatternWalk
{
    /// <summary>Finds a named pattern resource; it must be a pattern dictionary.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="scope">The resources.</param>
    /// <param name="name">The name.</param>
    /// <returns>The pattern, or null.</returns>
    internal static PdfAColourSpace? Find(PdfAColourState state, in PdfAColourResourceScope scope, PdfName name)
    {
        var resource = PdfAResources.Find(state, scope, KnownName.Pattern, name);
        var value = StoreReading.Resolve(state.Store, resource.Raw);
        return value.AsDictionary() is null ? null : PdfAColourSpaceParsing.Parse(state, resource.Raw, null, false);
    }

    /// <summary>Visits a pattern used to paint, once.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="frame">Where it is painted.</param>
    internal static void Visit(PdfAColourState state, in PdfAColourSpace pattern, in PdfAContentFrame frame)
    {
        if (pattern.Id.IsValid && !state.Visited.Add(new(pattern.Id, PdfAVisitKind.Pattern, 0)))
        {
            return;
        }

        var dictionary = pattern.Value.AsDictionary()!;
        if (dictionary.Get(KnownName.PatternType).AsInteger(-1) == PdfAPatterns.Shading)
        {
            VisitShading(state, dictionary.GetRaw(KnownName.Shading), pattern.Resources);
            return;
        }

        if (pattern.Value.AsStream() is null)
        {
            return;
        }

        var paint = frame.Paint;
        var initial = frame.Initial with
        {
            Fill = paint.FillUnderlying ?? frame.Initial.Fill,
            Stroke = paint.StrokeUnderlying ?? frame.Initial.Stroke,
        };
        if (dictionary.Get(KnownName.PaintType).AsInteger(-1) == PdfAPatterns.Uncoloured)
        {
            initial = initial with { ProcessColour = false };
        }

        var raw = pattern.Id.IsValid ? PdfValue.FromReference(pattern.Id) : pattern.Value;
        PdfAContentWalk.WalkStream(state, raw, PdfAResources.Extend(state, frame.Scope, dictionary), initial);
    }

    /// <summary>Visits a shading's colour space, once for each indirect shading.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="raw">The shading, possibly a reference.</param>
    /// <param name="resources">The resources names in its colour space are read from, or null.</param>
    internal static void VisitShading(PdfAColourState state, PdfValue raw, PdfDictionary? resources)
    {
        if (StoreReading.Resolve(state.Store, raw).AsDictionary() is not { } shading
            || (raw.IsReference && !state.Visited.Add(new(raw.AsReference(), PdfAVisitKind.Shading, 0))))
        {
            return;
        }

        PdfAColourChecks.Visit(state, PdfAColourSpaceParsing.Parse(state, shading.GetRaw(KnownName.ColorSpace), resources, false));
    }
}
