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
    /// <param name="session">The owning shaper's reusable work space.</param>
    /// <param name="context">The font and its layout tables.</param>
    /// <param name="plan">The plan for the run's script.</param>
    /// <param name="request">What kind of run it is.</param>
    /// <param name="codePoints">The characters of the run.</param>
    /// <param name="clusters">The UTF-16 index of each character in the shaped text.</param>
    /// <param name="output">Receives the glyphs.</param>
    internal static void Shape(
        ShapingSession session,
        ShapingContext context,
        ShapePlan plan,
        in SegmentKind request,
        ReadOnlySpan<int> codePoints,
        ReadOnlySpan<int> clusters,
        List<PdfShapedGlyph> output)
    {
        var (source, layout) = context;
        var buffer = session.Buffer;
        buffer.Clear(codePoints.Length + GrowthRoom);
        Fill(buffer, source, layout.Definitions, request, codePoints, clusters);
        Substitute(session, layout, plan, request);
        RemoveVirtual(buffer);
        Position(session, source, layout, plan, request);
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
            if (request.Script.Kind is ShapingKind.Arabic or ShapingKind.UniversalJoining)
            {
                ArabicJoining.AssignForms(codePoints, masks);
            }

            var i = 0;
            while (i < codePoints.Length)
            {
                // Hangul jamo that make a syllable the font has a glyph for become that syllable, as one glyph for the characters.
                if (HangulComposition.TryCompose(source, codePoints, i, out var syllable, out var used))
                {
                    buffer.Add(MakeRecord(source, definitions, syllable, clusters[i], masks[i]));
                    i += used;
                    continue;
                }

                AddCharacter(buffer, source, definitions, request, codePoints[i], clusters[i], masks[i]);
                i++;
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
        if (TrySplitVowel(request.Script, source, character, out var first, out var second))
        {
            AddCharacter(buffer, source, definitions, request, first, cluster, 0);
            buffer.Add(MakeRecord(source, definitions, second, cluster, 0));
            return;
        }

        // A font with no glyph for the sign "am" has it as the mark above the consonant and the long a after it.
        if (source.GetGlyph(character) == 0 && SaraAm.TrySplit(character, out var mark, out var vowel))
        {
            buffer.Add(MakeRecord(source, definitions, mark, cluster, 0));
            buffer.Add(MakeRecord(source, definitions, vowel, cluster, 0));
            return;
        }

        buffer.Add(MakeRecord(source, definitions, character, cluster, form));
    }

    /// <summary>Splits a vowel sign that is written on both sides of its consonant into the part before it and the part after.</summary>
    /// <param name="script">The script of the text.</param>
    /// <param name="source">The font.</param>
    /// <param name="character">The character.</param>
    /// <param name="first">Receives the part that goes before the consonant, which may split further.</param>
    /// <param name="second">Receives the part that goes after it.</param>
    /// <returns><see langword="true"/> when the character is split. Indic scripts split only when the font has no glyph for the sign; Khmer and Sinhala always do.</returns>
    private static bool TrySplitVowel(ShapingScript script, PdfFontSource source, int character, out int first, out int second)
    {
        first = 0;
        second = 0;
        return script.Kind switch
        {
            ShapingKind.Indic => source.GetGlyph(character) == 0 && IndicData.TrySplitVowelSign(character, out first, out second),
            ShapingKind.Khmer => KhmerShaper.TrySplitVowel(character, out first, out second),
            ShapingKind.Universal => IndicData.TrySplitVowelSign(character, out first, out second),
            _ => false,
        };
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
    /// <param name="session">The run's glyphs and engines.</param>
    /// <param name="layout">The font's layout tables.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="request">What kind of run it is.</param>
    private static void Substitute(ShapingSession session, OtFontLayout layout, ShapePlan plan, in SegmentKind request)
    {
        var buffer = session.Buffer;
        var engine = session.Substitute(layout.Substitution, layout.Definitions, request.RightToLeft);
        var stages = plan.Substitution;
        if (request.Script.Kind is ShapingKind.Khmer or ShapingKind.Myanmar or ShapingKind.Universal or ShapingKind.UniversalJoining)
        {
            SubstituteReordered(buffer, engine, stages, request.Script);
            return;
        }

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

    /// <summary>Runs the first stage of features, puts each syllable's glyphs in the order the script's font expects, then runs the other stages.</summary>
    /// <param name="buffer">The glyphs.</param>
    /// <param name="engine">The substitution engine.</param>
    /// <param name="stages">The stages of features.</param>
    /// <param name="script">The script of the text.</param>
    private static void SubstituteReordered(GlyphBuffer buffer, LayoutEngine engine, LookupEntry[][] stages, ShapingScript script)
    {
        engine.RunStage(stages[0]);
        switch (script.Kind)
        {
            case ShapingKind.Khmer:
            {
                KhmerShaper.Reorder(buffer);
                break;
            }

            case ShapingKind.Myanmar:
            {
                MyanmarShaper.Reorder(buffer);
                break;
            }

            case ShapingKind.Universal when script.Name == "Sinh":
            {
                UniversalShaper.ReorderSinhala(buffer);
                break;
            }

            default:
            {
                break;
            }
        }

        for (var s = 1; s < stages.Length; s++)
        {
            engine.RunStage(stages[s]);
        }
    }

    /// <summary>Drops the glyphs of invisible characters that substitution did not consume.</summary>
    /// <param name="buffer">The glyphs.</param>
    private static void RemoveVirtual(GlyphBuffer buffer)
    {
        for (var i = buffer.Count - 1; i >= 0; i--)
        {
            if ((buffer.Items[i].Flags & (GlyphFlags.Virtual | GlyphFlags.Hidden)) != 0)
            {
                buffer.Remove(i, 1);
            }
        }
    }

    /// <summary>Sets advances from the font's metrics and applies the positioning lookups.</summary>
    /// <param name="session">The run's glyphs and engines.</param>
    /// <param name="source">The font.</param>
    /// <param name="layout">The font's layout tables.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="request">What kind of run it is.</param>
    private static void Position(ShapingSession session, PdfFontSource source, OtFontLayout layout, ShapePlan plan, in SegmentKind request)
    {
        var buffer = session.Buffer;
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

        var engine = session.Position(layout.Positioning, layout.Definitions, request.RightToLeft);
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
