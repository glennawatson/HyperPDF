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

    /// <summary>The character shown in place of each letter of a secret.</summary>
    private const char Dot = '•';

    /// <summary>The lines moved by Page Up and Page Down.</summary>
    private const int PageLines = 12;

    /// <summary>The number a row's index is multiplied by to make room for the index of one of its list values.</summary>
    private const int OptionSpan = 4096;

    /// <summary>What a length is divided by to find its middle.</summary>
    private const int HalfDivisor = 2;

    /// <summary>The space that sets the values of an open list in from the row they belong to.</summary>
    private const string Indent = "      ";

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
        var answers = Signal.Merge(
                presses.Where(static item => item.Command is ChromeCommand.Accept or ChromeCommand.Cancel).Select(static item => item.Command == ChromeCommand.Accept),
                keys.Where(static key => key.Key == ViewerKey.Escape).Select(static _ => false),
                keys.Where(key => key.Key == ViewerKey.Enter && request.AcceptText is not null && (request.CanAccept?.Invoke() ?? true)).Select(static _ => true),
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
                (request.PadCleared ?? Signal.Never<RxVoid>()).SubscribeSafe(_ => changes.OnNext(static current => current with { Ink = PadInk.Empty }), ErrorTrace.Write),
                input.WherePad().SubscribeSafe(pointer => Draw(ui, changes, strokes, in pointer), ErrorTrace.Write),
                presses.Where(static item => item.Command == ChromeCommand.MenuEntry).SubscribeSafe(item => ChooseValue(request, builder, edits, changes, item.Index), ErrorTrace.Write),
                presses.Where(static item => item.Command == ChromeCommand.PickRow).SubscribeSafe(item => PressRow(request, builder, edits, changes, item.Index), ErrorTrace.Write),
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
        var row = Math.DivRem(index, OptionSpan, out var option);
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

    /// <summary>Gets the text a field row shows: its text, or dots in place of each letter when the text is secret.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The text to show.</returns>
    private static string Shown(FormRow row) => ReferenceEquals(row.Target, FormBuilder.MaskedField) ? new string(Dot, row.Value.Length) : row.Value;

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
        var focus = ui.Value.Focus;
        if ((uint)focus < (uint)builder.Rows.Count && builder.Rows[focus] is { Kind: FormRowKind.Field } row)
        {
            Apply(builder, edits, focus, 0, edit(row.Value));
        }
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
        var pictureWidth = request.Picture is null && request.Pad is null ? 0 : Math.Min(metrics.DialogWidth >> 1, size.Width - rowsWidth);
        var width = rowsWidth + pictureWidth;
        var inner = rowsWidth - metrics.Padding - metrics.Padding;
        var lines = Flatten(builder, ui.Value, runs, ChromeBars.Style(metrics), inner - metrics.Padding - metrics.Padding);
        var first = Math.Clamp(ui.Value.First, 0, Math.Max(0, lines.Count - VisibleLines));

        // A short window shows fewer lines, so the heading and the buttons always fit.
        var around = metrics.RowHeight + metrics.RowHeight + metrics.ToolbarHeight + metrics.Padding + metrics.Padding;
        var shown = Math.Min(VisibleLines, Math.Max(1, (size.Height - around) / metrics.RowHeight));
        var height = (metrics.RowHeight * (shown + 1)) + metrics.ToolbarHeight + metrics.Padding + metrics.Padding;
        var left = (size.Width - width) >> 1;
        var top = Math.Max(0, (size.Height - height) >> 1);
        items.Add(new(ChromeItemKind.Popup, ChromeCommand.None, 0, string.Empty, new(left, top, width, height), true, false));
        var y = top + metrics.Padding;
        items.Add(new(ChromeItemKind.Heading, ChromeCommand.None, 0, ChromeText.Fit(request.Title, ChromeBars.Style(metrics), inner, runs), new(left, y, rowsWidth, metrics.RowHeight), true, false));
        y += metrics.RowHeight;
        Report(request, ui, changes, AddPicture(items, request, new(left + rowsWidth, y, pictureWidth - metrics.Padding, metrics.RowHeight * shown)), first);

        var last = Math.Min(lines.Count, first + shown);
        AddLines(items, lines, first, last, new(left, y, rowsWidth, metrics.RowHeight), metrics.Padding);

        var buttonsTop = top + height - metrics.ToolbarHeight - metrics.Padding + metrics.Gap;
        var buttons = AddButtons(new(items, runs, metrics, left + metrics.Padding, buttonsTop, metrics.ToolbarHeight - metrics.Gap - metrics.Gap), request);
        if (lines.Count > shown)
        {
            _ = buttons.Label(UiText.Format("Lines {0} to {1} of {2}", first + 1, last, lines.Count));
        }
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
        if (first == now.First && ReferenceEquals(laid.Picture, now.Picture) && laid.Pad == now.Pad && seed is null)
        {
            return;
        }

        changes.OnNext(current => current with { First = first, Picture = laid.Picture, Pad = laid.Pad, Ink = seed ?? current.Ink, Seeded = current.Seeded || seed is not null });
    }

    /// <summary>Adds the buttons along the bottom: the one that agrees when there is one, and the one that closes.</summary>
    /// <param name="buttons">The row the buttons go in.</param>
    /// <param name="request">What the dialog shows.</param>
    /// <returns>The row after the buttons.</returns>
    private static ChromeRow AddButtons(ChromeRow buttons, FormRequest request)
    {
        var agreed = request.AcceptText is { } accept ? buttons.Button(accept, ChromeCommand.Accept, request.CanAccept?.Invoke() ?? true) : buttons;
        return agreed.Button(request.AcceptText is null ? UiText.Get("Close") : UiText.Get("Cancel"), ChromeCommand.Cancel, true);
    }

    /// <summary>Adds the lines in view, one under another.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="lines">The lines.</param>
    /// <param name="first">The first line in view.</param>
    /// <param name="last">The line after the last one in view.</param>
    /// <param name="row">Where the first line goes, which is as wide and as tall as every line.</param>
    /// <param name="padding">The room a typing box is set in from the sides.</param>
    private static void AddLines(List<ChromeItem> items, List<FormLine> lines, int first, int last, in LayoutRect row, int padding)
    {
        var y = row.Y;
        for (var i = first; i < last; i++)
        {
            var line = lines[i];
            var bounds = line.Kind == ChromeItemKind.Field ? new LayoutRect(row.X + padding, y, row.Width - padding - padding, row.Height) : row with { Y = y };
            items.Add(new(line.Kind, line.Command, line.Index, line.Text, bounds, line.Enabled, line.Selected) { Trait = line.Trait });
            y += row.Height;
        }
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

        items.Add(new(ChromeItemKind.Pad, ChromeCommand.None, 0, string.Empty, column, true, false));
        return FormUi.Start with { Pad = column };
    }

    /// <summary>Adds the dialog's picture or drawing pad beside the rows.</summary>
    /// <param name="items">Receives the picture or the pad.</param>
    /// <param name="request">What the dialog shows.</param>
    /// <param name="column">The room beside the rows, in device pixels.</param>
    /// <returns>The picture, and where the picture or the pad lies.</returns>
    private static FormUi AddPicture(List<ChromeItem> items, FormRequest request, in LayoutRect column)
    {
        if (request.Pad is not null)
        {
            return AddPad(items, request, column);
        }

        var none = FormUi.Start;
        if (request.Picture is not { } make || column.Width <= 0 || column.Height <= 0 || make(new((int)column.Width, (int)column.Height)) is not { } picture)
        {
            return none;
        }

        var x = column.X + ((column.Width - picture.Width) / HalfDivisor);
        var y = column.Y + ((column.Height - picture.Height) / HalfDivisor);
        var area = new LayoutRect(x, y, picture.Width, picture.Height);
        items.Add(new(ChromeItemKind.Thumbnail, ChromeCommand.None, Overlay.PictureIndex, string.Empty, area, true, false));
        return none with { Picture = picture, Pad = area };
    }

    /// <summary>Turns the rows into the lines that are shown.</summary>
    /// <param name="builder">The rows.</param>
    /// <param name="ui">What the user has done.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="style">How the text looks.</param>
    /// <param name="width">The width a line of text must fit.</param>
    /// <returns>The lines.</returns>
    private static List<FormLine> Flatten(FormBuilder builder, FormUi ui, TextRuns runs, TextStyle style, int width)
    {
        var lines = new List<FormLine>();
        var wrapped = new List<string>();
        for (var i = 0; i < builder.Rows.Count; i++)
        {
            var row = builder.Rows[i];
            switch (row.Kind)
            {
                case FormRowKind.Heading:
                    {
                        lines.Add(new(ChromeItemKind.Heading, ChromeCommand.None, i, ChromeText.Fit(row.Label, style, width, runs), true, false));
                        break;
                    }

                case FormRowKind.Text:
                    {
                        wrapped.Clear();
                        ChromeText.Wrap(row.Label, style, width, runs, wrapped);
                        foreach (var line in wrapped)
                        {
                            lines.Add(new(ChromeItemKind.Label, ChromeCommand.None, i, line, true, false));
                        }

                        break;
                    }

                default:
                    {
                        FlattenSetting(lines, row, i, ui, runs, style, width);
                        break;
                    }
            }
        }

        return lines;
    }

    /// <summary>Turns a row that can be pressed into its lines.</summary>
    /// <param name="lines">Receives the lines.</param>
    /// <param name="row">The row.</param>
    /// <param name="index">The row's index.</param>
    /// <param name="ui">What the user has done.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="style">How the text looks.</param>
    /// <param name="width">The width a line of text must fit.</param>
    private static void FlattenSetting(List<FormLine> lines, FormRow row, int index, FormUi ui, TextRuns runs, TextStyle style, int width)
    {
        switch (row.Kind)
        {
            case FormRowKind.Toggle:
                {
                    var state = row.Selected != 0 ? UiText.Get("On") : UiText.Get("Off");
                    var text = ChromeText.Fit($"{row.Label}: {state}", style, width, runs);
                    lines.Add(new(ChromeItemKind.Row, ChromeCommand.PickRow, index, text, row.Enabled, row.Selected != 0) { Trait = ChromeItemTrait.Switch });
                    break;
                }

            case FormRowKind.Choice:
                {
                    var chosen = (uint)row.Selected < (uint)row.Options.Count ? row.Options[row.Selected] : string.Empty;
                    lines.Add(new(ChromeItemKind.Row, ChromeCommand.PickRow, index, ChromeText.Fit($"{row.Label}: {chosen}  ›", style, width, runs), row.Enabled, false)
                    {
                        Trait = ui.Open == index ? ChromeItemTrait.Expanded : ChromeItemTrait.Collapsed,
                    });
                    for (var option = 0; ui.Open == index && option < row.Options.Count; option++)
                    {
                        var text = ChromeText.Fit(string.Concat(Indent, row.Options[option]), style, width, runs);
                        lines.Add(new(ChromeItemKind.Row, ChromeCommand.MenuEntry, (index * OptionSpan) + option, text, true, option == row.Selected));
                    }

                    break;
                }

            case FormRowKind.Field:
                {
                    lines.Add(new(ChromeItemKind.Row, ChromeCommand.PickRow, index, ChromeText.Fit(row.Label, style, width, runs), true, false));
                    lines.Add(new(ChromeItemKind.Field, ChromeCommand.PickRow, index, ChromeText.Fit(Shown(row), style, width, runs), true, ui.Focus == index));
                    break;
                }

            default:
                {
                    lines.Add(new(ChromeItemKind.Button, ChromeCommand.PickRow, index, ChromeText.Fit(row.Label, style, width, runs), row.Enabled, false)
                    {
                        Trait = ui.Open == index ? ChromeItemTrait.Expanded : ChromeItemTrait.Collapsed,
                    });
                    break;
                }
        }
    }
}
