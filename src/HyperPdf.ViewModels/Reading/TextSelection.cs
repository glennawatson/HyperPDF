// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Reading;

/// <summary>The text a reader has selected: where it starts and ends, its text, and the rectangles to show it with, whichever toolkit draws them.</summary>
[DebuggerDisplay("TextSelection: {Anchor} to {Focus}")]
public sealed class TextSelection
{
    /// <summary>How many emptied lists the spare stack has room for before it grows.</summary>
    private const int SpareCapacity = 8;

    /// <summary>The selection rectangles of each page, worked out when first asked for.</summary>
    private readonly Dictionary<int, List<PageRect>> _rects = [];

    /// <summary>Emptied rectangle lists kept for reuse, so a drag does not allocate a list per page.</summary>
    private readonly Stack<List<PageRect>> _spare = new(SpareCapacity);

    /// <summary>Gets or sets the character the selection started at, or a position on page -1 when there is no selection.</summary>
    public CharacterPosition Anchor { get; set; } = CaretNavigation.None();

    /// <summary>Gets or sets the character the selection reaches now, or a position on page -1 when there is no selection.</summary>
    public CharacterPosition Focus { get; set; } = CaretNavigation.None();

    /// <summary>Forgets the selection.</summary>
    public void Clear()
    {
        Anchor = CaretNavigation.None();
        Focus = CaretNavigation.None();
        _rects.Clear();
    }

    /// <summary>Forgets every page's cached rectangles, after something other than the focus changed them.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForgetRects() => _rects.Clear();

    /// <summary>Starts a selection at one character.</summary>
    /// <param name="position">The character.</param>
    public void Begin(CharacterPosition position)
    {
        Anchor = position;
        Focus = position;
    }

    /// <summary>Selects a run of characters on one page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="characterCount">How many characters the page has.</param>
    public void SelectPage(int page, int characterCount)
    {
        Begin(new(page, 0));
        Focus = new(page, characterCount - 1);
    }

    /// <summary>Moves the caret, extending the selection from where the caret was when asked, and otherwise dropping it.</summary>
    /// <param name="caret">Where the caret is now.</param>
    /// <param name="next">Where the caret goes.</param>
    /// <param name="extend">Whether to extend the selection.</param>
    public void MoveCaret(CharacterPosition caret, CharacterPosition next, bool extend)
    {
        if (extend)
        {
            if (Anchor.Page < 0)
            {
                Anchor = caret;
            }

            Focus = next;
        }
        else
        {
            Anchor = CaretNavigation.None();
            Focus = CaretNavigation.None();
        }

        _rects.Clear();
    }

    /// <summary>Gets the selection with its start before its end.</summary>
    /// <param name="start">The first character.</param>
    /// <param name="end">The last character.</param>
    /// <returns><see langword="true"/> when something is selected.</returns>
    public bool TryGet(out CharacterPosition start, out CharacterPosition end)
    {
        var anchor = Anchor;
        var focus = Focus;
        start = anchor;
        end = focus;
        if (anchor.Page < 0 || focus.Page < 0 || anchor == focus)
        {
            return false;
        }

        var reversed = focus.Page < anchor.Page || (focus.Page == anchor.Page && focus.Char < anchor.Char);
        start = reversed ? focus : anchor;
        end = reversed ? anchor : focus;
        return true;
    }

    /// <summary>
    /// Moves the end of the selection. Nothing changes when it is already there; otherwise only the pages between the old and
    /// new end lose their rectangles, because the pages outside that span keep the same selected range.
    /// </summary>
    /// <param name="next">The new end.</param>
    /// <returns><see langword="true"/> when the end moved.</returns>
    public bool MoveFocus(CharacterPosition next)
    {
        var previous = Focus;
        if (previous == next)
        {
            return false;
        }

        Focus = next;
        DropRects(Math.Min(previous.Page, next.Page), Math.Max(previous.Page, next.Page));
        return true;
    }

    /// <summary>Gets the selection rectangles on a page, working them out the first time they are asked for.</summary>
    /// <param name="document">The document, or <see langword="null"/> when none is open.</param>
    /// <param name="page">The page.</param>
    /// <returns>The rectangles, which the selection owns.</returns>
    public List<PageRect> GetRects(IDocument? document, int page)
    {
        if (_rects.TryGetValue(page, out var cached))
        {
            return cached;
        }

        var rects = _spare.Count > 0 ? _spare.Pop() : [];
        if (document is not null && TryGet(out var start, out var end) && page >= start.Page && page <= end.Page)
        {
            var first = page == start.Page ? start.Char : 0;
            var last = page == end.Page ? end.Char : document.GetCharacterCount(page) - 1;
            if (last >= first)
            {
                document.GetTextBounds(page, first, last - first + 1, rects);
            }
        }

        _rects[page] = rects;
        return rects;
    }

    /// <summary>Gets the selected text.</summary>
    /// <param name="document">The document, or <see langword="null"/> when none is open.</param>
    /// <returns>The text, or an empty string.</returns>
    public string GetText(IDocument? document)
    {
        if (document is null || !TryGet(out var start, out var end))
        {
            return string.Empty;
        }

        // One string per page joined once, so the text is copied a single time and a one page selection not at all.
        var parts = new List<string>(end.Page - start.Page + 1);
        for (var page = start.Page; page <= end.Page; page++)
        {
            var first = page == start.Page ? start.Char : 0;
            var last = page == end.Page ? end.Char : document.GetCharacterCount(page) - 1;
            parts.Add(last >= first ? document.GetText(page, first, last - first + 1) : string.Empty);
        }

        return parts.Count == 1 ? parts[0] : string.Join(Environment.NewLine, parts);
    }

    /// <summary>Forgets the cached rectangles on a run of pages, keeping their lists for reuse.</summary>
    /// <param name="firstPage">The first page.</param>
    /// <param name="lastPage">The last page.</param>
    public void DropRects(int firstPage, int lastPage)
    {
        for (var page = firstPage; page <= lastPage; page++)
        {
            if (!_rects.Remove(page, out var rects))
            {
                continue;
            }

            rects.Clear();
            _spare.Push(rects);
        }
    }
}
