// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.Core.Printing;
using HyperPdf.Core.Settings;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Checks that the viewer follows a document's open hints with HyperPDF, and that the reader's own settings and choices come first.</summary>
public sealed class DocumentOpenHintTests
{
    /// <summary>The zero-based page the test document's open action goes to: its third page.</summary>
    private const int OpenActionPage = 2;

    /// <summary>The page the reader was last at.</summary>
    private const int RememberedPage = 1;

    /// <summary>The copies the test document asks for.</summary>
    private const int Copies = 3;

    /// <summary>The catalog entries that make a document open at its third page.</summary>
    private const string OpenAtThirdPage = "/OpenAction [5 0 R /Fit]";

    /// <summary>The catalog entries that ask for spreads to read right to left.</summary>
    private const string RightToLeftHint = "/ViewerPreferences << /Direction /R2L >>";

    /// <summary>The catalog entries that ask for the document's title in the window title.</summary>
    private const string ShowTitleHint = "/ViewerPreferences << /DisplayDocTitle true >>";

    /// <summary>The catalog entries for every print default.</summary>
    private const string PrintHints = "/ViewerPreferences << /PrintScaling /None /NumCopies 3 /Duplex /DuplexFlipShortEdge >>";

    /// <summary>Verifies that a document opens at the page its open action names.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OpenAction_ShowsThePageTheDocumentNames()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);

        var tab = await OpenAsync(test, main, OpenAtThirdPage);

