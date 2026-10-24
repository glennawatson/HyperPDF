// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Turns one line of a paragraph into drawing commands: highlights, text, tab leaders, underlines, strikeouts, links and bookmarks.</summary>
[DebuggerDisplay("LineDrawer: {_line.Start}..{_line.End}")]
internal sealed class LineDrawer
{
    /// <summary>How far below the baseline an underline sits, as a fraction of the font size.</summary>
    private const float UnderlineDrop = 0.12F;

    /// <summary>The thickness of an underline or strikeout, as a fraction of the font size.</summary>
    private const float DecorationThickness = 0.06F;

    /// <summary>The thinnest an underline or strikeout is drawn, in points.</summary>
    private const float MinimumThickness = 0.5F;

    /// <summary>How far above the baseline a strikeout sits, as a fraction of the font size.</summary>
    private const float StrikeoutRise = 0.3F;

    /// <summary>The width of a solid tab leader, in points.</summary>
    private const float LeaderLineWidth = 0.5F;

    /// <summary>Half, for centring.</summary>
    private const float Half = 0.5F;

    /// <summary>The widths of two sizes or shifts that differ by less than this are the same.</summary>
    private const float SameTolerance = 0.001F;

    /// <summary>The marks of a dotted leader are this many times their own width apart.</summary>
    private const float LeaderSpacing = 2F;

    /// <summary>The paragraph the line is in.</summary>
    private readonly LaidOutParagraph _paragraph;

    /// <summary>The line.</summary>
    private readonly LineBox _line;

    /// <summary>The left edge of the text area on the page.</summary>
    private readonly float _left;

    /// <summary>The y of the top of the line.</summary>
    private readonly float _top;

    /// <summary>The structure node the line's text belongs to, or <see langword="null"/>.</summary>
    private readonly StructNode? _node;

    /// <summary>The kind of artifact the line is drawn as when it has no node.</summary>
    private readonly PdfArtifactKind _artifact;

    /// <summary>Where the commands go.</summary>
    private readonly List<DrawCommand> _sink;

    /// <summary>The atoms of the line with their places.</summary>
    private readonly List<PlacedAtom> _placed = [];

    /// <summary>The structure node of each placed atom: the paragraph's, or a link's.</summary>
    private readonly List<StructNode?> _nodes = [];

    /// <summary>The y of the baseline.</summary>
    private readonly float _baseline;

    /// <summary>Initializes a new instance of the <see cref="LineDrawer"/> class.</summary>
    /// <param name="paragraph">The paragraph the line is in.</param>
    /// <param name="line">The line.</param>
    /// <param name="left">The left edge of the text area on the page.</param>
    /// <param name="top">The y of the top of the line.</param>
    /// <param name="node">The structure node the line's text belongs to, or <see langword="null"/>.</param>
    /// <param name="artifact">The kind of artifact the line is drawn as when it has no node.</param>
    /// <param name="sink">Where the commands go.</param>
    internal LineDrawer(LaidOutParagraph paragraph, LineBox line, float left, float top, StructNode? node, PdfArtifactKind artifact, List<DrawCommand> sink)
    {
        _paragraph = paragraph;
        _line = line;
        _left = left;
        _top = top;
        _node = node;
        _artifact = artifact;
        _sink = sink;
        _baseline = top - line.Baseline;
    }

    /// <summary>Draws the line.</summary>
    internal void Draw()
    {
        Place();
        EmitMarks();
        EmitHighlights();
        EmitText();
        EmitLeaders();
        EmitDecorations();
        EmitLinks();
    }

    /// <summary>Determines whether two sizes or shifts are the same, allowing for rounding.</summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns><see langword="true"/> when they differ by less than the tolerance.</returns>
    private static bool Same(float a, float b) => MathF.Abs(a - b) < SameTolerance;

    /// <summary>Determines whether two atoms carry the same underline and strikeout.</summary>
    /// <param name="a">The first atom.</param>
    /// <param name="b">The second atom.</param>
    /// <returns><see langword="true"/> when one bar can run under both.</returns>
    private static bool SameDecoration(Atom a, Atom b) =>
        a.Format.Underline == b.Format.Underline
        && a.Format.Strikeout == b.Format.Strikeout
        && a.Format.Color == b.Format.Color
        && Same(a.Size, b.Size)
        && Same(a.Shift, b.Shift);

