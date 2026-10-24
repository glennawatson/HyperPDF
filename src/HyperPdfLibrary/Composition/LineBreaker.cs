// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Cuts a paragraph's atoms into lines that fit the width of the text area.</summary>
[DebuggerDisplay("LineBreaker: {_atoms.Count} atoms")]
internal sealed class LineBreaker
{
    /// <summary>How much wider than the space a word may be and still count as fitting, so rounding does not wrap a line that fits.</summary>
    private const float Tolerance = 0.01F;

    /// <summary>The narrowest a line may be, so a paragraph squeezed by indents still makes progress.</summary>
    private const float MinimumLineWidth = 1F;

    /// <summary>Half, for centring.</summary>
    private const float Half = 0.5F;

    /// <summary>The atoms of the paragraph, which breaking an overlong word may add to.</summary>
    private readonly List<Atom> _atoms;

    /// <summary>The paragraph formatting.</summary>
    private readonly ResolvedParagraph _paragraph;

    /// <summary>The width of the text area.</summary>
    private readonly float _textWidth;

    /// <summary>How far the text of an empty line rises above the baseline.</summary>
    private readonly float _emptyAscent;

    /// <summary>How far the text of an empty line falls below the baseline.</summary>
    private readonly float _emptyDescent;

    /// <summary>The next atom to place.</summary>
    private int _index;

    /// <summary>Whether the last line ended because the next word did not fit.</summary>
    private bool _afterWrap;

    /// <summary>Initializes a new instance of the <see cref="LineBreaker"/> class.</summary>
    /// <param name="atoms">The atoms of the paragraph.</param>
    /// <param name="paragraph">The paragraph formatting.</param>
    /// <param name="textWidth">The width of the text area.</param>
    /// <param name="empty">The formatting an empty line takes its height from.</param>
    /// <exception cref="InvalidOperationException">The formatting has no typeface.</exception>
    internal LineBreaker(List<Atom> atoms, ResolvedParagraph paragraph, float textWidth, ResolvedCharacter empty)
    {
        _atoms = atoms;
        _paragraph = paragraph;
        _textWidth = textWidth;
        var font = (empty.Family ?? throw new InvalidOperationException("Text has no typeface.")).Select(empty.Bold, empty.Italic);
        _emptyAscent = font.GetAscent(empty.Size);
        _emptyDescent = MathF.Abs(font.GetDescent(empty.Size));
    }

    /// <summary>Breaks the whole paragraph into lines.</summary>
    /// <param name="top">The y of the top of the first line, which only matters to <paramref name="bounds"/>.</param>
    /// <param name="bounds">How much room each line has.</param>
    /// <returns>The lines, at least one.</returns>
    internal List<LineBox> Break(float top, ILineBounds bounds)
    {
        var lines = new List<LineBox>();
        var first = true;
        _index = 0;
        _afterWrap = false;
        while (first || _index < _atoms.Count)
        {
            var estimate = (_emptyAscent + _emptyDescent) * _paragraph.LineSpacing;
            var line = BreakLine(first, bounds.GetSpan(top, estimate));
            lines.Add(line);
            top -= line.Height;
            first = false;
        }

        lines[^1].IsLast = true;
        return lines;
    }

    /// <summary>Counts the spaces seen so far as content, now that something follows them on the line.</summary>
    /// <param name="fill">The state of the line.</param>
    private static void AcceptPending(LineFill fill)
    {
        fill.Cursor += fill.PendingWidth;
        fill.AcceptedSpaces += fill.PendingSpaces;
        fill.PendingWidth = 0F;
        fill.PendingSpaces = 0;
    }

    /// <summary>Breaks one line.</summary>
    /// <param name="first">Whether the line is the first of the paragraph.</param>
    /// <param name="span">The space taken from the edges of the text area.</param>
    /// <returns>The line.</returns>
    private LineBox BreakLine(bool first, LineSpan span)
    {
        if (_afterWrap)
        {
            SkipSpaces();
        }

        var start = _paragraph.LeftIndent + (first ? _paragraph.FirstLineIndent : 0F) + span.Left;
        var end = MathF.Max(start + MinimumLineWidth, _textWidth - _paragraph.RightIndent - span.Right);
        var line = new LineBox { Start = _index, AvailableLeft = start, AvailableWidth = end - start };
        var fill = new LineFill(start, end, _index);
        while (_index < _atoms.Count && !fill.Done)
        {
            Place(line, fill);
        }

        _afterWrap = fill.Wrapped;
        line.End = fill.AcceptedEnd;
        line.ContentWidth = fill.Cursor - start;
        MeasureLine(line);
        ApplyJustify(line, fill);
        return line;
    }

    /// <summary>Skips the spaces at the start of a line that follows a wrap.</summary>
    private void SkipSpaces()
    {
        while (_index < _atoms.Count && _atoms[_index].Kind == AtomKind.Space)
        {
            _index++;
        }
    }

    /// <summary>Places the next atom, or the group of atoms that stay together, on the line.</summary>
    /// <param name="line">The line.</param>
    /// <param name="fill">The state of the line.</param>
    private void Place(LineBox line, LineFill fill)
    {
        var atom = _atoms[_index];
        switch (atom.Kind)
        {
            case AtomKind.Break:
            {
                line.EndsWithBreak = true;
                _index++;
                fill.AcceptedEnd = _index;
                fill.Done = true;
                break;
            }

            case AtomKind.Space:
            {
                fill.PendingWidth += atom.Width;
                fill.PendingSpaces++;
                _index++;
                break;
            }

            case AtomKind.Tab:
            {
                PlaceTab(line, fill);
                break;
            }

            default:
            {
                PlaceGroup(line, fill);
                break;
            }
        }
    }

