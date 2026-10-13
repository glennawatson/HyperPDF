// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Fonts.Shaping;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// A shaper written for this library that reads the font's own OpenType layout tables (OpenType specification: GSUB, GPOS and
/// GDEF) and applies them to text. It selects the script and language system, applies glyph substitution (single, multiple,
/// alternate, ligature, contextual, chaining contextual and reverse chaining lookups) and glyph positioning (single, pair,
/// cursive, mark-to-base, mark-to-ligature, mark-to-mark and contextual lookups), joins Arabic letters, forms and reorders
/// Indic syllables, and puts mixed-direction text in visual order (Unicode Standard Annex #9). It is the shaper used when
/// <see cref="PdfEmbeddedFontOptions.Shaper"/> is not set. Members are safe to call from several threads.
/// </summary>
/// <remarks>
/// Scripts that need their own shaping model are Arabic and the Indic scripts; other scripts use the default features, with
/// mark positioning, which covers Latin, Greek, Cyrillic, Hebrew, Thai and Han. Khmer, Myanmar, Tibetan, Sinhala, Hangul
/// jamo and Mongolian have no model of their own here, so they get the default features only.
/// </remarks>
[DebuggerDisplay("PdfOpenTypeShaper: {_source.PostScriptName}")]
public sealed class PdfOpenTypeShaper : IPdfGlyphShaper
{
    /// <summary>The font.</summary>
    private readonly PdfFontSource _source;

    /// <summary>The font's layout tables.</summary>
    private readonly OtFontLayout _layout;

