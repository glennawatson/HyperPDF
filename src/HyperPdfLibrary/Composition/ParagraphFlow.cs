// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Composition;

/// <summary>A paragraph placed line by line on pages, with widow and orphan control, keep together, footnotes, borders and shading.</summary>
[DebuggerDisplay("ParagraphFlow: next line {_next}")]
internal sealed class ParagraphFlow : Flowable
{
    /// <summary>The deepest level that has its own heading structure type.</summary>
    private const int MaxHeadingType = 6;

    /// <summary>The fewest lines that may be left alone at the start or end of a page by widow and orphan control.</summary>
    private const int MinimumRun = 2;

    /// <summary>The structure type of each heading level.</summary>
    private static readonly string[] HeadingTypes = ["H1", "H2", "H3", "H4", "H5", "H6"];

    /// <summary>The document and its styles.</summary>
    private readonly LayoutEnv _env;

    /// <summary>The paragraph.</summary>
    private readonly PdfParagraph _paragraph;

    /// <summary>What the container changes in the paragraph.</summary>
    private readonly ParagraphAdjust _adjust;

    /// <summary>The structure node the paragraph's node goes under, or <see langword="null"/> for the document.</summary>
    private readonly StructNode? _parent;

    /// <summary>The paragraph formatting, for spacing and page flow rules.</summary>
    private readonly ResolvedParagraph _resolved;

    /// <summary>The paragraph laid out, once its width is known.</summary>
    private LaidOutParagraph? _laid;

    /// <summary>The first line not placed yet.</summary>
    private int _next;

    /// <summary>The footnotes of the paragraph laid out, by their place in the paragraph's notes.</summary>
    private Dictionary<int, LaidOutNote>? _notes;

    /// <summary>Initializes a new instance of the <see cref="ParagraphFlow"/> class.</summary>
    /// <param name="env">The document and its styles.</param>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="adjust">What the container changes in the paragraph.</param>
    /// <param name="parent">The structure node the paragraph's node goes under, or <see langword="null"/> for the document.</param>
    internal ParagraphFlow(LayoutEnv env, PdfParagraph paragraph, ParagraphAdjust adjust, StructNode? parent)
    {
        _env = env;
        _paragraph = paragraph;
        _adjust = adjust;
        _parent = parent;
        _resolved = env.Styles.Resolve(paragraph.Style).Paragraph.Apply(paragraph.Format);
    }

    /// <inheritdoc/>
    internal override float SpaceBefore => _resolved.SpaceBefore;

    /// <inheritdoc/>
    internal override float SpaceAfter => _resolved.SpaceAfter;

    /// <inheritdoc/>
    internal override bool KeepWithNext => _resolved.KeepWithNext;

    /// <inheritdoc/>
    internal override bool PageBreakBefore => _resolved.PageBreakBefore && _next == 0;

    /// <inheritdoc/>
    internal override float MeasureStart(FlowCursor cursor)
    {
        var laid = EnsureLaidOut(cursor);
        var insets = ParagraphBox.GetInsets(laid.Style);
        var keepAll = _resolved.KeepTogether || laid.Lines.Count <= MinimumRun;
        var count = keepAll ? laid.Lines.Count : MinimumRun;
        var height = insets.Top + (keepAll ? insets.Bottom : 0F);
        for (var i = _next; i < _next + count && i < laid.Lines.Count; i++)
        {
            height += laid.Lines[i].Height;
        }

        return height;
    }

    /// <inheritdoc/>
    internal override PlaceResult Place(FlowCursor cursor)
    {
        var laid = EnsureLaidOut(cursor);
        var remaining = laid.Lines.Count - _next;
        if (_next == 0 && ShouldMoveWhole(cursor, laid))
        {
            return PlaceResult.NeedsPage;
        }

        var count = ChooseCount(cursor, CountFit(cursor, laid), remaining);
        if (count == 0)
        {
            return PlaceResult.NeedsPage;
        }

        DrawChunk(cursor, laid, count);
        _next += count;
        return _next >= laid.Lines.Count ? PlaceResult.Finished : PlaceResult.NeedsPage;
    }

