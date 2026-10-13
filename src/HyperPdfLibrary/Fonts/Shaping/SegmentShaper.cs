// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// Shapes one run of text of a single script and direction: maps characters to glyphs, applies the font's GSUB lookups stage
/// by stage, sets advances from the font's metrics, applies the GPOS lookups, and resolves mark attachments into offsets.
/// </summary>
internal static class SegmentShaper
{
    /// <summary>The GDEF class of base glyphs.</summary>
    private const int BaseClass = 1;

    /// <summary>The GDEF class of mark glyphs.</summary>
    private const int MarkClass = 3;

    /// <summary>The extra room for glyphs that substitutions add to a run.</summary>
    private const int GrowthRoom = 8;

    /// <summary>Shapes a run and appends its glyphs, in logical order.</summary>
    /// <param name="source">The font.</param>
    /// <param name="layout">The font's layout tables.</param>
    /// <param name="plan">The plan for the run's script.</param>
    /// <param name="request">What kind of run it is.</param>
    /// <param name="codePoints">The characters of the run.</param>
    /// <param name="clusters">The UTF-16 index of each character in the shaped text.</param>
    /// <param name="output">Receives the glyphs.</param>
    internal static void Shape(PdfFontSource source, OtFontLayout layout, ShapePlan plan, in SegmentKind request, ReadOnlySpan<int> codePoints, ReadOnlySpan<int> clusters, List<PdfShapedGlyph> output)
    {
        using var buffer = new GlyphBuffer(codePoints.Length + GrowthRoom);
        Fill(buffer, source, layout.Definitions, request, codePoints, clusters);
        Substitute(buffer, layout, plan, request);
        RemoveVirtual(buffer);
        Position(buffer, source, layout, plan, request);
        Emit(buffer, source.UnitsPerEm, request.Vertical, output);
    }

    /// <summary>Maps the characters of a run to glyphs.</summary>
    /// <param name="buffer">Receives the glyphs.</param>
    /// <param name="source">The font.</param>
    /// <param name="definitions">The font's glyph definitions.</param>
    /// <param name="request">What kind of run it is.</param>
    /// <param name="codePoints">The characters.</param>
    /// <param name="clusters">The cluster of each character.</param>
    private static void Fill(GlyphBuffer buffer, PdfFontSource source, OtGdef definitions, in SegmentKind request, ReadOnlySpan<int> codePoints, ReadOnlySpan<int> clusters)
    {
        var forms = ArrayPool<uint>.Shared.Rent(codePoints.Length);
        try
        {
            var masks = forms.AsSpan(0, codePoints.Length);
            masks.Clear();
            if (request.Script.Kind == ShapingKind.Arabic)
            {
                ArabicJoining.AssignForms(codePoints, masks);
            }

            for (var i = 0; i < codePoints.Length; i++)
            {
                AddCharacter(buffer, source, definitions, request, codePoints[i], clusters[i], masks[i]);
            }
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(forms);
        }
    }

    /// <summary>Adds the glyph of a character, or of the two parts of a vowel sign the font lacks as a whole.</summary>
    /// <param name="buffer">Receives the glyphs.</param>
    /// <param name="source">The font.</param>
    /// <param name="definitions">The font's glyph definitions.</param>
    /// <param name="request">What kind of run it is.</param>
    /// <param name="codePoint">The character.</param>
    /// <param name="cluster">The character's cluster.</param>
    /// <param name="form">The Arabic form mask of the character, or zero.</param>
    private static void AddCharacter(GlyphBuffer buffer, PdfFontSource source, OtGdef definitions, in SegmentKind request, int codePoint, int cluster, uint form)
    {
        var character = request.RightToLeft ? MirroredIfPresent(source, codePoint) : codePoint;
        if (request.Script.Kind == ShapingKind.Indic && source.GetGlyph(character) == 0 && IndicData.TrySplitVowelSign(character, out var first, out var second))
        {
            buffer.Add(MakeRecord(source, definitions, first, cluster, 0));
            buffer.Add(MakeRecord(source, definitions, second, cluster, 0));
            return;
        }

        buffer.Add(MakeRecord(source, definitions, character, cluster, form));
    }

    /// <summary>Gets the mirror image of a bracket when the font has a glyph for it.</summary>
    /// <param name="source">The font.</param>
    /// <param name="codePoint">The character.</param>
    /// <returns>The character to look up.</returns>
    private static int MirroredIfPresent(PdfFontSource source, int codePoint)
    {
        var mirror = ShapingCharacters.Mirror(codePoint);
        return mirror != codePoint && source.GetGlyph(mirror) != 0 ? mirror : codePoint;
    }

