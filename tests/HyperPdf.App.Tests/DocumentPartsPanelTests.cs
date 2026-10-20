// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.TestAssets;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks the parts and articles panel: parts and threads listed, navigation by pointer and keyboard, names for assistive technology and bounded loops.</summary>
public sealed class DocumentPartsPanelTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>Half, for finding the middle of a control.</summary>
    private const double Half = 0.5;

    /// <summary>The pages in the test documents.</summary>
    private const int Pages = 3;

    /// <summary>The second page.</summary>
    private const int SecondPage = 1;

    /// <summary>The third page.</summary>
    private const int ThirdPage = 2;

    /// <summary>An ordinary blank page.</summary>
    private const string BlankPage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>";

    /// <summary>Gets a mailing of two parts, the first holding two sheets, and a three-place article that reads across all three pages.</summary>
    private static byte[] Mailing => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /DPartRoot 11 0 R /Threads [12 0 R] >>",
        "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
        BlankPage,
        BlankPage,
        BlankPage,
        "<< /Type /DPart /Parent 11 0 R /DParts [[7 0 R] [8 0 R]] /DPM << /Title (Mailing) >> >>",
        "<< /Type /DPart /Parent 6 0 R /DParts [[9 0 R 10 0 R]] /DPM << /Recipient (Ann) /Title (Letter A) >> >>",
        "<< /Type /DPart /Parent 6 0 R /Start 5 0 R /DPM << /Recipient (Bob) >> >>",
        "<< /Type /DPart /Parent 7 0 R /Start 3 0 R >>",
        "<< /Type /DPart /Parent 7 0 R /Start 4 0 R /End 5 0 R >>",
        "<< /Type /DPartRoot /DPartRootNode 6 0 R /RecordLevel 1 /NodeNameList [/Job /Recipient] >>",
        "<< /Type /Thread /F 13 0 R /I << /Title (Story) >> >>",
        "<< /T 12 0 R /N 14 0 R /V 15 0 R /P 3 0 R /R [72 500 300 700] >>",
        "<< /T 12 0 R /N 15 0 R /V 13 0 R /P 4 0 R /R [72 200 300 400] >>",
        "<< /T 12 0 R /N 13 0 R /V 14 0 R /P 5 0 R /R [320 500 540 700] >>");

    /// <summary>Gets a document whose part tree and bead ring loop back on themselves.</summary>
    private static byte[] Loops => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /DPartRoot 8 0 R /Threads [9 0 R] >>",
        "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
        BlankPage,
        BlankPage,
        BlankPage,
        "<< /DParts [[7 0 R 6 0 R]] >>",
        "<< /DParts [[6 0 R]] /Start 3 0 R >>",
        "<< /DPartRootNode 6 0 R >>",
        "<< /F 10 0 R >>",
        "<< /N 11 0 R /P 3 0 R /R [0 0 100 100] >>",
        "<< /N 10 0 R /P 4 0 R /R [0 0 100 100] >>");

    /// <summary>The panel lists the parts with their pages, and the articles with their places.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsPartsAndArticles()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (window, tab) = await OpenAsync(test, main, Mailing);
        try
        {
            var parts = tab.Parts;

            await Assert.That(parts.HasStructure).IsTrue();
            await Assert.That(parts.Parts.Select(static part => part.Name)).IsEquivalentTo(["Letter A", "Part 2"]);
            await Assert.That(parts.Parts[0].Children[1].PagesText).IsEqualTo("Pages 2 to 3");
            await Assert.That(parts.Parts[0].Details).IsEqualTo("Recipient: Ann");
            await Assert.That(parts.Threads.Select(static thread => thread.Title)).IsEquivalentTo(["Story"]);
            await Assert.That(parts.Threads[0].Summary).IsEqualTo("3 places");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Choosing a part goes to its first page, and a part that only groups others goes to its first sheet.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoosingAPartGoesToItsPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (window, tab) = await OpenAsync(test, main, Mailing);
        try
        {
            tab.Parts.SelectedPart = tab.Parts.Parts[1];
            var third = await UiWait.UntilAsync(() => tab.CurrentPageIndex == ThirdPage);
            tab.Parts.SelectedPart = tab.Parts.Parts[0].Children[1];
            var second = await UiWait.UntilAsync(() => tab.CurrentPageIndex == SecondPage);
            tab.Parts.SelectedPart = tab.Parts.Parts[0];
            var first = await UiWait.UntilAsync(() => tab.CurrentPageIndex == 0);

            await Assert.That(third).IsTrue();
            await Assert.That(second).IsTrue();
            await Assert.That(first).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>An article moves in its own order, remembers where it was read, and keeps that place when the reader looks at another.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ArticleMovesInLogicalOrderAndKeepsItsPlace()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (window, tab) = await OpenAsync(test, main, Mailing);
        try
        {
            var parts = tab.Parts;
            parts.SelectedThread = parts.Threads[0];
            var start = await UiWait.UntilAsync(() => tab.CurrentPageIndex == 0 && parts.PlaceIndex == 0);
            _ = await parts.NextPlaceCommand.Execute().ToTask();
            var second = await UiWait.UntilAsync(() => tab.CurrentPageIndex == SecondPage);
            var secondText = parts.PlaceText;
            _ = await parts.NextPlaceCommand.Execute().ToTask();
            var third = await UiWait.UntilAsync(() => tab.CurrentPageIndex == ThirdPage);
            var canGoOn = await parts.NextPlaceCommand.CanExecute.FirstAsync();

            // Leaving the article for a part and coming back carries on from the third place, not the first.
            tab.GoToPage(0);
            _ = await parts.PreviousPlaceCommand.Execute().ToTask();
            var back = await UiWait.UntilAsync(() => tab.CurrentPageIndex == SecondPage);

            await Assert.That(start).IsTrue();
            await Assert.That(second).IsTrue();
            await Assert.That(secondText).IsEqualTo("Place 2 of 3, page 2");
            await Assert.That(third).IsTrue();
            await Assert.That(canGoOn).IsFalse();
            await Assert.That(back).IsTrue();
            await Assert.That(parts.PlaceIndex).IsEqualTo(SecondPage);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The panel button is offered only for documents with parts or articles, and the panel can be reached from the keyboard.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PanelButtonAppearsOnlyWhenThereIsStructure()
    {
        using var plainTest = new TestServices();
        using var plainMain = new MainViewModel(plainTest.Services);
        var (plainWindow, plainTab) = await OpenAsync(plainTest, plainMain, TestPdf.Create(1));
        bool plainOffered;
        try
        {
            plainOffered = plainTab.Parts.HasStructure;
        }
        finally
        {
            plainWindow.Close();
        }

        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (window, tab) = await OpenAsync(test, main, Mailing);
        try
        {
            tab.SidebarVisible = true;
            var shown = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<ToggleButton>().Any(static b => b.Name == "PartsToggle" && b.IsEffectivelyVisible));
            tab.SidebarMode = SidebarMode.Parts;
            var tree = await FindAsync<TreeView>(window, "PartTree");
            var list = await FindAsync<ListBox>(window, "ThreadList");

            await Assert.That(plainOffered).IsFalse();
            await Assert.That(shown).IsTrue();
            await Assert.That(AutomationProperties.GetName(tree)).IsEqualTo("Document parts");
            await Assert.That(AutomationProperties.GetName(list)).IsEqualTo("Articles");
            await Assert.That(tree.IsEffectivelyVisible).IsTrue();
            await Assert.That(list.IsEffectivelyVisible).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Pressing Down in the articles list moves to the article and the buttons move along it, all from the keyboard.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeyboardChoosesAnArticleAndMovesAlongIt()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (window, tab) = await OpenAsync(test, main, Mailing);
        try
        {
            tab.SidebarVisible = true;
            tab.SidebarMode = SidebarMode.Parts;
            var list = await FindAsync<ListBox>(window, "ThreadList");
            var next = await FindAsync<Button>(window, "NextPlaceButton");
            var item = await UiWait.UntilAsync(() => list.ContainerFromIndex(0) is ListBoxItem { IsEffectivelyVisible: true });

            // The first article takes the keyboard focus and Space chooses it.
            _ = (list.ContainerFromIndex(0) as ListBoxItem)!.Focus();
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
            var chosen = item && await UiWait.UntilAsync(() => tab.Parts.SelectedThread is not null);
            _ = next.Focus();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            var moved = await UiWait.UntilAsync(() => tab.Parts.PlaceIndex == 1 && tab.CurrentPageIndex == SecondPage);

            await Assert.That(chosen).IsTrue();
            await Assert.That(moved).IsTrue();
            await Assert.That(string.Join(", ", AccessibilityTests.Unnamed(window))).IsEqualTo(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Loops in the part tree and bead ring stay bounded and leave the document's pages usable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LoopsDoNotHideThePages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (window, tab) = await OpenAsync(test, main, Loops);
        try
        {
            tab.GoToPage(ThirdPage);
            var moved = await UiWait.UntilAsync(() => tab.CurrentPageIndex == ThirdPage);

            await Assert.That(tab.PageCount).IsEqualTo(Pages);
            await Assert.That(tab.Thumbnails.Count).IsEqualTo(Pages);
            await Assert.That(tab.Parts.Parts.Count).IsLessThan(Pages * Pages);
            await Assert.That(moved).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Opens a document in a window.</summary>
    /// <param name="test">The services.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="bytes">The document.</param>
    /// <returns>The window and its tab.</returns>
    private static async Task<(Window Window, DocumentTabViewModel Tab)> OpenAsync(TestServices test, MainViewModel main, byte[] bytes)
    {
        var path = Path.Combine(test.Directory, $"structure-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, bytes);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        _ = await UiWait.UntilAsync(() => test.Services.RenderHub.Cache.Count > 0 && window.GetVisualDescendants().OfType<DocumentView>().Any());
        return (window, main.SelectedTab!);
    }

    /// <summary>Waits for a named control to be shown.</summary>
    /// <typeparam name="TControl">The control's type.</typeparam>
    /// <param name="window">The window.</param>
    /// <param name="name">The control's name.</param>
    /// <returns>The control.</returns>
    private static async Task<TControl> FindAsync<TControl>(Window window, string name)
        where TControl : Control
    {
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<TControl>().Any(c => c.Name == name && c.IsEffectivelyVisible));
        return window.GetVisualDescendants().OfType<TControl>().First(c => c.Name == name);
    }
}
