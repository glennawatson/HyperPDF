// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Reading;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels.Reading;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer.Text;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// Focus mode: the document's text as one narrow column of plain, larger text in place of the pages, with nothing else to look at.
/// The arrow and page keys scroll it, a press on a paragraph reads aloud from there, and Escape goes back to the pages. A page's
/// text is read only when the page first comes into view.
/// </summary>
internal static class FocusOverlays
{
    /// <summary>The focus text size that is drawn at the size of the window's own text. Larger sizes are drawn larger in step.</summary>
    private const double BaseFontSize = 12;

    /// <summary>The widest a line is, in text sizes, which is about seventy characters: the width a line is easiest to follow at.</summary>
    private const int ColumnSizes = 36;

    /// <summary>The least room between lines, as a share of the text size.</summary>
    private const double LeastLineSpacing = 1.2;

    /// <summary>The lines a turn of the wheel moves.</summary>
    private const int WheelLines = 3;

    /// <summary>
    /// Opens focus mode. A press on a paragraph reads aloud from there, Escape runs the command that turns focus mode off, and the arrow
    /// and page keys and the wheel move through the text. It never closes by itself: the window takes it away when focus mode is turned off.
    /// </summary>
    /// <param name="focus">The focus mode of the tab shown.</param>
    /// <returns>Focus mode.</returns>
    internal static Overlay Open(FocusModeViewModel focus)
    {
        var input = new Signal<OverlayInput>();
        var edits = new Signal<Func<FocusState, FocusState>>();
        var loads = new Signal<FocusPageViewModel>();
        var state = edits.ScanWithInitial(FocusState.Start, static (current, edit) => edit(current)).ToReadOnlyState(FocusState.Start, static current => current);
        var keys = input.WhereKeyPress();
        return new(
            OverlayKind.Focus,
            OverlayTraits.TakesAllKeys | OverlayTraits.StaysUnder,
            (List<ChromeItem> items, in OverlayPlace place) => Build(items, focus, state, edits, loads, in place),
            input,
            Signal.Merge(
                focus.WhenChanged(static f => f.FontSize).AsSignal(),
                focus.WhenChanged(static f => f.LineSpacing).AsSignal(),
                focus.WhenChanged(static f => f.Pages).SwitchMap(PageChanges)),
            Signal.Never<RxVoid>(),
            new MultipleDisposable(
                input,
                edits,
                loads,
                state,
                input.SelectPressedItems()
                    .Where(item => item.Command == ChromeCommand.PickRow && (uint)item.Index < (uint)state.Value.Blocks.Length)
                    .Select(item => state.Value.Blocks[item.Index])
                    .InvokeCommand(focus.ReadFromHereCommand),
                keys.Where(static key => key.Key == ViewerKey.Escape).AsSignal().InvokeCommand(focus.ToggleCommand),
                Signal.Merge(keys.Select(key => LinesOf(state.Value, key.Key)), input.SelectScrollRows().Select(static rows => rows * WheelLines))
                    .Where(static lines => lines != 0)
                    .SubscribeSafe(lines => edits.OnNext(current => current with { First = current.First + lines }), ErrorTrace.Write),
                loads.SelectMany(static page => page.Load()).SubscribeSafe(static _ => { }, ErrorTrace.Write)));
    }

    /// <summary>Follows the text of every page, which arrives when a page is first read.</summary>
    /// <param name="pages">The pages.</param>
    /// <returns>A signal now, and again each time a page's text arrives.</returns>
    private static IObservable<RxVoid> PageChanges(IReadOnlyList<FocusPageViewModel> pages)
    {
        var changes = new List<IObservable<RxVoid>>(pages.Count);
        foreach (var page in pages)
        {
            changes.Add(page.WhenChanged(static p => p.Blocks).AsSignal());
        }

        return Signal.Merge(changes).StartWith(RxVoid.Default);
    }

