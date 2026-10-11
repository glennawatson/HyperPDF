// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Platform;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests the Open Recent menu, which stays available while documents are open.</summary>
public sealed class RecentMenuTests
{
    /// <summary>Enough results to include every document in a test.</summary>
    private const int MaxRecent = 10;

    /// <summary>The midpoint of each button dimension.</summary>
    private const double Half = 0.5;

    /// <summary>Remove and Clear controls update the visible list and its persisted store.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CleansRecentDocumentsFromStartPage()
    {
        var storePath = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-recent-{Guid.NewGuid():N}.json");
        var store = new JsonRecentDocumentStore(storePath, TimeProvider.System);
        using var test = new TestServices(new PrintingPlatform(new RecordingPrinter(), store));
        var first = test.CreateDocument("first.pdf", 1);
        var second = test.CreateDocument("second.pdf", 1);
        store.Add(first);
        store.Add(second);
        using var main = new MainViewModel(test.Services);
        var window = new MainWindow { DataContext = main };
        window.Show();
        try
        {
            var start = window.GetVisualDescendants().OfType<StartView>().Single();
            await Assert.That(await UiWait.UntilAsync(() => start.GetVisualDescendants().OfType<RecentDocumentView>().Count() == 2)).IsTrue();
            var row = start.GetVisualDescendants().OfType<RecentDocumentView>().Single(view => view.ViewModel?.FilePath == first);
            Click(window, row.FindControl<Button>("RemoveButton")!);

            await Assert.That(main.RecentDocuments.Select(static entry => entry.FilePath)).IsEquivalentTo([second]);
            await Assert.That(main.Tabs).IsEmpty();
            await Assert.That(new JsonRecentDocumentStore(storePath, TimeProvider.System).GetRecent(MaxRecent).Select(static entry => entry.FilePath)).IsEquivalentTo([second]);

            Click(window, start.FindControl<Button>("ClearRecentButton")!);
            await Assert.That(main.RecentDocuments).IsEmpty();
            await Assert.That(new JsonRecentDocumentStore(storePath, TimeProvider.System).GetRecent(MaxRecent)).IsEmpty();
            await Assert.That(File.Exists(first) && File.Exists(second)).IsTrue();
        }
        finally
        {
            window.Close();
            File.Delete(storePath);
        }
    }

    /// <summary>Verifies Open Recent lists recent documents while a document is open, and opens one at its last page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensRecentDocumentFromMenu()
    {
        const int pages = 3;
        const int lastPage = 2;
        const int bothTabs = 2;
        var storePath = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-recent-{Guid.NewGuid():N}.json");
        var store = new JsonRecentDocumentStore(storePath, TimeProvider.System);
        using var test = new TestServices(new PrintingPlatform(new RecordingPrinter(), store));
        var earlier = test.CreateDocument("earlier.pdf", pages);
        test.Services.RecentDocuments.Add(earlier);
        Core.Settings.LastPages.Remember(test.Services.Settings.LastPages, earlier, lastPage);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("current.pdf", 1)]);
        var window = new MainWindow { DataContext = main };
        window.Show();
        try
        {
            var menuButton = window.FindControl<Button>("TabsMenuButton")!;
            menuButton.Flyout!.ShowAt(menuButton);
            var recent = window.FindControl<MenuItem>("RecentMenuItem")!;
            await Assert.That(await UiWait.UntilAsync(() => recent.IsEnabled)).IsTrue();
            recent.Open();
            await Assert.That(await UiWait.UntilAsync(() => FindRecentItem(recent, earlier) is { Command: not null })).IsTrue();
            var item = FindRecentItem(recent, earlier)!;
            await Assert.That(ControlHelp.GetText(item)).Contains("earlier.pdf");
            item.Command!.Execute(item.CommandParameter);

            await Assert.That(await UiWait.UntilAsync(() => main.SelectedTab?.FilePath == earlier)).IsTrue();
            await Assert.That(main.SelectedTab!.CurrentPageIndex).IsEqualTo(lastPage);
            await Assert.That(main.Tabs.Count).IsEqualTo(bothTabs);
        }
        finally
        {
            window.Close();
            File.Delete(storePath);
        }
    }

    /// <summary>Finds the menu item for a recent document.</summary>
    /// <param name="recent">The Open Recent menu.</param>
    /// <param name="path">The document.</param>
    /// <returns>The item, or <see langword="null"/> before it is shown.</returns>
    private static MenuItem? FindRecentItem(MenuItem recent, string path)
    {
        foreach (var container in recent.GetRealizedContainers())
        {
            if (container is MenuItem { DataContext: RecentDocument document } item && document.FilePath == path)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Clicks a button through the headless pointer input.</summary>
    /// <param name="window">The input root.</param>
    /// <param name="button">The target.</param>
    private static void Click(MainWindow window, Button button)
    {
        var point = button.TranslatePoint(new(button.Bounds.Width * Half, button.Bounds.Height * Half), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }
}
