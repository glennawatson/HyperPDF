// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels.Markup;
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
/// An on-page comment bubble drawn right beside a sticky note or commented passage without dimming the page. The reader can
/// read existing replies, edit the note, add a reply or remove the note, and click anywhere else on the page to put it away.
/// </summary>
internal static class NoteBubbleOverlays
{
    /// <summary>The field index for the main note text.</summary>
    private const int NoteFieldIndex = 0;

    /// <summary>The field index for a reply being typed.</summary>
    private const int ReplyFieldIndex = 1;

    /// <summary>The most wrapped lines of existing replies shown at once.</summary>
    private const int MaxReplyLines = 6;

    /// <summary>The most wrapped lines of the note shown at once.</summary>
    private const int MaxNoteLines = 4;

    /// <summary>The thickness of the annotation colour stripe at the top of the card, in border lines.</summary>
    private const int StripeLines = 4;

    /// <summary>The sides of padding (left and right, or top and bottom).</summary>
    private const int MarginSides = 2;

    /// <summary>The opaque alpha bits for the annotation colour stripe.</summary>
    private const uint Opaque = 0xFF000000;

    /// <summary>The RGB bits of a colour.</summary>
    private const uint ColorMask = 0x00FFFFFF;

    /// <summary>Opens the comment bubble beside the active note on the page.</summary>
    /// <param name="marks">The annotation tools of the tab shown.</param>
    /// <param name="canvas">The page canvas, which knows where the note lies in the window.</param>
    /// <returns>The comment bubble overlay.</returns>
    internal static Overlay Open(AnnotationsViewModel marks, ViewerCanvas canvas)
    {
        var input = new Signal<OverlayInput>();
        var placed = new Signal<LayoutRect>();
        var anchorState = placed.ToReadOnlyState(default, static rect => rect);
        var presses = input.SelectPressedItems();
        var keys = input.WhereKeyPress();
        var focusReply = presses
            .Where(static item => item.Kind == ChromeItemKind.Field)
            .Select(static item => item.Index == ReplyFieldIndex)
            .Merge(marks.WhenChanged(static m => m.IsReplyingInBubble))
            .ToReadOnlyState(marks.IsReplyingInBubble, static r => r);
        var closes = Signal.Merge(
            keys.Where(static key => key.Key == ViewerKey.Escape).AsSignal(),
            input.SelectDismissals(),
            marks.WhenChanged(static m => m.ActiveBubble).Where(static b => b is null).AsSignal());
        return new(
            OverlayKind.NoteBubble,
            OverlayTraits.TakesAllKeys | OverlayTraits.ClosesOnOutsidePress,
            (List<ChromeItem> items, in OverlayPlace place) => Build(items, marks, canvas, placed, focusReply.Value, in place),
            input,
            Signal.Merge(
                marks.WhenChanged(static m => m.ActiveBubble).AsSignal(),
                marks.WhenChanged(static m => m.BubbleNoteText).AsSignal(),
                marks.WhenChanged(static m => m.BubbleReplyText).AsSignal(),
                marks.WhenChanged(static m => m.IsReplyingInBubble).AsSignal()),
            closes,
            new MultipleDisposable(
                input,
                placed,
                anchorState,
                focusReply,
                keys.Where(static key => key.Key == ViewerKey.Enter).AsSignal().InvokeCommand(marks.SaveBubbleCommand),
                keys.Where(static key => key.Key == ViewerKey.Back).SubscribeSafe(_ => EditText(marks, focusReply.Value, ChromeText.WithoutLast), ErrorTrace.Write),
                input.SelectTypedRunes().SubscribeSafe(rune => EditText(marks, focusReply.Value, text => string.Concat(text, rune.ToString())), ErrorTrace.Write),
                closes.SubscribeSafe(_ => CloseIfOpen(marks), ErrorTrace.Write)))
        {
            HasMoved = () => marks.ActiveBubble is { } bubble && canvas.GetWindowRect(bubble.PageIndex, bubble.Bounds) != anchorState.Value,
        };
    }

