// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Fonts.Shaping;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Resolves Unicode grapheme clusters across a primary <see cref="PdfCanvasFont"/>, its configured fallback chain,
/// and any document-level default backup fonts.
/// </summary>
internal static class FontFallbackResolver
{
    /// <summary>The first character that is not ASCII.</summary>
    private const char FirstNonAscii = '\u0080';

    /// <summary>The carriage return, which forms one cluster with a following line feed.</summary>
    private const char CarriageReturn = '\r';

    /// <summary>The first combining mark; no character that extends a cluster is below it.</summary>
    private const char FirstExtender = '̀';

    /// <summary>Zero-width non-joiner code point (<c>U+200C</c>).</summary>
    private const int ZeroWidthNonJoiner = 0x200C;

    /// <summary>Zero-width joiner code point (<c>U+200D</c>).</summary>
    private const int ZeroWidthJoiner = 0x200D;

    /// <summary>First regional indicator symbol code point (<c>U+1F1E6</c>).</summary>
    private const int FirstRegionalIndicator = 0x1F1E6;

    /// <summary>Last regional indicator symbol code point (<c>U+1F1FF</c>).</summary>
    private const int LastRegionalIndicator = 0x1F1FF;

    /// <summary>First Fitzpatrick emoji skin-tone modifier (<c>U+1F3FB</c>).</summary>
    private const int FirstEmojiModifier = 0x1F3FB;

    /// <summary>Last Fitzpatrick emoji skin-tone modifier (<c>U+1F3FF</c>).</summary>
    private const int LastEmojiModifier = 0x1F3FF;

    /// <summary>First BMP variation selector (<c>U+FE00</c>).</summary>
    private const int FirstVariationSelector = 0xFE00;

    /// <summary>Last BMP variation selector (<c>U+FE0F</c>).</summary>
    private const int LastVariationSelector = 0xFE0F;

    /// <summary>First supplementary variation selector (<c>U+E0100</c>).</summary>
    private const int FirstVariationSelectorSupplement = 0xE0100;

    /// <summary>Last supplementary variation selector (<c>U+E01EF</c>).</summary>
    private const int LastVariationSelectorSupplement = 0xE01EF;

    /// <summary>First emoji tag character (<c>U+E0020</c>).</summary>
    private const int FirstTagCharacter = 0xE0020;

    /// <summary>Cancel tag character (<c>U+E007F</c>).</summary>
    private const int LastTagCharacter = 0xE007F;

    /// <summary>
    /// Returns a <see cref="PdfCanvasFont"/> with <paramref name="scopedFallbacks"/> applied when specified,
    /// or <paramref name="font"/> unchanged when <paramref name="scopedFallbacks"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="font">The base font.</param>
    /// <param name="scopedFallbacks">The scoped fallback list to apply, or <see langword="null"/> to keep the font's existing fallbacks.</param>
    /// <returns>The effective font with fallback configuration applied.</returns>
    internal static PdfCanvasFont ApplyFallbacks(PdfCanvasFont font, IReadOnlyList<PdfCanvasFont>? scopedFallbacks) =>
        scopedFallbacks is null || ReferenceEquals(font.Fallbacks, scopedFallbacks)
            ? font
            : font.WithFallbacks(scopedFallbacks);

    /// <summary>
    /// Resolves a scoped font and fallback list against an <paramref name="inheritedFont"/>, inheriting the parent's
    /// fallback list when neither <paramref name="explicitFallbacks"/> nor the chosen font's own <see cref="PdfCanvasFont.Fallbacks"/>
    /// specifies one.
    /// </summary>
    /// <param name="explicitFont">The font configured at the current scope, or <see langword="null"/> to inherit <paramref name="inheritedFont"/>.</param>
    /// <param name="explicitFallbacks">The fallback list configured at the current scope, or <see langword="null"/> to inherit.</param>
    /// <param name="inheritedFont">The resolved font from the enclosing scope.</param>
    /// <returns>The effective <see cref="PdfCanvasFont"/> for the current scope.</returns>
    internal static PdfCanvasFont ResolveScopedFont(
        PdfCanvasFont? explicitFont,
        IReadOnlyList<PdfCanvasFont>? explicitFallbacks,
        PdfCanvasFont inheritedFont)
    {
        var baseFont = explicitFont ?? inheritedFont;
        var fallbacks = explicitFallbacks
            ?? (baseFont.Fallbacks.Count == 0 && inheritedFont.Fallbacks.Count > 0 ? inheritedFont.Fallbacks : null);
        return ApplyFallbacks(baseFont, fallbacks);
    }

