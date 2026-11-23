// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.Viewer.Text;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// The yes or no dialog, drawn inside the window. A button answers it. Enter, Escape and being sent away all answer no, so
/// backing out is the default; Tab moves to the button that agrees.
/// </summary>
internal static class ConfirmOverlays
{
    /// <summary>Opens the dialog.</summary>
    /// <param name="request">What is asked.</param>
    /// <returns>The dialog and its answer. The dialog closes as soon as it has its answer, which is before the one who asked gets it.</returns>
    internal static OpenedOverlay<bool> Open(ConfirmRequest request)
    {
        var input = new Signal<OverlayInput>();
        var state = OverlayStates.Track(input, new ConfirmState(request, false, false), Reduce);
        var answered = state.Where(static current => current.Answered);
        var overlay = new Overlay(
            OverlayKind.Dialog,
            OverlayTraits.Modal | OverlayTraits.TakesAllKeys,
            (List<ChromeItem> items, in OverlayPlace place) => Build(items, state.Value, in place),
            input,
            Signal.Never<RxVoid>(),
            answered.AsSignal(),
            new MultipleDisposable(state, input));
        return new(overlay, answered.Select(static current => current.Agreed).Take(1));
    }

    /// <summary>Makes the state that follows what the user did.</summary>
    /// <param name="state">The state before.</param>
    /// <param name="input">What the user did.</param>
    /// <returns>The state after.</returns>
    private static ConfirmState Reduce(ConfirmState state, OverlayInput input) => state.Answered ? state : input switch
    {
        { Kind: OverlayInputKind.Press, Item.Action: OverlayAction.Accept } => state with { Answered = true, Agreed = true },

        // Cancel is the default: Enter with the keyboard on no button backs out, so a stray Enter never throws work away.
        // Tab moves to the button that agrees, and Enter there presses it.
        { Kind: OverlayInputKind.Press, Item.Action: OverlayAction.Cancel }
            or { Kind: OverlayInputKind.Key, Key: ViewerKey.Enter }
            or { Kind: OverlayInputKind.Key, Key: ViewerKey.Escape }
            or { Kind: OverlayInputKind.Dismiss } => state with { Answered = true },
        _ => state,
    };

    /// <summary>Lays the dialog out.</summary>
    /// <param name="items">Receives the controls, back to front.</param>
    /// <param name="state">What the dialog shows.</param>
    /// <param name="place">What to lay them out with.</param>
    private static void Build(List<ChromeItem> items, ConfirmState state, in OverlayPlace place)
    {
        var request = state.Request;
        var runs = place.Runs;
        var metrics = place.Metrics;
        var size = place.Size;
        var width = Math.Min(metrics.DialogWidth, size.Width);
        var inner = Math.Max(1, width - metrics.Padding - metrics.Padding);
        var lines = new List<string>();
        ChromeText.Wrap(request.Message, ChromeBars.Style(metrics), inner, runs, lines);
        var panelIndex = items.Count;
        items.Add(new(ChromeItemKind.Popup, null, 0, string.Empty, default, true, false));
        var y = metrics.Padding;
        items.Add(new(ChromeItemKind.Heading, null, 0, ChromeText.Fit(request.Title, ChromeBars.Style(metrics), inner, runs), new(0, y, width, metrics.RowHeight), true, false));
        y += metrics.RowHeight;
        for (var i = 0; i < lines.Count; i++)
        {
            items.Add(new(ChromeItemKind.Label, null, i, lines[i], new(0, y, width, metrics.RowHeight), true, false));
            y += metrics.RowHeight;
        }

        var row = new ChromeRow(items, runs, metrics, metrics.Padding, y + metrics.Gap, metrics.ToolbarHeight - metrics.Gap - metrics.Gap) { Limit = width - metrics.Padding };
        row = row.Button(request.ConfirmText, OverlayAction.Accept, true);
        row = row.Button(UiText.Get("Cancel"), OverlayAction.Cancel, true);
        var height = y + (row.Lines * metrics.ToolbarHeight) + metrics.Padding;
        var left = (size.Width - width) >> 1;
        var top = Math.Max(0, (size.Height - height) >> 1);
        items[panelIndex] = items[panelIndex] with { Bounds = new(left, top, width, height) };
        for (var i = panelIndex + 1; i < items.Count; i++)
        {
            var bounds = items[i].Bounds;
            items[i] = items[i] with { Bounds = new(bounds.X + left, bounds.Y + top, bounds.Width, bounds.Height) };
        }
    }
}