    /// <summary>Updates either the reply text or the note text as characters are typed.</summary>
    /// <param name="marks">The annotation tools.</param>
    /// <param name="editingReply">Whether typing goes into the reply box.</param>
    /// <param name="change">Transforms the text.</param>
    private static void EditText(AnnotationsViewModel marks, bool editingReply, Func<string, string> change)
    {
        if (editingReply && marks.IsReplyingInBubble)
        {
            marks.BubbleReplyText = change(marks.BubbleReplyText);
            return;
        }

        marks.BubbleNoteText = change(marks.BubbleNoteText);
    }

    /// <summary>Closes the bubble session if it is still open.</summary>
    /// <param name="marks">The annotation tools.</param>
    private static void CloseIfOpen(AnnotationsViewModel marks)
    {
        if (marks.ActiveBubble is not null)
        {
            marks.CloseBubble();
        }
    }

    /// <summary>Lays the comment bubble out beside its annotation on the page.</summary>
    /// <param name="items">Receives the controls, back to front.</param>
    /// <param name="marks">The annotation tools.</param>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="placed">Records the window rectangle of the anchor.</param>
    /// <param name="editingReply">Whether keyboard typing is focused on the reply box.</param>
    /// <param name="place">What to lay the bubble out with.</param>
    private static void Build(List<ChromeItem> items, AnnotationsViewModel marks, ViewerCanvas canvas, Signal<LayoutRect> placed, bool editingReply, in OverlayPlace place)
    {
        if (marks.ActiveBubble is not { } bubble)
        {
            return;
        }

        var anchor = canvas.GetWindowRect(bubble.PageIndex, bubble.Bounds);
        placed.OnNext(anchor);
        var metrics = place.Metrics;
        var runs = place.Runs;
        var area = place.Area.Width > 0 && place.Area.Height > 0 ? place.Area : new LayoutRect(0, 0, place.Size.Width, place.Size.Height);
        var width = (int)Math.Min(metrics.SidebarWidth + (metrics.Padding * MarginSides), Math.Max(metrics.PopupWidth, area.Width - (metrics.Gap * MarginSides)));
        var inner = Math.Max(1, width - (metrics.Padding * MarginSides));
        var style = ChromeBars.Style(metrics);
        var replyLines = WrapCap(bubble.RepliesText, style, inner, runs, MaxReplyLines);
        var noteLines = WrapNote(marks.BubbleNoteText, style, inner - (metrics.Padding * MarginSides), runs);

        var panelIndex = items.Count;
        items.Add(new(ChromeItemKind.Popup, null, 0, string.Empty, default, true, false));
        var stripeIndex = items.Count;
        var stripeHeight = metrics.Line * StripeLines;
        items.Add(new(ChromeItemKind.Panel, null, 0, string.Empty, default, false, false) { Ground = Opaque | (bubble.Color & ColorMask) });

        var y = stripeHeight + metrics.Gap;
        y = AddHeader(items, marks, bubble.Heading, in metrics, runs, width, y);
        y = AddReplies(items, replyLines, in metrics, width, y);
        y = AddNoteBox(items, marks, noteLines, in metrics, inner, y, !(editingReply && marks.IsReplyingInBubble));
        if (marks.IsReplyingInBubble)
        {
            var replyShown = ChromeText.FitEnd(marks.BubbleReplyText, style, Math.Max(1, inner - (metrics.Padding * MarginSides)), runs);
            items.Add(new(ChromeItemKind.Field, null, ReplyFieldIndex, replyShown, new(metrics.Padding, y, inner, metrics.RowHeight), true, editingReply) { Help = UiText.Get("Your reply") });
            y += metrics.RowHeight + metrics.Gap;
        }

        var height = AddActions(items, marks, in metrics, runs, width, y) + metrics.Gap;
        var box = PlaceCard(in anchor, in area, width, height, metrics.Gap);
        items[panelIndex] = items[panelIndex] with { Bounds = box };
        var stripeInset = metrics.Line * MarginSides;
        items[stripeIndex] = items[stripeIndex] with { Bounds = new(box.X + stripeInset, box.Y + stripeInset, box.Width - (metrics.Line * StripeLines), stripeHeight) };
        for (var i = stripeIndex + 1; i < items.Count; i++)
        {
            var bounds = items[i].Bounds;
            items[i] = items[i] with { Bounds = new(bounds.X + box.X, bounds.Y + box.Y, bounds.Width, bounds.Height) };
        }
    }

