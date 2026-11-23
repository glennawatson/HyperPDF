// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer.Text;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>
/// A dialog made of rows: text, switches, lists to choose from, text boxes and actions. The preferences, properties, licences
/// and print windows are all this dialog with different rows. The rows are read from the view model again whenever something
/// changes, so the dialog always shows what the view model holds.
/// </summary>
internal static class FormOverlays
{
    /// <summary>The lines of rows shown at once.</summary>
    private const int VisibleLines = 15;

    /// <summary>The share of a dialog's width the column of setting names takes.</summary>
    private const double LabelShare = 0.42;

    /// <summary>The paddings taken off the column of controls: one on each side of a control and one at the dialog's edge.</summary>
    private const int ControlPaddings = 3;

    /// <summary>The fewest lines a dialog with a picture or a pad is tall, so the picture has room.</summary>
    private const int PictureLines = 10;

    /// <summary>The height of the line of words shown in place of a picture, in device pixels.</summary>
    private const int PreviewLineHeight = 40;

    /// <summary>The lines moved by Page Up and Page Down.</summary>
    private const int PageLines = 12;

    /// <summary>What a length is divided by to find its middle.</summary>
    private const int HalfDivisor = 2;

    /// <summary>Opens the dialog.</summary>
    /// <param name="request">What the dialog shows.</param>
    /// <returns>The dialog and its answer: <see langword="true"/> when the user agreed, <see langword="false"/> when the dialog was closed.</returns>
    internal static OpenedOverlay<bool> Open(FormRequest request)
    {
        var input = new Signal<OverlayInput>();
        var edits = new Signal<FormEdit>();
        var strokes = new Signal<PadStroke>();
        var changes = new Signal<Func<FormUi, FormUi>>();
        var builder = new FormBuilder();
        var ui = changes.ScanWithInitial(FormUi.Start, static (current, change) => change(current)).ToReadOnlyState(FormUi.Start, static current => current);
        var presses = input.SelectPressedItems();
        var keys = input.WhereKeyPress();
        var acceptTriggers = Signal.Merge(
            presses.Where(static item => item.Action == OverlayAction.Accept).AsSignal(),
            keys.Where(key => key.Key == ViewerKey.Enter && request.AcceptText is not null && CanRunAccept(request)).AsSignal());
        var answers = Signal.Merge(
                acceptTriggers.Where(_ => request.AcceptCommand is null).Select(static _ => true),
                presses.Where(static item => item.Action == OverlayAction.Cancel).Select(static _ => false),
                keys.Where(static key => key.Key == ViewerKey.Escape).Select(static _ => false),
                input.SelectDismissals().Select(static _ => false),
                (request.Answered ?? Signal.Never<bool>()).ObserveOn(RxSchedulers.MainThreadScheduler))
            .Select(static agreed => (bool?)agreed)
            .Take(1)
            .ToReadOnlyState(null, static answer => answer);
        var answered = answers.Where(static answer => answer is not null);
        var overlay = new Overlay(
            OverlayKind.Dialog,
            request.Pad is null ? OverlayTraits.Modal | OverlayTraits.TakesAllKeys : OverlayTraits.Modal | OverlayTraits.TakesAllKeys | OverlayTraits.TakesPointer,
            (List<ChromeItem> items, in OverlayPlace place) => Build(items, request, builder, ui, changes, in place),
            input,
            request.Changes,
            answered.AsSignal(),
            new MultipleDisposable(
                input,
                edits,
                strokes,
                changes,
                ui,
                answers,
                request.Bind(edits),
                request.Pad?.Invoke(strokes) ?? EmptyDisposable.Instance,
                request.AcceptCommand is { } acceptCommand ? acceptTriggers.InvokeCommand(acceptCommand) : EmptyDisposable.Instance,
                (request.PadCleared ?? Signal.Never<RxVoid>()).SubscribeSafe(_ => changes.OnNext(static current => current with { Ink = PadInk.Empty }), ErrorTrace.Write),
                input.WherePad().SubscribeSafe(pointer => Draw(ui, changes, strokes, in pointer), ErrorTrace.Write),
                presses.Where(static item => item.Action == OverlayAction.PickOption).SubscribeSafe(item => ChooseValue(request, builder, edits, changes, item.Index), ErrorTrace.Write),
                presses.Where(static item => item.Action == OverlayAction.PickRow).SubscribeSafe(item => PressRow(request, builder, edits, changes, item.Index), ErrorTrace.Write),
                presses.Where(static item => item.Action == OverlayAction.ResetRow).SubscribeSafe(item => ResetRow(request, builder, edits, item.Index), ErrorTrace.Write),
                keys.Where(static key => key.Key == ViewerKey.Back).SubscribeSafe(_ => EditFocused(request, builder, ui, edits, ChromeText.WithoutLast), ErrorTrace.Write),
                Signal.Merge(keys.Select(static key => LinesOf(key.Key)), input.SelectScrollRows()).Where(static lines => lines != 0)
                    .SubscribeSafe(lines => changes.OnNext(current => current with { First = Math.Max(0, current.First + lines) }), ErrorTrace.Write),
                input.SelectTypedRunes().SubscribeSafe(rune => EditFocused(request, builder, ui, edits, text => string.Concat(text, rune.ToString())), ErrorTrace.Write)))
        {
            Picture = () => ui.Value.Picture,
            Ink = () => ui.Value.Ink,
        };
        return new(overlay, answered.Select(static answer => answer!.Value).Take(1));
    }