    /// <summary>Gets the structure type of a paragraph at an outline level.</summary>
    /// <param name="level">The outline level, 0 for body text.</param>
    /// <returns>The structure type.</returns>
    private static string GetNodeType(int level) => level is >= 1 ? HeadingTypes[Math.Min(level, MaxHeadingType) - 1] : "P";

    /// <summary>Draws the shading and borders round the lines just placed.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="laid">The laid out paragraph.</param>
    /// <param name="insertAt">Where the shading goes in the page's commands.</param>
    /// <param name="height">The height of the stretch.</param>
    /// <param name="first">Whether the stretch starts the paragraph.</param>
    /// <param name="last">Whether the stretch ends the paragraph.</param>
    /// <param name="top">The y of the top of the stretch.</param>
    private static void EmitBox(FlowCursor cursor, LaidOutParagraph laid, int insertAt, float height, bool first, bool last, float top)
    {
        if (laid.Style.Shading is null && laid.Style.Borders.IsEmpty)
        {
            return;
        }

        var insets = ParagraphBox.GetInsets(laid.Style);
        var left = cursor.Left + laid.Style.LeftIndent - insets.Left;
        var right = cursor.Left + cursor.Width - (laid.Style.RightIndent - insets.Right);
        ParagraphBox.Emit(cursor.Page.Commands, insertAt, laid.Style, new(left, top - height, right - left, height), first, last);
    }

    /// <summary>Lays the paragraph out the first time it is needed.</summary>
    /// <param name="cursor">The cursor the paragraph will be placed at.</param>
    /// <returns>The laid out paragraph.</returns>
    private LaidOutParagraph EnsureLaidOut(FlowCursor cursor)
    {
        if (_laid is { } known)
        {
            return known;
        }

        ILineBounds bounds = cursor.Page.Exclusions.Count == 0 ? FullLineBounds.Instance : new CursorBounds(cursor.Page);
        var laid = ParagraphBuilder.Build(_env, _paragraph, cursor.Width, _adjust, cursor.Y, bounds);
        laid.Node = _env.CreateNode(GetNodeType(laid.Style.OutlineLevel), _parent);
        _laid = laid;
        return laid;
    }

    /// <summary>Determines whether a paragraph that wants to stay on one page should move to the next, because it would split here but fits whole on an empty page.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="laid">The laid out paragraph.</param>
    /// <returns><see langword="true"/> to start a new page first.</returns>
    private bool ShouldMoveWhole(FlowCursor cursor, LaidOutParagraph laid)
    {
        if (!_resolved.KeepTogether || cursor.AtTop)
        {
            return false;
        }

        var insets = ParagraphBox.GetInsets(laid.Style);
        var total = laid.LinesHeight + insets.Top + insets.Bottom;
        return total > cursor.Remaining && total <= cursor.Top - cursor.BottomBase;
    }

    /// <summary>Counts the lines that fit in the room left on the page, with the footnotes their numbers bring.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="laid">The laid out paragraph.</param>
    /// <returns>The number of lines that fit.</returns>
    private int CountFit(FlowCursor cursor, LaidOutParagraph laid)
    {
        var insets = ParagraphBox.GetInsets(laid.Style);
        var used = _next == 0 ? insets.Top : 0F;
        List<LaidOutNote> notes = [];
        var count = 0;
        for (var i = _next; i < laid.Lines.Count; i++)
        {
            var line = laid.Lines[i];

            // Most lines carry no footnote number, and then the notes so far are the notes to cost, so no list is built.
            var withNotes = line.Notes.Count == 0 ? notes : [.. notes, .. GetNotes(cursor, laid, line)];
            var tail = i == laid.Lines.Count - 1 ? insets.Bottom : 0F;
            if (used + line.Height + tail + cursor.GetNotesCost(withNotes) > cursor.Remaining)
            {
                break;
            }

            used += line.Height;
            notes = withNotes;
            count++;
        }

        return count;
    }