        await Assert.That(tab.Destination.IsRequest).IsTrue();
        await Assert.That(tab.Destination.PageIndex).IsEqualTo(OpenActionPage);
    }

    /// <summary>Verifies that the page the reader last read wins over the open action.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RememberedPage_WinsOverTheOpenAction()
    {
        using var test = new TestServices();
        var path = test.CreateDocument("remembered.pdf", OpenHintSamples.Create(OpenAtThirdPage));
        LastPages.Remember(test.Services.Settings.LastPages, path, RememberedPage);
        using var main = new MainViewModel(test.Services);

        await TestServices.OpenAndWaitAsync(main, [path]);

        await Assert.That(main.SelectedTab!.Destination.IsRequest).IsFalse();
        await Assert.That(main.SelectedTab.CurrentPageIndex).IsEqualTo(RememberedPage);
    }

    /// <summary>Verifies that turning the setting off leaves the open action alone.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OpenAction_IsLeftAloneWhenTheSettingIsOff()
    {
        using var test = new TestServices();
        test.Services.Settings.FollowOpenAction = false;
        using var main = new MainViewModel(test.Services);

        var tab = await OpenAsync(test, main, OpenAtThirdPage);

        await Assert.That(tab.Destination.IsRequest).IsFalse();
    }

    /// <summary>Verifies that the page mode opens the panel it names, and that the setting turns that off.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PageMode_OpensTheNamedPanel()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tab = await OpenAsync(test, main, "/PageMode /UseOutlines");

        using var other = new TestServices();
        other.Services.Settings.FollowPageMode = false;
        using var otherMain = new MainViewModel(other.Services);
        var untouched = await OpenAsync(other, otherMain, "/PageMode /UseOutlines");

        await Assert.That(tab.SidebarMode).IsEqualTo(SidebarMode.Outline);
        await Assert.That(tab.SidebarVisible).IsTrue();
        await Assert.That(untouched.SidebarMode).IsEqualTo(SidebarMode.Thumbnails);
    }

    /// <summary>Verifies that the attachments page mode opens the attachments panel.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UseAttachments_OpensTheAttachmentsPanel()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);

        var tab = await OpenAsync(test, main, "/PageMode /UseAttachments");

        await Assert.That(tab.SidebarMode).IsEqualTo(SidebarMode.Attachments);
    }

    /// <summary>Verifies that a layout that starts with the first page alone opens in the cover arrangement, and one with a spread opens in the dual arrangement.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PageLayout_OpensTheNamedArrangement()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var cover = await OpenAsync(test, main, "/PageLayout /TwoPageRight");
        var dual = await OpenAsync(test, main, "/PageLayout /TwoColumnLeft");

        await Assert.That(cover.LayoutMode).IsEqualTo(PageLayoutMode.DualCover);
        await Assert.That(dual.LayoutMode).IsEqualTo(PageLayoutMode.Dual);
    }

    /// <summary>Verifies that a right to left direction is followed, and that the setting turns that off.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Direction_ReadsRightToLeftWhenAllowed()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var followed = await OpenAsync(test, main, RightToLeftHint);

        using var other = new TestServices();
        other.Services.Settings.FollowPageLayout = false;
        using var otherMain = new MainViewModel(other.Services);
        var ignored = await OpenAsync(other, otherMain, RightToLeftHint);

        await Assert.That(followed.RightToLeft).IsTrue();
        await Assert.That(ignored.RightToLeft).IsFalse();
    }

    /// <summary>Verifies that the document's title is the tab's title only when the document asks for it and the reader allows it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DisplayDocTitle_NamesTheTabByTheTitleWhenItAsks()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var asked = await OpenAsync(test, main, ShowTitleHint);
        var notAsked = await OpenAsync(test, main, string.Empty);

        using var other = new TestServices();
        other.Services.Settings.FollowDisplayDocTitle = false;
        using var otherMain = new MainViewModel(other.Services);
        var refused = await OpenAsync(other, otherMain, ShowTitleHint);

        await Assert.That(asked.WindowName).IsEqualTo(OpenHintSamples.Title);
        await Assert.That(notAsked.WindowName).IsEqualTo(notAsked.FileName);
        await Assert.That(refused.WindowName).IsEqualTo(refused.FileName);
        await Assert.That(asked.Title).IsEqualTo(OpenHintSamples.Title);
    }

    /// <summary>Verifies that the window title changes to the document's title once a document that asks for it has loaded.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WindowTitle_ShowsTheDocumentTitleWhenItAsks()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);

        _ = await OpenAsync(test, main, ShowTitleHint);

        await Assert.That(main.WindowTitle).StartsWith(OpenHintSamples.Title);
    }

    /// <summary>Verifies that the print window starts with the copies, two-sided choice and scaling the document asks for.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PrintHints_AreTheDefaultsOfThePrintWindow()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tab = await OpenAsync(test, main, PrintHints);

        using var preview = new PrintPreviewViewModel(tab, test.Services);

        await Assert.That(preview.Copies).IsEqualTo(Copies);
        await Assert.That(preview.TwoSided).IsTrue();
        await Assert.That(preview.Binding).IsEqualTo(DuplexBinding.ShortEdge);
        await Assert.That(preview.Scaling).IsEqualTo(PrintScaling.ActualSize);
    }

    /// <summary>Verifies that without print hints, or with the setting off, the print window starts with the viewer's own defaults.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PrintHints_AreIgnoredWhenTheSettingIsOff()
    {
        using var test = new TestServices();
        test.Services.Settings.FollowPrintDefaults = false;
        using var main = new MainViewModel(test.Services);
        var tab = await OpenAsync(test, main, PrintHints);

        using var preview = new PrintPreviewViewModel(tab, test.Services);

        await Assert.That(preview.Copies).IsEqualTo(1);
        await Assert.That(preview.TwoSided).IsFalse();
    }

    /// <summary>Verifies that Properties says whether the document is a tagged PDF.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Properties_SaysWhetherTheDocumentIsTagged()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tagged = await OpenAsync(test, main, "/MarkInfo << /Marked true >>");
        var untagged = await OpenAsync(test, main, string.Empty);

        await Assert.That(new PropertiesViewModel(tagged).Entries).Contains(new PropertyEntry("Tagged PDF", "Yes"));
        await Assert.That(new PropertiesViewModel(untagged).Entries).Contains(new PropertyEntry("Tagged PDF", "No"));
    }

    /// <summary>Verifies that a bookmark's bold style is kept, and its colour is left out until the setting allows it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OutlineStyle_KeepsBoldAndLeavesOutColoursUnlessAllowed()
    {
        using var test = new TestServices();
        var path = test.CreateDocument("outline.pdf", OpenHintSamples.CreateStyledOutline());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var comfortable = main.SelectedTab!.Outline[0];

        var coloured = OutlineItemViewModel.Create(main.SelectedTab.Source.Outline, false)[0];

        await Assert.That(comfortable.IsBold).IsTrue();
        await Assert.That(comfortable.IsItalic).IsFalse();
        await Assert.That(comfortable.Colour).IsNull();
        await Assert.That(coloured.Colour).IsEqualTo(0xFF0000U);
        await Assert.That(main.SelectedTab.Outline[1].IsBold).IsFalse();
    }

    /// <summary>Opens a document with catalog entries and waits for its tab to load.</summary>
    /// <param name="test">The test services.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="catalogEntries">The entries added to the catalog.</param>
    /// <returns>The tab.</returns>
    private static async Task<DocumentTabViewModel> OpenAsync(TestServices test, MainViewModel main, string catalogEntries)
    {
        var name = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"hints-{Guid.NewGuid():N}.pdf");
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(name, OpenHintSamples.Create(catalogEntries))]);
        return main.SelectedTab!;
    }
}
