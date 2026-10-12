// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Applies a graphics state parameter dictionary set with <c>gs</c> and checks its rendering intent and halftone
/// (ISO 19005-2 6.2.5). Each indirect dictionary and halftone is checked once.
/// </summary>
internal static class PdfAGraphicsStateChecks
{
    /// <summary>The halftone types PDF/A allows: 1 and 5.</summary>
    private const long SpotHalftone = 1;

    /// <summary>The halftone type that holds a halftone for each colourant.</summary>
    private const long ColourantHalftones = 5;

    /// <summary>The PDF/A part without the transfer function rule.</summary>
    private const int Part4 = 4;

    /// <summary>Applies <c>gs</c>: checks the dictionary and returns the graphics state with its overprint and font settings.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="resource">The dictionary resource.</param>
    /// <param name="paint">The graphics state before.</param>
    /// <returns>The graphics state after.</returns>
    internal static PdfAColourPaintState Apply(PdfAColourState state, in PdfAResource resource, in PdfAColourPaintState paint)
    {
        if (StoreReading.Resolve(state.Store, resource.Raw).AsDictionary() is not { } dictionary)
        {
            return paint;
        }

        if (!resource.Id.IsValid || state.Visited.Add(new(resource.Id, PdfAVisitKind.GraphicsState, 0)))
        {
            PdfARenderingIntents.Check(state, dictionary.GetRaw(KnownName.RI));
            VisitHalftone(state, dictionary.GetRaw(KnownName.HT), default);
        }

        var font = dictionary.GetArray(KnownName.Font)?.GetRaw(0) ?? default;
        return Overprint(dictionary, paint) with { Font = StoreReading.Resolve(state.Store, font).AsDictionary() is null ? paint.Font : new(font, null) };
    }

    /// <summary>Applies the overprint entries.</summary>
    /// <param name="dictionary">The graphics state dictionary.</param>
    /// <param name="paint">The graphics state before.</param>
    /// <returns>The graphics state after.</returns>
    private static PdfAColourPaintState Overprint(PdfDictionary dictionary, in PdfAColourPaintState paint)
    {
        var stroke = dictionary.Get(KnownName.OP);
        var fill = dictionary.Get(KnownName.FillOverprint);
        var fillValue = fill.Kind == PdfKind.Boolean ? fill : stroke;
        var mode = dictionary.Get(KnownName.OPM);
        return paint with
        {
            OverprintStroke = stroke.AsBoolean(paint.OverprintStroke),
            OverprintFill = fillValue.AsBoolean(paint.OverprintFill),
            OverprintMode = mode.IsNumber ? (int)mode.AsInteger() : paint.OverprintMode,
        };
    }

    /// <summary>Checks a halftone, and for type 5 the halftone of each colourant.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="raw">The halftone, possibly a reference; a name, dictionary or stream.</param>
    /// <param name="colourant">The colourant the halftone is for inside a type 5 halftone, or none.</param>
    private static void VisitHalftone(PdfAColourState state, PdfValue raw, PdfName colourant)
    {
        var value = StoreReading.Resolve(state.Store, raw);
        if (value.TryGetName(out var name))
        {
            CheckNamedHalftone(state, name);
            return;
        }

        if (value.AsDictionary() is not { } halftone || (raw.IsReference && !state.Visited.Add(new(raw.AsReference(), PdfAVisitKind.Halftone, 0))))
        {
            return;
        }

        var type = halftone.Get(state.Names.HalftoneType);
        state.Context.Require(type.IsNumber && type.AsInteger() is SpotHalftone or ColourantHalftones, PdfARule.HalftoneType, null);
        state.Context.Require(halftone.Get(state.Names.HalftoneName).Kind != PdfKind.String, PdfARule.HalftoneName, null);
        CheckTransfer(state, halftone, colourant);
        if (type.IsNumber && type.AsInteger() == ColourantHalftones)
        {
            VisitColourantHalftones(state, halftone);
        }
    }

    /// <summary>Checks a halftone given by name: only <c>/Default</c> is a type 1 halftone without a name.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="name">The name.</param>
    private static void CheckNamedHalftone(PdfAColourState state, PdfName name)
    {
        var isDefault = name == state.Names.Default;
        state.Context.Require(isDefault, PdfARule.HalftoneType, null);
        state.Context.Require(isDefault, PdfARule.HalftoneName, null);
    }

    /// <summary>
    /// Checks the transfer function rule: a halftone for a spot colourant needs one, a halftone for a process colourant or
    /// the whole page must not have one, and the <c>/Default</c> colourant may do either.
    /// </summary>
    /// <param name="state">The walk.</param>
    /// <param name="halftone">The halftone.</param>
    /// <param name="colourant">The colourant, or none.</param>
    private static void CheckTransfer(PdfAColourState state, PdfDictionary halftone, PdfName colourant)
    {
        if (state.Context.Part == Part4 || colourant == state.Names.Default)
        {
            return;
        }

        var hasTransfer = !halftone.Get(state.Names.TransferFunction).IsNull;
        var isProcess = colourant.IsNone || Array.IndexOf(state.Names.ProcessColourants, colourant) >= 0;
        state.Context.Require(isProcess ? !hasTransfer : hasTransfer, PdfARule.HalftoneTransfer, null);
    }

    /// <summary>Visits the halftones of a type 5 halftone, one for each colourant key.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="halftone">The type 5 halftone.</param>
    private static void VisitColourantHalftones(PdfAColourState state, PdfDictionary halftone)
    {
        for (var i = 0; i < halftone.Count; i++)
        {
            var value = halftone.GetValueAt(i);
            if (StoreReading.Resolve(state.Store, value).AsDictionary() is not null)
            {
                VisitHalftone(state, value, halftone.GetKeyAt(i));
            }
        }
    }
}