    /// <summary>Tells how far a key moves the rows.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The lines to move down, negative to move up, or zero for a key that does not move the rows.</returns>
    private static int LinesOf(ViewerKey key) => key switch
    {
        ViewerKey.Down => 1,
        ViewerKey.Up => -1,
        ViewerKey.PageDown => PageLines,
        ViewerKey.PageUp => -PageLines,
        _ => 0,
    };

    /// <summary>Reads the rows from the view model again.</summary>
    /// <param name="request">What the dialog shows.</param>
    /// <param name="builder">Receives the rows.</param>
    private static void Describe(FormRequest request, FormBuilder builder)
    {
        builder.Rows.Clear();
        request.Describe(builder);
    }

    /// <summary>
    /// Gets the room every line keeps at its end for the button that puts a setting back: a square as tall as a line, when
    /// any row of the dialog has a value it starts with, and none otherwise.
    /// </summary>
    /// <param name="builder">The rows.</param>
    /// <param name="metrics">The sizes.</param>
    /// <returns>The room in device pixels.</returns>
    private static int ResetRoom(FormBuilder builder, in ChromeMetrics metrics)
    {
        foreach (var row in builder.Rows)
        {
            if (row.CanReset)
            {
                return metrics.RowHeight + metrics.Gap;
            }
        }

        return 0;
    }

    /// <summary>Asks the view model to put the setting of a row back to the value it starts with.</summary>
    /// <param name="request">What the dialog shows.</param>
    /// <param name="builder">The rows.</param>
    /// <param name="edits">Takes the change to the view model.</param>
    /// <param name="index">The row.</param>
    private static void ResetRow(FormRequest request, FormBuilder builder, Signal<FormEdit> edits, int index)
    {
        Describe(request, builder);
        if ((uint)index < (uint)builder.Rows.Count && builder.Rows[index] is { CanReset: true, Id: >= 0 } row)
        {
            edits.OnNext(new(row.Id, 0, string.Empty, FormBuilder.ResetRequest));
        }
    }

    /// <summary>Writes a new value of a row to the view model.</summary>
    /// <param name="builder">The rows.</param>
    /// <param name="edits">Takes the change to the view model.</param>
    /// <param name="row">The row.</param>
    /// <param name="index">The chosen index, for a list or a switch.</param>
    /// <param name="text">The text, for a typed setting.</param>
    private static void Apply(FormBuilder builder, Signal<FormEdit> edits, int row, int index, string text)
    {
        if ((uint)row < (uint)builder.Rows.Count && builder.Rows[row] is { Enabled: true, Id: >= 0 } changed)
        {
            edits.OnNext(new(changed.Id, index, text, changed.Target));
        }
    }

