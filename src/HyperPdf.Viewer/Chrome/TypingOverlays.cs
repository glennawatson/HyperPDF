// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.Core.Reading;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer.Text;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// A typing box that lies over a form field or a text box on the page, so the text is typed where it will be. It moves with the
/// page as the page scrolls. Enter writes the text, Escape gives it up, Tab writes it and moves to the next field, and a press
/// anywhere else writes it. In a text box, which can have several lines, Enter starts a new line and Control with Enter writes the text.
/// </summary>
internal static class TypingOverlays
{
    /// <summary>What Enter adds to a box of several lines.</summary>
    private const string NewLine = "\n";

    /// <summary>The paper a form field is filled in on: white, as the page is.</summary>
    private const uint FieldPaper = 0xFFFFFFFF;

    /// <summary>The ink a form field is filled in with: near black, as the page's own letters are.</summary>
    private const uint FieldInk = 0xFF1A1A1A;

    /// <summary>The sides of a box that have padding: left and right.</summary>
    private const int PaddingSides = 2;

    /// <summary>The share of a field's height its letters take.</summary>
    private const double TextShare = 0.7;

    /// <summary>The smallest letters a field is filled in with, in device pixels.</summary>
    private const int MinTextSize = 9;

    /// <summary>The thinnest box a field is filled in with, in device pixels, so its letters can be read on a page zoomed far out.</summary>
    private const int MinFieldHeight = 14;

    /// <summary>How many times the window's own text size the letters of a field may be, on a page zoomed far in.</summary>
    private const int MaxTextScale = 4;

    /// <summary>
    /// Opens the box. Each way of ending the typing tells the view model first, and then closes the box: a press somewhere else keeps
    /// what was typed, as leaving a field does in any form.
    /// </summary>
    /// <param name="target">What is typed into.</param>
    /// <param name="canvas">The page canvas, which knows where the page lies in the window.</param>
    /// <returns>The box.</returns>
    internal static Overlay Open(TypingTarget target, ViewerCanvas canvas)
    {
        var input = new Signal<OverlayInput>();
        var placed = new Signal<LayoutRect>();
        var place = placed.ToReadOnlyState(default, static rect => rect);
        var keys = input.WhereKeyPress();
        var enters = keys.Where(static key => key.Key == ViewerKey.Enter);
        var writes = enters.Where(key => !target.TakesLines || (key.Modifiers & ViewerModifiers.Control) != 0).Do(_ => target.Commit());
        var gives = keys.Where(static key => key.Key == ViewerKey.Escape).Do(_ => target.Cancel());

        // The view model starts typing in the next field, which opens a box of its own.
        var moves = keys.Where(static key => key.Key == ViewerKey.Tab).Do(key => target.CommitAndMove((key.Modifiers & ViewerModifiers.Shift) != 0));
        return new(
            OverlayKind.Typing,
            OverlayTraits.TakesAllKeys | OverlayTraits.ClosesOnOutsidePress | OverlayTraits.UsesTab,
            (List<ChromeItem> items, in OverlayPlace area) => Build(items, target, canvas, placed, in area),
            input,
            target.Changes,
            Signal.Merge(writes.AsSignal(), gives.AsSignal(), moves.AsSignal(), input.SelectDismissals()),
            new MultipleDisposable(
                input,
                placed,
                place,
                target.Lifetime,
                input.SelectDismissals().Where(_ => target.IsOpen()).SubscribeSafe(_ => target.Commit(), ErrorTrace.Write),
                enters.Where(key => target.TakesLines && (key.Modifiers & ViewerModifiers.Control) == 0)
                    .SubscribeSafe(_ => target.Write(string.Concat(target.Read(), NewLine)), ErrorTrace.Write),
                keys.Where(static key => key.Key == ViewerKey.Back).SubscribeSafe(_ => target.Write(ChromeText.WithoutLast(target.Read())), ErrorTrace.Write),
                input.SelectPressedItems().Where(static item => item.Action == OverlayAction.PickRow).SubscribeSafe(item => target.Correct(item.Index), ErrorTrace.Write),
                input.SelectTypedRunes().SubscribeSafe(rune => target.Write(string.Concat(target.Read(), rune.ToString())), ErrorTrace.Write)))
        { HasMoved = () => canvas.GetWindowRect(target.Page, target.Bounds) != place.Value };
    }

