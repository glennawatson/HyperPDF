// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Creation;

/// <summary>Accumulates inline styled segments onto wrapped lines.</summary>
internal sealed class RichLineAccumulator
{
    /// <summary>The characters in a carriage return followed by a line feed.</summary>
    private const int CrLfLength = 2;

    /// <summary>Tolerance for comparing segment font sizes.</summary>
    private const float SizeTolerance = 0.001F;

    /// <summary>The fallback font for empty lines.</summary>
    private readonly PdfCanvasFont _defaultFont;

    /// <summary>The fallback font size for empty lines.</summary>
    private readonly float _defaultSize;

    /// <summary>The line spacing multiplier.</summary>
    private readonly float _lineSpacing;

    /// <summary>The destination list of visual items.</summary>
    private readonly List<CellVisualItem> _items;

    /// <summary>The segments accumulated on the current line.</summary>
    private readonly List<StyledSegment> _current = [];

    /// <summary>A space waiting to be attached before the next word on the same line.</summary>
    private StyledSegment? _pendingSpace;

    /// <summary>The current line's width in points.</summary>
    private float _currentWidth;

    /// <summary>Initializes a new instance of the <see cref="RichLineAccumulator"/> class.</summary>
    /// <param name="defaultFont">The default font.</param>
    /// <param name="defaultSize">The default font size.</param>
    /// <param name="lineSpacing">The line spacing multiplier.</param>
    /// <param name="items">Receives completed lines.</param>
    internal RichLineAccumulator(PdfCanvasFont defaultFont, float defaultSize, float lineSpacing, List<CellVisualItem> items)
    {
        _defaultFont = defaultFont;
        _defaultSize = defaultSize;
        _lineSpacing = lineSpacing;
        _items = items;
    }

    /// <summary>Appends one styled text run into the line accumulator, splitting on line breaks and word boundaries.</summary>
    /// <param name="text">The run's text.</param>
    /// <param name="font">The run's font.</param>
    /// <param name="size">The run's font size.</param>
    /// <param name="color">The run's colour.</param>
    /// <param name="maxWidth">The maximum line width.</param>
    internal void AppendRun(string text, PdfCanvasFont font, float size, PdfRgbColor color, float maxWidth)
    {
        if (text.Length == 0)
        {
            return;
        }

        var start = 0;
        var span = text.AsSpan();
        while (start <= span.Length)
        {
            var breakIndex = span[start..].IndexOfAny('\r', '\n');
            var paragraphEnd = breakIndex < 0 ? span.Length : start + breakIndex;
            AppendParagraph(text, start, paragraphEnd, font, size, color, maxWidth);
            if (breakIndex < 0)
            {
                break;
            }

            FlushLine(forceEmpty: true);
            start = span[paragraphEnd] == '\r' && paragraphEnd + 1 < span.Length && span[paragraphEnd + 1] == '\n'
                ? paragraphEnd + CrLfLength
                : paragraphEnd + 1;
        }
    }

    /// <summary>Flushes the current line into the item list.</summary>
    /// <param name="forceEmpty">Whether to emit an empty line when no segments are buffered.</param>
    internal void FlushLine(bool forceEmpty)
    {
        _pendingSpace = null;
        if (_current.Count == 0)
        {
            if (forceEmpty)
            {
                _items.Add(CellVisualItem.CreateText([], 0F, _defaultSize * _lineSpacing, _defaultFont.GetAscent(_defaultSize), _defaultFont.GetDescent(_defaultSize)));
            }

            return;
        }

        var maxSize = 0F;
        var maxAscent = 0F;
        var minDescent = 0F;
        for (var i = 0; i < _current.Count; i++)
        {
            var seg = _current[i];
            maxSize = Math.Max(maxSize, seg.Size);
            maxAscent = Math.Max(maxAscent, seg.Font.GetAscent(seg.Size));
            minDescent = Math.Min(minDescent, seg.Font.GetDescent(seg.Size));
        }

        _items.Add(CellVisualItem.CreateText([.. _current], _currentWidth, maxSize * _lineSpacing, maxAscent, minDescent));
        _current.Clear();
        _currentWidth = 0F;
    }

    /// <summary>Finishes the accumulator, flushing any remaining segments.</summary>
    internal void Finish()
    {
        if (_current.Count > 0 || _items.Count == 0)
        {
            FlushLine(forceEmpty: _items.Count == 0);
        }
    }

