// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Follows the operators of a content stream for the transparency search, as veraPDF does: a graphics state counts only
/// when something is painted with it. Fills, images, forms and shadings use the fill alpha, strokes the stroke alpha,
/// text whichever its rendering mode paints; a blend mode or soft mask makes any of them transparent. Inline images and
/// paths ended with <c>n</c> paint nothing that counts.
/// </summary>
internal static class PdfAPaintOperators
{
    /// <summary>The highest text rendering mode.</summary>
    private const int LastRenderMode = 7;

    /// <summary>The text rendering modes that fill glyphs: 0, 2, 4 and 6.</summary>
    private const int FillModes = 0b0101_0101;

    /// <summary>The text rendering modes that stroke glyphs: 1, 2, 5 and 6.</summary>
    private const int StrokeModes = 0b0110_0110;

    /// <summary>Applies one operator and determines whether it paints with transparency.</summary>
    /// <param name="walk">The search.</param>
    /// <param name="state">The stream's graphics state.</param>
    /// <param name="op">The operator.</param>
    /// <param name="reader">The reader, on the operator.</param>
    /// <returns><see langword="true"/> when the operator paints with transparency.</returns>
    internal static bool Uses(PdfATransparencyWalk walk, PdfAPaintState state, ContentOperator op, ref ContentReader reader)
    {
        if (ChangesState(state, op, ref reader) || ChangesColor(state, op, ref reader))
        {
            return false;
        }

        return op switch
        {
            ContentOperator.Fill or ContentOperator.FillEvenOdd => PathUses(walk, state, true, false),
            ContentOperator.Stroke or ContentOperator.CloseStroke => PathUses(walk, state, false, true),
            ContentOperator.FillStroke or ContentOperator.FillStrokeEvenOdd or ContentOperator.CloseFillStroke or ContentOperator.CloseFillStrokeEvenOdd =>
                PathUses(walk, state, true, true),
            ContentOperator.PaintShading => state.Current.FillsTransparent,
            ContentOperator.PaintXObject => PdfATransparencyUse.XObjectUses(walk, state, reader.Operand(0).Name),
            _ => ShowUses(walk, state, op, ref reader),
        };
    }

    /// <summary>Determines whether a blend mode is other than Normal; an array is read by its first name. veraPDF counts Compatible as blending.</summary>
    /// <param name="mode">The <c>/BM</c> value.</param>
    /// <returns><see langword="true"/> when it blends.</returns>
    internal static bool IsBlended(PdfValue mode)
    {
        var name = mode.AsArray() is { Count: > 0 } list ? list.GetName(0) : mode.AsName();
        return !name.IsNone && !name.Is(KnownName.Normal);
    }

    /// <summary>Determines whether a text-showing operator paints with transparency, from the state or from the Type 3 glyphs it shows.</summary>
    /// <param name="walk">The search.</param>
    /// <param name="state">The stream's graphics state.</param>
    /// <param name="op">The operator.</param>
    /// <param name="reader">The reader, on the operator.</param>
    /// <returns><see langword="true"/> when it does; <see langword="false"/> for any other operator.</returns>
    private static bool ShowUses(PdfATransparencyWalk walk, PdfAPaintState state, ContentOperator op, ref ContentReader reader)
    {
        var paint = state.Current;
        return op switch
        {
            ContentOperator.ShowText or ContentOperator.NextLineShowText => TextUses(paint) || PdfAType3Glyphs.StringUses(walk, paint.Font, ref reader, 0, state.Depth),
            ContentOperator.SetSpacingNextLineShowText =>
                TextUses(paint) || PdfAType3Glyphs.StringUses(walk, paint.Font, ref reader, PdfAType3Glyphs.SpacingStringOperand, state.Depth),
            ContentOperator.ShowTextArray => TextUses(paint) || PdfAType3Glyphs.ArrayUses(walk, paint.Font, ref reader, state.Depth),
            _ => false,
        };
    }