    /// <summary>Builds the text of a leader: marks with spaces between them.</summary>
    /// <param name="mark">The mark.</param>
    /// <param name="count">The number of marks.</param>
    /// <returns>The text.</returns>
    private static string BuildLeader(string mark, int count)
    {
        var text = new StringBuilder(count * (mark.Length + 1));
        for (var i = 0; i < count; i++)
        {
            _ = text.Append(i == 0 ? mark : $" {mark}");
        }

        return text.ToString();
    }

    /// <summary>Works out where each atom of the line goes, and the structure node it belongs to.</summary>
    private void Place()
    {
        var spare = _line.AvailableWidth - _line.ContentWidth;
        var shift = _paragraph.Style.Alignment switch
        {
            PdfTextAlignment.Center => spare * Half,
            PdfTextAlignment.Right => spare,
            _ => 0F,
        };
        var x = _left + _line.AvailableLeft + MathF.Max(0F, shift);
        StructNode? linkNode = null;
        PdfLinkTarget? linkTarget = null;
        for (var i = _line.Start; i < _line.End; i++)
        {
            var atom = _paragraph.Atoms[i];
            var width = GetWidth(atom, i);
            _placed.Add(new(atom, x, width));
            if (atom.Link is null)
            {
                linkNode = null;
                linkTarget = null;
            }
            else if (!ReferenceEquals(atom.Link, linkTarget))
            {
                linkTarget = atom.Link;
                linkNode = _node is null ? null : new StructNode("Link", _node, PdfTagOptions.Default with { AlternativeText = atom.LinkDescription });
            }

            _nodes.Add(atom.Link is null ? atom.Node ?? _node : linkNode);
            x += width;
        }
    }

    /// <summary>Gets the width an atom takes on this line.</summary>
    /// <param name="atom">The atom.</param>
    /// <param name="index">The atom's place in the paragraph.</param>
    /// <returns>The width in points.</returns>
    private float GetWidth(Atom atom, int index)
    {
        switch (atom.Kind)
        {
            case AtomKind.Space:
            {
                return atom.Width + _line.SpaceExtra;
            }

            case AtomKind.Tab:
            {
                return GetTabWidth(index);
            }

            default:
            {
                return atom.Width;
            }
        }
    }

    /// <summary>Gets the width of the tab at an atom on this line.</summary>
    /// <param name="index">The tab's place in the paragraph.</param>
    /// <returns>The width in points.</returns>
    private float GetTabWidth(int index)
    {
        foreach (var tab in _line.Tabs)
        {
            if (tab.AtomIndex == index)
            {
                return tab.Width;
            }
        }

        return 0F;
    }

    /// <summary>Records the places of bookmarks on the line.</summary>
    private void EmitMarks()
    {
        foreach (var placed in _placed)
        {
            if (placed.Atom.Bookmark is { } name)
            {
                _sink.Add(new DestinationCommand(name, placed.X, _top));
            }
        }
    }

    /// <summary>Paints the highlight behind runs that have one.</summary>
    private void EmitHighlights()
    {
        var start = 0;
        while (start < _placed.Count)
        {
            var color = _placed[start].Atom.Format.Highlight;
            var end = start + 1;
            while (end < _placed.Count && _placed[end].Atom.Format.Highlight == color)
            {
                end++;
            }

            if (color is { } fill)
            {
                var from = _placed[start].X;
                var box = new PdfCanvasBox(from, _baseline - _line.Descent, _placed[end - 1].End - from, _line.Ascent + _line.Descent);
                _sink.Add(new FillCommand(_nodes[start], _artifact, box, fill));
            }

            start = end;
        }
    }

    /// <summary>Draws the text, in as few commands as the formatting allows.</summary>
    private void EmitText()
    {
        var justified = _line.SpaceExtra > 0F;
        var run = new StringBuilder();
        var first = -1;
        for (var i = 0; i < _placed.Count; i++)
        {
            var atom = _placed[i].Atom;
            var drawn = atom.Kind is AtomKind.Word or AtomKind.Note || (atom.Kind == AtomKind.Space && !justified);
            if (!drawn || (first >= 0 && !CanJoin(_placed[first].Atom, atom, i)))
            {
                FlushRun(run, first);
                first = -1;
            }

            if (!drawn)
            {
                continue;
            }

            first = first < 0 ? i : first;
            _ = run.Append(atom.Text);
        }

        FlushRun(run, first);
    }

    /// <summary>Determines whether an atom can be drawn in the same command as the run before it.</summary>
    /// <param name="head">The first atom of the run.</param>
    /// <param name="atom">The atom.</param>
    /// <param name="index">The atom's place among the placed atoms.</param>
    /// <returns><see langword="true"/> when the atom can join the run.</returns>
    private bool CanJoin(Atom head, Atom atom, int index) =>
        ReferenceEquals(head.Font, atom.Font)
        && Same(head.Size, atom.Size)
        && Same(head.Shift, atom.Shift)
        && head.Format.Color == atom.Format.Color
        && ReferenceEquals(_nodes[index], _nodes[index - 1]);

