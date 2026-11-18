// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Presentation.Input;
using HyperPdf.Viewer.Text;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome;

/// <summary>The dialog that asks for one line of text, drawn inside the window, such as the password of a locked document.</summary>
internal static class PromptOverlays
{
    /// <summary>The rows of the dialog besides the buttons: heading, label and typing box.</summary>
    private const int Rows = 3;

    /// <summary>The mark that stands for each character of a password.</summary>
    private const char MaskCharacter = '•';

    /// <summary>Opens the dialog.</summary>
    /// <param name="request">What is asked.</param>
    /// <returns>The dialog and its answer: the text typed, or <see langword="null"/> when it was turned down.</returns>
    internal static OpenedOverlay<string?> Open(PromptRequest request)
    {
        var input = new Signal<OverlayInput>();
        var state = OverlayStates.Track(input, new PromptState(request, request.Text, false, null), Reduce);
        var answered = state.Where(static current => current.Answered);
        var overlay = new Overlay(
            OverlayKind.Dialog,
            OverlayTraits.Modal | OverlayTraits.TakesAllKeys,
            (List<ChromeItem> items, in OverlayPlace place) => Build(items, state.Value, in place),
            input,
            Signal.Never<RxVoid>(),
            answered.AsSignal(),
            new MultipleDisposable(state, input));
        return new(overlay, answered.Select(static current => current.Answer).Take(1));
    }

    /// <summary>Makes the state that follows what the user did.</summary>
    /// <param name="state">The state before.</param>
    /// <param name="input">What the user did.</param>
    /// <returns>The state after.</returns>
    private static PromptState Reduce(PromptState state, OverlayInput input) => state.Answered ? state : input switch
    {
        { IsTypedCharacter: true } => state with { Entry = string.Concat(state.Entry, input.TypedText) },
        { Kind: OverlayInputKind.Key, Key: ViewerKey.Back } => state with { Entry = ChromeText.WithoutLast(state.Entry) },
        { Kind: OverlayInputKind.Press, Item.Action: OverlayAction.Accept } or { Kind: OverlayInputKind.Key, Key: ViewerKey.Enter } => state with { Answered = true, Answer = state.Entry },
        { Kind: OverlayInputKind.Press, Item.Action: OverlayAction.Cancel }
            or { Kind: OverlayInputKind.Key, Key: ViewerKey.Escape }
            or { Kind: OverlayInputKind.Dismiss } => state with { Answered = true },
        _ => state,
    };

    /// <summary>Lays the dialog out.</summary>
    /// <param name="items">Receives the controls, back to front.</param>
    /// <param name="state">What the dialog shows.</param>
    /// <param name="place">What to lay them out with.</param>
    private static void Build(List<ChromeItem> items, PromptState state, in OverlayPlace place)
    {
        var request = state.Request;
        var runs = place.Runs;
        var metrics = place.Metrics;
        var size = place.Size;
        var style = ChromeBars.Style(metrics);
        var width = Math.Min(metrics.DialogWidth, size.Width);
        var inner = width - metrics.Padding - metrics.Padding;
        var height = (metrics.RowHeight * Rows) + metrics.ToolbarHeight + metrics.Padding + metrics.Padding;
        var left = (size.Width - width) >> 1;
        var top = Math.Max(0, (size.Height - height) >> 1);
        items.Add(new(ChromeItemKind.Popup, null, 0, string.Empty, new(left, top, width, height), true, false));
        var y = top + metrics.Padding;
        items.Add(new(ChromeItemKind.Heading, null, 0, ChromeText.Fit(request.Title, style, inner, runs), new(left, y, width, metrics.RowHeight), true, false));
        y += metrics.RowHeight;
        items.Add(new(ChromeItemKind.Label, null, 0, ChromeText.Fit(request.Label, style, inner, runs), new(left, y, width, metrics.RowHeight), true, false));
        y += metrics.RowHeight;

        // A password is never drawn or handed to a screen reader: each character shows as a dot.
        var shown = request.Masked ? new string(MaskCharacter, state.Entry.Length) : state.Entry;
        var fitted = ChromeText.FitEnd(shown, style, inner - metrics.Padding - metrics.Padding, runs);
        items.Add(new(ChromeItemKind.Field, null, 0, fitted, new(left + metrics.Padding, y, inner, metrics.RowHeight), true, true));
        y += metrics.RowHeight;
        var buttons = new ChromeRow(items, runs, metrics, left + metrics.Padding, y + metrics.Gap, metrics.ToolbarHeight - metrics.Gap - metrics.Gap);
        buttons = buttons.Button(request.AcceptText, OverlayAction.Accept, true);
        buttons = buttons.Button(UiText.Get("Cancel"), OverlayAction.Cancel, true);
    }
}
