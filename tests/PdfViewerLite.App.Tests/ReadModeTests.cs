// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.VisualTree;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks read mode: the tool bars and sidebar are put away, a named button brings them back, and the sidebar returns as it was.</summary>
public sealed class ReadModeTests
{
    /// <summary>The pages in the document.</summary>
    private const int Pages = 2;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>Read mode hides the tool bars and sidebar, shows only the way back, and leaving restores both.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PutsTheToolBarsAway()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("read.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.SidebarVisible = true;
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            _ = await tab.ReadModeCommand.Execute().ToTask();
            _ = await UiWait.UntilAsync(() => Visible(window, "ReadModeBar"));
            var chromeWhileReading = Visible(window, "Chrome");
            var sidebarWhileReading = tab.SidebarVisible;
            var unnamed = string.Join(", ", AccessibilityTests.Unnamed(window));
            _ = await tab.ReadModeCommand.Execute().ToTask();
            _ = await UiWait.UntilAsync(() => Visible(window, "Chrome"));

            await Assert.That(chromeWhileReading).IsFalse();
            await Assert.That(sidebarWhileReading).IsFalse();
            await Assert.That(unnamed).IsEqualTo(string.Empty);
            await Assert.That(tab.IsReading).IsFalse();
            await Assert.That(tab.SidebarVisible).IsTrue();
            await Assert.That(Visible(window, "ReadModeBar")).IsFalse();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Determines whether a named control is visible.</summary>
    /// <param name="window">The window.</param>
    /// <param name="name">The control's name.</param>
    /// <returns><see langword="true"/> when it is shown.</returns>
    private static bool Visible(MainWindow window, string name) =>
        window.GetVisualDescendants().OfType<Control>().Any(control => control.Name == name && control.IsEffectivelyVisible);
}
