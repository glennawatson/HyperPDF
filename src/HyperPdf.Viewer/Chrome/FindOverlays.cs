// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Finding;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer.Text;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// The search bar, drawn at the top right of the pages. Typing changes the query, which the view model searches for as it changes.
/// Enter goes to the next match, Shift with Enter to the one before, and Escape or Close puts the bar away.
/// </summary>
internal static class FindOverlays
{
    /// <summary>The share of the dialog's width the typing box takes: one over this.</summary>
    private const int FieldShare = 2;

    /// <summary>Opens the search bar of a tab.</summary>
    /// <param name="search">The search of the tab shown.</param>
    /// <returns>The search bar.</returns>
    internal static Overlay Open(SearchViewModel search)
    {
        var input = new Signal<OverlayInput>();
        var presses = input.SelectPressedItems();
        var keys = input.WhereKeyPress();
        var enters = keys.Where(static key => key.Key == ViewerKey.Enter);
        var closes = Signal.Merge(
            presses.Where(static item => item.Command == ChromeCommand.Cancel).AsSignal(),
            keys.Where(static key => key.Key == ViewerKey.Escape).AsSignal(),
            input.SelectDismissals());
        return new(
            OverlayKind.Find,
            OverlayTraits.None,
            (List<ChromeItem> items, in OverlayPlace place) => Build(items, search, in place),
            input,
            Signal.Merge(
                search.WhenChanged(static s => s.Query).AsSignal(),
                search.WhenChanged(static s => s.Status).AsSignal(),
                search.WhenChanged(static s => s.MatchCase).AsSignal(),
                search.WhenChanged(static s => s.WholeWord).AsSignal(),
                search.WhenChanged(static s => s.IgnoreDiacritics).AsSignal(),
                ((IList)search.Results).CollectionChanges.AsSignal()),
            closes,
            new MultipleDisposable(
                input,
                Signal.Merge(
                    presses.Where(static item => item.Command == ChromeCommand.FindNext).AsSignal(),
                    enters.Where(static key => (key.Modifiers & ViewerModifiers.Shift) == 0).AsSignal()).SubscribeSafe(_ => search.Next(), ErrorTrace.Write),
                Signal.Merge(
                    presses.Where(static item => item.Command == ChromeCommand.FindPrevious).AsSignal(),
                    enters.Where(static key => (key.Modifiers & ViewerModifiers.Shift) != 0).AsSignal()).SubscribeSafe(_ => search.Previous(), ErrorTrace.Write),
                presses.Where(static item => item.Command == ChromeCommand.FindMatchCase).SubscribeSafe(_ => search.MatchCase = !search.MatchCase, ErrorTrace.Write),
                presses.Where(static item => item.Command == ChromeCommand.FindWholeWord).SubscribeSafe(_ => search.WholeWord = !search.WholeWord, ErrorTrace.Write),
                presses.Where(static item => item.Command == ChromeCommand.FindIgnoreAccents).SubscribeSafe(_ => search.IgnoreDiacritics = !search.IgnoreDiacritics, ErrorTrace.Write),
                keys.Where(static key => key.Key == ViewerKey.Back).SubscribeSafe(_ => search.Query = ChromeText.WithoutLast(search.Query), ErrorTrace.Write),
                input.SelectTypedRunes().SubscribeSafe(rune => search.Query = string.Concat(search.Query, rune.ToString()), ErrorTrace.Write),
                closes.SubscribeSafe(_ => search.Close(), ErrorTrace.Write)));
    }

    /// <summary>Lays the bar out against the right edge, under the toolbar.</summary>
    /// <param name="items">Receives the controls, back to front.</param>
    /// <param name="search">The search of the tab shown.</param>
    /// <param name="place">What to lay them out with.</param>
    private static void Build(List<ChromeItem> items, SearchViewModel search, in OverlayPlace place)
    {
        var runs = place.Runs;
        var metrics = place.Metrics;
        var size = place.Size;
        var style = ChromeBars.Style(metrics);
        var fieldWidth = metrics.DialogWidth / FieldShare;
        var height = metrics.ToolbarHeight;
        var top = Math.Max((int)place.Area.Y, metrics.TabHeight + metrics.ToolbarHeight);
        var panelIndex = items.Count;
        items.Add(new(ChromeItemKind.Popup, ChromeCommand.None, 0, string.Empty, default, true, false));
        var row = new ChromeRow(items, runs, metrics, 0, top + metrics.Gap, height - metrics.Gap - metrics.Gap);
        row = row.Button(UiText.Get("Previous match"), ChromeCommand.FindPrevious, search.Results.Count > 0);
        row = row.Button(UiText.Get("Next match"), ChromeCommand.FindNext, search.Results.Count > 0);
        row = row.Button(UiText.Get("Match case"), ChromeCommand.FindMatchCase, 0, search.MatchCase);
        row = row.Button(UiText.Get("Whole words"), ChromeCommand.FindWholeWord, 0, search.WholeWord);
        row = row.Button(UiText.Get("Ignore accents"), ChromeCommand.FindIgnoreAccents, 0, search.IgnoreDiacritics);
        row = row.Label(search.Status.Length > 0 ? search.Status : UiText.Get("Type to find"));
        row = row.Button(UiText.Get("Close"), ChromeCommand.Cancel, true);

        // The bar sits against the right edge, so the controls laid out from zero are moved across once their width is known.
        var width = fieldWidth + metrics.Padding + row.Next;
        var left = Math.Max(0, size.Width - width);
        items[panelIndex] = items[panelIndex] with { Bounds = new(left, top, width, height) };
        for (var i = panelIndex + 1; i < items.Count; i++)
        {
            var bounds = items[i].Bounds;
            items[i] = items[i] with { Bounds = new(bounds.X + left + fieldWidth + metrics.Padding, bounds.Y, bounds.Width, bounds.Height) };
        }

        var shown = ChromeText.Fit(search.Query, style, fieldWidth - metrics.Padding - metrics.Padding - metrics.Gap, runs);
        items.Add(new(ChromeItemKind.Field, ChromeCommand.None, 0, shown, new(left + metrics.Gap, top + metrics.Gap, fieldWidth, height - metrics.Gap - metrics.Gap), true, true));
    }
}
