// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks the page tools and thumbnail keyboard selection in a headless document view.</summary>
public sealed class PageManagementViewTests
{
    /// <summary>The number of pages in the headless interaction scenario.</summary>
    private const int PageCount = 4;

    /// <summary>The first selected page index.</summary>
    private const int FirstPage = 0;

    /// <summary>The second selected page index.</summary>
    private const int SecondPage = 1;

    /// <summary>The page index selected after keyboard movement.</summary>
    private const int ThirdPage = 2;

    /// <summary>The modifiers for moving selected pages later.</summary>
    private const KeyModifiers MoveKeyModifiers = KeyModifiers.Control | KeyModifiers.Alt;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>The menu command labels exposed to accessibility clients.</summary>
    private static readonly string[] ActionNames =
    [
        "Delete selected pages",
        "Rotate selected pages clockwise",
        "Rotate selected pages anticlockwise",
        "Duplicate selected pages",
        "Move selected pages earlier",
        "Move selected pages later",
        "Insert PDF before selection",
        "Merge PDFs at end",
        "Extract selected pages",
        "Undo page edit",
        "Redo page edit",
    ];

    /// <summary>The names of the action controls, in the same order as <see cref="ActionNames"/>.</summary>
    private static readonly string[] ActionControlNames =
    [
        "DeleteItem",
        "ClockwiseItem",
        "CounterclockwiseItem",
        "DuplicateItem",
        "EarlierItem",
        "LaterItem",
        "InsertItem",
        "MergeItem",
        "ExtractItem",
        "UndoItem",
        "RedoItem",
    ];

    /// <summary>Verifies action labels, thumbnail selection synchronization and the keyboard move shortcut.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LabelsActionsAndMovesThumbnailSelectionWithKeyboard()
    {
        using var test = new TestServices(TestEngineChoice.HyperPdf);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("page-view.pdf", PageCount)]);
        var tab = main.SelectedTab!;
        tab.SidebarMode = SidebarMode.Thumbnails;
        var window = new MainWindow { ViewModel = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any(static view => view.IsLoaded));
            var list = window.GetVisualDescendants().OfType<ThumbnailListBox>().Single();
            var pageView = window.GetVisualDescendants().OfType<PageManagementView>().Single();
            var names = ActionControlNames
                .Select(name => pageView.FindControl<MenuItem>(name) is { } item ? AutomationProperties.GetName(item) : null)
                .Concat(pageView.GetVisualDescendants().OfType<MenuItem>().Select(AutomationProperties.GetName))
                .Where(static name => name is not null)
                .ToArray();

            _ = list.SelectedItems!.Add(tab.Thumbnails[FirstPage]);
            _ = list.SelectedItems.Add(tab.Thumbnails[SecondPage]);
            _ = await UiWait.UntilAsync(
                () => tab.Pages.SelectedPages.SequenceEqual([FirstPage, SecondPage]),
                () => DescribeSelectionState(tab, list));
            list.RaiseEvent(CreateMoveKey(list));
            _ = await UiWait.UntilAsync(
                () => tab.Pages.SelectedPages.SequenceEqual([SecondPage, ThirdPage]),
                () => DescribeSelectionState(tab, list));
            tab.ShowPage(PageCount - 1);
            _ = await UiWait.UntilAsync(
                () => tab.CurrentPageIndex == PageCount - 1 && tab.Pages.SelectedPages.SequenceEqual([SecondPage, ThirdPage]),
                () => DescribeSelectionState(tab, list));

            using (Assert.Multiple())
            {
                await Assert.That(names).Contains("Page actions");
                foreach (var actionName in ActionNames)
                {
                    await Assert.That(names).Contains(actionName);
                }

                await Assert.That(tab.Pages.SelectedThumbnails.Cast<object>().OfType<ThumbnailItemViewModel>().Select(static item => item.PageIndex).ToArray()).IsEquivalentTo([SecondPage, ThirdPage]);
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Creates the keyboard event that moves the selected pages later.</summary>
    /// <param name="list">The thumbnail list.</param>
    /// <returns>The keyboard event.</returns>
    private static KeyEventArgs CreateMoveKey(ThumbnailListBox list) => new() { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down, KeyModifiers = MoveKeyModifiers, Source = list };

    /// <summary>Describes the view and view-model selection state when a UI wait times out.</summary>
    /// <param name="tab">The open document tab.</param>
    /// <param name="list">The thumbnail list.</param>
    /// <returns>The selection state.</returns>
    private static string DescribeSelectionState(DocumentTabViewModel tab, ThumbnailListBox list)
    {
        var listSelection = list.SelectedItems is null ? "<null>" : string.Join(',', list.SelectedItems.Cast<ThumbnailItemViewModel>().Select(static item => item.PageIndex));
        var pageSelection = string.Join(',', tab.Pages.SelectedPages);
        var thumbnailSelection = string.Join(',', tab.Pages.SelectedThumbnails.Cast<ThumbnailItemViewModel>().Select(static item => item.PageIndex));
        var selectedThumbnail = tab.SelectedThumbnail?.PageIndex.ToString() ?? "<null>";
        return $"Engine={TestEngineChoice.HyperPdf}; SelectedPages=[{pageSelection}]; SelectedItems=[{listSelection}]; "
            + $"SelectedThumbnail={selectedThumbnail}; CurrentPage={tab.CurrentPageIndex}; IsBusy={tab.Pages.IsBusy}; "
            + $"PageViewItems=[{thumbnailSelection}]; ListSelectedIndex={list.SelectedIndex}.";
    }
}
