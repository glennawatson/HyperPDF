// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;

namespace HyperPdf.App.Controls;

/// <summary>
/// Annotation input on the page canvas: right-click menus that depend on what is under the pointer (a selection, an
/// annotation or blank page), drawing, placing notes and text, and the outline of the picked annotation.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>The width of the live stroke and the selected annotation outline, in device independent pixels.</summary>
    private const double OverlayPenWidth = 1.5;

    /// <summary>The size of colour swatches in menus.</summary>
    private const double SwatchSize = 12;

    /// <summary>The corner radius of colour swatches.</summary>
    private const double SwatchRadius = 2;

    /// <summary>The corner radius of on-page comment indicator badges.</summary>
    private const double BadgeRadius = 3;

    /// <summary>The left inset of the ruled lines inside a comment indicator badge, as a fraction of its width.</summary>
    private const double BadgeLineLeft = 0.25;

    /// <summary>The right inset of the ruled lines inside a comment indicator badge, as a fraction of its width.</summary>
    private const double BadgeLineRight = 0.75;

    /// <summary>The vertical position of the first ruled line inside a comment indicator badge, as a fraction of its height.</summary>
    private const double BadgeFirstLineY = 0.38;

    /// <summary>The vertical position of the second ruled line inside a comment indicator badge, as a fraction of its height.</summary>
    private const double BadgeSecondLineY = 0.64;

    /// <summary>How far the selected annotation outline sits outside the annotation.</summary>
    private const double OutlineInset = 2;

    /// <summary>Points kept per drawing before a new stroke is needed.</summary>
    private const int MaxStrokePoints = 4096;

    /// <summary>The colour of the live stroke while drawing: the annotation ink colour.</summary>
    private static readonly ImmutablePen StrokePen = new(new ImmutableSolidColorBrush(Color.FromUInt32(0xFF000000U | AnnotationColors.Ink)), OverlayPenWidth);

    /// <summary>The border and line pen for on-page comment indicator badges.</summary>
    private static readonly ImmutablePen BadgeBorderPen = new(new ImmutableSolidColorBrush(Color.FromArgb(0xB0, 0x20, 0x20, 0x20)), 1);

    /// <summary>The points of the stroke being drawn, in page space.</summary>
    private readonly List<PagePoint> _stroke = [];

    /// <summary>Scratch list reused while drawing comment indicator badges on visible pages.</summary>
    private readonly List<PageAnnotation> _badgeAnnotations = [];

    /// <summary>The page the stroke is drawn on, or -1.</summary>
    private int _strokePage = -1;

    /// <summary>The page a click tool was pressed on, or -1.</summary>
    private int _clickPage = -1;

    /// <summary>Whether the click fits text to a place to write; Alt places it freely.</summary>
    private bool _clickSnaps = true;

    /// <summary>Runs a click tool: adds a note, text or a stamp where the page was clicked.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="clicked">The page.</param>
    /// <param name="point">The point, in page space.</param>
    /// <param name="snaps">Whether text clicked into a place to write fits it; Alt turns this off.</param>
    private static void RunClickTool(DocumentTabViewModel tab, int clicked, PagePoint point, bool snaps)
    {
        switch (tab.Annotations.Tool)
        {
            case AnnotationTool.Note:
            {
                if (tab.Annotations.HitTest(clicked, point) is { Kind: AnnotationKind.Note } existing)
                {
                    _ = tab.Annotations.OpenBubble(existing);
                }
                else
                {
                    _ = tab.Annotations.BeginNoteBubble(clicked, point);
                }

                break;
            }

            case AnnotationTool.Text:
            {
                RunTextTool(tab.Annotations, clicked, point, snaps);
                break;
            }

            case AnnotationTool.Stamp:
            {
                _ = tab.Annotations.AddStamp(clicked, point);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>
    /// Clicking a text box edits it where it is; clicking a place to write on a printed form types fitted to it;
    /// clicking anywhere else starts typing there.
    /// </summary>
    /// <param name="annotations">The annotation state.</param>
    /// <param name="page">The page.</param>
    /// <param name="point">The point, in page space.</param>
    /// <param name="snaps">Whether text fits a place to write.</param>
    private static void RunTextTool(AnnotationsViewModel annotations, int page, PagePoint point, bool snaps)
    {
        if (annotations.HitTest(page, point) is { Kind: AnnotationKind.TextBox } box)
        {
            _ = annotations.EditText(box);
            return;
        }

        _ = snaps && annotations.RegionAt(page, point) is { } region ? annotations.BeginInRegion(page, region) : annotations.BeginText(page, point, 0);
    }

    /// <summary>Builds the menu for an annotation: its note, its colour and removing it.</summary>
    /// <param name="annotations">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The items.</returns>
    private static List<Control> AnnotationMenu(AnnotationsViewModel annotations, PageAnnotation annotation)
    {
        // The menu is only built right after the annotation was selected, so the commands' null parameter targets it.
        var name = AnnotationNames.Get(annotation.Kind);
        var colour = new MenuItem
        {
            Header = "C_olour",
            ItemsSource = ColorItems(annotations.RecolorAnnotationCommand, annotation, static (picked, color) => new AnnotationColorChoice(picked, color)),
        };
        List<Control> items = [new MenuItem { Header = name, IsEnabled = false }];
        if (annotation.Kind == AnnotationKind.TextBox)
        {
            // A text box's contents are its text, edited on the page rather than as a note.
            items.Add(Item("_Edit Text", "Enter", annotations.EditSelectedTextCommand, null, true));
            items.Add(Item("Text _Properties…", null, annotations.EditTextPropertiesCommand, null, true));
        }
        else
        {
            items.Add(Item(annotation.Contents.Length > 0 ? "_Edit Note" : "Add _Note", null, annotations.OpenBubbleForCommand, null, true));
        }

        items.AddRange(
        [
            Item("_Reply", null, annotations.ReplyInBubbleForCommand, null, true),
            new MenuItem { Header = "_Status", ItemsSource = StatusItems(annotations, annotation) },
            colour,
            new Separator(),
            Item($"_Delete {name}", "Delete", annotations.DeleteCommand, null, true),
        ]);
        return items;
    }

    /// <summary>Builds the menu for blank page: add a note or text where the pointer is.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="location">The page and point in page space.</param>
    /// <returns>The items.</returns>
    private static List<Control> PageMenu(DocumentTabViewModel tab, PageLocation location)
    {
        var annotations = tab.Annotations;
        var editable = annotations.CanAnnotate;
        return
        [
            Item("_Read Aloud from Here", null, tab.ReadAloud.ReadFromPointCommand, location, true),
            new Separator(),
            Item("Add _Note Here", null, annotations.BeginNoteBubbleHereCommand, location, editable),
            Item("_Type Text Here", null, annotations.TypeTextHereCommand, location, editable),
            new Separator(),
            Item("_Annotate…", null, annotations.StartCommand, null, editable),
        ];
    }

    /// <summary>Builds one item per review status.</summary>
    /// <param name="annotations">The annotation state.</param>
    /// <param name="annotation">The comment.</param>
    /// <returns>The items.</returns>
    private static List<MenuItem> StatusItems(AnnotationsViewModel annotations, PageAnnotation annotation)
    {
        var command = annotations.SetAnnotationStatusCommand;
        return
        [
            Item("_Accepted", null, command, new AnnotationStatusChoice(annotation, ReviewState.Accepted), true),
            Item("_Rejected", null, command, new AnnotationStatusChoice(annotation, ReviewState.Rejected), true),
            Item("_Cancelled", null, command, new AnnotationStatusChoice(annotation, ReviewState.Cancelled), true),
            Item("C_ompleted", null, command, new AnnotationStatusChoice(annotation, ReviewState.Completed), true),
        ];
    }

    /// <summary>Builds one named colour item per annotation colour.</summary>
    /// <typeparam name="TState">The type of what the colour applies to.</typeparam>
    /// <param name="command">The command a colour is chosen with.</param>
    /// <param name="state">What the colour applies to.</param>
    /// <param name="parameter">Makes the command parameter from the state and the colour.</param>
    /// <returns>The items.</returns>
    private static List<MenuItem> ColorItems<TState>(ICommand command, TState state, Func<TState, uint, object> parameter)
    {
        var items = new List<MenuItem>(AnnotationColors.All.Count);
        foreach (var (name, color) in AnnotationColors.All)
        {
            var swatch = new Border { Width = SwatchSize, Height = SwatchSize, CornerRadius = new(SwatchRadius), Background = new SolidColorBrush(Color.FromUInt32(0xFF000000U | color)) };
            var item = Item(name, null, command, parameter(state, color), true);
            item.Icon = swatch;
            items.Add(item);
        }

        return items;
    }

    /// <summary>Creates a menu item running a command.</summary>
    /// <param name="header">The header, with an access key.</param>
    /// <param name="gesture">The shortcut shown, or <see langword="null"/>.</param>
    /// <param name="command">The command.</param>
    /// <param name="parameter">The command parameter, or <see langword="null"/>.</param>
    /// <param name="enabled">Whether the item can be chosen.</param>
    /// <returns>The item.</returns>
    private static MenuItem Item(string header, string? gesture, ICommand command, object? parameter, bool enabled) =>
        new() { Header = header, Command = command, CommandParameter = parameter, IsEnabled = enabled, InputGesture = gesture is null ? null : KeyGesture.Parse(gesture) };

    /// <summary>Draws a small note badge rectangle with two ruled lines inside.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="rect">The badge rectangle on the canvas.</param>
    /// <param name="color">The badge colour as 0xRRGGBB.</param>
    private static void DrawBadgeIcon(DrawingContext context, in Rect rect, uint color)
    {
        context.DrawRectangle(ColourBrushes.Opaque(color), BadgeBorderPen, rect, BadgeRadius, BadgeRadius);
        var x1 = rect.Left + (rect.Width * BadgeLineLeft);
        var x2 = rect.Left + (rect.Width * BadgeLineRight);
        var y1 = rect.Top + (rect.Height * BadgeFirstLineY);
        var y2 = rect.Top + (rect.Height * BadgeSecondLineY);
        context.DrawLine(BadgeBorderPen, new(x1, y1), new(x2, y1));
        context.DrawLine(BadgeBorderPen, new(x1, y2), new(x2, y2));
    }

    /// <summary>Handles a left press for the active annotation tool.</summary>
    /// <param name="position">The canvas point.</param>
    /// <param name="e">The event.</param>
    /// <returns><see langword="true"/> when the press was handled.</returns>
    private bool BeginAnnotationPress(Point position, PointerPressedEventArgs e)
    {
        if (Tab is not { } tab)
        {
            return false;
        }

        var annotations = tab.Annotations;
        var page = _layout.HitTest(position.X, position.Y);
        if (BeginDrawingTool(tab, page, position, e))
        {
            return true;
        }

        switch (annotations.Tool)
        {
            case AnnotationTool.Draw when page >= 0:
            {
                _stroke.Clear();
                _strokePage = page;
                _stroke.Add(ToPage(tab, page, position));
                e.Pointer.Capture(this);
                return true;
            }

            case AnnotationTool.Text when page >= 0 && TryActivateField(tab, page, position):
            {
                // Form fields are filled in, not typed over.
                return true;
            }

            case AnnotationTool.Note or AnnotationTool.Text or AnnotationTool.Stamp when page >= 0:
            {
                _clickPage = page;
                _clickSnaps = (e.KeyModifiers & KeyModifiers.Alt) == 0;
                return true;
            }

            case AnnotationTool.Select when page >= 0:
            {
                return BeginSelectPress(tab, page, position, e);
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Handles a press with the select tool: fills a field, edits double-clicked text, or picks and drags an annotation.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page pressed.</param>
    /// <param name="position">The canvas point.</param>
    /// <param name="e">The event.</param>
    /// <returns><see langword="true"/> when the press was handled.</returns>
    private bool BeginSelectPress(DocumentTabViewModel tab, int page, Point position, PointerPressedEventArgs e)
    {
        if (TryActivateField(tab, page, position))
        {
            return true;
        }

        // Double-clicking text on the page edits it there. Movable annotations are dragged to move them; text markup is
        // picked and the text stays selectable.
        var annotations = tab.Annotations;
        return e.ClickCount >= DoubleClick && annotations.HitTest(page, ToPage(tab, page, position)) is { Kind: AnnotationKind.TextBox } box
            ? annotations.EditText(box)
            : BeginEdit(tab, page, position, e) || (annotations.Selected is not null && !TryHitTestCharacter(position, out _, out _));
    }

    /// <summary>Adds a point to the stroke being drawn.</summary>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> when a stroke is being drawn.</returns>
    private bool ContinueStroke(Point position)
    {
        HoverPolygon(position);
        if (ContinueShape(position) || ContinueEdit(position))
        {
            return true;
        }

        if (_strokePage < 0 || Tab is not { } tab)
        {
            return false;
        }

        if (_stroke.Count < MaxStrokePoints)
        {
            _stroke.Add(ToPage(tab, _strokePage, position));
            InvalidateVisual();
        }

        return true;
    }

    /// <summary>Finishes a drawing or click tool on release.</summary>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> when the release was handled.</returns>
    private bool EndAnnotationPress(Point position)
    {
        if (Tab is not { } tab)
        {
            return false;
        }

        var annotations = tab.Annotations;
        if (EndShape(tab, position) || EndEdit(tab, position))
        {
            return true;
        }

        if (_strokePage >= 0)
        {
            var page = _strokePage;
            _strokePage = -1;
            if (_stroke.Count > 1)
            {
                ReadOnlySpan<int> lengths = [_stroke.Count];
                _ = annotations.AddInk(page, CollectionsMarshal.AsSpan(_stroke), lengths, AnnotationKind.Ink);
            }

            _stroke.Clear();
            InvalidateVisual();
            return true;
        }

        if (_clickPage < 0)
        {
            return false;
        }

        var clicked = _clickPage;
        _clickPage = -1;
        RunClickTool(tab, clicked, ToPage(tab, clicked, position), _clickSnaps);
        return true;
    }

    /// <summary>Marks the selected text when a markup tool is active, then clears the selection.</summary>
    private void ApplyMarkupTool()
    {
        if (Tab is not { } tab || AnnotationsViewModel.GetMarkupKind(tab.Annotations.Tool) is not { } kind)
        {
            return;
        }

        MarkSelection(tab, kind, tab.Annotations.Color);
    }

    /// <summary>Marks the selected text and clears the selection.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="kind">The markup kind.</param>
    /// <param name="color">The colour.</param>
    private void MarkSelection(DocumentTabViewModel tab, AnnotationKind kind, uint color)
    {
        if (!TryGetSelection(out _, out _))
        {
            return;
        }

        if (tab.Annotations.MarkText(kind, GetSelectionLines(), color))
        {
            ClearSelection();
        }
    }

    /// <summary>Gets the line rectangles of the selection on each page it covers.</summary>
    /// <returns>The rectangles by page; empty when nothing is selected.</returns>
    private Dictionary<int, List<PageRect>> GetSelectionLines()
    {
        var lines = new Dictionary<int, List<PageRect>>();
        if (!TryGetSelection(out var start, out var end))
        {
            return lines;
        }

        for (var page = start.Page; page <= end.Page; page++)
        {
            lines[page] = [.. GetSelectionRects(page)];
        }

        return lines;
    }

    /// <summary>Opens the right-click menu for whatever is under the pointer.</summary>
    /// <param name="position">The canvas point.</param>
    private void ShowContextMenu(Point position)
    {
        if (Tab is not { } tab)
        {
            return;
        }

        var page = _layout.HitTest(position.X, position.Y);
        var annotations = tab.Annotations;
        var hit = page >= 0 ? annotations.HitTest(page, ToPage(tab, page, position)) : null;
        List<Control> items;
        if (TryGetSelection(out _, out _))
        {
            items = SelectionMenu(tab);
        }
        else if (hit is not null)
        {
            annotations.Select(hit);
            items = AnnotationMenu(annotations, hit);
        }
        else if (page >= 0)
        {
            items = PageMenu(tab, new(page, ToPage(tab, page, position)));
        }
        else
        {
            return;
        }

        var menu = new ContextMenu { ItemsSource = items, Placement = PlacementMode.Pointer };
        menu.Open(this);
        InvalidateVisual();
    }

    /// <summary>Builds the menu for selected text: copy, then the ways to mark it.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns>The items.</returns>
    private List<Control> SelectionMenu(DocumentTabViewModel tab)
    {
        var annotations = tab.Annotations;
        var editable = annotations.CanAnnotate;
        var mark = annotations.MarkSelectionCommand;
        var lines = GetSelectionLines();
        var readFrom = TryGetSelection(out var start, out _) ? new PageCharacter(start.Page, start.Char) : default;
        var highlight = new MenuItem
        {
            Header = "_Highlight",
            IsEnabled = editable,
            ItemsSource = ColorItems(mark, lines, static (selected, color) => new MarkSelectionRequest(AnnotationKind.Highlight, color, selected)),
        };
        return
        [
            Item("_Copy", "Ctrl+C", _copyCommand, GetSelectedText(), true),
            Item("_Read Aloud from Here", null, tab.ReadAloud.ReadFromCharacterCommand, readFrom, true),
            new Separator(),
            highlight,
            Item("_Underline", null, mark, new MarkSelectionRequest(AnnotationKind.Underline, annotations.Color, lines), editable),
            Item("_Strike Out", null, mark, new MarkSelectionRequest(AnnotationKind.StrikeOut, AnnotationColors.Clay, lines), editable),
            Item("S_quiggly Underline", null, mark, new MarkSelectionRequest(AnnotationKind.Squiggly, AnnotationColors.Clay, lines), editable),
        ];
    }

    /// <summary>
    /// Handles the polygon tools' keys, Escape (clear the selection and the pick), Delete (remove the picked
    /// annotation) and the arrow keys (nudge the picked annotation).
    /// </summary>
    /// <param name="e">The key press.</param>
    /// <returns><see langword="true"/> when the key was used.</returns>
    private bool HandleAnnotationKey(KeyEventArgs e)
    {
        var key = e.Key;
        if (HandlePolygonKey(key) || NudgeSelected(e))
        {
            return true;
        }

        if (key == Key.Escape)
        {
            ClearSelection();
            Tab?.Annotations.CloseBubble();
            Tab?.Annotations.Select(null);
            return false;
        }

        if (EditPickedText(e))
        {
            return true;
        }

        if (key != Key.Delete || Tab?.Annotations is not { Selected: { } selected } annotations)
        {
            return false;
        }

        annotations.Delete(selected);
        return true;
    }

    /// <summary>Edits the picked text box on Enter or F2, as a spreadsheet edits its cell.</summary>
    /// <param name="e">The key press.</param>
    /// <returns><see langword="true"/> when editing started.</returns>
    private bool EditPickedText(KeyEventArgs e) =>
        e.Key is Key.Enter or Key.F2 && e.KeyModifiers == KeyModifiers.None && Tab?.Annotations is { Selected.Kind: AnnotationKind.TextBox } annotations
        && annotations.EditText(annotations.Selected);

    /// <summary>Puts text on the clipboard.</summary>
    /// <param name="text">The text.</param>
    private void CopyToClipboard(string text)
    {
        if (text.Length > 0 && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            _ = clipboard.SetTextAsync(text);
        }
    }

    /// <summary>Converts a canvas point to page space on a page.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="position">The canvas point.</param>
    /// <returns>The page point.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PagePoint ToPage(DocumentTabViewModel tab, int page, Point position) =>
        new PageTransform(_layout.GetPageBounds(page), _sizes[page], tab.Rotation, _layout.Options.Scale).ToPage(position);

    /// <summary>Draws the stroke being drawn, comment badges and the outline of the picked annotation.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    private void DrawAnnotationOverlay(DrawingContext context, DocumentTabViewModel tab)
    {
        DrawCommentBadges(context, tab);
        DrawShapePreview(context, tab);
        DrawPolygonPreview(context, tab);
        DrawPlacement(context, tab);
        DrawEditing(context, tab);
        DrawFieldFocus(context, tab);
        if (_strokePage >= 0 && _stroke.Count > 1)
        {
            var transform = new PageTransform(_layout.GetPageBounds(_strokePage), _sizes[_strokePage], tab.Rotation, _layout.Options.Scale);
            var previous = transform.ToCanvas(_stroke[0]);
            for (var i = 1; i < _stroke.Count; i++)
            {
                var next = transform.ToCanvas(_stroke[i]);
                context.DrawLine(StrokePen, previous, next);
                previous = next;
            }
        }

        if (tab.Annotations.Selected is { } selected && selected.PageIndex < _sizes.Length && _currentHitPen is { } pen)
        {
            var transform = new PageTransform(_layout.GetPageBounds(selected.PageIndex), _sizes[selected.PageIndex], tab.Rotation, _layout.Options.Scale);
            context.DrawRectangle(null, pen, transform.ToCanvas(selected.Bounds).Inflate(OutlineInset));
        }
    }

    /// <summary>
    /// Draws the speech-bubble badges on commented annotations on visible pages, and the icon preview of a new sticky note
    /// being written in the on-page bubble.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    private void DrawCommentBadges(DrawingContext context, DocumentTabViewModel tab)
    {
        if (_scroller is not { } scroller)
        {
            return;
        }

        _layout.GetVisiblePages(scroller.Offset.Y, scroller.Offset.Y + scroller.Viewport.Height, out var first, out var last);
        for (var page = first; page <= last && page >= 0 && page < _sizes.Length; page++)
        {
            DrawPageCommentBadges(context, tab, page);
        }

        if (tab.Annotations.ActiveBubble is not { NewNoteLocation: not null } bubble || (uint)bubble.PageIndex >= (uint)_sizes.Length)
        {
            return;
        }

        var transform = new PageTransform(_layout.GetPageBounds(bubble.PageIndex), _sizes[bubble.PageIndex], tab.Rotation, _layout.Options.Scale);
        DrawBadgeIcon(context, transform.ToCanvas(bubble.Bounds), bubble.Color);
    }

    /// <summary>Draws the speech-bubble badges on one page.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page index.</param>
    private void DrawPageCommentBadges(DrawingContext context, DocumentTabViewModel tab, int page)
    {
        tab.Annotations.GetPageAnnotations(page, _badgeAnnotations);
        if (_badgeAnnotations.Count == 0)
        {
            return;
        }

        var transform = new PageTransform(_layout.GetPageBounds(page), _sizes[page], tab.Rotation, _layout.Options.Scale);
        foreach (var annotation in _badgeAnnotations)
        {
            if (AnnotationsViewModel.HasCommentBadge(annotation))
            {
                DrawBadgeIcon(context, transform.ToCanvas(AnnotationsViewModel.CommentBadgeBounds(annotation)), annotation.Color);
            }
        }
    }
}
