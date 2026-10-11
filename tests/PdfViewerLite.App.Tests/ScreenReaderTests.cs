// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks what a blind screen reader user hears: item names, landmarks, live updates and the pages themselves.</summary>
public sealed class ScreenReaderTests
{
    /// <summary>The pages in the test document.</summary>
    private const int Pages = 3;

    /// <summary>The zero based index of the third page.</summary>
    private const int ThirdPage = 2;

    /// <summary>The page count of a long document, for descriptions.</summary>
    private const int LongDocument = 40;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>The test document's file name.</summary>
    private const string FileName = "reader.pdf";

    /// <summary>Words found once on every page of the test document.</summary>
    private const string Words = "lazy dog";

    /// <summary>The start of a name a peer makes from a data object's type, which a screen reader would read out.</summary>
    private const string TypeNamePrefix = "PdfViewerLite.";

    /// <summary>Every list and tree item in the sidebar and tab strip says what it is, never a type name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListItemsHaveSpokenNames()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(FileName, Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var view = await FindDocumentViewAsync(window);
            var tab = main.SelectedTab!;
            tab.SidebarVisible = true;
            tab.IsThumbnailsMode = true;
            var thumbnails = view.FindControl<ThumbnailListBox>("ThumbnailList")!;
            await Assert.That(await UiWait.UntilAsync(() => thumbnails.GetRealizedContainers().Count() == Pages)).IsTrue();

            await Assert.That(NameOf(thumbnails.ContainerFromIndex(0)!)).IsEqualTo("Page 1 of 3");
            await Assert.That(NameOf(thumbnails.ContainerFromIndex(ThirdPage)!)).IsEqualTo("Page 3 of 3");
            await Assert.That(AutomationProperties.GetPositionInSet(thumbnails.ContainerFromIndex(ThirdPage)!)).IsEqualTo(Pages);
            await Assert.That(AutomationProperties.GetSizeOfSet(thumbnails.ContainerFromIndex(ThirdPage)!)).IsEqualTo(Pages);

            tab.Search.Query = Words;
            await Assert.That(await UiWait.UntilAsync(
                () => !tab.Search.IsSearching && tab.Search.Results.Count == Pages,
                () => $"Query '{tab.Search.Query}', searching {tab.Search.IsSearching}, results {tab.Search.Results.Count}, status '{tab.Search.Status}', "
                    + $"page {tab.CurrentPageIndex}, loaded {tab.IsLoaded}, closed {tab.TryGetDocument()?.IsDisposed}.")).IsTrue();
            tab.IsSearchMode = true;
            var results = view.FindControl<ListBox>("SearchResultList")!;
            await Assert.That(await UiWait.UntilAsync(() => results.ContainerFromIndex(0) is not null)).IsTrue();
            await Assert.That(NameOf(results.ContainerFromIndex(0)!)).StartsWith("Page 1: ");
            await Assert.That(AutomationProperties.GetPositionInSet(results.ContainerFromIndex(0)!)).IsEqualTo(1);
            await Assert.That(AutomationProperties.GetHelpText(results.ContainerFromIndex(0)!)).IsEqualTo(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ? $"1 of {Pages}" : null);

            var strip = window.FindControl<ListBox>("TabStrip")!;
            await Assert.That(NameOf(strip.ContainerFromIndex(0)!)).IsEqualTo(FileName);

            await Assert.That(string.Join(", ", UnnamedItems(window))).IsEqualTo(string.Empty);

