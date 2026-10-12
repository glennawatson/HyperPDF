// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Fonts.Programs;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Works out how a substitute program serves a simple font: the glyph and width of each code, and its text. A code the
/// font shows must have a glyph whose width is within one unit of the font's width for it; other codes are mapped only
/// when they qualify too.
/// </summary>
[DebuggerDisplay("PdfAConversionGlyphPlan: {Glyphs.Count} codes")]
internal sealed class PdfAConversionGlyphPlan
{
    /// <summary>The number of one-byte codes.</summary>
    private const int CodeCount = 256;

    /// <summary>The glyph space of PDF widths.</summary>
    private const float GlyphSpace = 1000;

    /// <summary>The largest width difference PDF/A accepts between the dictionary and the program.</summary>
    private const float WidthTolerance = 1;

    /// <summary>The most UTF-16 units one glyph name gives.</summary>
    private const int MaxText = 8;

    /// <summary>Gets the glyph of each mapped code.</summary>
    internal SortedDictionary<int, int> Glyphs { get; } = [];

    /// <summary>Gets the width of each mapped code, in glyph space.</summary>
    internal SortedDictionary<int, float> Widths { get; } = [];

    /// <summary>Gets the text of each mapped code that has one.</summary>
    internal SortedDictionary<int, string> Texts { get; } = [];

    /// <summary>Gets the reason the substitute cannot serve the font, or <see langword="null"/>.</summary>
    internal string? Problem { get; private set; }

    /// <summary>Builds the plan.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="standard">The standard font it names.</param>
    /// <param name="program">The substitute program.</param>
    /// <param name="used">The codes shown visibly, or <see langword="null"/>.</param>
    /// <param name="usageUnknown">Whether every code may be used.</param>
    /// <returns>The plan.</returns>
    internal static PdfAConversionGlyphPlan Build(PdfDictionary font, StandardFont standard, TrueTypeProgram program, HashSet<int>? used, bool usageUnknown)
    {
        var plan = new PdfAConversionGlyphPlan();
        var symbolic = (FontDescriptor.Read(font.GetDictionary(KnownName.FontDescriptor)).Flags & FontFlags.Symbolic) != 0;
        var encoding = SimpleEncoding.Read(font, new(FontEncoding.Standard, false, font.IsName(KnownName.Subtype, KnownName.TrueType), symbolic, false));
        var first = font.GetInt32(KnownName.FirstChar, 0);
        var widths = font.GetArray(KnownName.Widths);
        for (var code = 0; code < CodeCount; code++)
        {
            var shown = usageUnknown ? widths is null || (code >= first && code < first + widths.Count) : used?.Contains(code) == true;
            if (!TryMap(plan, code, encoding.GetName(code), program, OriginalWidth(widths, first, code, standard, encoding.GetName(code))) && shown)
            {
                plan.Problem ??= "the matching open font lacks some of its characters or draws them at a different width.";
            }
        }

        return plan;
    }

    /// <summary>Maps a code to the program's glyph for its name when the glyph exists and keeps the font's width.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="code">The code.</param>
    /// <param name="name">The code's glyph name, or empty.</param>
    /// <param name="program">The substitute program.</param>
    /// <param name="original">The font's width for the code, or <see langword="null"/> when it gives none.</param>
    /// <returns><see langword="true"/> when the code was mapped.</returns>
    private static bool TryMap(PdfAConversionGlyphPlan plan, int code, ReadOnlySpan<byte> name, TrueTypeProgram program, float? original)
    {
        var glyph = name.IsEmpty ? -1 : program.GetGlyphByName(name);
        if (glyph <= 0)
        {
            return false;
        }

        var width = program.GetAdvanceWidth(glyph) * GlyphSpace / program.UnitsPerEm;
        if (original is { } known && Math.Abs(known - width) > WidthTolerance)
        {
            return false;
        }

        plan.Glyphs[code] = glyph;
        plan.Widths[code] = original ?? MathF.Round(width);
        AddText(plan, code, name);
        return true;
    }

    /// <summary>Gets the font's width for a code: from /Widths, else from the standard font's metrics.</summary>
    /// <param name="widths">The /Widths array, or <see langword="null"/>.</param>
    /// <param name="first">The /FirstChar value.</param>
    /// <param name="code">The code.</param>
    /// <param name="standard">The standard font.</param>
    /// <param name="name">The glyph name.</param>
    /// <returns>The width, or <see langword="null"/> when the font gives none.</returns>
    private static float? OriginalWidth(PdfArray? widths, int first, int code, StandardFont standard, ReadOnlySpan<byte> name)
    {
        if (widths is not null && code >= first && code < first + widths.Count)
        {
            return widths.GetSingle(code - first);
        }

        return StandardFonts.TryGetWidth(standard, name, out var width) ? width : null;
    }

    /// <summary>Records the text of a code's glyph name, unless it holds a character ToUnicode may not map to.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="code">The code.</param>
    /// <param name="name">The glyph name.</param>
    private static void AddText(PdfAConversionGlyphPlan plan, int code, ReadOnlySpan<byte> name)
    {
        Span<char> text = stackalloc char[MaxText];
        if (!GlyphList.TryGetUnicode(name, text, out var length) || length == 0)
        {
            return;
        }

        var value = text[..length];
        if (value.IndexOfAny('\0', '￾', '﻿') < 0)
        {
            plan.Texts[code] = value.ToString();
        }
    }
}