    /// <summary>Measures the advance width in points of <paramref name="text"/> at <paramref name="fontSize"/> across resolved fallback runs.</summary>
    /// <param name="primary">The primary font.</param>
    /// <param name="text">The text to measure.</param>
    /// <param name="fontSize">The font size in points.</param>
    /// <returns>The total advance width in points.</returns>
    internal static float Measure(PdfCanvasFont primary, ReadOnlySpan<char> text, float fontSize)
    {
        if (text.IsEmpty)
        {
            return 0F;
        }

        if (!HasFallbackCandidates(primary))
        {
            return primary.MeasurePrimary(text, fontSize);
        }

        var total = 0F;
        int runLength;
        for (var offset = 0; offset < text.Length; offset += runLength)
        {
            var remaining = text.Slice(offset);
            runLength = ResolveNextRun(primary, remaining, out var runFont);
            total += runFont.MeasurePrimary(remaining.Slice(0, runLength), fontSize);
        }

        return total;
    }

    /// <summary>Splits <paramref name="text"/> into contiguous runs resolved against <paramref name="primary"/> and its fallbacks.</summary>
    /// <param name="primary">The primary font.</param>
    /// <param name="text">The text to split.</param>
    /// <param name="runs">The destination list to populate with resolved runs (cleared before adding).</param>
    internal static void SplitRuns(PdfCanvasFont primary, ReadOnlySpan<char> text, List<PdfFontFallbackRun> runs)
    {
        runs.Clear();
        if (text.IsEmpty)
        {
            return;
        }

        if (!HasFallbackCandidates(primary))
        {
            runs.Add(new(0, text.Length, primary));
            return;
        }

        int runLength;
        for (var offset = 0; offset < text.Length; offset += runLength)
        {
            runLength = ResolveNextRun(primary, text.Slice(offset), out var runFont);
            runs.Add(new(offset, runLength, runFont));
        }
    }

    /// <summary>Resolves the next contiguous run starting at index <c>0</c> of <paramref name="text"/> that uses a single font.</summary>
    /// <param name="primary">The primary font.</param>
    /// <param name="text">The remaining text to inspect.</param>
    /// <param name="runFont">Receives the font selected for the run.</param>
    /// <returns>The UTF-16 character length of the resolved run.</returns>
    internal static int ResolveNextRun(PdfCanvasFont primary, ReadOnlySpan<char> text, out PdfCanvasFont runFont)
    {
        if (text.IsEmpty || !HasFallbackCandidates(primary))
        {
            runFont = primary;
            return text.Length;
        }

        var offset = GetNextClusterLength(text);
        runFont = SelectFontForCluster(primary, text.Slice(0, offset));

        while (offset < text.Length)
        {
            var clusterLength = GetNextClusterLength(text.Slice(offset));
            var nextFont = SelectFontForCluster(primary, text.Slice(offset, clusterLength));
            if (!ReferenceEquals(nextFont, runFont))
            {
                break;
            }

            offset += clusterLength;
        }

        return offset;
    }

    /// <summary>Checks whether <paramref name="font"/> contains glyphs for all non-ignorable code points in <paramref name="cluster"/>.</summary>
    /// <param name="font">The canvas font to inspect.</param>
    /// <param name="cluster">The UTF-16 grapheme cluster slice.</param>
    /// <returns><see langword="true"/> when <paramref name="font"/> can render <paramref name="cluster"/> without <c>.notdef</c>.</returns>
    internal static bool SupportsCluster(PdfCanvasFont font, ReadOnlySpan<char> cluster)
    {
        if (cluster.IsEmpty)
        {
            return true;
        }

        _ = Rune.DecodeFromUtf16(cluster, out var firstRune, out var firstConsumed);
        return firstConsumed >= cluster.Length
            ? font.HasGlyph(firstRune)
            : SupportsMultiRuneCluster(font, cluster, firstRune);
    }