    /// <summary>Takes the value chosen from an open list, writes it to the view model and closes the list.</summary>
    /// <param name="request">What the dialog shows.</param>
    /// <param name="builder">The rows.</param>
    /// <param name="edits">Takes the change to the view model.</param>
    /// <param name="changes">Takes the change to what the user has done.</param>
    /// <param name="index">The row and the value within it, as one number.</param>
    private static void ChooseValue(FormRequest request, FormBuilder builder, Signal<FormEdit> edits, Signal<Func<FormUi, FormUi>> changes, int index)
    {
        Describe(request, builder);
        var row = Math.DivRem(index, FormLines.OptionSpan, out var option);
        Apply(builder, edits, row, option, string.Empty);
        changes.OnNext(static current => current with { Open = -1 });
    }

    /// <summary>Handles a press on a row: flips a switch, opens or closes a list, starts typing into a text box or runs an action.</summary>
    /// <param name="request">What the dialog shows.</param>
    /// <param name="builder">The rows.</param>
    /// <param name="edits">Takes the change to the view model.</param>
    /// <param name="changes">Takes the change to what the user has done.</param>
    /// <param name="index">The row.</param>
    private static void PressRow(FormRequest request, FormBuilder builder, Signal<FormEdit> edits, Signal<Func<FormUi, FormUi>> changes, int index)
    {
        Describe(request, builder);
        if ((uint)index >= (uint)builder.Rows.Count)
        {
            return;
        }

        var row = builder.Rows[index];
        switch (row.Kind)
        {
            case FormRowKind.Toggle:
                {
                    Apply(builder, edits, index, row.Selected == 0 ? 1 : 0, string.Empty);
                    break;
                }

            case FormRowKind.Choice:
                {
                    changes.OnNext(current => current with { Open = current.Open == index ? -1 : index });
                    break;
                }

            case FormRowKind.Field:
                {
                    changes.OnNext(current => current with { Focus = index });
                    break;
                }

            default:
                {
                    Apply(builder, edits, index, 0, string.Empty);
                    break;
                }
        }
    }

    /// <summary>Changes the text of the text box being typed into.</summary>
    /// <param name="request">What the dialog shows.</param>
    /// <param name="builder">The rows.</param>
    /// <param name="ui">What the user has done.</param>
    /// <param name="edits">Takes the change to the view model.</param>
    /// <param name="edit">Makes the new text from the text now.</param>
    private static void EditFocused(FormRequest request, FormBuilder builder, ReadOnlyState<FormUi> ui, Signal<FormEdit> edits, Func<string, string> edit)
    {
        // The rows are read again first, so two characters typed between frames both land on the text the view model holds.
        Describe(request, builder);
        var focus = FocusedField(request, builder, ui.Value.Focus);
        if (focus >= 0)
        {
            Apply(builder, edits, focus, 0, edit(builder.Rows[focus].Value));
        }
    }

    /// <summary>
    /// Finds the text box that typing goes to: the one pressed. A dialog that is opened to be typed into, such as the one
    /// that takes a name to sign with, starts in its first text box. Any other dialog takes no typing until a box is
    /// pressed, so a stray key never changes a setting.
    /// </summary>
    /// <param name="request">What the dialog shows.</param>
    /// <param name="builder">The rows.</param>
    /// <param name="pressed">The row pressed last, or -1.</param>
    /// <returns>The row of the text box, or -1 when typing goes nowhere.</returns>
    private static int FocusedField(FormRequest request, FormBuilder builder, int pressed)
    {
        if ((uint)pressed < (uint)builder.Rows.Count && builder.Rows[pressed].Kind == FormRowKind.Field)
        {
            return pressed;
        }

        return request.StartsTyping ? builder.Rows.FindIndex(static row => row.Kind == FormRowKind.Field) : -1;
    }

