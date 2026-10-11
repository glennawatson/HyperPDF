// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks session saving and the window's unsaved edit guard.</summary>
public sealed class MainWindowReactiveLifecycleTests
{
    /// <summary>Verifies closing a shown window saves its tabs and selected document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Close_SavesSession()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var path = test.CreateDocument("session.pdf", 1);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var window = new MainWindow { ViewModel = main };
        window.Show();
        try
        {
            await Assert.That(await UiWait.UntilAsync(() => window.IsLoaded)).IsTrue();
            window.Close();
            using var restored = new MainViewModel(test.Services);
            restored.RestoreSession();
            await Assert.That(restored.Tabs.Count).IsEqualTo(1);
            await Assert.That(restored.SelectedTab?.FilePath).IsEqualTo(path);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies repeated close requests share a confirmation and cancellation preserves the window.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Close_WithUnsavedEdits_ConfirmsOnceAndCanCancel()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("unsaved.pdf", 1)]);
        var tab = main.SelectedTab!;
        var lines = new List<PageRect>();
        tab.TryGetDocument()!.GetTextBounds(0, 0, TestPdf.Sentence.Length, lines);
        await Assert.That(tab.Annotations.MarkText(AnnotationKind.Highlight, new Dictionary<int, List<PageRect>> { [0] = lines }, AnnotationColors.Sage)).IsTrue();
        var window = new MainWindow { ViewModel = main };
        window.Show();
        try
        {
            await Assert.That(await UiWait.UntilAsync(() => window.IsLoaded)).IsTrue();
            window.Close();
            window.Close();
            await Assert.That(await UiWait.UntilAsync(() => window.OwnedWindows.OfType<ConfirmWindow>().Any())).IsTrue();
            var confirmation = window.OwnedWindows.OfType<ConfirmWindow>().Single();
            confirmation.Close(false);
            await Assert.That(await UiWait.UntilAsync(() => !window.OwnedWindows.OfType<ConfirmWindow>().Any() && !main.IsConfirmingDiscard)).IsTrue();
            await Assert.That(window.IsVisible).IsTrue();

            window.Close();
            await Assert.That(await UiWait.UntilAsync(() => window.OwnedWindows.OfType<ConfirmWindow>().Any())).IsTrue();
            window.OwnedWindows.OfType<ConfirmWindow>().Single().Close(true);
            await Assert.That(await UiWait.UntilAsync(() => !window.IsVisible)).IsTrue();
        }
        finally
        {
            foreach (var owned in window.OwnedWindows)
            {
                owned.Close(true);
            }

            window.Close();
        }
    }
}