    /// <summary>Draws a line under each misspelled word of a line of text, when the whole line is drawn.</summary>
    /// <param name="items">Receives the lines.</param>
    /// <param name="words">The misspelled words of the whole text.</param>
    /// <param name="line">The line of text that is drawn.</param>
    /// <param name="offset">Where the line starts in the whole text.</param>
    /// <param name="row">Where the text of the line starts, and how tall the row is.</param>
    /// <param name="style">How the line of text is drawn.</param>
    /// <param name="at">The sizes of the window's controls and its drawn text.</param>
    private static void AddUnderlines(List<ChromeItem> items, TextRange[] words, string line, int offset, in LayoutRect row, TextStyle style, in OverlayPlace at)
    {
        var metrics = at.Metrics;
        var runs = at.Runs;

        // A line that was cut to fit is drawn from its end, so where a word lies is not known and no line is drawn.
        if (words.Length == 0 || runs.Measure(line, style) > row.Width - metrics.Padding - metrics.Padding)
        {
            return;
        }

        foreach (var word in words)
        {
            var start = word.Start - offset;
            if (start < 0 || start + word.Length > line.Length)
            {
                continue;
            }

            var before = runs.Measure(line[..start], style);
            var width = runs.Measure(line.Substring(start, word.Length), style);
            items.Add(new(ChromeItemKind.Underline, null, 0, string.Empty, new(row.X + before, row.Bottom - metrics.Line - metrics.Line, width, metrics.Line), false, false));
        }
    }

    /// <summary>Lays out what can be done about a word spelled wrong, as rows under the box.</summary>
    /// <param name="items">Receives the controls, back to front.</param>
    /// <param name="corrections">The corrections.</param>
    /// <param name="under">Where the rows go: the place just under the box.</param>
    /// <param name="metrics">The sizes of the window's controls.</param>
    /// <param name="runs">The window's drawn text.</param>
    private static void AddCorrections(List<ChromeItem> items, string[] corrections, in LayoutRect under, in ChromeMetrics metrics, TextRuns runs)
    {
        if (corrections.Length == 0)
        {
            return;
        }

        var style = ChromeBars.Style(metrics);
        var room = (int)under.Width - metrics.Padding - metrics.Padding;
        items.Add(new(ChromeItemKind.Popup, null, 0, string.Empty, new(under.X, under.Y, under.Width, corrections.Length * metrics.RowHeight), true, false));
        for (var i = 0; i < corrections.Length; i++)
        {
            var bounds = new LayoutRect(under.X, under.Y + (i * metrics.RowHeight), under.Width, metrics.RowHeight);
            items.Add(new(ChromeItemKind.Row, OverlayAction.PickRow, i, ChromeText.Fit(corrections[i], style, room, runs), bounds, true, false));
        }
    }