    /// <summary>Places a tab: the spaces before it count as content, and the tab jumps to the next stop.</summary>
    /// <param name="line">The line.</param>
    /// <param name="fill">The state of the line.</param>
    private void PlaceTab(LineBox line, LineFill fill)
    {
        AcceptPending(fill);
        var gap = TabResolver.Resolve(_atoms, _index, _paragraph.TabStops, fill.Cursor);
        var width = MathF.Min(gap.Width, MathF.Max(0F, fill.End - fill.Cursor));
        line.Tabs.Add(gap with { Width = width });
        fill.Cursor += width;
        _index++;
        fill.AcceptedEnd = _index;
        fill.HasContent = true;
    }

    /// <summary>Places a group of atoms that join each other, wrapping to the next line when they do not fit.</summary>
    /// <param name="line">The line.</param>
    /// <param name="fill">The state of the line.</param>
    private void PlaceGroup(LineBox line, LineFill fill)
    {
        var groupEnd = FindGroupEnd(_index);
        var width = SumWidths(_index, groupEnd);
        var fits = fill.Cursor + fill.PendingWidth + width <= fill.End + Tolerance;
        if (!fits && fill.HasContent)
        {
            fill.Done = true;
            fill.Wrapped = true;
            return;
        }

        if (!fits && TrySplitWord(_index, fill.End - fill.Cursor - fill.PendingWidth))
        {
            groupEnd = _index + 1;
            width = _atoms[_index].Width;
        }

        AcceptPending(fill);
        for (var i = _index; i < groupEnd; i++)
        {
            if (_atoms[i].Kind == AtomKind.Note)
            {
                line.Notes.Add(_atoms[i].NoteIndex);
            }
        }

        fill.Cursor += width;
        fill.HasContent |= width > 0F;
        _index = groupEnd;
        fill.AcceptedEnd = groupEnd;
    }

    /// <summary>Finds the end of the group of atoms, starting at one, that must not be split by a line break.</summary>
    /// <param name="start">The first atom of the group.</param>
    /// <returns>The place after the last atom of the group.</returns>
    private int FindGroupEnd(int start)
    {
        var end = start + 1;
        while (end < _atoms.Count && _atoms[end].JoinsPrevious && _atoms[end].Kind is AtomKind.Word or AtomKind.Note or AtomKind.Mark)
        {
            end++;
        }

        return end;
    }

    /// <summary>Adds up the widths of some atoms.</summary>
    /// <param name="from">The first atom.</param>
    /// <param name="to">The place after the last atom.</param>
    /// <returns>The width.</returns>
    private float SumWidths(int from, int to)
    {
        var width = 0F;
        for (var i = from; i < to; i++)
        {
            width += _atoms[i].Width;
        }

        return width;
    }

    /// <summary>Splits a word that is wider than a whole line, so the first part fills the line.</summary>
    /// <param name="index">The word's place among the atoms.</param>
    /// <param name="room">The room left on the line.</param>
    /// <returns><see langword="true"/> when the word was split.</returns>
    private bool TrySplitWord(int index, float room)
    {
        var atom = _atoms[index];
        if (atom.Kind != AtomKind.Word || atom.Text.Length < 2)
        {
            return false;
        }

        var keep = 1;
        while (keep + 1 < atom.Text.Length && atom.Font!.Measure(atom.Text.AsSpan(0, keep + 1), atom.Size) <= room)
        {
            keep++;
        }

        var head = atom.WithText(atom.Text[..keep]);
        var tail = atom.WithText(atom.Text[keep..]);
        tail.JoinsPrevious = true;
        AtomMetrics.Measure(head);
        AtomMetrics.Measure(tail);
        _atoms[index] = head;
        _atoms.Insert(index + 1, tail);
        return true;
    }

    /// <summary>Works out the line's height, and where its baseline is, from the atoms on it and the paragraph's line spacing.</summary>
    /// <param name="line">The line.</param>
    private void MeasureLine(LineBox line)
    {
        var ascent = 0F;
        var descent = 0F;
        for (var i = line.Start; i < line.End; i++)
        {
            ascent = MathF.Max(ascent, _atoms[i].Ascent);
            descent = MathF.Max(descent, _atoms[i].Descent);
        }

        if (line.End == line.Start)
        {
            ascent = _emptyAscent;
            descent = _emptyDescent;
        }

        var natural = ascent + descent;
        line.Ascent = ascent;
        line.Descent = descent;
        line.Height = _paragraph.LineSpacingRule switch
        {
            PdfLineSpacingRule.Exactly => _paragraph.LineSpacing,
            PdfLineSpacingRule.AtLeast => MathF.Max(natural, _paragraph.LineSpacing),
            _ => natural * _paragraph.LineSpacing,
        };
        line.Baseline = ((line.Height - natural) * Half) + ascent;
    }

    /// <summary>Shares the room left on a justified line between its spaces.</summary>
    /// <param name="line">The line.</param>
    /// <param name="fill">The state of the line.</param>
    private void ApplyJustify(LineBox line, LineFill fill)
    {
        var justify = _paragraph.Alignment == PdfTextAlignment.Justify && !line.IsLast && !line.EndsWithBreak && line.Tabs.Count == 0;
        if (justify && fill.AcceptedSpaces > 0 && fill.Wrapped)
        {
            line.SpaceExtra = (line.AvailableWidth - line.ContentWidth) / fill.AcceptedSpaces;
        }
    }
}
