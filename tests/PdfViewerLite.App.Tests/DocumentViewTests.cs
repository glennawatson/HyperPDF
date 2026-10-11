// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.VisualTree;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks interaction registration when a document view is reused for another tab.</summary>
public sealed class DocumentViewTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>Opens Print after switching to another tab without losing the preview interaction handler.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PrintsAfterSwitchingTabs()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("first.pdf", 1), test.CreateDocument("second.pdf", 1)]);
        main.SelectedTab = main.Tabs[0];
        var window = new MainWindow { ViewModel = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            await Assert.That(await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any(static view => view.IsLoaded))).IsTrue();
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            main.SelectedTab = main.Tabs[1];
            await Assert.That(await UiWait.UntilAsync(() => ReferenceEquals(view.ViewModel, main.SelectedTab))).IsTrue();
            var printing = main.SelectedTab.PrintCommand.Execute().ToTask();
            await Assert.That(await UiWait.UntilAsync(() => window.OwnedWindows.OfType<PrintPreviewWindow>().Any() || printing.IsCompleted)).IsTrue();
            var preview = window.OwnedWindows.OfType<PrintPreviewWindow>().SingleOrDefault();
            preview?.Close(false);
            _ = await printing;
            await Assert.That(preview).IsNotNull();
        }
        finally
        {
            foreach (var owned in window.OwnedWindows)
            {
                owned.Close();
            }

            window.Close();
        }
    }
}