    /// <summary>Initializes a new instance of the <see cref="PdfOpenTypeShaper"/> class.</summary>
    /// <param name="source">The font to take glyphs, metrics and layout tables from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public PdfOpenTypeShaper(PdfFontSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _layout = new(source.Font);
    }

    /// <inheritdoc/>
    public bool Shape(ReadOnlySpan<char> text, PdfShapingOptions options, List<PdfShapedGlyph> output)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        if (text.IsEmpty)
        {
            return false;
        }

        var vertical = options.WritingMode == PdfWritingMode.Vertical;
        var codePoints = ArrayPool<int>.Shared.Rent(text.Length);
        var clusters = ArrayPool<int>.Shared.Rent(text.Length);
        var levels = ArrayPool<byte>.Shared.Rent(text.Length);
        var scripts = ArrayPool<ShapingScript>.Shared.Rent(text.Length);
        try
        {
            var count = Decode(text, codePoints, clusters);
            var baseLevel = vertical ? Bidi.LeftToRight : BaseLevel(options.Direction, codePoints.AsSpan(0, count));
            levels.AsSpan(0, count).Clear();
            if (!vertical)
            {
                Bidi.ResolveLevels(codePoints.AsSpan(0, count), baseLevel, levels.AsSpan(0, count));
            }

            ResolveScripts(codePoints.AsSpan(0, count), options.Script, scripts.AsSpan(0, count));
            ShapeSegments(new(codePoints, clusters, levels, scripts, count), options, vertical, output);
            return baseLevel == Bidi.RightToLeft;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(codePoints);
            ArrayPool<int>.Shared.Return(clusters);
            ArrayPool<byte>.Shared.Return(levels);
            ArrayPool<ShapingScript>.Shared.Return(scripts, true);
        }
    }

    /// <summary>Splits text into characters.</summary>
    /// <param name="text">The text.</param>
    /// <param name="codePoints">Receives the characters.</param>
    /// <param name="clusters">Receives the UTF-16 index of each character.</param>
    /// <returns>The number of characters.</returns>
    private static int Decode(ReadOnlySpan<char> text, int[] codePoints, int[] clusters)
    {
        var count = 0;
        var index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            codePoints[count] = rune.Value;
            clusters[count] = index;
            count++;
            index += rune.Utf16SequenceLength;
        }

        return count;
    }

    /// <summary>Gets the paragraph level for a direction setting.</summary>
    /// <param name="direction">The setting.</param>
    /// <param name="codePoints">The text.</param>
    /// <returns>0 or 1.</returns>
    private static byte BaseLevel(PdfTextDirection direction, ReadOnlySpan<int> codePoints) => direction switch
    {
        PdfTextDirection.LeftToRight => Bidi.LeftToRight,
        PdfTextDirection.RightToLeft => Bidi.RightToLeft,
        _ => Bidi.FirstStrongLevel(codePoints),
    };

    /// <summary>Finds the script of each character; characters of no script take the script of their neighbours.</summary>
    /// <param name="codePoints">The text.</param>
    /// <param name="named">The script the caller named, or <see langword="null"/>.</param>
    /// <param name="scripts">Receives the script of each character.</param>
    private static void ResolveScripts(ReadOnlySpan<int> codePoints, string? named, Span<ShapingScript> scripts)
    {
        if (!string.IsNullOrEmpty(named))
        {
            scripts.Fill(UnicodeScripts.Named(named));
            return;
        }

        var current = ShapingScript.Common;
        for (var i = 0; i < codePoints.Length; i++)
        {
            var script = UnicodeScripts.Of(codePoints[i]);
            current = script.IsCommon ? current : script;
            scripts[i] = current;
        }

        var next = ShapingScript.Common;
        for (var i = codePoints.Length - 1; i >= 0; i--)
        {
            next = scripts[i].IsCommon ? next : scripts[i];
            scripts[i] = next;
        }
    }

    /// <summary>Puts shaped runs in visual order (Unicode Standard Annex #9, rule L2) and reverses the glyphs of right-to-left runs.</summary>
    /// <param name="output">The glyphs, in logical order.</param>
    /// <param name="first">The index of the first glyph of the text.</param>
    /// <param name="segments">The runs in logical order.</param>
    private static void Reorder(List<PdfShapedGlyph> output, int first, ReadOnlySpan<RunSpan> segments)
    {
        if (segments.Length == 1)
        {
            if ((segments[0].Level & 1) != 0)
            {
                output.Reverse(segments[0].Start, segments[0].Count);
            }

            return;
        }

        var order = new int[segments.Length];
        var flips = new bool[segments.Length];
        VisualOrder(segments, order, flips);
        var glyphs = CollectionsMarshal.AsSpan(output);
        var copy = ArrayPool<PdfShapedGlyph>.Shared.Rent(output.Count - first);
        try
        {
            glyphs[first..].CopyTo(copy);
            var at = first;
            foreach (var index in order)
            {
                var segment = segments[index];
                var target = glyphs.Slice(at, segment.Count);
                copy.AsSpan(segment.Start - first, segment.Count).CopyTo(target);
                if (flips[index])
                {
                    target.Reverse();
                }

                at += segment.Count;
            }
        }
        finally
        {
            ArrayPool<PdfShapedGlyph>.Shared.Return(copy);
        }
    }

    /// <summary>Works out the visual order of runs: from the highest level down to the lowest odd level, each sequence of runs at that level or above is reversed.</summary>
    /// <param name="segments">The runs in logical order.</param>
    /// <param name="order">Receives the runs' indexes in visual order.</param>
    /// <param name="flips">Receives whether each run's glyphs are reversed, which they are when the run is reversed an odd number of times.</param>
    private static void VisualOrder(ReadOnlySpan<RunSpan> segments, int[] order, bool[] flips)
    {
        var highest = 0;
        var lowestOdd = int.MaxValue;
        for (var i = 0; i < segments.Length; i++)
        {
            order[i] = i;
            highest = Math.Max(highest, segments[i].Level);
            lowestOdd = (segments[i].Level & 1) != 0 ? Math.Min(lowestOdd, segments[i].Level) : lowestOdd;
        }

        for (var level = highest; level >= lowestOdd; level--)
        {
            ReverseRunsAtOrAbove(segments, order, flips, level);
        }
    }

    /// <summary>Reverses each sequence of runs whose level is at least a level.</summary>
    /// <param name="segments">The runs in logical order.</param>
    /// <param name="order">The runs' indexes in their present order, changed in place.</param>
    /// <param name="flips">Whether each run is reversed, changed in place.</param>
    /// <param name="level">The level.</param>
    private static void ReverseRunsAtOrAbove(ReadOnlySpan<RunSpan> segments, int[] order, bool[] flips, int level)
    {
        var i = 0;
        while (i < order.Length)
        {
            if (segments[order[i]].Level < level)
            {
                i++;
                continue;
            }

            var end = i;
            while (end < order.Length && segments[order[end]].Level >= level)
            {
                flips[order[end]] = !flips[order[end]];
                end++;
            }

            Array.Reverse(order, i, end - i);
            i = end;
        }
    }

    /// <summary>Shapes each run of one script and level and puts the runs in visual order.</summary>
    /// <param name="text">The characters, with their clusters, levels and scripts.</param>
    /// <param name="options">The shaping options.</param>
    /// <param name="vertical">Whether the text is written vertically.</param>
    /// <param name="output">Receives the glyphs.</param>
    private void ShapeSegments(in RunText text, PdfShapingOptions options, bool vertical, List<PdfShapedGlyph> output)
    {
        var first = output.Count;
        var language = OtLanguages.TagFor(options.Language);
        var segments = ArrayPool<RunSpan>.Shared.Rent(text.Count);
        try
        {
            var segmentCount = 0;
            var start = 0;
            while (start < text.Count)
            {
                var end = start + 1;
                while (end < text.Count && text.Levels[end] == text.Levels[start] && ReferenceEquals(text.Scripts[end], text.Scripts[start]))
                {
                    end++;
                }

                var before = output.Count;
                var kind = new SegmentKind(text.Scripts[start], (text.Levels[start] & 1) != 0, vertical);
                var plan = _layout.PlanFor(kind.Script, language, kind.RightToLeft, vertical, options.Features);
                SegmentShaper.Shape(_source, _layout, plan, kind, text.CodePoints.AsSpan(start, end - start), text.Clusters.AsSpan(start, end - start), output);
                segments[segmentCount] = new(before, output.Count - before, text.Levels[start]);
                segmentCount++;
                start = end;
            }

            Reorder(output, first, segments.AsSpan(0, segmentCount));
        }
        finally
        {
            ArrayPool<RunSpan>.Shared.Return(segments);
        }
    }

    /// <summary>The characters of a text with what is known about each.</summary>
    /// <param name="CodePoints">The characters.</param>
    /// <param name="Clusters">The UTF-16 index of each character.</param>
    /// <param name="Levels">The embedding level of each character.</param>
    /// <param name="Scripts">The script of each character.</param>
    /// <param name="Count">The number of characters.</param>
    private readonly record struct RunText(int[] CodePoints, int[] Clusters, byte[] Levels, ShapingScript[] Scripts, int Count);

    /// <summary>A shaped run of glyphs in the output.</summary>
    /// <param name="Start">The index of its first glyph.</param>
    /// <param name="Count">The number of glyphs.</param>
    /// <param name="Level">The embedding level of its characters.</param>
    private readonly record struct RunSpan(int Start, int Count, byte Level);
}
