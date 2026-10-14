// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.App.Controls;

/// <summary>
/// Caret navigation (F7): a steady text cursor moved with the arrow keys, across lines and pages; Shift extends the
/// selection. The cursor never blinks, following the comfort rules.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>The caret line width in device independent pixels.</summary>
    private const double CaretWidth = 2;

    /// <summary>How far the caret reaches above and below the glyph, as a fraction of its height, to span the line.</summary>
    private const double CaretOverhang = 0.3;

    /// <summary>How far past a line, as a fraction of its height, to look for the next line.</summary>
    private const float LineProbe = 0.75F;

    /// <summary>The most lines skipped when the next line is far away, such as across a gap in the page.</summary>
    private const int MaxLineProbes = 12;

    /// <summary>The character bounds reused for caret lookups.</summary>
    private readonly List<PageRect> _caretBounds = [];

    /// <summary>The caret, or page -1 when there is none.</summary>
    private (int Page, int Char) _caret = (-1, -1);

    /// <summary>Gets the caret position, for tests.</summary>
    internal (int Page, int Char) Caret => _caret;

    /// <summary>Steps one character, crossing to the next or previous page with text.</summary>
    /// <param name="document">The document.</param>
    /// <param name="from">The current position.</param>
    /// <param name="direction">1 for forward, -1 for back.</param>
    /// <returns>The new position, or page -1 at either end of the document.</returns>
    private static (int Page, int Char) Step(IDocument document, (int Page, int Char) from, int direction)
    {
        var target = from.Char + direction;
        if (target >= 0 && target < document.GetCharacterCount(from.Page))
        {
            return (from.Page, target);
        }

        for (var page = from.Page + direction; page >= 0 && page < document.PageCount; page += direction)
        {
            var count = document.GetCharacterCount(page);
            if (count > 0)
            {
                return (page, direction > 0 ? 0 : count - 1);
            }
        }

        return (-1, -1);
    }

    /// <summary>Places the caret, for example where the user clicked.</summary>
    /// <param name="page">The page.</param>
    /// <param name="character">The character.</param>
    private void PlaceCaret(int page, int character)
    {
        _caret = (page, character);
        InvalidateVisual();
    }

    /// <summary>Puts the caret at the first character of the current page or later, when caret mode starts.</summary>
    /// <param name="tab">The tab.</param>
    private void StartCaret(DocumentTabViewModel tab)
    {
        if (_caret.Page >= 0 || tab.TryGetDocument() is not { } document)
        {
            return;
        }

        for (var page = Math.Max(0, tab.CurrentPageIndex); page < document.PageCount; page++)
        {
            if (document.GetCharacterCount(page) <= 0)
            {
                continue;
            }

            PlaceCaret(page, 0);
            return;
        }
    }

    /// <summary>Handles the arrow keys in caret mode.</summary>
    /// <param name="e">The key event.</param>
    /// <returns><see langword="true"/> when handled.</returns>
    private bool HandleCaretKey(KeyEventArgs e)
    {
        if (Tab is not { IsCaretMode: true } tab || tab.TryGetDocument() is not { } document || (e.KeyModifiers & ~KeyModifiers.Shift) != 0)
        {
            return false;
        }

        StartCaret(tab);
        if (_caret.Page < 0)
        {
            return false;
        }

        (int Page, int Char) next = e.Key switch
        {
            Key.Right => Step(document, _caret, 1),
            Key.Left => Step(document, _caret, -1),
            Key.Down => StepLine(document, _caret, 1),
            Key.Up => StepLine(document, _caret, -1),
            _ => (-1, -1),
        };
        if (next.Page < 0)
        {
            return e.Key is Key.Right or Key.Left or Key.Down or Key.Up;
        }

        MoveCaret(next, (e.KeyModifiers & KeyModifiers.Shift) != 0);
        return true;
    }

    /// <summary>Moves the caret, extending the selection when asked, and keeps it in view.</summary>
    /// <param name="next">The new position.</param>
    /// <param name="extend">Whether to extend the selection.</param>
    private void MoveCaret((int Page, int Char) next, bool extend)
    {
        if (extend)
        {
            if (_selectionAnchor.Page < 0)
            {
                _selectionAnchor = _caret;
            }

            _selectionFocus = next;
        }
        else
        {
            _selectionAnchor = (-1, -1);
            _selectionFocus = (-1, -1);
        }

        _selectionRects.Clear();
        _caret = next;
        ScrollCaretIntoView();
        InvalidateVisual();
    }

    /// <summary>Steps one line down or up, keeping the caret's horizontal position, crossing pages at the edges.</summary>
    /// <param name="document">The document.</param>
    /// <param name="from">The current position.</param>
    /// <param name="direction">1 for down, -1 for up.</param>
    /// <returns>The new position, or page -1 at either end of the document.</returns>
    private (int Page, int Char) StepLine(IDocument document, (int Page, int Char) from, int direction)
    {
        _caretBounds.Clear();
        document.GetTextBounds(from.Page, from.Char, 1, _caretBounds);
        if (_caretBounds.Count > 0)
        {
            var bounds = _caretBounds[0];
            var height = Math.Max(1, bounds.Height);
            for (var probe = 1; probe <= MaxLineProbes; probe++)
            {
                var y = direction > 0 ? bounds.Bottom + (height * LineProbe * probe) : bounds.Top - (height * LineProbe * probe);
                var found = document.GetCharacterIndexAt(from.Page, new(bounds.Left, y), height);
                if (found >= 0 && found != from.Char && IsOtherLine(document, from.Page, found, bounds, direction))
                {
                    return (from.Page, found);
                }
            }
        }

        // No further line on this page: go to the first or last character of the next page with text.
        var edge = direction > 0 ? (from.Page, document.GetCharacterCount(from.Page) - 1) : (from.Page, 0);
        return Step(document, edge, direction);
    }

    /// <summary>Determines whether a character sits on a line beyond the caret's, not just reaching past it like a descender.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="character">The character found.</param>
    /// <param name="caret">The caret character's bounds.</param>
    /// <param name="direction">1 for down, -1 for up.</param>
    /// <returns><see langword="true"/> when its centre is past the caret's line.</returns>
    private bool IsOtherLine(IDocument document, int page, int character, in PageRect caret, int direction)
    {
        _caretBounds.Clear();
        document.GetTextBounds(page, character, 1, _caretBounds);
        if (_caretBounds.Count == 0)
        {
            return false;
        }

        var centre = _caretBounds[0].Top + (_caretBounds[0].Height * (float)Half);
        return direction > 0 ? centre > caret.Bottom : centre < caret.Top;
    }

    /// <summary>Scrolls just enough to show the caret.</summary>
    private void ScrollCaretIntoView()
    {
        if (_scroller is null || Tab is not { } tab || CaretRect(tab) is not { } caret)
        {
            return;
        }

        var offset = _scroller.Offset;
        var viewport = _scroller.Viewport;
        var y = offset.Y;
        if (caret.Top < y)
        {
            y = caret.Top - ContentMargin;
        }
        else if (caret.Bottom > y + viewport.Height)
        {
            y = caret.Bottom - viewport.Height + ContentMargin;
        }

        var x = offset.X;
        if (caret.Left < x)
        {
            x = caret.Left - ContentMargin;
        }
        else if (caret.Right > x + viewport.Width)
        {
            x = caret.Right - viewport.Width + ContentMargin;
        }

        _scroller.Offset = new(x, y);
    }

    /// <summary>Gets the caret's line on the canvas.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns>The rectangle, or <see langword="null"/> when there is no caret.</returns>
    private Rect? CaretRect(DocumentTabViewModel tab)
    {
        if (_caret.Page < 0 || _caret.Page >= _layout.PageCount || tab.TryGetDocument() is not { } document)
        {
            return null;
        }

        _caretBounds.Clear();
        document.GetTextBounds(_caret.Page, _caret.Char, 1, _caretBounds);
        if (_caretBounds.Count == 0)
        {
            return null;
        }

        var transform = new PageTransform(_layout.GetPageBounds(_caret.Page), _sizes[_caret.Page], tab.Rotation, _layout.Options.Scale);
        var area = transform.ToCanvas(_caretBounds[0]);
        var overhang = area.Height * CaretOverhang;
        return new(area.X - (CaretWidth * Half), area.Y - overhang, CaretWidth, area.Height + overhang + overhang);
    }

    /// <summary>Draws the steady caret when caret mode is on.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    private void DrawCaret(DrawingContext context, DocumentTabViewModel tab)
    {
        if (!tab.IsCaretMode || CaretBrush() is not { } brush || CaretRect(tab) is not { } caret)
        {
            return;
        }

        context.FillRectangle(brush, caret);
    }

    /// <summary>Gets the caret colour: the theme's accent, as used for the current search hit outline.</summary>
    /// <returns>The brush.</returns>
    private IBrush? CaretBrush() => CurrentHitOutline ?? SelectionBrush;
}