    /// <summary>Decides how many lines to place, after widow and orphan control.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="fit">The number of lines that fit.</param>
    /// <param name="remaining">The number of lines not placed yet.</param>
    /// <returns>The number of lines to place; zero to start a new page first. At the top of a page it is at least one.</returns>
    private int ChooseCount(FlowCursor cursor, int fit, int remaining)
    {
        if (fit >= remaining)
        {
            return remaining;
        }

        var count = _resolved.WidowControl ? ApplyWidowControl(fit, remaining) : fit;
        if (count == 0 && cursor.AtTop)
        {
            count = Math.Max(1, fit);
        }

        return count;
    }

    /// <summary>Keeps a page from starting or ending with a single line of a paragraph.</summary>
    /// <param name="count">The number of lines that fit.</param>
    /// <param name="remaining">The number of lines not placed yet.</param>
    /// <returns>The number of lines to place, or zero to move the paragraph to the next page.</returns>
    private int ApplyWidowControl(int count, int remaining)
    {
        if (_next == 0 && count < MinimumRun)
        {
            return 0;
        }

        if (remaining - count < MinimumRun)
        {
            count--;
        }

        return _next == 0 && count < MinimumRun ? 0 : count;
    }

    /// <summary>Gets the laid out footnotes whose numbers are on a line.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="laid">The laid out paragraph.</param>
    /// <param name="line">The line.</param>
    /// <returns>The notes, in order.</returns>
    private List<LaidOutNote> GetNotes(FlowCursor cursor, LaidOutParagraph laid, LineBox line)
    {
        List<LaidOutNote> notes = [];
        foreach (var index in line.Notes)
        {
            _notes ??= [];
            ref var note = ref CollectionsMarshal.GetValueRefOrAddDefault(_notes, index, out var exists);
            if (!exists)
            {
                note = NoteLayout.Build(_env, laid.Notes[index], cursor.Width);
            }

            notes.Add(note!);
        }

        return notes;
    }

    /// <summary>Draws some lines on the page and moves the cursor below them.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="laid">The laid out paragraph.</param>
    /// <param name="count">The number of lines to draw.</param>
    private void DrawChunk(FlowCursor cursor, LaidOutParagraph laid, int count)
    {
        var page = cursor.Page;
        var insertAt = page.Commands.Count;
        var startY = cursor.Y;
        var first = _next == 0;
        var last = _next + count >= laid.Lines.Count;
        var insets = ParagraphBox.GetInsets(laid.Style);
        var y = startY - (first ? insets.Top : 0F);
        if (first)
        {
            AddOutline(cursor, laid);
        }

        List<LaidOutNote> notes = [];
        for (var i = _next; i < _next + count; i++)
        {
            var line = laid.Lines[i];
            new LineDrawer(laid, line, cursor.Left, y, laid.Node, cursor.Artifact, page.Commands).Draw();
            y -= line.Height;
            if (line.Notes.Count > 0)
            {
                notes.AddRange(GetNotes(cursor, laid, line));
            }
        }

        y -= last ? insets.Bottom : 0F;
        if (notes.Count > 0)
        {
            cursor.AddNotes(notes);
        }

        EmitBox(cursor, laid, insertAt, startY - y, first, last, startY);
        cursor.Y = y;
    }

    /// <summary>Adds the outline entry and destination of a heading.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="laid">The laid out paragraph.</param>
    private void AddOutline(FlowCursor cursor, LaidOutParagraph laid)
    {
        if (laid.Style.OutlineLevel <= 0)
        {
            return;
        }

        var name = _env.NextOutlineName();
        cursor.Page.Commands.Add(new DestinationCommand(name, cursor.Left, cursor.Y));
        _env.Outline.Add(new(laid.Style.OutlineLevel, laid.PlainText, name));
    }
}