    /// <summary>
    /// Adds the box a form field is filled in with. It lies exactly on the field, on the page's own paper with dark letters
    /// the size the field's height gives, and an outline in the accent colour, so the field is filled in where it is and as
    /// it will look, as a browser does. Only a field too thin to read at this zoom gets a taller box.
    /// </summary>
    /// <param name="items">Receives the box, the lines under misspelled words and the corrections.</param>
    /// <param name="target">The field being typed into.</param>
    /// <param name="text">The text typed so far.</param>
    /// <param name="place">Where the field lies in the window.</param>
    /// <param name="at">What to lay the box out with.</param>
    private static void AddField(List<ChromeItem> items, TypingTarget target, string text, in LayoutRect place, in OverlayPlace at)
    {
        var metrics = at.Metrics;
        var height = Math.Max(place.Height, MinFieldHeight);
        var width = Math.Max(place.Width, height);
        var size = Math.Clamp((int)(height * TextShare), MinTextSize, metrics.TextSize * MaxTextScale);
        var style = ChromeBars.Style(metrics) with { Size = size };
        var room = (int)width - metrics.Padding - metrics.Padding;
        var box = new LayoutRect(place.X, place.Y, width, height);
        items.Add(new(ChromeItemKind.Field, null, 0, ChromeText.FitEnd(text, style, room, at.Runs), box, true, true) { TextSize = size, Ground = FieldPaper, Tint = FieldInk });
        AddUnderlines(items, target.Misspelled(), text, 0, box with { X = box.X + metrics.Padding }, style, in at);
        AddCorrections(items, target.Corrections(), new(place.X, place.Y + height, Math.Max(width, metrics.PopupWidth), 0), in metrics, at.Runs);
    }

    /// <summary>Lays the box out over the field or the text box on the page.</summary>
    /// <param name="items">Receives the controls, back to front.</param>
    /// <param name="target">What is typed into.</param>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="placed">Takes where the text lies in the window, so a scroll of the page can be told from no move.</param>
    /// <param name="at">What to lay them out with.</param>
    private static void Build(List<ChromeItem> items, TypingTarget target, ViewerCanvas canvas, Signal<LayoutRect> placed, in OverlayPlace at)
    {
        var runs = at.Runs;
        var metrics = at.Metrics;

        var place = canvas.GetWindowRect(target.Page, target.Bounds);
        placed.OnNext(place);
        var text = target.Read();
        if (!target.TakesLines)
        {
            AddField(items, target, text, in place, in at);
            return;
        }

        // A box of several lines is drawn as a field is: on paper, in dark letters the size its lines give, inside an
        // outline, one row for each line. Its place gives the height of one line.
        var rowHeight = Math.Max(place.Height, MinFieldHeight);
        var size = Math.Clamp((int)(rowHeight * TextShare), MinTextSize, metrics.TextSize * MaxTextScale);
        var style = ChromeBars.Style(metrics) with { Size = size };
        var lines = text.Split('\n');

        // The box grows with its longest line, so what is typed is never cut off at the box's edge.
        var widest = 0;
        foreach (var line in lines)
        {
            widest = Math.Max(widest, runs.Measure(line, style));
        }

        var width = Math.Max(Math.Max(place.Width, rowHeight), widest + (metrics.Padding * PaddingSides) + metrics.Padding);
        var room = (int)width - metrics.Padding - metrics.Padding;
        items.Add(new(ChromeItemKind.Field, null, 0, string.Empty, new(place.X, place.Y, width, lines.Length * rowHeight), true, true) { Ground = FieldPaper, HidesText = true });
        var offset = 0;
        var marked = target.Misspelled();
        var last = string.Empty;
        for (var i = 0; i < lines.Length; i++)
        {
            var bounds = new LayoutRect(place.X, place.Y + (i * rowHeight), width, rowHeight);
            last = ChromeText.FitEnd(lines[i], style, room, runs);
            items.Add(new(ChromeItemKind.Label, null, i, last, bounds, true, false) { TextSize = size, Tint = FieldInk });
            AddUnderlines(items, marked, lines[i], offset, bounds with { X = bounds.X + metrics.Padding }, style, in at);
            offset += lines[i].Length + 1;
        }

        // The caret sits after the last line, where the next typed character goes.
        var caretTop = place.Y + ((lines.Length - 1) * rowHeight) + metrics.Gap;
        var caret = new LayoutRect(place.X + metrics.Padding + runs.Measure(last, style), caretTop, metrics.Line + metrics.Line, rowHeight - metrics.Gap - metrics.Gap);
        items.Add(new(ChromeItemKind.Popup, null, 0, string.Empty, caret, false, false) { Ground = FieldInk });
    }
}
