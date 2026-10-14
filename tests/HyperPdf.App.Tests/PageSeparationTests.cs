// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks how the viewer explains the colour separation pages of a preseparated file.</summary>
public sealed class PageSeparationTests
{
    /// <summary>The yellow separation of printed page 1.</summary>
    private const int YellowPage = 2;

    /// <summary>The black separation of printed page 1.</summary>
    private const int BlackPage = 3;

    /// <summary>The spot separation of printed page 2.</summary>
    private const int SpotPage = 7;

    /// <summary>The separations of printed page 1 after one is lost.</summary>
    private const int ThreeKept = 3;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>The bar names the colourant and printed page, choosing a colourant shows its page, and an ordinary page is named as one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeparationPagesAreExplained()
    {
        using var test = new TestServices();
        var path = await WriteFixtureAsync(test);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        var requests = new List<NavigationRequest>();
        using var navigation = tab.Destinations.SubscribeSafe(requests.Add, static _ => { });
        var first = tab.Separations.Description;
        var colorants = tab.Separations.Colorants.ToArray();

        tab.Separations.SelectedIndex = YellowPage;
        tab.ReportPosition(new(YellowPage, 0), YellowPage);
        var yellow = tab.Separations.Description;
        tab.ReportPosition(new(SeparationPdf.OrdinaryPage, 0), SeparationPdf.OrdinaryPage);

        await Assert.That(first).IsEqualTo("Separation: Cyan, part of printed page 1 (4 colourants)");
        await Assert.That(colorants).IsEquivalentTo(SeparationPdf.FirstColorants);
        await Assert.That(requests.Select(static request => request.PageIndex)).IsEquivalentTo([YellowPage]);
        await Assert.That(yellow).IsEqualTo("Separation: Yellow, part of printed page 1 (4 colourants)");
        await Assert.That(tab.Separations.Description).IsEqualTo("Ordinary page, not a colour separation");
        await Assert.That(tab.Separations.HasSeparations).IsTrue();
        await Assert.That(tab.Separations.Colorants).IsEmpty();
        await Assert.That(tab.Separations.SelectedIndex).IsEqualTo(-1);
        await Assert.That(tab.Thumbnails[0].Caption).IsEqualTo("1 (Cyan)");
        await Assert.That(tab.Thumbnails[SpotPage].Caption).IsEqualTo("8 (PANTONE 35 CV)");
        await Assert.That(tab.Thumbnails[SeparationPdf.OrdinaryPage].Caption).IsEqualTo("9");
        await Assert.That(tab.GetPageDisplay(0)).IsEqualTo("1");
        await Assert.That(ItemAutomation.Describe(tab.Thumbnails[0], 0, SeparationPdf.PreseparatedPages)).IsEqualTo("Page 1 of 9, Cyan separation");
        await Assert.That(new PropertiesViewModel(tab).Entries).Contains(new PropertyEntry("Colour separations", "8 colour separation pages, making 2 printed pages"));
    }

    /// <summary>The bar is shown with a visible, named and labelled colourant list on a separation page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BarIsShownAndNamed()
    {
        using var test = new TestServices();
        var path = await WriteFixtureAsync(test);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var window = new MainWindow { ViewModel = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            await Assert.That(await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any(static view => view.IsLoaded))).IsTrue();
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var box = view.SeparationBox;
            var label = view.SeparationChoiceLabel;
            await Assert.That(await UiWait.UntilAsync(() => view.SeparationBar.IsVisible && box.ItemCount == SeparationPdf.ColorantsPerPage)).IsTrue();
            await Assert.That(view.SeparationText.Text).IsEqualTo("Separation: Cyan, part of printed page 1 (4 colourants)");
            await Assert.That(box.SelectedIndex).IsEqualTo(0);
            await Assert.That(AutomationProperties.GetName(box)).IsEqualTo("Show separation");
            await Assert.That(AutomationProperties.GetLabeledBy(box)).IsSameReferenceAs(label);
            await Assert.That(label.IsVisible).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>An ordinary file shows no separation bar or captions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrdinaryFileShowsNothing()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("ordinary.pdf", 1)]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();

        await Assert.That(tab.Separations.Description).IsNull();
        await Assert.That(tab.Separations.HasSeparations).IsFalse();
        await Assert.That(tab.Separations.Summary).IsNull();
        await Assert.That(tab.Thumbnails[0].Colorant).IsNull();
    }

    /// <summary>Deleting one separation tells the reader, in plain words, that the printed page kept the others.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeletingASeparationShowsANotice()
    {
        using var test = new TestServices();
        var path = await WriteFixtureAsync(test);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        var pages = tab.Pages;

        pages.SelectPages([BlackPage]);
        _ = await pages.DeleteCommand.Execute().ToTask();

        await Assert.That(tab.Notice).IsEqualTo("Printed page 1 kept 3 of its 4 colour separations, and they stay linked.");
        await Assert.That(tab.Thumbnails[BlackPage].Caption).IsEqualTo("4 (Cyan)");
        await Assert.That(tab.Separations.Description).IsEqualTo("Separation: Cyan, part of printed page 1 (3 colourants)");
    }

    /// <summary>Each kind of change is described in plain words.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangesAreDescribed()
    {
        SeparationChange[] changes = [new(1, SeparationPdf.ColorantsPerPage, ThreeKept, 1), new(0, 0, 0, 1)];
        await Assert.That(SeparationText.ChangeNotice([])).IsNull();
        await Assert.That(SeparationText.ChangeNotice(changes)).IsEqualTo(
            "Printed page 1 kept 3 of its 4 colour separations, and they stay linked. 1 page of printed page 1 is no longer marked as a colour separation. "
            + "Damaged colour separation details on 1 page were removed.");
    }

    /// <summary>Writes the preseparated document into the test's folder.</summary>
    /// <param name="test">The test services.</param>
    /// <returns>The file's path.</returns>
    private static async Task<string> WriteFixtureAsync(TestServices test)
    {
        var path = Path.Combine(test.Directory, "preseparated.pdf");
        await File.WriteAllBytesAsync(path, SeparationPdf.CreatePreseparated());
        return path;
    }
}