            // Outline entries are named as they are shown.
            tab.IsOutlineMode = true;
            var outline = view.FindControl<TreeView>("OutlineTree")!;
            await Assert.That(await UiWait.UntilAsync(() => outline.GetVisualDescendants().OfType<TreeViewItem>().Any(static item => item.IsEffectivelyVisible))).IsTrue();
            await Assert.That(string.Join(", ", UnnamedItems(window))).IsEqualTo(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The tool bar, sidebar, pages, find bar and tab bar are landmarks a screen reader can jump between.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RegionsAreLandmarks()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(FileName, Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var view = await FindDocumentViewAsync(window);
            await Assert.That(Landmark(view, "MainToolBar")).IsEqualTo(AutomationLandmarkType.Navigation);
            await Assert.That(Landmark(view, "Sidebar")).IsEqualTo(AutomationLandmarkType.Navigation);
            await Assert.That(Landmark(view, "PageArea")).IsEqualTo(AutomationLandmarkType.Main);
            await Assert.That(Landmark(view, "FindBar")).IsEqualTo(AutomationLandmarkType.Search);
            await Assert.That(Landmark(window, "TabBar")).IsEqualTo(AutomationLandmarkType.Navigation);
            await Assert.That(NameOf(view.FindControl<Control>("PageArea")!)).IsEqualTo("Pages");
            await Assert.That(NameOf(view.FindControl<Control>("MainToolBar")!)).IsEqualTo("Tool bar");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Status that changes on its own is announced without moving the focus.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangingStatusIsAnnounced()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(FileName, Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var view = await FindDocumentViewAsync(window);
            string[] polite = ["SearchStatusText", "ReadAloudText", "RecognitionText", "ZoomButton", "Canvas", "MeasureResult"];
            foreach (var name in polite)
            {
                await Assert.That(LiveSetting(view, name)).IsEqualTo(AutomationLiveSetting.Polite);
            }

            await Assert.That(LiveSetting(view, "NoticeText")).IsEqualTo(AutomationLiveSetting.Assertive);
            await Assert.That(LiveSetting(view, "ErrorText")).IsEqualTo(AutomationLiveSetting.Assertive);
            await Assert.That(LiveSetting(window, "StatusText")).IsEqualTo(AutomationLiveSetting.Polite);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The pages say which page is shown and give its text, and follow the reader to the next page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PagesSayThePageAndItsText()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(FileName, Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var view = await FindDocumentViewAsync(window);
            var canvas = view.GetVisualDescendants().OfType<PageCanvas>().Single();
            var peer = ControlAutomationPeer.CreatePeerForElement(canvas);
            var tab = main.SelectedTab!;

            await Assert.That(peer).IsTypeOf<PageCanvasAutomationPeer>();
            await Assert.That(peer.GetAutomationControlType()).IsEqualTo(AutomationControlType.Document);
            await Assert.That(peer.GetName()).IsEqualTo("Page 1 of 3");
            await Assert.That(peer.GetHelpText()).IsEqualTo(Descriptions.Pages);
            var value = (IValueProvider)peer;
            await Assert.That(value.IsReadOnly).IsTrue();
            await Assert.That(value.Value).Contains(TestPdf.Sentence);

            var announced = new List<string?>();
            using var names = Signal.FromEvent<EventHandler<AutomationPropertyChangedEventArgs>, AutomationPropertyChangedEventArgs>(
                    static next => (_, e) => next(e),
                    handler => peer.PropertyChanged += handler,
                    handler => peer.PropertyChanged -= handler)
                .Where(static args => args.Property == AutomationElementIdentifiers.NameProperty)
                .SubscribeSafe(args => announced.Add(args.NewValue as string), static _ => { });
            tab.GoToPage(1);
            await Assert.That(await UiWait.UntilAsync(() => tab.CurrentPageIndex == 1)).IsTrue();

            await Assert.That(peer.GetName()).IsEqualTo("Page 2 of 3");
            await Assert.That(announced).Contains("Page 2 of 3");
            await Assert.That(value.Value).Contains(TestPdf.Sentence);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Form fields in the dialogs are tied to the labels beside them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DialogFieldsAreLabelled()
    {
        using var test = new TestServices();
        var preferences = new PreferencesWindow { ViewModel = new(test.Services) };
        var prompt = new PromptWindow { ViewModel = new(new TextPrompt("Add a note", "Note text", string.Empty, "Add", false)) };
        preferences.Show();
        try
        {
            var scheme = preferences.FindControl<ComboBox>("SchemeBox")!;
            await Assert.That(await UiWait.UntilAsync(() => scheme.IsFocused)).IsTrue();
            await Assert.That(AutomationProperties.GetLabeledBy(scheme)).IsSameReferenceAs(preferences.FindControl<TextBlock>("SchemeLabel"));
            await Assert.That(AutomationProperties.GetLabeledBy(preferences.FindControl<TextBox>("TimestampServerBox")!)).IsNotNull();

            prompt.Show();
            _ = await UiWait.UntilAsync(() => prompt.IsVisible);

            // The prompt's box has no name of its own, so it takes the question as its name.
            var input = prompt.FindControl<TextBox>("InputBox")!;
            await Assert.That(ControlAutomationPeer.CreatePeerForElement(input).GetName()).IsEqualTo("Note text");
        }
        finally
        {
            preferences.Close();
            prompt.Close();
        }
    }

    /// <summary>Closing the find bar with Escape puts the focus back on the pages instead of losing it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosingFindReturnsFocusToPages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(FileName, Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var view = await FindDocumentViewAsync(window);
            var box = view.FindControl<TextBox>("SearchBox")!;
            var canvas = view.GetVisualDescendants().OfType<PageCanvas>().Single();
            main.SelectedTab!.Search.IsOpen = true;
            await Assert.That(await UiWait.UntilAsync(() => box.IsFocused)).IsTrue();

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);

            await Assert.That(await UiWait.UntilAsync(() => !main.SelectedTab.Search.IsOpen && canvas.IsFocused)).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Hiding the sidebar while a list in it has the focus puts the focus back on the pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HidingSidebarReturnsFocusToPages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(FileName, Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var view = await FindDocumentViewAsync(window);
            var tab = main.SelectedTab!;
            tab.SidebarVisible = true;
            tab.IsThumbnailsMode = true;
            var thumbnails = view.FindControl<ThumbnailListBox>("ThumbnailList")!;
            await Assert.That(await UiWait.UntilAsync(() => thumbnails.ContainerFromIndex(0) is not null)).IsTrue();
            var item = thumbnails.ContainerFromIndex(0)!;
            _ = item.Focus();
            await Assert.That(await UiWait.UntilAsync(() => item.IsFocused)).IsTrue();

            tab.SidebarVisible = false;
            var canvas = view.GetVisualDescendants().OfType<PageCanvas>().Single();

            await Assert.That(await UiWait.UntilAsync(() => canvas.IsFocused)).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A settings flyout opened from the keyboard takes the focus, and Escape closes it and returns to its button.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FlyoutsTakeAndReturnFocus()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(FileName, Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var view = await FindDocumentViewAsync(window);
            main.SelectedTab!.ReadAloud.IsOpen = true;
            var button = view.FindControl<Button>("ReadAloudOptionsButton")!;
            var check = view.FindControl<CheckBox>("WordMarkCheck")!;
            await Assert.That(await UiWait.UntilAsync(() => button.IsEffectivelyVisible)).IsTrue();
            _ = button.Focus();
            await Assert.That(await UiWait.UntilAsync(() => button.IsFocused)).IsTrue();

            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            await Assert.That(await UiWait.UntilAsync(() => check.IsFocused)).IsTrue();

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            await Assert.That(await UiWait.UntilAsync(() => !button.Flyout!.IsOpen && button.IsFocused)).IsTrue();
        }
        finally
        {
            main.SelectedTab!.ReadAloud.IsOpen = false;
            window.Close();
        }
    }

    /// <summary>Pages and tabs are described in words, including what is shown only by a mark.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DescriptionsSayWhatIsSeen()
    {
        await Assert.That(ItemAutomation.DescribePage("3", ThirdPage, LongDocument)).IsEqualTo("Page 3 of 40");
        await Assert.That(ItemAutomation.DescribePage("iii", ThirdPage, LongDocument)).IsEqualTo("Page iii, 3 of 40");
        await Assert.That(ItemAutomation.DescribeTab(FileName, true)).IsEqualTo("reader.pdf, unsaved changes");
        await Assert.That(ItemAutomation.DescribeTab(FileName, false)).IsEqualTo(FileName);
    }

    /// <summary>Lists the shown list and tree items whose name is missing or is a type name.</summary>
    /// <param name="root">Where to look.</param>
    /// <returns>Each one's type and name.</returns>
    private static List<string> UnnamedItems(Control root)
    {
        var unnamed = new List<string>();
        foreach (var visual in root.GetVisualDescendants())
        {
            // Screen readers skip what is hidden, such as a sidebar panel that is not chosen.
            if (visual is not (ListBoxItem or TreeViewItem) || visual is not Control { IsEffectivelyVisible: true } item)
            {
                continue;
            }

            var name = NameOf(item);
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith(TypeNamePrefix, StringComparison.Ordinal) || name.StartsWith("Avalonia.", StringComparison.Ordinal))
            {
                unnamed.Add($"{item.GetType().Name} '{name}'");
            }
        }

        return unnamed;
    }

    /// <summary>Gets the name a screen reader says for a control.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The name.</returns>
    private static string? NameOf(Control control) => ControlAutomationPeer.CreatePeerForElement(control).GetName();

    /// <summary>Gets the landmark type of a named control.</summary>
    /// <param name="root">Where to look.</param>
    /// <param name="name">The control's name.</param>
    /// <returns>The landmark type.</returns>
    private static AutomationLandmarkType? Landmark(Control root, string name) =>
        ControlAutomationPeer.CreatePeerForElement(Find(root, name)).GetLandmarkType();

    /// <summary>Gets the live setting of a named control.</summary>
    /// <param name="root">Where to look.</param>
    /// <param name="name">The control's name.</param>
    /// <returns>The live setting.</returns>
    private static AutomationLiveSetting LiveSetting(Control root, string name) =>
        ControlAutomationPeer.CreatePeerForElement(Find(root, name)).GetLiveSetting();

    /// <summary>Finds a named control anywhere below a root, visible or not.</summary>
    /// <param name="root">Where to look.</param>
    /// <param name="name">The name.</param>
    /// <returns>The control.</returns>
    private static Control Find(Control root, string name) =>
        root.GetVisualDescendants().OfType<Control>().First(control => control.Name == name);

    /// <summary>Waits for the document view to appear.</summary>
    /// <param name="window">The window.</param>
    /// <returns>The view.</returns>
    private static async Task<DocumentView> FindDocumentViewAsync(MainWindow window)
    {
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
        return window.GetVisualDescendants().OfType<DocumentView>().Single();
    }
}