    /// <summary>Makes the record of a character's glyph.</summary>
    /// <param name="source">The font.</param>
    /// <param name="definitions">The font's glyph definitions.</param>
    /// <param name="codePoint">The character.</param>
    /// <param name="cluster">The character's cluster.</param>
    /// <param name="form">The Arabic form mask, or zero.</param>
    /// <returns>The record.</returns>
    private static GlyphRecord MakeRecord(PdfFontSource source, OtGdef definitions, int codePoint, int cluster, uint form)
    {
        var glyph = source.GetGlyph(codePoint);
        var record = new GlyphRecord(glyph, cluster, codePoint) { Mask = ShapeMasks.Global | form };
        if (ShapingCharacters.IsDefaultIgnorable(codePoint))
        {
            record.Flags |= glyph == 0 ? GlyphFlags.Virtual | GlyphFlags.Hidden : GlyphFlags.Hidden;
        }

        record.GlyphClass = definitions.HasGlyphClasses ? definitions.GlyphClass(glyph) : ClassOfCharacter(codePoint);
        return record;
    }

    /// <summary>Guesses a glyph's class from its character when the font has no glyph classes: combining marks are marks and everything else is a base.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns>The class.</returns>
    private static int ClassOfCharacter(int codePoint) =>
        CharUnicodeInfo.GetUnicodeCategory(codePoint) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark ? MarkClass : BaseClass;

    /// <summary>Runs the GSUB stages of the plan.</summary>
    /// <param name="buffer">The glyphs.</param>
    /// <param name="layout">The font's layout tables.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="request">What kind of run it is.</param>
    private static void Substitute(GlyphBuffer buffer, OtFontLayout layout, ShapePlan plan, in SegmentKind request)
    {
        var engine = new LayoutEngine(layout.Substitution, layout.Definitions, buffer, false, request.RightToLeft);
        var stages = plan.Substitution;
        if (request.Script.Kind != ShapingKind.Indic)
        {
            foreach (var stage in stages)
            {
                engine.RunStage(stage);
            }

            return;
        }

        IndicShaper.Prepare(buffer);
        engine.RunStage(stages[0]);
        IndicShaper.ReorderInitially(buffer);
        for (var s = 1; s <= PlanBuilder.IndicLastBasic; s++)
        {
            engine.RunStage(stages[s]);
            if (s == PlanBuilder.IndicReph)
            {
                IndicShaper.SettleReph(buffer);
            }
        }

        IndicShaper.ReorderFinally(buffer);
        for (var s = PlanBuilder.IndicLastBasic + 1; s < stages.Length; s++)
        {
            engine.RunStage(stages[s]);
        }

        IndicShaper.MergeClusters(buffer);
    }

    /// <summary>Drops the glyphs of invisible characters the font has no glyph for.</summary>
    /// <param name="buffer">The glyphs.</param>
    private static void RemoveVirtual(GlyphBuffer buffer)
    {
        for (var i = buffer.Count - 1; i >= 0; i--)
        {
            if ((buffer.Items[i].Flags & GlyphFlags.Virtual) != 0)
            {
                buffer.Remove(i, 1);
            }
        }
    }

    /// <summary>Sets advances from the font's metrics and applies the positioning lookups.</summary>
    /// <param name="buffer">The glyphs.</param>
    /// <param name="source">The font.</param>
    /// <param name="layout">The font's layout tables.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="request">What kind of run it is.</param>
    private static void Position(GlyphBuffer buffer, PdfFontSource source, OtFontLayout layout, ShapePlan plan, in SegmentKind request)
    {
        buffer.BeginPositions();
        var items = buffer.Items;
        var positions = buffer.Positions;
        for (var i = 0; i < items.Length; i++)
        {
            if ((items[i].Flags & GlyphFlags.Hidden) == 0)
            {
                positions[i].XAdvance = request.Vertical ? source.GetDesignVerticalAdvance(items[i].Glyph) : source.GetDesignAdvance(items[i].Glyph);
            }
        }

        if (request.Vertical)
        {
            return;
        }

        if (layout.Definitions.HasGlyphClasses)
        {
            PositionResolver.ZeroMarkAdvances(buffer);
        }

        var engine = new LayoutEngine(layout.Positioning, layout.Definitions, buffer, true, request.RightToLeft);
        foreach (var stage in plan.Positioning)
        {
            engine.RunStage(stage);
        }

        if (plan.UsesLegacyKerning)
        {
            LegacyKerning.Apply(layout.LegacyKerning.Span, buffer);
        }

        PositionResolver.Resolve(buffer, request.RightToLeft);
    }

    /// <summary>Copies the shaped glyphs out, in ems.</summary>
    /// <param name="buffer">The glyphs, with positions.</param>
    /// <param name="unitsPerEm">The font's design units in one em.</param>
    /// <param name="vertical">Whether the run is written vertically, so offsets are not used.</param>
    /// <param name="output">Receives the glyphs.</param>
    private static void Emit(GlyphBuffer buffer, int unitsPerEm, bool vertical, List<PdfShapedGlyph> output)
    {
        var scale = 1F / Math.Max(unitsPerEm, 1);
        var items = buffer.Items;
        var positions = buffer.Positions;
        for (var i = 0; i < items.Length; i++)
        {
            var position = positions[i];
            output.Add(new(
                items[i].Glyph,
                items[i].Cluster,
                position.XAdvance * scale,
                vertical ? 0F : position.XOffset * scale,
                vertical ? 0F : position.YOffset * scale));
        }
    }
}