    /// <summary>Appends a line-break-free slice of a run into the line accumulator.</summary>
    /// <param name="text">The run's text.</param>
    /// <param name="start">The slice start.</param>
    /// <param name="end">The slice end.</param>
    /// <param name="font">The run's font.</param>
    /// <param name="size">The run's font size.</param>
    /// <param name="color">The run's colour.</param>
    /// <param name="maxWidth">The maximum line width.</param>
    private void AppendParagraph(string text, int start, int end, PdfCanvasFont font, float size, PdfRgbColor color, float maxWidth)
    {
        var span = text.AsSpan();
        var cursor = start;
        while (cursor < end)
        {
            if (span[cursor] == ' ')
            {
                var spaceEnd = cursor;
                while (spaceEnd < end && span[spaceEnd] == ' ')
                {
                    spaceEnd++;
                }

                var spaceSeg = new StyledSegment(text, cursor, spaceEnd - cursor, font.Measure(span[cursor..spaceEnd], size), font, size, color);
                SetPendingSpace(in spaceSeg);
                cursor = spaceEnd;
                continue;
            }

            var wordEnd = cursor;
            while (wordEnd < end && span[wordEnd] != ' ')
            {
                wordEnd++;
            }

            AddWord(text, cursor, wordEnd, font, size, color, maxWidth);
            cursor = wordEnd;
        }
    }

    /// <summary>Records a space segment that will precede the next word if it stays on the current line.</summary>
    /// <param name="space">The space segment.</param>
    private void SetPendingSpace(in StyledSegment space)
    {
        if (_current.Count == 0)
        {
            return;
        }

        _pendingSpace = space;
    }

    /// <summary>Adds a word to the current line, wrapping or breaking by character when needed.</summary>
    /// <param name="text">The source string.</param>
    /// <param name="start">The word start index.</param>
    /// <param name="end">The word end index.</param>
    /// <param name="font">The word's font.</param>
    /// <param name="size">The word's font size.</param>
    /// <param name="color">The word's colour.</param>
    /// <param name="maxWidth">The maximum line width.</param>
    private void AddWord(string text, int start, int end, PdfCanvasFont font, float size, PdfRgbColor color, float maxWidth)
    {
        var span = text.AsSpan();
        var wordWidth = font.Measure(span[start..end], size);
        var spaceWidth = _pendingSpace?.Width ?? 0F;
        if (_current.Count > 0 && _currentWidth + spaceWidth + wordWidth > maxWidth)
        {
            FlushLine(forceEmpty: false);
            _pendingSpace = null;
        }

        if (_pendingSpace is { } space)
        {
            AppendSegment(in space);
            _pendingSpace = null;
        }

        if (wordWidth <= maxWidth - _currentWidth)
        {
            var wordSegment = new StyledSegment(text, start, end - start, wordWidth, font, size, color);
            AppendSegment(in wordSegment);
            return;
        }

        if (_current.Count > 0)
        {
            FlushLine(forceEmpty: false);
        }

        BreakOverlongWord(text, start, end, font, size, color, maxWidth);
    }

    /// <summary>Appends a segment to the current line, coalescing adjacent segments from the same run.</summary>
    /// <param name="segment">The segment to append.</param>
    private void AppendSegment(in StyledSegment segment)
    {
        if (_current.Count > 0)
        {
            var last = _current[^1];
            if (ReferenceEquals(last.Source, segment.Source)
                && ReferenceEquals(last.Font, segment.Font)
                && Math.Abs(last.Size - segment.Size) <= SizeTolerance
                && last.Color == segment.Color
                && last.Start + last.Length == segment.Start)
            {
                var combinedLength = last.Length + segment.Length;
                var combinedWidth = last.Font.Measure(last.Source.AsSpan(last.Start, combinedLength), last.Size);
                _currentWidth += combinedWidth - last.Width;
                _current[^1] = last with { Length = combinedLength, Width = combinedWidth };
                return;
            }
        }

        _current.Add(segment);
        _currentWidth += segment.Width;
    }

    /// <summary>Splits a word that is wider than a full line across multiple lines at text-element boundaries.</summary>
    /// <param name="text">The source string.</param>
    /// <param name="start">The word start index.</param>
    /// <param name="end">The word end index.</param>
    /// <param name="font">The font.</param>
    /// <param name="size">The font size.</param>
    /// <param name="color">The colour.</param>
    /// <param name="maxWidth">The maximum line width.</param>
    private void BreakOverlongWord(string text, int start, int end, PdfCanvasFont font, float size, PdfRgbColor color, float maxWidth)
    {
        var span = text.AsSpan();
        var chunkStart = start;
        var chunkEnd = start;
        var chunkWidth = 0F;
        var offset = start;
        while (offset < end)
        {
            var next = offset + StringInfo.GetNextTextElementLength(span[offset..end]);
            var width = font.Measure(span[chunkStart..next], size);
            if (width > maxWidth && chunkEnd > chunkStart)
            {
                var part = new StyledSegment(text, chunkStart, chunkEnd - chunkStart, chunkWidth, font, size, color);
                AppendSegment(in part);
                FlushLine(forceEmpty: false);
                chunkStart = offset;
                width = font.Measure(span[offset..next], size);
            }

            chunkEnd = next;
            chunkWidth = width;
            offset = next;
        }

        if (chunkEnd <= chunkStart)
        {
            return;
        }

        var tail = new StyledSegment(text, chunkStart, chunkEnd - chunkStart, chunkWidth, font, size, color);
        AppendSegment(in tail);
    }
}