    /// <summary>Tells what kind of line a paragraph starts with: a heading is drawn bold.</summary>
    /// <param name="block">The paragraph.</param>
    /// <returns>The kind.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ChromeItemKind KindOf(FocusBlockViewModel block) => block.Kind == ReadingBlockKind.Heading ? ChromeItemKind.Heading : ChromeItemKind.Row;

    /// <summary>Tells how far a key moves the text.</summary>
    /// <param name="state">What focus mode shows.</param>
    /// <param name="key">The key.</param>
    /// <returns>The lines to move down, negative to move up, or zero for a key that does not move the text.</returns>
    private static int LinesOf(FocusState state, ViewerKey key) => key switch
    {
        ViewerKey.Down => 1,
        ViewerKey.Up => -1,
        ViewerKey.PageDown or ViewerKey.Space => state.Visible - 1,
        ViewerKey.PageUp => 1 - state.Visible,
        ViewerKey.Home => -state.Lines.Length,
        ViewerKey.End => state.Lines.Length,
        _ => 0,
    };

    /// <summary>Lays the column of text out over the part of the window left for the pages.</summary>
    /// <param name="items">Receives the controls, back to front.</param>
    /// <param name="focus">The focus mode of the tab shown.</param>
    /// <param name="state">What focus mode shows.</param>
    /// <param name="edits">Takes the state that this layout works out, so what moves the text knows how many lines there are.</param>
    /// <param name="loads">Takes each page whose lines came into view.</param>
    /// <param name="place">What to lay them out with.</param>
    private static void Build(
        List<ChromeItem> items,
        FocusModeViewModel focus,
        ReadOnlyState<FocusState> state,
        Signal<Func<FocusState, FocusState>> edits,
        IObserver<FocusPageViewModel> loads,
        in OverlayPlace place)
    {
        var metrics = place.Metrics;
        var area = place.Area;
        var textSize = Math.Max(metrics.TextSize, (int)(metrics.TextSize * focus.FontSize / BaseFontSize));
        var rowHeight = (int)(textSize * Math.Max(LeastLineSpacing, focus.LineSpacing));
        var width = (int)Math.Min(area.Width - metrics.Padding - metrics.Padding, textSize * ColumnSizes);
        var left = (int)area.X + (((int)area.Width - width) >> 1);
        items.Add(new(ChromeItemKind.Popup, ChromeCommand.None, 0, string.Empty, area, true, false));
        if (width <= 0 || rowHeight <= 0)
        {
            return;
        }

        var wrapped = Wrap(state.Value, focus, place.Runs, new(textSize, false, 0), width);
        var visible = Math.Max(1, (int)(area.Height / rowHeight));
        var first = Math.Clamp(wrapped.First, 0, Math.Max(0, wrapped.Lines.Length - visible));
        var laid = wrapped with { First = first, Visible = visible };
        if (laid != state.Value)
        {
            edits.OnNext(_ => laid);
        }

        var y = (int)area.Y;
        var last = Math.Min(laid.Lines.Length, first + visible);
        for (var i = first; i < last; i++)
        {
            var line = laid.Lines[i];
            if (line.Text.Length > 0)
            {
                var spoken = line.Block >= 0 && laid.Blocks[line.Block].Spoken != TextRange.None;
                items.Add(new(line.Kind, line.Starts ? ChromeCommand.PickRow : ChromeCommand.None, line.Block, line.Text, new(left, y, width, rowHeight), true, spoken) { TextSize = textSize });
            }

            y += rowHeight;
        }

        AskForPagesInView(laid, focus, first, last, edits, loads);
    }

