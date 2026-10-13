// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// What one page draws, glyph by glyph in drawing order, with the marked content each glyph is in. Structure elements
/// map to the page through it: a marked content id gives its glyphs, their text and their boxes. Never changes once
/// recorded, so it is safe to read from any thread.
/// </summary>
[DebuggerDisplay("PdfMarkedContentPage: page {PageIndex}, {GlyphCount} glyphs")]
public sealed class PdfMarkedContentPage
{
    /// <summary>The glyphs in drawing order.</summary>
    private readonly PdfMarkedGlyph[] _glyphs;

    /// <summary>The glyph indices grouped by marked content id, each group in drawing order.</summary>
    private readonly int[] _order;

    /// <summary>Each marked content id's group in <see cref="_order"/>.</summary>
    private readonly Dictionary<int, McidRange> _ranges;

    /// <summary>The bounds of what each marked content id paints other than text: paths, images and shadings.</summary>
    private readonly Dictionary<int, PdfViewerRect> _graphics;

    /// <summary>Initializes a new instance of the <see cref="PdfMarkedContentPage"/> class.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="text">The glyphs' Unicode text, back to back.</param>
    /// <param name="glyphs">The glyphs in drawing order.</param>
    /// <param name="graphics">The bounds of each marked content id's paths and images.</param>
    /// <param name="hasImages">Whether the page draws any image.</param>
    internal PdfMarkedContentPage(int pageIndex, string text, PdfMarkedGlyph[] glyphs, Dictionary<int, PdfViewerRect> graphics, bool hasImages)
    {
        PageIndex = pageIndex;
        Text = text;
        HasImages = hasImages;
        _glyphs = glyphs;
        _graphics = graphics;
        _ranges = [];
        _order = GroupByMcid(glyphs, _ranges);
        CountCharacters();
    }

    /// <summary>Gets the zero based page index.</summary>
    public int PageIndex { get; }

    /// <summary>Gets the glyphs' Unicode text, back to back; each glyph's slice is given by its text start and length.</summary>
    public string Text { get; }

    /// <summary>Gets the glyphs in drawing order.</summary>
    public ReadOnlySpan<PdfMarkedGlyph> Glyphs => _glyphs;

    /// <summary>Gets the number of glyphs.</summary>
    public int GlyphCount => _glyphs.Length;

    /// <summary>Gets a value indicating whether the page draws any image.</summary>
    public bool HasImages { get; }

    /// <summary>Gets a value indicating whether the page draws images and no text, so only text recognition can read it.</summary>
    public bool IsImageOnly => _glyphs.Length == 0 && HasImages;

    /// <summary>Gets the number of visible characters (not white space) drawn in marked content with an id, outside artifacts.</summary>
    public int MarkedCharacterCount { get; private set; }

    /// <summary>Gets the number of visible characters (not white space) drawn.</summary>
    public int VisibleCharacterCount { get; private set; }

    /// <summary>Gets a glyph's Unicode text.</summary>
    /// <param name="glyphIndex">The glyph.</param>
    /// <returns>The text; empty when the font gives none.</returns>
    public ReadOnlySpan<char> GetText(int glyphIndex)
    {
        var glyph = _glyphs[glyphIndex];
        return Text.AsSpan(glyph.TextStart, glyph.TextLength);
    }

    /// <summary>Gets the glyphs drawn in a marked content id, in drawing order.</summary>
    /// <param name="mcid">The marked content id.</param>
    /// <returns>The glyph indices; empty when the id draws no text.</returns>
    public ReadOnlySpan<int> GetGlyphs(int mcid) =>
        _ranges.TryGetValue(mcid, out var range) ? _order.AsSpan(range.Start, range.Count) : [];

    /// <summary>Gets the box around everything a marked content id draws: its glyphs, paths and images.</summary>
    /// <param name="mcid">The marked content id.</param>
    /// <returns>The box in viewer space; empty when the id draws nothing.</returns>
    public PdfViewerRect GetBounds(int mcid)
    {
        var bounds = _graphics.GetValueOrDefault(mcid);
        foreach (var index in GetGlyphs(mcid))
        {
            bounds = bounds.Union(_glyphs[index].Bounds);
        }

        return bounds;
    }

    /// <summary>Determines whether a marked content id draws anything on the page.</summary>
    /// <param name="mcid">The marked content id.</param>
    /// <returns><see langword="true"/> when it draws text, paths or images.</returns>
    public bool HasContent(int mcid) => _ranges.ContainsKey(mcid) || _graphics.ContainsKey(mcid);

    /// <summary>Groups glyph indices by marked content id, keeping drawing order inside each group.</summary>
    /// <param name="glyphs">The glyphs.</param>
    /// <param name="ranges">Receives each id's group.</param>
    /// <returns>The grouped indices.</returns>
    private static int[] GroupByMcid(PdfMarkedGlyph[] glyphs, Dictionary<int, McidRange> ranges)
    {
        var ids = new List<int>();
        foreach (var glyph in glyphs)
        {
            if (glyph.Mcid < 0)
            {
                continue;
            }

            ref var counted = ref CollectionsMarshal.GetValueRefOrAddDefault(ranges, glyph.Mcid, out var exists);
            if (!exists)
            {
                ids.Add(glyph.Mcid);
            }

            counted = counted with { Count = counted.Count + 1 };
        }

        var start = 0;
        foreach (var mcid in ids)
        {
            var count = ranges[mcid].Count;
            ranges[mcid] = new(start, 0);
            start += count;
        }

        var order = new int[start];
        for (var i = 0; i < glyphs.Length; i++)
        {
            var mcid = glyphs[i].Mcid;
            if (mcid < 0)
            {
                continue;
            }

            var range = ranges[mcid];
            order[range.Start + range.Count] = i;
            ranges[mcid] = range with { Count = range.Count + 1 };
        }

        return order;
    }

    /// <summary>Counts the characters of a glyph that are not white space; a glyph with no text counts as one.</summary>
    /// <param name="text">The glyph's text.</param>
    /// <returns>The count.</returns>
    private static int CountVisible(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return 1;
        }

        var count = 0;
        foreach (var character in text)
        {
            count += char.IsWhiteSpace(character) ? 0 : 1;
        }

        return count;
    }

    /// <summary>Counts the visible characters, and those in marked content, so callers can tell how much of the page the structure tree covers.</summary>
    private void CountCharacters()
    {
        for (var i = 0; i < _glyphs.Length; i++)
        {
            var visible = CountVisible(GetText(i));
            VisibleCharacterCount += visible;
            if (_glyphs[i].Mcid >= 0 && !_glyphs[i].IsArtifact)
            {
                MarkedCharacterCount += visible;
            }
        }
    }
}
