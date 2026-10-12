// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Core.Text.Layout;

/// <summary>
/// Lays a text box out: shapes each paragraph, wraps lines at spaces (or anywhere in CJK text) to a width, applies
/// character and line spacing, aligns lines, and for comb fields puts one character centred in each box. Positions
/// are in points from the box's top-left corner. The instance keeps its buffers, so laying out again allocates nothing.
/// </summary>
[DebuggerDisplay("TextBoxLayout: {Lines.Count} lines")]
public sealed class TextBoxLayout
{
    /// <summary>Halves a length.</summary>
    private const float Half = 0.5F;

    /// <summary>The first code point of the CJK radicals, where any character may start a line.</summary>
    private const int CjkStart = 0x2E80;

    /// <summary>The last code point of the CJK compatibility ideographs.</summary>
    private const int CjkEnd = 0xFAFF;

    /// <summary>The length of a carriage return and line feed.</summary>
    private const int CrLfLength = 2;

    /// <summary>The glyphs of the paragraph being laid out.</summary>
    private readonly List<ShapedGlyph> _shaped = [];

    /// <summary>The placed glyphs.</summary>
    private readonly List<LaidGlyph> _glyphs = [];

    /// <summary>The lines.</summary>
    private readonly List<LaidLine> _lines = [];

    /// <summary>The paragraph's glyph advances, in points, spacing included.</summary>
    private readonly List<float> _advances = [];

    /// <summary>Gets the placed glyphs, line after line.</summary>
    public ReadOnlySpan<LaidGlyph> Glyphs => CollectionsMarshal.AsSpan(_glyphs);

    /// <summary>Gets the lines.</summary>
    public IReadOnlyList<LaidLine> Lines => _lines;

    /// <summary>Gets the box width: the wrap width, or the widest line.</summary>
    public float Width { get; private set; }

    /// <summary>Gets the box height: the line height times the number of lines.</summary>
    public float Height { get; private set; }

    /// <summary>Gets how far an underline sits below each baseline, in points.</summary>
    public float UnderlineOffset { get; private set; }

    /// <summary>Gets how thick an underline is, in points.</summary>
    public float UnderlineThickness { get; private set; }

    /// <summary>Gets the line height, in points.</summary>
    public float LineHeight { get; private set; }