    /// <summary>Follows the pointer on the pad: a stroke starts where it goes down, grows as it moves and is finished where it is let go.</summary>
    /// <param name="ui">What the user has done.</param>
    /// <param name="changes">Takes the change to what the user has done.</param>
    /// <param name="strokes">Takes the stroke, for whatever the view model does with it.</param>
    /// <param name="pointer">Where the pointer is, and whether it went down, moved or was let go.</param>
    private static void Draw(ReadOnlyState<FormUi> ui, Signal<Func<FormUi, FormUi>> changes, Signal<PadStroke> strokes, in OverlayInput pointer)
    {
        var now = ui.Value;
        var drawing = pointer.Kind == OverlayInputKind.PadDown ? now.Pad.Contains(pointer.Value, pointer.Y) : now.Drawing;
        if (!drawing)
        {
            return;
        }

        // A stroke stays on the paper, so a pointer that leaves it ends the stroke at the edge.
        var point = new PagePoint(
            (float)Math.Clamp(pointer.Value - now.Pad.X, 0, now.Pad.Width - 1),
            (float)Math.Clamp(pointer.Y - now.Pad.Y, 0, now.Pad.Height - 1));
        var done = pointer.Kind == OverlayInputKind.PadUp;
        PagePoint[] points = pointer.Kind == OverlayInputKind.PadDown ? [point] : [.. now.Stroke, point];
        var stroke = new PadStroke(points, done);
        changes.OnNext(current => current with { Ink = PadInks.Follow(current.Seeded ? PadInk.Empty : current.Ink, stroke), Seeded = false, Drawing = !done, Stroke = done ? [] : points });
        strokes.OnNext(stroke);
    }

