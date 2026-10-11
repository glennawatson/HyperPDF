// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests for <see cref="MainViewModel"/>.</summary>
public sealed class MainViewModelTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 4;

    /// <summary>The number of distinct documents most tests open.</summary>
    private const int TwoTabs = 2;

    /// <summary>The last page of the test documents.</summary>
    private const int LastPage = Pages - 1;

    /// <summary>The first document name.</summary>
    private const string FirstName = "a.pdf";

    /// <summary>The second document name.</summary>
    private const string SecondName = "b.pdf";

    /// <summary>Verifies opening, de-duplication and selection.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensFilesAsTabs()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var first = test.CreateDocument("first.pdf", Pages);
        var second = test.CreateDocument("second.pdf", Pages);

        await TestServices.OpenAndWaitAsync(main, [first, second, first]);
        await Assert.That(await UiWait.UntilAsync(() => main.SelectedTab!.IsLoaded)).IsTrue();

        await Assert.That(main.Tabs.Count).IsEqualTo(TwoTabs);
        await Assert.That(main.HasTabs).IsTrue();
        await Assert.That(main.SelectedTab!.FilePath).IsEqualTo(first);
        await Assert.That(main.SelectedTab.IsLoaded).IsTrue();
        await Assert.That(main.SelectedTab.PageCount).IsEqualTo(Pages);
        await Assert.That(main.Tabs[1].IsLoaded).IsFalse();
    }

    /// <summary>Verifies file URIs from file managers are accepted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensFileUris()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var path = test.CreateDocument("uri.pdf", Pages);
        await TestServices.OpenAndWaitAsync(main, [new Uri(path).AbsoluteUri]);

        await Assert.That(main.SelectedTab?.FilePath).IsEqualTo(path);
    }

    /// <summary>Verifies closing selects a neighbour and the tab can be reopened.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosesAndReopensTabs()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var first = test.CreateDocument(FirstName, Pages);
        var second = test.CreateDocument(SecondName, Pages);
        await TestServices.OpenAndWaitAsync(main, [first]);
        await TestServices.OpenAndWaitAsync(main, [second]);

        main.CloseTabWithoutAsking(main.SelectedTab);
        await Assert.That(main.Tabs.Count).IsEqualTo(1);
        await Assert.That(main.SelectedTab!.FilePath).IsEqualTo(first);

        _ = await main.ReopenClosedTabCommand.Execute().ToTask();
        await Assert.That(main.Tabs.Count).IsEqualTo(TwoTabs);
        await Assert.That(main.SelectedTab!.FilePath).IsEqualTo(second);

        var asked = 0;
        using var confirm = main.ConfirmInteraction.RegisterHandler(context =>
        {
            asked++;
            context.SetOutput(asked > 1);
        });

        _ = await main.CloseAllTabsCommand.Execute().ToTask();
        await Assert.That(main.Tabs.Count).IsEqualTo(TwoTabs);

        _ = await main.CloseAllTabsCommand.Execute().ToTask();
        await Assert.That(main.HasTabs).IsFalse();

        _ = await main.ReopenClosedTabCommand.Execute().ToTask();
        await Assert.That(main.Tabs.Count).IsEqualTo(TwoTabs);
    }

    /// <summary>Verifies the session is saved and restored with the selected tab.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RestoresSession()
    {
        using var test = new TestServices();
        var first = test.CreateDocument(FirstName, Pages);
        var second = test.CreateDocument(SecondName, Pages);
        using (var main = new MainViewModel(test.Services))
        {
            main.Open([first, second]);
            main.SelectedTab = main.Tabs[0];
            main.SaveSession();
        }

        using var restored = new MainViewModel(test.Services);
        restored.RestoreSession();

        await Assert.That(restored.Tabs.Count).IsEqualTo(TwoTabs);
        await Assert.That(restored.SelectedTab!.FilePath).IsEqualTo(first);
    }

    /// <summary>Verifies a document reopens at the page it was closed at, unless that is turned off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReopensAtLastPage()
    {
        using var test = new TestServices();
        var path = test.CreateDocument(FirstName, Pages);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        main.SelectedTab!.ReportPosition(new(LastPage, 0), LastPage);
        main.CloseTabWithoutAsking(main.SelectedTab);
        await TestServices.OpenAndWaitAsync(main, [path]);
        await Assert.That(main.SelectedTab!.CurrentPageIndex).IsEqualTo(LastPage);

        main.CloseTabWithoutAsking(main.SelectedTab);
        using (var preferences = new PreferencesViewModel(test.Services))
        {
            await Assert.That(preferences.ReopenAtLastPage).IsTrue();
            preferences.ReopenAtLastPage = false;
        }

        await Assert.That(test.Services.Settings.ReopenAtLastPage).IsFalse();
        await TestServices.OpenAndWaitAsync(main, [path]);
        await Assert.That(main.SelectedTab!.CurrentPageIndex).IsEqualTo(0);
    }

    /// <summary>Verifies New Window shows the selected document at the same page, and leaves the session to the first window.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensNewWindowAtSamePage()
    {
        using var test = new TestServices();
        var first = test.CreateDocument(FirstName, Pages);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [first, test.CreateDocument(SecondName, Pages)]);
        main.SelectedTab = main.Tabs[0];
        main.SelectedTab.ReportPosition(new(LastPage, 0), LastPage);
        main.SaveSession();
        MainViewModel? shown = null;
        using var handler = main.NewWindowInteraction.RegisterHandler(context =>
        {
            shown = context.Input;
            context.SetOutput(RxVoid.Default);
        });

        _ = await main.NewWindowCommand.Execute().ToTask();
        using var window = shown!;
        await Assert.That(window.IsSecondaryWindow).IsTrue();
        await Assert.That(window.Tabs.Count).IsEqualTo(1);
        await Assert.That(window.SelectedTab!.FilePath).IsEqualTo(first);
        await Assert.That(window.SelectedTab.CurrentPageIndex).IsEqualTo(LastPage);

        window.SaveSession();
        await Assert.That(test.Services.Settings.Session.Count).IsEqualTo(TwoTabs);
    }

    /// <summary>Verifies tabs can be reordered.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesTabs()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(FirstName, 1), test.CreateDocument(SecondName, 1), test.CreateDocument("c.pdf", 1)]);
        const int last = 2;

        main.MoveTab(0, last);

        await Assert.That(main.Tabs[last].FileName).IsEqualTo(FirstName);
    }

    /// <summary>Verifies a broken file reports an error instead of throwing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BrokenFileShowsError()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var path = Path.Combine(test.Directory, "broken.pdf");
        await File.WriteAllTextAsync(path, "not a pdf");

        main.Open([path]);
        await Assert.That(await UiWait.UntilAsync(() => main.SelectedTab!.ErrorMessage is not null)).IsTrue();

        await Assert.That(main.SelectedTab!.ErrorMessage).IsNotNull();
        await Assert.That(main.SelectedTab.IsLoaded).IsFalse();
    }

    /// <summary>Verifies a thousand tabs open lazily, any of them can show a hover preview, and the tab finder narrows them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HandlesThousandTabs()
    {
        const int tabCount = 1000;
        const int hovered = 742;
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var paths = new string[tabCount];
        for (var i = 0; i < tabCount; i++)
        {
            paths[i] = test.CreateDocument(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"doc-{i}.pdf"), 1);
        }

        main.Open(paths);
        var tab = main.Tabs[hovered];
        await tab.PreparePreviewAsync(CancellationToken.None);
        main.TabQuery = "doc-742";
        main.RefreshFoundTabs();

        await Assert.That(main.Tabs.Count).IsEqualTo(tabCount);
        await Assert.That(test.Services.Pool.OpenCount).IsLessThanOrEqualTo(test.Services.Pool.Capacity);
        await Assert.That(tab.PreviewCaption).IsEqualTo("Page 1 of 1");
        await Assert.That(tab.PreviewHeight).IsGreaterThan(0);
        await Assert.That(main.FoundTabs.Count).IsEqualTo(1);
        await Assert.That(main.FoundTabs[0]).IsEqualTo(tab);
    }
}