    /// <summary>
    /// Gets how far below the box's top the first baseline sits, in points. Each line is a box of the line height with
    /// the font's extent centred in it, as in CSS.
    /// </summary>
    /// <param name="format">How the text looks.</param>
    /// <param name="shaper">The font's shaper.</param>
    /// <returns>The baseline's depth.</returns>
    public static float FirstBaseline(TextFormat format, ITextShaper shaper)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(shaper);
        var size = format.FontSize;
        return (((size * format.LineSpacing) - ((shaper.Ascent - shaper.Descent) * size)) * Half) + (shaper.Ascent * size);
    }

    /// <summary>Lays text out.</summary>
    /// <param name="text">The text; line breaks start new paragraphs.</param>
    /// <param name="format">How the text looks.</param>
    /// <param name="shaper">The font's shaper.</param>
    /// <param name="wrapWidth">The width lines wrap at, in points, or 0 to break only where the text does.</param>
    public void Layout(ReadOnlySpan<char> text, TextFormat format, ITextShaper shaper, float wrapWidth)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(shaper);
        _glyphs.Clear();
        _lines.Clear();
        var size = format.FontSize;
        LineHeight = size * format.LineSpacing;
        UnderlineOffset = -shaper.UnderlinePosition * size;
        UnderlineThickness = shaper.UnderlineThickness * size;

        var context = new LineContext(format, FirstBaseline(format, shaper), Math.Max(wrapWidth, 0));
        if (format.CombCells > 0 && wrapWidth > 0)
        {
            LayoutComb(text, format, shaper, context);
        }
        else
        {
            LayoutParagraphs(text, shaper, context);
        }

        var widest = 0F;
        foreach (var line in _lines)
        {
            widest = Math.Max(widest, line.Width);
        }

        Width = wrapWidth > 0 ? wrapWidth : widest;
        Height = Math.Max(_lines.Count, 1) * LineHeight;
        if (format.CombCells == 0)
        {
            Align(format.Alignment);
        }
    }

    /// <summary>Determines whether a line may break before or after a character anywhere, as in Chinese and Japanese.</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for CJK characters.</returns>
    private static bool BreaksAnywhere(char c) => c is >= (char)CjkStart and <= (char)CjkEnd;

    /// <summary>Gets the length of the line break at an index: two for a carriage return and line feed.</summary>
    /// <param name="text">The text.</param>
    /// <param name="at">The break's first character.</param>
    /// <returns>The break's length.</returns>
    private static int BreakLength(ReadOnlySpan<char> text, int at) => text[at..].StartsWith("\r\n") ? CrLfLength : 1;

    /// <summary>Lays each paragraph out in turn.</summary>
    /// <param name="text">The text.</param>
    /// <param name="shaper">The shaper.</param>
    /// <param name="context">The layout settings.</param>
    private void LayoutParagraphs(ReadOnlySpan<char> text, ITextShaper shaper, in LineContext context)
    {
        var start = 0;
        while (true)
        {
            var end = text[start..].IndexOfAny('\r', '\n');
            var length = end < 0 ? text.Length - start : end;
            LayoutParagraph(text, start, length, shaper, context);
            if (end < 0)
            {
                return;
            }

            start += length + BreakLength(text, start + length);
        }
    }

    /// <summary>Shapes one paragraph and breaks it into lines.</summary>
    /// <param name="text">The whole text.</param>
    /// <param name="start">The paragraph's first character.</param>
    /// <param name="length">The paragraph's length.</param>
    /// <param name="shaper">The shaper.</param>
    /// <param name="context">The layout settings.</param>
    private void LayoutParagraph(ReadOnlySpan<char> text, int start, int length, ITextShaper shaper, in LineContext context)
    {
        var paragraph = text.Slice(start, length);
        _shaped.Clear();
        var rightToLeft = shaper.Shape(paragraph, _shaped);
        if (_shaped.Count == 0)
        {
            _lines.Add(new(_glyphs.Count, 0, Baseline(context), 0, 0, start, length));
            return;
        }

        MeasureAdvances(context);
        var lineStart = 0;
        while (lineStart < _shaped.Count)
        {
            var lineEnd = context.WrapWidth > 0 ? FindBreak(paragraph, lineStart, context.WrapWidth) : _shaped.Count;
            EmitLine(paragraph, (lineStart, lineEnd), start, rightToLeft, context);
            lineStart = lineEnd;
        }
    }

    /// <summary>Works out each glyph's advance in points, adding character spacing after each character.</summary>
    /// <param name="context">The layout settings.</param>
    private void MeasureAdvances(in LineContext context)
    {
        _advances.Clear();
        for (var i = 0; i < _shaped.Count; i++)
        {
            var endsCluster = i + 1 == _shaped.Count || _shaped[i + 1].Cluster != _shaped[i].Cluster;
            _advances.Add((_shaped[i].Advance * context.Format.FontSize) + (endsCluster ? context.Format.CharacterSpacing : 0));
        }
    }

    /// <summary>Finds where the line starting at a glyph ends: after the last space that fits, or mid-word when a word is too long.</summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="lineStart">The line's first glyph.</param>
    /// <param name="wrapWidth">The wrap width.</param>
    /// <returns>The glyph after the line's last.</returns>
    private int FindBreak(ReadOnlySpan<char> paragraph, int lineStart, float wrapWidth)
    {
        var x = 0F;
        var lastBreak = -1;
        for (var i = lineStart; i < _shaped.Count; i++)
        {
            var whitespace = IsWhitespace(paragraph, i);
            if (x + _advances[i] > wrapWidth && !whitespace && i > lineStart)
            {
                return lastBreak > lineStart ? lastBreak : ClusterStart(i, lineStart);
            }

            x += _advances[i];
            if (CanBreakAfter(paragraph, i))
            {
                lastBreak = i + 1;
            }
        }

        return _shaped.Count;
    }

    /// <summary>Moves a mid-word break back to the start of a cluster, so a character is never split.</summary>
    /// <param name="index">The glyph that did not fit.</param>
    /// <param name="lineStart">The line's first glyph.</param>
    /// <returns>The glyph to break before.</returns>
    private int ClusterStart(int index, int lineStart)
    {
        var at = index;
        while (at > lineStart + 1 && _shaped[at - 1].Cluster == _shaped[at].Cluster)
        {
            at--;
        }

        return at;
    }

    /// <summary>Determines whether a line may end after a glyph.</summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="index">The glyph.</param>
    /// <returns><see langword="true"/> after a space, a hyphen or a CJK character.</returns>
    private bool CanBreakAfter(ReadOnlySpan<char> paragraph, int index)
    {
        if (index + 1 < _shaped.Count && _shaped[index + 1].Cluster == _shaped[index].Cluster)
        {
            return false;
        }

        var c = paragraph[_shaped[index].Cluster];
        var next = index + 1 < _shaped.Count ? paragraph[_shaped[index + 1].Cluster] : ' ';
        return char.IsWhiteSpace(c) || c == '-' || BreaksAnywhere(c) || BreaksAnywhere(next);
    }

    /// <summary>Determines whether a glyph shows white space.</summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="index">The glyph.</param>
    /// <returns><see langword="true"/> for spaces and tabs.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsWhitespace(ReadOnlySpan<char> paragraph, int index) => char.IsWhiteSpace(paragraph[_shaped[index].Cluster]);

    /// <summary>Places a line's glyphs, right to left for right-to-left text, and records the line.</summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="range">The line's first glyph and the glyph after its last.</param>
    /// <param name="paragraphStart">The paragraph's first character in the whole text.</param>
    /// <param name="rightToLeft">Whether the paragraph reads right to left.</param>
    /// <param name="context">The layout settings.</param>
    private void EmitLine(ReadOnlySpan<char> paragraph, (int Start, int End) range, int paragraphStart, bool rightToLeft, in LineContext context)
    {
        var width = MeasureLine(paragraph, range, context.Format.CharacterSpacing);
        var baseline = Baseline(context);
        var glyphStart = _glyphs.Count;
        var size = context.Format.FontSize;
        var x = rightToLeft ? width : 0F;
        for (var i = range.Start; i < range.End; i++)
        {
            var shaped = _shaped[i];
            x -= rightToLeft ? _advances[i] : 0;
            _glyphs.Add(new(shaped.Glyph, x + (shaped.OffsetX * size), baseline - (shaped.OffsetY * size), paragraphStart + shaped.Cluster));
            x += rightToLeft ? 0 : _advances[i];
        }

        var textStart = _shaped[range.Start].Cluster;
        var textEnd = range.End < _shaped.Count ? _shaped[range.End].Cluster : paragraph.Length;
        _lines.Add(new(glyphStart, range.End - range.Start, baseline, 0, Math.Max(width, 0), paragraphStart + textStart, textEnd - textStart));
    }

    /// <summary>Measures a line without its trailing spaces.</summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="range">The line's first glyph and the glyph after its last.</param>
    /// <param name="spacing">The character spacing.</param>
    /// <returns>The width in points.</returns>
    private float MeasureLine(ReadOnlySpan<char> paragraph, (int Start, int End) range, float spacing)
    {
        var inked = range.End;
        while (inked > range.Start && IsWhitespace(paragraph, inked - 1))
        {
            inked--;
        }

        var width = 0F;
        for (var i = range.Start; i < inked; i++)
        {
            width += _advances[i];
        }

        // Spacing after the line's last character is not part of its width.
        return inked > range.Start ? width - spacing : width;
    }

    /// <summary>Puts one character centred in each comb box, on one line; characters past the last box are left out.</summary>
    /// <param name="text">The text; only its first line is used.</param>
    /// <param name="format">The format.</param>
    /// <param name="shaper">The shaper.</param>
    /// <param name="context">The layout settings.</param>
    private void LayoutComb(ReadOnlySpan<char> text, TextFormat format, ITextShaper shaper, in LineContext context)
    {
        var end = text.IndexOfAny('\r', '\n');
        var line = end < 0 ? text : text[..end];
        _shaped.Clear();
        _ = shaper.Shape(line, _shaped);
        var cell = context.WrapWidth / format.CombCells;
        var baseline = Baseline(context);
        var size = format.FontSize;
        var used = 0;
        var i = 0;
        while (i < _shaped.Count && used < format.CombCells)
        {
            var clusterEnd = i;
            var advance = 0F;
            while (clusterEnd < _shaped.Count && _shaped[clusterEnd].Cluster == _shaped[i].Cluster)
            {
                advance += _shaped[clusterEnd].Advance * size;
                clusterEnd++;
            }

            var x = (used * cell) + ((cell - advance) * Half);
            for (var g = i; g < clusterEnd; g++)
            {
                var shaped = _shaped[g];
                _glyphs.Add(new(shaped.Glyph, x + (shaped.OffsetX * size), baseline - (shaped.OffsetY * size), shaped.Cluster));
                x += shaped.Advance * size;
            }

            used++;
            i = clusterEnd;
        }

        var textLength = i < _shaped.Count ? _shaped[i].Cluster : line.Length;
        _lines.Add(new(0, _glyphs.Count, baseline, 0, context.WrapWidth, 0, textLength));
    }

    /// <summary>Gets the baseline of the next line.</summary>
    /// <param name="context">The layout settings.</param>
    /// <returns>The baseline, in points down from the top.</returns>
    private float Baseline(in LineContext context) => context.Ascent + (_lines.Count * LineHeight);

    /// <summary>Moves each line across the box as the alignment asks.</summary>
    /// <param name="alignment">The alignment.</param>
    private void Align(TextBoxAlignment alignment)
    {
        if (alignment == TextBoxAlignment.Left)
        {
            return;
        }

        var glyphs = CollectionsMarshal.AsSpan(_glyphs);
        for (var l = 0; l < _lines.Count; l++)
        {
            var line = _lines[l];
            var shift = alignment == TextBoxAlignment.Center ? (Width - line.Width) * Half : Width - line.Width;
            foreach (ref var glyph in glyphs.Slice(line.GlyphStart, line.GlyphCount))
            {
                glyph = glyph with { X = glyph.X + shift };
            }

            _lines[l] = line with { Left = line.Left + shift };
        }
    }

    /// <summary>The settings every line of one layout shares.</summary>
    /// <param name="Format">The format.</param>
    /// <param name="Ascent">The first baseline's depth below the top, in points.</param>
    /// <param name="WrapWidth">The wrap width, or 0.</param>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct LineContext(TextFormat Format, float Ascent, float WrapWidth);
}
