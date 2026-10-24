// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.Views;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Headless UI and view-model checks for the on-page annotation comment bubble and comment indicator badges on <see cref="PageCanvas"/>.</summary>
public sealed class AnnotationBubbleUiTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>Halves a size to find its centre.</summary>
    private const float Half = 0.5F;

    /// <summary>The note text typed into the bubble.</summary>
    private const string NoteText = "Check this figure";

    /// <summary>Where the sticky note is placed on the page.</summary>
    private static readonly PagePoint NoteAt = new(220, 260);

    /// <summary>
    /// Clicking the page with the Note tool opens the on-page comment bubble, saving creates the sticky note, clicking
    /// the sticky note reopens the bubble, and adding a reply updates the bubble's replies inline.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlacesEditsAndRepliesToNotesInOnPageBubble()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("bubble.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            tab.SidebarMode = SidebarMode.Annotations;
            var canvas = await ReadyAsync(window, tab);
            var bubbleView = window.GetVisualDescendants().OfType<AnnotationBubbleView>().Single();

            _ = await annotations.SetToolCommand.Execute(AnnotationTool.Note).ToTask();
            Click(window, ToWindow(canvas, window, NoteAt));
            _ = await UiWait.UntilAsync(() => bubbleView.BubbleCard.IsVisible && annotations.HasBubble);

            await Assert.That(annotations.IsExistingBubble).IsFalse();
            bubbleView.NoteEditor.Text = NoteText;
            bubbleView.SaveBubbleButton.Command!.Execute(null);
            _ = await UiWait.UntilAsync(() => !bubbleView.BubbleCard.IsVisible && annotations.Items.Count == 1);

            var placed = annotations.Items[0].Annotation;
            await Assert.That(placed.Kind).IsEqualTo(AnnotationKind.Note);
            await Assert.That(placed.Contents).IsEqualTo(NoteText);

            _ = await annotations.SetToolCommand.Execute(AnnotationTool.Select).ToTask();
            var noteCenter = new PagePoint(placed.Bounds.Left + (placed.Bounds.Width * Half), placed.Bounds.Top + (placed.Bounds.Height * Half));
            Click(window, ToWindow(canvas, window, noteCenter));
            _ = await UiWait.UntilAsync(() => bubbleView.BubbleCard.IsVisible && annotations.IsExistingBubble);

            await Assert.That(bubbleView.NoteEditor.Text).IsEqualTo(NoteText);
            bubbleView.ReplyBubbleButton.Command!.Execute(null);
            _ = await UiWait.UntilAsync(() => bubbleView.ReplyPanel.IsVisible);

            bubbleView.ReplyEditor.Text = "Confirmed with the author";
            bubbleView.SendReplyButton.Command!.Execute(null);
            _ = await UiWait.UntilAsync(() => bubbleView.RepliesText.IsVisible);

            await Assert.That(bubbleView.RepliesText.Text).Contains("Confirmed with the author");

            bubbleView.DeleteBubbleButton.Command!.Execute(null);
            _ = await UiWait.UntilAsync(() => !bubbleView.BubbleCard.IsVisible && annotations.Items.Count == 0);
            await Assert.That(annotations.HasBubble).IsFalse();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// A commented highlight shows a speech-bubble indicator badge at its top-right corner, and clicking that badge on
    /// the page opens the on-page comment bubble for the highlight.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClickingCommentBadgeOnHighlightOpensBubble()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("badge.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            tab.SidebarMode = SidebarMode.Annotations;
            var canvas = await ReadyAsync(window, tab);
            var bubbleView = window.GetVisualDescendants().OfType<AnnotationBubbleView>().Single();

            var document = tab.TryGetDocument()!;
            var lines = new List<PageRect>();
            document.GetTextBounds(0, 0, TestPdf.Sentence.Length, lines);
            _ = annotations.MarkText(AnnotationKind.Highlight, new Dictionary<int, List<PageRect>> { [0] = lines }, AnnotationColors.Sage);
            var highlight = annotations.Items[0].Annotation;

            await Assert.That(AnnotationsViewModel.HasCommentBadge(highlight)).IsFalse();

            _ = annotations.OpenBubble(highlight);
            annotations.BubbleNoteText = "Important sentence";
            _ = annotations.SaveBubble();
            var commented = annotations.Items[0].Annotation;

            await Assert.That(AnnotationsViewModel.HasCommentBadge(commented)).IsTrue();
            var badge = AnnotationsViewModel.CommentBadgeBounds(commented);
            var badgeCenter = new PagePoint(badge.Left + (badge.Width * Half), badge.Top + (badge.Height * Half));
            await Assert.That(annotations.HitTestCommentBadge(0, badgeCenter)).IsNotNull();

            Click(window, ToWindow(canvas, window, badgeCenter));
            _ = await UiWait.UntilAsync(() => bubbleView.BubbleCard.IsVisible && annotations.HasBubble);

            await Assert.That(annotations.BubbleNoteText).IsEqualTo("Important sentence");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Waits until the document and page canvas are ready.</summary>
    /// <param name="window">The window.</param>
    /// <param name="tab">The tab.</param>
    /// <returns>The page canvas.</returns>
    private static async Task<PageCanvas> ReadyAsync(MainWindow window, DocumentTabViewModel tab)
    {
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<PageCanvas>().Any());
        _ = await tab.Annotations.StartCommand.Execute().ToTask();
        var hub = tab.RenderHub;
        _ = await UiWait.UntilAsync(() => hub.Cache.Count > 0 && hub.Scheduler.QueueLength == 0);
        return window.GetVisualDescendants().OfType<PageCanvas>().Single();
    }

    /// <summary>Converts a point on the first page to window coordinates.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="window">The window.</param>
    /// <param name="point">The page point.</param>
    /// <returns>The window point.</returns>
    private static Point ToWindow(PageCanvas canvas, MainWindow window, PagePoint point) =>
        canvas.TranslatePoint(canvas.PageToCanvas(0, point), window)!.Value;

    /// <summary>Clicks a window point with the left mouse button.</summary>
    /// <param name="window">The window.</param>
    /// <param name="point">The point.</param>
    private static void Click(MainWindow window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }
}