    /// <summary>Lays the dialog out, reading its rows from the view model first.</summary>
    /// <param name="items">Receives the controls, back to front.</param>
    /// <param name="request">What the dialog shows.</param>
    /// <param name="builder">The rows.</param>
    /// <param name="ui">What the user has done.</param>
    /// <param name="changes">Takes what this layout works out, so what moves the rows knows how many lines there are.</param>
    /// <param name="place">What to lay them out with.</param>
    private static void Build(List<ChromeItem> items, FormRequest request, FormBuilder builder, ReadOnlyState<FormUi> ui, Signal<Func<FormUi, FormUi>> changes, in OverlayPlace place)
    {
        var runs = place.Runs;
        var metrics = place.Metrics;
        var size = place.Size;
        Describe(request, builder);
        var rowsWidth = Math.Min(metrics.DialogWidth + metrics.PopupWidth, size.Width);

        // A dialog with a picture, such as the sheet a print makes, is wider by the picture's column when the window has the room.
        var pictureWidth = request.Picture is null && request.Pad is null ? 0 : Math.Min(metrics.DialogWidth >> 1, Math.Max(0, size.Width - rowsWidth));
        var width = rowsWidth + pictureWidth;
        var inner = rowsWidth - metrics.Padding - metrics.Padding;
        var focused = FocusedField(request, builder, ui.Value.Focus);

        // The names of the settings share one column and their controls another, so every dialog reads as the same form.
        var labelWidth = (int)(rowsWidth * LabelShare);
        var resetRoom = ResetRoom(builder, in metrics);
        var fit = new FormFit(
            runs,
            ChromeBars.Style(metrics),
            inner - metrics.Padding - metrics.Padding - resetRoom,
            labelWidth - metrics.Padding - metrics.Padding,
            rowsWidth - labelWidth - (metrics.Padding * ControlPaddings) - resetRoom,
            metrics.RowHeight);
        var lines = FormLines.Flatten(builder, ui.Value with { Focus = focused }, in fit);
        var first = Math.Clamp(ui.Value.First, 0, Math.Max(0, lines.Count - VisibleLines));

        // A short window shows fewer lines, so the heading and the buttons always fit. A dialog with few rows is only as tall
        // as its rows, with room kept for a picture or a pad, so it is not a slab of empty space. Once open it never gets
        // shorter: rows that go away leave room behind, and the buttons stay where they were.
        var around = metrics.RowHeight + metrics.RowHeight + metrics.ToolbarHeight + metrics.Padding + metrics.Padding;
        var wanted = Math.Max(Math.Max(lines.Count + 1, pictureWidth > 0 ? PictureLines : 1), ui.Value.Tall);
        var shown = Math.Min(Math.Min(VisibleLines, wanted), Math.Max(1, (size.Height - around) / metrics.RowHeight));
        var height = (metrics.RowHeight * (shown + 1)) + metrics.ToolbarHeight + metrics.Padding + metrics.Padding;
        var left = (size.Width - width) >> 1;
        var top = Math.Max(0, (size.Height - height) >> 1);
        items.Add(new(ChromeItemKind.Popup, null, 0, string.Empty, new(left, top, width, height), true, false));
        var y = top + metrics.Padding;
        items.Add(new(ChromeItemKind.Heading, null, 0, ChromeText.Fit(request.Title, ChromeBars.Style(metrics), inner, runs), new(left, y, rowsWidth, metrics.RowHeight), true, false));
        y += metrics.RowHeight;
        Report(request, ui, changes, AddPicture(items, request, new(left + rowsWidth, y, pictureWidth - metrics.Padding, metrics.RowHeight * shown)) with { Tall = shown }, first);

        var last = Math.Min(lines.Count, first + shown);
        var lineLook = new FormLineLook(labelWidth, metrics.Padding, metrics.Gap, runs, fit.Style) { ResetRoom = resetRoom, ResetName = UiText.Get("Reset") };
        FormLayout.AddLines(items, lines.GetRange(first, last - first), new(left, y, rowsWidth, metrics.RowHeight), in lineLook);
        FormLayout.AddScrollBar(items, new(left + rowsWidth - metrics.Padding, y, metrics.Gap, metrics.RowHeight * shown), first, shown, lines.Count, place.Theme);

        var buttonsTop = top + height - metrics.ToolbarHeight - metrics.Padding + metrics.Gap;
        var buttonRow = new ChromeRow(items, runs, metrics, left + metrics.Padding, buttonsTop, metrics.ToolbarHeight - metrics.Gap - metrics.Gap) { Limit = left + rowsWidth - metrics.Padding };

        // The bar down the side already says how far through the rows the view is, so no count of lines is written out.
        _ = AddButtons(in buttonRow, request);
    }

    /// <summary>Tells the state what this layout worked out: the first line, the picture and the pad, and the drawing a pad starts with.</summary>
    /// <param name="request">What the dialog shows.</param>
    /// <param name="ui">What the user has done.</param>
    /// <param name="changes">Takes the change.</param>
    /// <param name="laid">The picture and where the picture or the pad lies.</param>
    /// <param name="first">The first line shown.</param>
    private static void Report(FormRequest request, ReadOnlyState<FormUi> ui, Signal<Func<FormUi, FormUi>> changes, FormUi laid, int first)
    {
        var now = ui.Value;
        var seed = now.Seeded || laid.Pad.Width <= 0 ? null : request.PadSeed?.Invoke(laid.Pad);
        if (first == now.First && ReferenceEquals(laid.Picture, now.Picture) && laid.Pad == now.Pad && seed is null && laid.Tall <= now.Tall)
        {
            return;
        }

        changes.OnNext(current => current with
        {
            First = first,
            Picture = laid.Picture,
            Pad = laid.Pad,
            Ink = seed ?? current.Ink,
            Seeded = current.Seeded || seed is not null,
            Tall = Math.Max(current.Tall, laid.Tall),
        });
    }

    /// <summary>Tells whether the dialog's primary action can run right now.</summary>
    /// <param name="request">What the dialog shows.</param>
    /// <returns><see langword="true"/> when the primary action is enabled.</returns>
    private static bool CanRunAccept(FormRequest request) =>
        (request.CanAccept?.Invoke() ?? true) && (request.AcceptCommand?.CanExecute(null) ?? true);