    /// <summary>Checks whether <paramref name="source"/> contains glyphs for all non-ignorable code points in <paramref name="cluster"/>.</summary>
    /// <param name="source">The font source to inspect.</param>
    /// <param name="cluster">The UTF-16 grapheme cluster slice.</param>
    /// <returns><see langword="true"/> when <paramref name="source"/> can render <paramref name="cluster"/> without <c>.notdef</c>.</returns>
    internal static bool SupportsCluster(PdfFontSource source, ReadOnlySpan<char> cluster)
    {
        if (cluster.IsEmpty)
        {
            return true;
        }

        _ = Rune.DecodeFromUtf16(cluster, out var firstRune, out var firstConsumed);
        return firstConsumed >= cluster.Length
            ? source.HasGlyph(firstRune)
            : SupportsMultiRuneCluster(source, cluster, firstRune);
    }

    /// <summary>Determines whether <paramref name="primary"/> has any explicit or default fallback candidates.</summary>
    /// <param name="primary">The primary font.</param>
    /// <returns><see langword="true"/> when at least one explicit or default fallback is available.</returns>
    internal static bool HasFallbackCandidates(PdfCanvasFont primary) =>
        primary.Fallbacks.Count > 0 || primary.FallbackHost is not null;

    /// <summary>Computes the UTF-16 length of the first extended grapheme / emoji cluster in <paramref name="text"/>.</summary>
    /// <param name="text">The non-empty text span.</param>
    /// <returns>The UTF-16 character length of the leading cluster.</returns>
    internal static int GetNextClusterLength(ReadOnlySpan<char> text)
    {
        // An ASCII character stands alone unless it is a CR (which joins a following LF) or a joiner, mark or other extender follows; nothing that extends a cluster is below U+0300.
        if (text[0] is < FirstNonAscii and not CarriageReturn && (text.Length == 1 || text[1] < FirstExtender))
        {
            return 1;
        }

        _ = Rune.DecodeFromUtf16(text, out var firstRune, out var consumed);
        if (consumed <= 0)
        {
            return 1;
        }

        if (IsRegionalIndicator(firstRune.Value) && consumed < text.Length)
        {
            _ = Rune.DecodeFromUtf16(text.Slice(consumed), out var secondRune, out var secondConsumed);
            if (secondConsumed > 0 && IsRegionalIndicator(secondRune.Value))
            {
                consumed += secondConsumed;
            }
        }

        var clusterLength = ConsumeClusterContinuations(text, consumed);
        return Math.Max(clusterLength, StringInfo.GetNextTextElementLength(text));
    }

    /// <summary>Selects the first font that supports <paramref name="cluster"/>, falling back to <paramref name="primary"/> if none does.</summary>
    /// <param name="primary">The primary font.</param>
    /// <param name="cluster">The UTF-16 grapheme cluster slice.</param>
    /// <returns>The selected font.</returns>
    private static PdfCanvasFont SelectFontForCluster(PdfCanvasFont primary, ReadOnlySpan<char> cluster)
    {
        if (SupportsCluster(primary, cluster))
        {
            return primary;
        }

        var fallbacks = primary.Fallbacks;
        for (var i = 0; i < fallbacks.Count; i++)
        {
            var candidate = fallbacks[i];
            if (SupportsCluster(candidate, cluster))
            {
                return candidate;
            }
        }

        return primary.FallbackHost is { } host && host.TryResolveDefaultFallback(cluster, out var defaultFallback)
            ? defaultFallback
            : primary;
    }

    /// <summary>Checks whether a multi-rune cluster is supported by <paramref name="font"/>.</summary>
    /// <param name="font">The canvas font to inspect.</param>
    /// <param name="cluster">The multi-rune cluster.</param>
    /// <param name="firstRune">The first decoded rune of the cluster.</param>
    /// <returns><see langword="true"/> when all visible runes in the cluster exist in <paramref name="font"/>.</returns>
    private static bool SupportsMultiRuneCluster(PdfCanvasFont font, ReadOnlySpan<char> cluster, Rune firstRune)
    {
        var offset = 0;
        var checkedAny = false;
        while (offset < cluster.Length)
        {
            _ = Rune.DecodeFromUtf16(cluster.Slice(offset), out var rune, out var consumed);
            if (consumed <= 0)
            {
                return false;
            }

            offset += consumed;
            if (ShapingCharacters.IsDefaultIgnorable(rune.Value))
            {
                continue;
            }

            checkedAny = true;
            if (!font.HasGlyph(rune))
            {
                return false;
            }
        }

        return checkedAny || font.HasGlyph(firstRune);
    }