    /// <summary>Applies the operators that save, restore or change the graphics or text state.</summary>
    /// <param name="state">The stream's graphics state.</param>
    /// <param name="op">The operator.</param>
    /// <param name="reader">The reader, on the operator.</param>
    /// <returns><see langword="true"/> when the operator was one of them.</returns>
    private static bool ChangesState(PdfAPaintState state, ContentOperator op, ref ContentReader reader)
    {
        switch (op)
        {
            case ContentOperator.Save:
                {
                    state.Saved.Push(state.Current);
                    return true;
                }

            case ContentOperator.Restore:
                {
                    state.Current = state.Saved.TryPop(out var saved) ? saved : state.Current;
                    return true;
                }

            case ContentOperator.SetGraphicsState:
                {
                    state.Current = Apply(state.Current, PdfATransparencyUse.Resource(state.Resources, KnownName.ExtGState, reader.Operand(0).Name).AsDictionary());
                    return true;
                }

            case ContentOperator.SetRenderMode:
                {
                    state.Current = state.Current with { RenderMode = (int)reader.Number(0) };
                    return true;
                }

            case ContentOperator.SetFont:
                {
                    var font = PdfATransparencyUse.Resource(state.Resources, KnownName.Font, reader.Operand(0).Name).AsDictionary();
                    state.Current = state.Current with { Font = PdfAType3Glyphs.Select(font, state.Resources) };
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>Applies the operators that set the fill or stroke colour, tracking the tiling pattern each paints with.</summary>
    /// <param name="state">The stream's graphics state.</param>
    /// <param name="op">The operator.</param>
    /// <param name="reader">The reader, on the operator.</param>
    /// <returns><see langword="true"/> when the operator was one of them.</returns>
    private static bool ChangesColor(PdfAPaintState state, ContentOperator op, ref ContentReader reader)
    {
        switch (op)
        {
            case ContentOperator.SetFillColorN:
                {
                    state.Current = state.Current with { FillPattern = PatternName(ref reader) };
                    return true;
                }

            case ContentOperator.SetStrokeColorN:
                {
                    state.Current = state.Current with { StrokePattern = PatternName(ref reader) };
                    return true;
                }

            case ContentOperator.SetFillColorSpace or ContentOperator.SetFillColor or ContentOperator.SetFillGray or ContentOperator.SetFillRgb
                or ContentOperator.SetFillCmyk:
                {
                    state.Current = state.Current with { FillPattern = default };
                    return true;
                }

            case ContentOperator.SetStrokeColorSpace or ContentOperator.SetStrokeColor or ContentOperator.SetStrokeGray or ContentOperator.SetStrokeRgb
                or ContentOperator.SetStrokeCmyk:
                {
                    state.Current = state.Current with { StrokePattern = default };
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>Reads the pattern name <c>scn</c> or <c>SCN</c> sets, its last operand.</summary>
    /// <param name="reader">The reader, on the operator.</param>
    /// <returns>The pattern name, or no name when the colour is not a pattern.</returns>
    private static PdfName PatternName(ref ContentReader reader)
    {
        var last = reader.Operand(reader.OperandCount - 1);
        return last.Kind == ContentOperandKind.Name ? last.Name : default;
    }

    /// <summary>Applies an <c>/ExtGState</c>: each entry it sets replaces the matching part of the state.</summary>
    /// <param name="paint">The state in force.</param>
    /// <param name="graphics">The graphics state dictionary, or <see langword="null"/>.</param>
    /// <returns>The new state.</returns>
    private static PdfAPaint Apply(in PdfAPaint paint, PdfDictionary? graphics)
    {
        if (graphics is null)
        {
            return paint;
        }

        var stroke = graphics.Get(KnownName.CA);
        var fill = graphics.Get(KnownName.FillAlpha);
        var mode = graphics.Get(KnownName.BM);
        var mask = graphics.Get(KnownName.SMask);
        return paint with
        {
            StrokeAlpha = stroke.IsNumber ? stroke.AsNumber() < 1 : paint.StrokeAlpha,
            FillAlpha = fill.IsNumber ? fill.AsNumber() < 1 : paint.FillAlpha,
            Blend = mode.IsNull ? paint.Blend : IsBlended(mode),
            SoftMask = mask.IsNull ? paint.SoftMask : mask.AsDictionary() is not null,
        };
    }

    /// <summary>Determines whether a path paint is transparent, from the state or from the tiling pattern it paints with.</summary>
    /// <param name="walk">The search.</param>
    /// <param name="state">The stream's graphics state.</param>
    /// <param name="fills">Whether the operator fills.</param>
    /// <param name="strokes">Whether the operator strokes.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool PathUses(PdfATransparencyWalk walk, PdfAPaintState state, bool fills, bool strokes)
    {
        var paint = state.Current;
        return (fills && (paint.FillsTransparent || PdfATransparencyUse.PatternUses(walk, state, paint.FillPattern)))
            || (strokes && (paint.StrokesTransparent || PdfATransparencyUse.PatternUses(walk, state, paint.StrokePattern)));
    }

    /// <summary>Determines whether shown text paints with transparency under its rendering mode.</summary>
    /// <param name="paint">The state in force.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool TextUses(in PdfAPaint paint)
    {
        var mode = 1 << Math.Clamp(paint.RenderMode, 0, LastRenderMode);
        return ((mode & FillModes) != 0 && paint.FillsTransparent) || ((mode & StrokeModes) != 0 && paint.StrokesTransparent);
    }
}
