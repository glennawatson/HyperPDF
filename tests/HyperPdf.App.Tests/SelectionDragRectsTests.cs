// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.TestAssets;

namespace HyperPdf.App.Tests;

/// <summary>Checks that moving the text selection focus gives the same rectangles, text and caret as selecting the range from scratch.</summary>
public sealed class SelectionDragRectsTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>The pages in the test document.</summary>
    private const int PageCount = 3;

    /// <summary>The text lines on each page.</summary>
    private const int LinesPerPage = 20;

    /// <summary>A forward drag inside one page moves the focus later each step.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task ForwardDragOnOnePageKeepsRectsTextAndCaret() =>
        CheckDragAsync(PageRotation.None, "0/2", "0/3", "0/5", "0/5", "0/9");

    /// <summary>A backward drag inside one page moves the focus earlier each step.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task BackwardDragOnOnePageKeepsRectsTextAndCaret() =>
        CheckDragAsync(PageRotation.None, "1/8", "1/7", "1/4", "1/4", "1/1");

    /// <summary>A drag from the first page to the last grows the selection over the middle page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task DragAcrossThreePagesKeepsRectsTextAndCaret() =>
        CheckDragAsync(PageRotation.None, "0/4", "0/9", "1/3", "1/3", "2/2", "2/8");

    /// <summary>A drag that goes forward then back across pages shrinks the selection again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task DragThatReversesAcrossPagesKeepsRectsTextAndCaret() =>
        CheckDragAsync(PageRotation.None, "1/5", "2/6", "2/1", "1/9", "0/3", "0/8", "1/7");

    /// <summary>A drag on a rotated view selects the same page ranges as on an upright one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task DragOnARotatedViewKeepsRectsTextAndCaret() =>
        CheckDragAsync(PageRotation.Rotate90, "0/4", "1/3", "2/2", "1/6", "0/9");

    /// <summary>Starts a selection, moves the focus step by step, and checks every step against a from-scratch computation.</summary>
    /// <param name="rotation">The view rotation.</param>
    /// <param name="anchor">The anchor, as a page, a slash and tenths of that page's characters.</param>
    /// <param name="steps">The focus moves, written like the anchor.</param>
    /// <returns>A task.</returns>
    private static async Task CheckDragAsync(PageRotation rotation, string anchor, params string[] steps)
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("selection-drag.pdf", TestPdf.CreateTextFilled(PageCount, LinesPerPage))]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<PageCanvas>().Any());
            var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
            _ = await UiWait.UntilAsync(() => canvas.Tab is not null && canvas.Tab.TryGetDocument() is not null);
            tab.Rotation = rotation;
            var document = tab.TryGetDocument()!;

            var start = Step.Parse(anchor).ToCharacter(document);
            canvas.SelectionAnchor = start;
            canvas.SelectionFocus = start;
            canvas.Caret = start;
            canvas.SelectionRects.Clear();

            var matches = true;
            foreach (var step in steps)
            {
                _ = canvas.MoveSelectionFocus(Step.Parse(step).ToCharacter(document));
                matches &= SelectionMatches(canvas, document);
                matches &= canvas.Caret == start;
            }

            await Assert.That(matches).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Compares the canvas selection rectangles and text on every page with ones worked out directly from the document.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="document">The document.</param>
    /// <returns><see langword="true"/> when they all match.</returns>
    private static bool SelectionMatches(PageCanvas canvas, IDocument document)
    {
        var hasSelection = canvas.TryGetSelection(out var from, out var to);
        var text = new StringBuilder();
        var same = true;
        for (var page = 0; page < document.PageCount; page++)
        {
            var expected = new List<PageRect>();
            if (hasSelection && page >= from.Page && page <= to.Page)
            {
                AppendExpected(document, page, from, to, expected, text);
            }

            same &= expected.SequenceEqual(canvas.GetSelectionRects(page));
        }

        return same && canvas.GetSelectedText() == text.ToString();
    }

    /// <summary>Works out one page's selection bounds and text straight from the document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="from">The selection start.</param>
    /// <param name="to">The selection end.</param>
    /// <param name="bounds">Receives the bounds.</param>
    /// <param name="text">Receives the text.</param>
    private static void AppendExpected(IDocument document, int page, PageCharacter from, PageCharacter to, List<PageRect> bounds, StringBuilder text)
    {
        var first = page == from.Page ? from.Char : 0;
        var last = page == to.Page ? to.Char : document.GetCharacterCount(page) - 1;
        if (last >= first)
        {
            document.GetTextBounds(page, first, last - first + 1, bounds);
            _ = text.Append(document.GetText(page, first, last - first + 1));
        }

        if (page != to.Page)
        {
            _ = text.AppendLine();
        }
    }

    /// <summary>A position in a page, as a page and tenths of that page's characters.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="Tenth">The tenths.</param>
    private readonly record struct Step(int Page, int Tenth)
    {
        /// <summary>The number of tenths in a page.</summary>
        private const int Tenths = 10;

        /// <summary>Reads a step written as a page, a slash and the tenths.</summary>
        /// <param name="text">The text.</param>
        /// <returns>The step.</returns>
        internal static Step Parse(string text)
        {
            var slash = text.IndexOf('/', StringComparison.Ordinal);
            return new(int.Parse(text.AsSpan(0, slash), CultureInfo.InvariantCulture), int.Parse(text.AsSpan(slash + 1), CultureInfo.InvariantCulture));
        }

        /// <summary>Turns the step into a character position.</summary>
        /// <param name="document">The document.</param>
        /// <returns>The character.</returns>
        internal PageCharacter ToCharacter(IDocument document)
        {
            var count = document.GetCharacterCount(Page);
            return new(Page, Math.Min(count - 1, count * Tenth / Tenths));
        }
    }
}