    /// <summary>Adds the buttons along the bottom: the one that agrees when there is one, and the one that closes.</summary>
    /// <param name="buttons">The row the buttons go in.</param>
    /// <param name="request">What the dialog shows.</param>
    /// <returns>The row after the buttons.</returns>
    private static ChromeRow AddButtons(in ChromeRow buttons, FormRequest request)
    {
        var agreed = request.AcceptText is { } accept ? buttons.Button(request.AcceptIcon, accept, OverlayAction.Accept, CanRunAccept(request)) : buttons;
        var closeLabel = request.CloseText ?? (request.AcceptText is null ? UiText.Get("Close") : UiText.Get("Cancel"));
        return agreed.Button(closeLabel, OverlayAction.Cancel, true);
    }

    /// <summary>Adds the dialog's drawing pad, which fills its column. It is drawn into the frame itself, so there is no picture to make.</summary>
    /// <param name="items">Receives the pad.</param>
    /// <param name="request">What the dialog shows.</param>
    /// <param name="column">The room beside the rows, in device pixels.</param>
    /// <returns>Where the pad lies, or nothing when it is not shown.</returns>
    private static FormUi AddPad(List<ChromeItem> items, FormRequest request, in LayoutRect column)
    {
        if (column.Width <= 0 || column.Height <= 0 || !(request.PadShown?.Invoke() ?? true))
        {
            return FormUi.Start;
        }

        items.Add(new(ChromeItemKind.Pad, null, 0, string.Empty, column, true, false));
        return FormUi.Start with { Pad = column };
    }

    /// <summary>Adds the dialog's picture or drawing pad beside the rows.</summary>
    /// <param name="items">Receives the picture or the pad.</param>
    /// <param name="request">What the dialog shows.</param>
    /// <param name="column">The room beside the rows, in device pixels.</param>
    /// <returns>The picture, and where the picture or the pad lies.</returns>
    private static FormUi AddPicture(List<ChromeItem> items, FormRequest request, in LayoutRect column)
    {
        // A dialog with a pad shows the pad while it is drawn on, and its picture or its words the rest of the time.
        if (request.Pad is not null && (request.PadShown?.Invoke() ?? true))
        {
            return AddPad(items, request, column);
        }

        var none = FormUi.Start;
        if (column.Width <= 0 || column.Height <= 0)
        {
            return none;
        }

        if (request.Picture?.Invoke(new((int)column.Width, (int)column.Height)) is not { } picture)
        {
            AddPreviewText(items, request, column);
            return none;
        }

        var x = column.X + ((column.Width - picture.Width) / HalfDivisor);
        var y = column.Y + ((column.Height - picture.Height) / HalfDivisor);
        var area = new LayoutRect(x, y, picture.Width, picture.Height);
        items.Add(new(ChromeItemKind.Thumbnail, null, Overlay.PictureIndex, string.Empty, area, true, false));

        // Only a dialog without a pad takes the pointer on its picture; one with a pad draws on the pad alone.
        return none with { Picture = picture, Pad = request.Pad is null ? area : default };
    }

    /// <summary>Adds the words a dialog shows where its picture would be, in the middle of the column.</summary>
    /// <param name="items">Receives the words.</param>
    /// <param name="request">What the dialog shows.</param>
    /// <param name="column">The room beside the rows, in device pixels.</param>
    private static void AddPreviewText(List<ChromeItem> items, FormRequest request, in LayoutRect column)
    {
        if (request.PreviewText?.Invoke() is not { Length: > 0 } words)
        {
            return;
        }

        var top = column.Y + ((column.Height - PreviewLineHeight) / HalfDivisor);
        items.Add(new(ChromeItemKind.Heading, null, 0, words, new(column.X, top, column.Width, PreviewLineHeight), true, false));
    }
}