    /// <summary>Wraps the text into lines again when the width, the text size or the text of a page changed since the last time.</summary>
    /// <param name="state">What focus mode shows.</param>
    /// <param name="focus">The focus mode of the tab shown.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="style">How the text looks.</param>
    /// <param name="width">The width of the column.</param>
    /// <returns>The state with its lines wrapped for the width, which is <paramref name="state"/> when nothing changed.</returns>
    private static FocusState Wrap(FocusState state, FocusModeViewModel focus, TextRuns runs, TextStyle style, int width)
    {
        var from = default(HashCode);
        from.Add(width);
        from.Add(style.Size);
        foreach (var page in focus.Pages)
        {
            from.Add(RuntimeHelpers.GetHashCode(page.Blocks));
        }

        var stamp = from.ToHashCode();
        if (stamp == state.Stamp && state.Lines.Length > 0)
        {
            return state;
        }

        var lines = new List<FocusLine>();
        var blocks = new List<FocusBlockViewModel>();
        var wrapped = new List<string>();
        foreach (var page in focus.Pages)
        {
            lines.Add(new(ChromeItemKind.Heading, -1, page.PageIndex, false, page.Label));
            WrapPage(page, runs, style, width, lines, blocks, wrapped);
            lines.Add(new(ChromeItemKind.Label, -1, page.PageIndex, false, string.Empty));
        }

        return state with { Lines = [.. lines], Blocks = [.. blocks], Stamp = stamp };
    }

    /// <summary>Wraps the paragraphs of one page, or says that the page has no text or that its text is on its way.</summary>
    /// <param name="page">The page.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="style">How the text looks.</param>
    /// <param name="width">The width of the column.</param>
    /// <param name="lines">Receives the lines.</param>
    /// <param name="blocks">Receives the paragraphs.</param>
    /// <param name="wrapped">The wrapped lines of one paragraph, reused for every paragraph.</param>
    private static void WrapPage(FocusPageViewModel page, TextRuns runs, TextStyle style, int width, List<FocusLine> lines, List<FocusBlockViewModel> blocks, List<string> wrapped)
    {
        if (page.Blocks.Count == 0)
        {
            lines.Add(new(ChromeItemKind.Label, -1, page.PageIndex, false, page.IsEmpty ? UiText.Get("This page has no text.") : UiText.Get("Reading this page…")));
            return;
        }

        foreach (var block in page.Blocks)
        {
            wrapped.Clear();
            ChromeText.Wrap(block.Text, style with { Bold = block.Kind == ReadingBlockKind.Heading }, width, runs, wrapped);
            var number = blocks.Count;
            blocks.Add(block);
            for (var i = 0; i < wrapped.Count; i++)
            {
                lines.Add(new(i == 0 ? KindOf(block) : ChromeItemKind.Label, number, page.PageIndex, i == 0, wrapped[i]));
            }

            // An empty line sets paragraphs apart.
            lines.Add(new(ChromeItemKind.Label, -1, page.PageIndex, false, string.Empty));
        }
    }

    /// <summary>Asks for the text of the pages that have lines in view.</summary>
    /// <param name="state">What focus mode shows.</param>
    /// <param name="focus">The focus mode of the tab shown.</param>
    /// <param name="first">The first line in view.</param>
    /// <param name="last">The line after the last one in view.</param>
    /// <param name="edits">Takes the pages that were asked for, so a page is asked for once.</param>
    /// <param name="loads">Takes each page asked for.</param>
    private static void AskForPagesInView(FocusState state, FocusModeViewModel focus, int first, int last, Signal<Func<FocusState, FocusState>> edits, IObserver<FocusPageViewModel> loads)
    {
        var asked = new List<int>();
        for (var i = first; i < last; i++)
        {
            var index = state.Lines[i].Page;
            if (Array.IndexOf(state.Asked, index) >= 0 || asked.Contains(index))
            {
                continue;
            }

            asked.Add(index);
            foreach (var page in focus.Pages)
            {
                if (page.PageIndex == index)
                {
                    loads.OnNext(page);
                }
            }
        }

        if (asked.Count > 0)
        {
            edits.OnNext(current => current with { Asked = [.. current.Asked, .. asked] });
        }
    }
}