    /// <summary>Checks whether a multi-rune cluster is supported by <paramref name="source"/>.</summary>
    /// <param name="source">The font source to inspect.</param>
    /// <param name="cluster">The multi-rune cluster.</param>
    /// <param name="firstRune">The first decoded rune of the cluster.</param>
    /// <returns><see langword="true"/> when all visible runes in the cluster exist in <paramref name="source"/>.</returns>
    private static bool SupportsMultiRuneCluster(PdfFontSource source, ReadOnlySpan<char> cluster, Rune firstRune)
    {
        var offset = 0;
        var checkedAny = false;
        while (offset < cluster.Length)
        {
            _ = Rune.DecodeFromUtf16(cluster.Slice(offset), out var rune, out var consumed);
            if (consumed <= 0)
            {
                return false;
            }

            offset += consumed;
            if (ShapingCharacters.IsDefaultIgnorable(rune.Value))
            {
                continue;
            }

            checkedAny = true;
            if (!source.HasGlyph(rune))
            {
                return false;
            }
        }

        return checkedAny || source.HasGlyph(firstRune);
    }

    /// <summary>Advances <paramref name="offset"/> past any combining marks, variation selectors, skin-tone modifiers, or ZWJ sequences.</summary>
    /// <param name="text">The text span being segmented.</param>
    /// <param name="offset">The UTF-16 offset after the initial base rune.</param>
    /// <returns>The UTF-16 offset at the end of the cluster.</returns>
    private static int ConsumeClusterContinuations(ReadOnlySpan<char> text, int offset)
    {
        while (offset < text.Length)
        {
            _ = Rune.DecodeFromUtf16(text.Slice(offset), out var rune, out var step);
            if (step <= 0)
            {
                break;
            }

            if (rune.Value == ZeroWidthJoiner)
            {
                offset = ConsumeJoinedRune(text, offset + step);
                continue;
            }

            if (!IsClusterContinuation(rune))
            {
                break;
            }

            offset += step;
        }

        return offset;
    }

    /// <summary>Consumes the rune immediately following a zero-width joiner (<c>U+200D</c>).</summary>
    /// <param name="text">The text span being segmented.</param>
    /// <param name="afterJoinerOffset">The UTF-16 offset immediately after the ZWJ.</param>
    /// <returns>The UTF-16 offset after the joined rune, or <paramref name="afterJoinerOffset"/> if at end of span.</returns>
    private static int ConsumeJoinedRune(ReadOnlySpan<char> text, int afterJoinerOffset)
    {
        if (afterJoinerOffset >= text.Length)
        {
            return afterJoinerOffset;
        }

        _ = Rune.DecodeFromUtf16(text.Slice(afterJoinerOffset), out _, out var joinedStep);
        return joinedStep > 0 ? afterJoinerOffset + joinedStep : afterJoinerOffset;
    }

    /// <summary>Determines whether <paramref name="rune"/> continues the preceding grapheme cluster.</summary>
    /// <param name="rune">The rune to inspect.</param>
    /// <returns><see langword="true"/> when <paramref name="rune"/> attaches to the preceding base character.</returns>
    private static bool IsClusterContinuation(Rune rune)
    {
        if (IsVariationSelectorOrModifier(rune.Value))
        {
            return true;
        }

        var category = Rune.GetUnicodeCategory(rune);
        return category is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark;
    }

    /// <summary>Determines whether <paramref name="codePoint"/> is a variation selector, emoji modifier, tag character, or ZWNJ.</summary>
    /// <param name="codePoint">The Unicode scalar value.</param>
    /// <returns><see langword="true"/> when the code point modifies or extends the preceding character.</returns>
    private static bool IsVariationSelectorOrModifier(int codePoint) =>
        codePoint is (>= FirstVariationSelector and <= LastVariationSelector)
            or (>= FirstEmojiModifier and <= LastEmojiModifier)
            or (>= FirstVariationSelectorSupplement and <= LastVariationSelectorSupplement)
            or (>= FirstTagCharacter and <= LastTagCharacter)
            or ZeroWidthNonJoiner;

    /// <summary>Determines whether <paramref name="codePoint"/> is a Regional Indicator symbol (<c>U+1F1E6..U+1F1FF</c>).</summary>
    /// <param name="codePoint">The Unicode scalar value.</param>
    /// <returns><see langword="true"/> when <paramref name="codePoint"/> is a regional indicator.</returns>
    private static bool IsRegionalIndicator(int codePoint) =>
        codePoint is >= FirstRegionalIndicator and <= LastRegionalIndicator;
}