    /// <summary>Wraps text and keeps at most a fixed number of lines.</summary>
    /// <param name="text">The text to wrap.</param>
    /// <param name="style">The text style.</param>
    /// <param name="width">The wrap width.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="maxLines">The line limit.</param>
    /// <returns>The wrapped lines.</returns>
    private static List<string> WrapCap(string text, TextStyle style, int width, TextRuns runs, int maxLines)
    {
        var lines = new List<string>();
        if (text.Length == 0)
        {
            return lines;
        }

        ChromeText.Wrap(text, style, width, runs, lines);
        if (lines.Count > maxLines)
        {
            lines.RemoveRange(0, lines.Count - maxLines);
        }

        return lines;
    }

    /// <summary>Wraps the note text being edited, keeping at least one line.</summary>
    /// <param name="text">The note text.</param>
    /// <param name="style">The text style.</param>
    /// <param name="width">The wrap width.</param>
    /// <param name="runs">The text measurer.</param>
    /// <returns>The lines to draw inside the note box.</returns>
    private static List<string> WrapNote(string text, TextStyle style, int width, TextRuns runs)
    {
        var lines = WrapCap(text, style, Math.Max(1, width), runs, MaxNoteLines);
        if (lines.Count == 0)
        {
            lines.Add(string.Empty);
        }

        return lines;
    }

    /// <summary>Adds the heading and the close button at the top of the card.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="marks">The annotation tools.</param>
    /// <param name="heading">The heading words.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="width">The card width.</param>
    /// <param name="y">The current vertical offset.</param>
    /// <returns>The next vertical offset.</returns>
    private static int AddHeader(List<ChromeItem> items, AnnotationsViewModel marks, string heading, in ChromeMetrics metrics, TextRuns runs, int width, int y)
    {
        var closeSide = metrics.RowHeight - metrics.Gap;
        var titleRoom = Math.Max(1, width - (metrics.Padding * MarginSides) - closeSide);
        var fitted = ChromeText.Fit(heading, ChromeBars.Style(metrics) with { Bold = true }, titleRoom, runs);
        items.Add(new(ChromeItemKind.Heading, null, 0, fitted, new(0, y, width - closeSide - metrics.Gap, metrics.RowHeight), true, false));
        items.Add(new(ChromeItemKind.Button, marks.CloseBubbleCommand, 0, UiText.Get("Close"), new(width - closeSide - metrics.Gap, y, closeSide, closeSide), true, false)
        {
            Icon = ChromeIcon.Close,
            HidesText = true,
        });
        return y + metrics.RowHeight;
    }

    /// <summary>Adds the existing replies and a rule under them when present.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="replies">The wrapped reply lines.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="width">The card width.</param>
    /// <param name="y">The current vertical offset.</param>
    /// <returns>The next vertical offset.</returns>
    private static int AddReplies(List<ChromeItem> items, List<string> replies, in ChromeMetrics metrics, int width, int y)
    {
        if (replies.Count == 0)
        {
            return y;
        }

        for (var i = 0; i < replies.Count; i++)
        {
            items.Add(new(ChromeItemKind.Label, null, i, replies[i], new(0, y, width, metrics.RowHeight), true, false));
            y += metrics.RowHeight;
        }

        items.Add(new(ChromeItemKind.Separator, null, 0, string.Empty, new(metrics.Padding, y, width - (metrics.Padding * MarginSides), metrics.Line), false, false));
        return y + metrics.Line + metrics.Gap;
    }

