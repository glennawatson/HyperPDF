// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Windows.Input;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Finding;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer.Icons;
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

    /// <summary>The narrowest share of the popup width the typing box shrinks to before buttons drop their labels.</summary>
    private const int NarrowFieldShare = 2;

    /// <summary>The paddings inside the typing box: one on each side.</summary>
    private const int FieldPaddings = 2;

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
            presses.Where(static item => item.Action == OverlayAction.Cancel).AsSignal(),
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
                enters.Where(static key => (key.Modifiers & ViewerModifiers.Shift) == 0).AsSignal().InvokeCommand(search.NextCommand),
                enters.Where(static key => (key.Modifiers & ViewerModifiers.Shift) != 0).AsSignal().InvokeCommand(search.PreviousCommand),
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
        var areaLeft = (int)Math.Max(0, place.Area.X);
        var areaRight = place.Area.Width > 0 ? (int)place.Area.Right : size.Width;
        var available = Math.Max(metrics.PopupWidth, areaRight - areaLeft);
        var wantedField = metrics.DialogWidth / FieldShare;
        var minField = Math.Max(metrics.PopupWidth / NarrowFieldShare, metrics.IconSize + (metrics.Padding * FieldPaddings));
        var top = Math.Max((int)place.Area.Y, metrics.TabHeight + metrics.ToolbarHeight);
        var panelIndex = items.Count;
        items.Add(new(ChromeItemKind.Popup, null, 0, string.Empty, default, true, false));
        var controlsWidth = AddControls(items, search, in place, top, true, int.MaxValue);
        if (minField + metrics.Padding + controlsWidth > available)
        {
            // When the page area is too narrow for every word, the match toggles keep their icons alone and wrap if still needed.
            items.RemoveRange(panelIndex + 1, items.Count - panelIndex - 1);
            var controlLimit = Math.Max(metrics.PopupWidth - minField, available - minField - metrics.Padding - metrics.Gap);
            controlsWidth = AddControls(items, search, in place, top, false, controlLimit);
        }

        var fieldWidth = Math.Clamp(available - metrics.Padding - controlsWidth, minField, wantedField);
        var lines = CountControlLines(items, panelIndex + 1, top + metrics.Gap, metrics.ToolbarHeight);
        var height = lines * metrics.ToolbarHeight;
        var width = Math.Min(available, fieldWidth + metrics.Padding + controlsWidth);
        var left = Math.Max(areaLeft, areaRight - width);
        items[panelIndex] = items[panelIndex] with { Bounds = new(left, top, width, height) };
        for (var i = panelIndex + 1; i < items.Count; i++)
        {
            var bounds = items[i].Bounds;
            items[i] = items[i] with { Bounds = new(bounds.X + left + fieldWidth + metrics.Padding, bounds.Y, bounds.Width, bounds.Height) };
        }

        var iconRoom = metrics.IconSize + ChromePainter.IconGap(metrics);
        var textRoom = Math.Max(1, fieldWidth - (metrics.Padding * FieldPaddings) - metrics.Gap - iconRoom);
        var shown = ChromeText.Fit(search.Query, style, textRoom, runs);
        var fieldBounds = new Core.Layout.LayoutRect(left + metrics.Gap, top + metrics.Gap, fieldWidth, metrics.ToolbarHeight - metrics.Gap - metrics.Gap);
        items.Add(new(ChromeItemKind.Field, null, 0, shown, fieldBounds, true, true) { Icon = ChromeIcon.Search });
    }

    /// <summary>Adds the match buttons, toggles, status text and close button after the typing box.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="search">The search state.</param>
    /// <param name="place">What to lay them out with.</param>
    /// <param name="top">The top of the bar.</param>
    /// <param name="showLabels">Whether the match switches show their words beside their icons.</param>
    /// <param name="limit">The furthest right edge before wrapping.</param>
    /// <returns>The width the controls take across.</returns>
    private static int AddControls(List<ChromeItem> items, SearchViewModel search, in OverlayPlace place, int top, bool showLabels, int limit)
    {
        var metrics = place.Metrics;
        var row = new ChromeRow(items, place.Runs, metrics, 0, top + metrics.Gap, metrics.ToolbarHeight - metrics.Gap - metrics.Gap) { ShowLabels = showLabels, Limit = limit };
        row = row.IconButton(ChromeIcon.PageUp, UiText.Get("Previous match"), search.PreviousCommand, search.Results.Count > 0);
        row = row.IconButton(ChromeIcon.PageDown, UiText.Get("Next match"), search.NextCommand, search.Results.Count > 0);
        row = row.Control(Toggle(ChromeIcon.Case, UiText.Get("Match case"), search.ToggleMatchCaseCommand, search.MatchCase));
        row = row.Control(Toggle(ChromeIcon.WholeWord, UiText.Get("Whole words"), search.ToggleWholeWordCommand, search.WholeWord));
        row = row.Control(Toggle(ChromeIcon.Accent, UiText.Get("Ignore accents"), search.ToggleIgnoreDiacriticsCommand, search.IgnoreDiacritics));
        row = row.Label(search.Status.Length > 0 ? search.Status : UiText.Get("Type to find"));
        row = row.Control(new(ChromeItemKind.Button, OverlayAction.Cancel, 0, UiText.Get("Close"), default, true, false) { Icon = ChromeIcon.Close, HidesText = true });
        var right = 0;
        for (var i = items.Count - 1; i >= 0 && items[i].Kind != ChromeItemKind.Popup; i--)
        {
            right = Math.Max(right, (int)items[i].Bounds.Right);
        }

        return right + metrics.Gap;
    }

    /// <summary>Counts the toolbar lines occupied by the controls of the bar.</summary>
    /// <param name="items">The laid out items.</param>
    /// <param name="first">The first control index after the popup panel.</param>
    /// <param name="firstTop">The top of the first line.</param>
    /// <param name="lineHeight">The height of one toolbar line.</param>
    /// <returns>The number of lines, at least one.</returns>
    private static int CountControlLines(List<ChromeItem> items, int first, int firstTop, int lineHeight)
    {
        var lowest = firstTop;
        for (var i = first; i < items.Count; i++)
        {
            lowest = Math.Max(lowest, (int)items[i].Bounds.Y);
        }

        return ((lowest - firstTop) / Math.Max(1, lineHeight)) + 1;
    }

    /// <summary>Makes a button that turns one way of matching on or off, and shows whether it is on.</summary>
    /// <param name="icon">The icon.</param>
    /// <param name="text">The name, which is shown beside the icon.</param>
    /// <param name="command">The command that turns it on or off.</param>
    /// <param name="on">Whether it is on.</param>
    /// <returns>The button, without its place.</returns>
    private static ChromeItem Toggle(ChromeIcon icon, string text, ICommand command, bool on) =>
        new(ChromeItemKind.Button, command, 0, text, default, true, on) { Icon = icon, Trait = ChromeItemTrait.Switch };
}
