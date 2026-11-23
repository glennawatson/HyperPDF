// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Sidebar;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Tests the menus a right-click opens on the rows of the side panel and on a link.</summary>
public sealed class SidebarContextMenusTests
{
    /// <summary>The pages of the plain document.</summary>
    private const int Pages = 3;

    /// <summary>The index of the page a right-click lands on.</summary>
    private const int PressedPage = 2;

    /// <summary>The entries of an attached file's menu: open and save.</summary>
    private const int AttachmentEntries = 2;

    /// <summary>Where the note of the comment test goes.</summary>
    private static readonly PagePoint NoteAt = new(300, 300);

    /// <summary>The page a right-click lands on, alone.</summary>
    private static readonly int[] PressedOnly = [PressedPage];

    /// <summary>A right-click on a page that is not selected selects it alone, and the menu is the page menu.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageRowSelectsThePageAndOffersThePageTools()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("row-page.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.Pages.SelectPages([0]);

        var items = SidebarContextMenus.ForRow(tab, new ThumbnailItemViewModel(PressedPage, "3", 1, 1));

        await Assert.That(items).IsNotNull();
        await Assert.That(tab.Pages.SelectedPages).IsEquivalentTo(PressedOnly);
        await Assert.That(items!.Exists(static item => item.Header == "Dele_te")).IsTrue();
    }

    /// <summary>A comment's row selects the comment and offers going to it before what the page's own menu offers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CommentRowSelectsTheCommentAndOffersGoingToIt()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("row-comment.pdf", Pages)]);
        var tab = main.SelectedTab!;
        using var prompt = tab.Annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("A note"));
        _ = await tab.Annotations.AddNote(0, NoteAt).ToTask();
        var row = tab.Annotations.Items[0];
        tab.Annotations.Select(null);

        var items = SidebarContextMenus.ForRow(tab, row);

        await Assert.That(items![1].Header).IsEqualTo("_Go to This Comment");
        await Assert.That(items[1].Parameter).IsEqualTo(row);
        await Assert.That(tab.Annotations.Selected).IsEqualTo(row.Annotation);
        await Assert.That(items[^1].Gesture).IsEqualTo("Delete");
    }

    /// <summary>An attached file's row selects the file and offers opening it and saving a copy.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AttachmentRowSelectsTheFileAndOffersOpenAndSave()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("row-attachment.pdf", TestPdf.CreateWithAttachment())]);
        var tab = main.SelectedTab!;
        tab.Attachments.Refresh();
        var file = tab.Attachments.Items[0];
        tab.Attachments.Selected = null;

        var items = SidebarContextMenus.ForRow(tab, file);

        await Assert.That(items!.Count).IsEqualTo(AttachmentEntries);
        await Assert.That(tab.Attachments.Selected).IsEqualTo(file);
        await Assert.That(items[0].IsEnabled).IsTrue();
        await Assert.That(items[1].IsEnabled).IsTrue();
    }

    /// <summary>A layer's menu hides a shown layer, and showing one layer alone hides the others.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayerRowHidesTheLayerAndShowsItAlone()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("row-layers.pdf", TestPdf.CreateWithLayers())]);
        var tab = main.SelectedTab!;
        var layers = tab.Layers;
        var first = FirstLayer(layers);
        var wasVisible = first.IsVisible;

        var items = SidebarContextMenus.ForRow(tab, first);
        _ = await layers.ToggleLayerCommand.Execute(first).ToTask();
        var afterToggle = first.IsVisible;
        _ = await layers.ShowOnlyCommand.Execute(first).ToTask();

        await Assert.That(items![0].Header).IsEqualTo(wasVisible ? "_Hide This Layer" : "_Show This Layer");
        await Assert.That(items[0].Parameter).IsEqualTo(first);
        await Assert.That(afterToggle).IsEqualTo(!wasVisible);
        await Assert.That(first.IsVisible).IsTrue();
        await Assert.That(OthersShown(layers, first)).IsEqualTo(0);
    }

    /// <summary>A link's menu starts with following the link; copying its address is offered only when it has one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinkMenuOffersFollowingAndCopyingTheAddress()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("row-link.pdf", Pages)]);
        var tab = main.SelectedTab!;
        using var copy = ReactiveCommand.Create<string>(static _ => { });
        PageLocation here = new(0, NoteAt);
        PageLink web = new(default, new(LinkTargetKind.Uri, 0, null, "https://example.org/"));
        PageLink inside = new(default, new(LinkTargetKind.Page, 1, null, null));

        var webItems = ContextMenus.ForLink(tab, web, here, copy);
        var insideItems = ContextMenus.ForLink(tab, inside, here, copy);

        await Assert.That(webItems[0].Header).IsEqualTo("_Open the Link");
        await Assert.That(webItems[0].Parameter).IsEqualTo(web.Target);
        await Assert.That(webItems[1].Parameter).IsEqualTo("https://example.org/");
        await Assert.That(webItems[1].IsEnabled).IsTrue();
        await Assert.That(insideItems[1].IsEnabled).IsFalse();
    }

    /// <summary>A row that stands for nothing with a menu gets none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownRowHasNoMenu()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("row-none.pdf", Pages)]);

        await Assert.That(SidebarContextMenus.ForRow(main.SelectedTab!, "words")).IsNull();
        await Assert.That(SidebarContextMenus.ForRow(main.SelectedTab!, null)).IsNull();
    }

    /// <summary>Finds the first layer that can be shown and hidden, or the first row when none can.</summary>
    /// <param name="layers">The layers.</param>
    /// <returns>The layer.</returns>
    private static LayerItemViewModel FirstLayer(LayersViewModel layers)
    {
        foreach (var layer in layers.Items)
        {
            if (layer.CanToggle)
            {
                return layer;
            }
        }

        return layers.Items[0];
    }

    /// <summary>Counts the layers, other than one, that can be hidden and are shown.</summary>
    /// <param name="layers">The layers.</param>
    /// <param name="keep">The layer left out of the count.</param>
    /// <returns>The count.</returns>
    private static int OthersShown(LayersViewModel layers, LayerItemViewModel keep)
    {
        var shown = 0;
        foreach (var layer in layers.Items)
        {
            if (layer.CanToggle && layer.IsVisible && !ReferenceEquals(layer, keep))
            {
                shown++;
            }
        }

        return shown;
    }
}