    /// <summary>Adds the editable note box.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="marks">The annotation tools.</param>
    /// <param name="lines">The wrapped lines of the note.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="inner">The inner width.</param>
    /// <param name="y">The current vertical offset.</param>
    /// <param name="focused">Whether the note box has the typing caret.</param>
    /// <returns>The next vertical offset.</returns>
    private static int AddNoteBox(List<ChromeItem> items, AnnotationsViewModel marks, List<string> lines, in ChromeMetrics metrics, int inner, int y, bool focused)
    {
        var boxHeight = lines.Count * metrics.RowHeight;
        var singleLine = lines.Count == 1;
        items.Add(new(ChromeItemKind.Field, null, NoteFieldIndex, singleLine ? lines[0] : string.Empty, new(metrics.Padding, y, inner, boxHeight), true, focused)
        {
            HidesText = !singleLine,
            Help = UiText.Get("The note"),
        });
        if (!singleLine)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                items.Add(new(ChromeItemKind.Label, null, i, lines[i], new(metrics.Padding, y + (i * metrics.RowHeight), inner, metrics.RowHeight), true, false));
            }
        }
        else if (marks.BubbleNoteText.Length == 0)
        {
            items.Add(new(ChromeItemKind.Label, null, 0, UiText.Get("Write a note…"), new(metrics.Padding, y, inner, metrics.RowHeight), false, false) { Dimmed = true });
        }

        return y + boxHeight + metrics.Gap;
    }

    /// <summary>Adds the Save, Reply and Delete buttons along the bottom of the bubble.</summary>
    /// <param name="items">Receives the buttons.</param>
    /// <param name="marks">The annotation tools.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="width">The card width.</param>
    /// <param name="y">The current vertical offset.</param>
    /// <returns>The bottom of the button bar.</returns>
    private static int AddActions(List<ChromeItem> items, AnnotationsViewModel marks, in ChromeMetrics metrics, TextRuns runs, int width, int y)
    {
        var tight = metrics with { Padding = metrics.Gap + metrics.Gap };
        var buttonHeight = tight.ToolbarHeight - tight.Gap - tight.Gap;
        var row = new ChromeRow(items, runs, tight, metrics.Padding, y, buttonHeight) { Limit = width - metrics.Padding };
        var saveCommand = marks.IsReplyingInBubble ? marks.SendBubbleReplyCommand : marks.SaveBubbleCommand;
        var saveText = UiText.Get(marks.IsReplyingInBubble ? "Send reply" : "Save note");
        row = row.Control(new(ChromeItemKind.Button, saveCommand, 0, saveText, default, true, false) { Icon = ChromeIcon.Done, Framed = true, Action = OverlayAction.Accept });
        if (marks.CanReplyInBubble)
        {
            row = row.Control(new(ChromeItemKind.Button, marks.StartBubbleReplyCommand, 0, UiText.Get("Reply"), default, true, false) { Icon = ChromeIcon.Note, Framed = true });
        }

        if (marks.IsExistingBubble)
        {
            row = row.Control(new(ChromeItemKind.Button, marks.DeleteBubbleAnnotationCommand, 0, UiText.Get("Delete"), default, true, false) { Icon = ChromeIcon.Close, Framed = true });
        }

        return y + (row.Lines * tight.ToolbarHeight);
    }

    /// <summary>Places the bubble beside the annotation when room allows, clamped inside the page area.</summary>
    /// <param name="anchor">The annotation bounds in window space.</param>
    /// <param name="area">The page area.</param>
    /// <param name="width">The card width.</param>
    /// <param name="height">The card height.</param>
    /// <param name="gap">The gap from the annotation or area edge.</param>
    /// <returns>The card bounds.</returns>
    private static LayoutRect PlaceCard(in LayoutRect anchor, in LayoutRect area, int width, int height, int gap)
    {
        var rightX = anchor.Right + gap;
        var leftX = anchor.X - width - gap;
        var fallbackX = Math.Clamp(anchor.X, area.X + gap, Math.Max(area.X + gap, area.Right - width - gap));
        var x = rightX + width <= area.Right - gap ? rightX : leftX;
        if (rightX + width > area.Right - gap && leftX < area.X + gap)
        {
            x = fallbackX;
        }

        var y = Math.Clamp(anchor.Y, area.Y + gap, Math.Max(area.Y + gap, area.Bottom - height - gap));
        return new(Math.Floor(x), Math.Floor(y), width, height);
    }
}