    /// <summary>Writes the text collected so far as one command and starts afresh.</summary>
    /// <param name="run">The text.</param>
    /// <param name="first">The first atom of the run, or -1 when there is none.</param>
    private void FlushRun(StringBuilder run, int first)
    {
        var text = run.ToString().TrimEnd();
        _ = run.Clear();
        if (first < 0 || text.Length == 0)
        {
            return;
        }

        var atom = _placed[first].Atom;
        _sink.Add(new TextCommand(_nodes[first], _artifact, atom.Font!, atom.Size, atom.Format.Color, _placed[first].X, _baseline + atom.Shift, text));
    }

    /// <summary>Fills the space tabs skip with their leaders.</summary>
    private void EmitLeaders()
    {
        foreach (var tab in _line.Tabs)
        {
            if (tab.Leader != PdfTabLeader.None && tab.Width > 0F)
            {
                EmitLeader(tab.AtomIndex - _line.Start, tab.Leader);
            }
        }
    }

    /// <summary>Draws one tab leader.</summary>
    /// <param name="index">The tab's place among the placed atoms.</param>
    /// <param name="leader">What fills the tab.</param>
    private void EmitLeader(int index, PdfTabLeader leader)
    {
        var tab = _placed[index];
        var atom = tab.Atom;
        if (leader == PdfTabLeader.Line)
        {
            var pen = new PdfPen(atom.Format.Color, LeaderLineWidth);
            _sink.Add(new StrokeCommand(_nodes[index], _artifact, pen, new(tab.X, _baseline), new(tab.End, _baseline)));
            return;
        }

        var mark = leader == PdfTabLeader.Dots ? "." : "-";
        var count = (int)MathF.Floor(tab.Width / (atom.Font!.Measure(mark, atom.Size) * LeaderSpacing));
        if (count <= 0)
        {
            return;
        }

        var text = BuildLeader(mark, count);
        var width = atom.Font.Measure(text, atom.Size);
        _sink.Add(new TextCommand(_nodes[index], _artifact, atom.Font, atom.Size, atom.Format.Color, tab.End - width, _baseline, text));
    }

    /// <summary>Draws underlines and strikeouts under runs that have them.</summary>
    private void EmitDecorations()
    {
        var start = 0;
        while (start < _placed.Count)
        {
            var atom = _placed[start].Atom;
            var end = start + 1;
            while (end < _placed.Count && SameDecoration(atom, _placed[end].Atom))
            {
                end++;
            }

            var from = _placed[start].X;
            var width = _placed[end - 1].End - from;
            if (atom.Format.Underline)
            {
                EmitBar(start, from, width, _baseline + atom.Shift - (atom.Size * UnderlineDrop), atom);
            }

            if (atom.Format.Strikeout)
            {
                EmitBar(start, from, width, _baseline + atom.Shift + (atom.Size * StrikeoutRise), atom);
            }

            start = end;
        }
    }

    /// <summary>Draws a thin bar for an underline or strikeout.</summary>
    /// <param name="placedIndex">The first atom the bar runs under.</param>
    /// <param name="x">The bar's left edge.</param>
    /// <param name="width">The bar's width.</param>
    /// <param name="y">The y of the bar's centre.</param>
    /// <param name="atom">The atom whose size and colour set the bar's thickness and colour.</param>
    private void EmitBar(int placedIndex, float x, float width, float y, Atom atom)
    {
        var thickness = MathF.Max(MinimumThickness, atom.Size * DecorationThickness);
        _sink.Add(new FillCommand(_nodes[placedIndex], _artifact, new(x, y - (thickness * Half), width, thickness), atom.Format.Color));
    }

    /// <summary>Adds the clickable areas of the links on the line.</summary>
    private void EmitLinks()
    {
        var start = 0;
        while (start < _placed.Count)
        {
            var target = _placed[start].Atom.Link;
            var end = start + 1;
            while (end < _placed.Count && ReferenceEquals(_placed[end].Atom.Link, target))
            {
                end++;
            }

            if (target is not null)
            {
                var from = _placed[start].X;
                var box = new PdfCanvasBox(from, _baseline - _line.Descent, _placed[end - 1].End - from, _line.Ascent + _line.Descent);
                _sink.Add(new LinkCommand(_nodes[start], _artifact, box, target));
            }

            start = end;
        }
    }
}
