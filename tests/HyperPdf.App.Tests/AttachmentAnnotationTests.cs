// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;
using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;

namespace HyperPdf.App.Tests;

/// <summary>Checks that the icon of a file attachment annotation reaches the file it holds, with HyperPDF.</summary>
public sealed class AttachmentAnnotationTests
{
    /// <summary>Verifies that the page has a link over the icon, and that the panel lists the file with its page.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Icon_IsALinkAndItsFileIsListed()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("clip.pdf", AttachmentAnnotationSamples.Create())]);
        var tab = main.SelectedTab!;

        var links = tab.TryGetDocument()!.GetLinks(0);
        var attached = links.Where(static link => link.Target.Kind == LinkTargetKind.AttachedFile).ToArray();

        await Assert.That(attached.Length).IsEqualTo(1);
        await Assert.That(tab.Attachments.Items.Count).IsEqualTo(1);
        await Assert.That(tab.Attachments.Items[0].Name).IsEqualTo(AttachmentAnnotationSamples.FileName);
        await Assert.That(tab.Attachments.Items[0].PageIndex).IsEqualTo(0);
        await Assert.That(tab.Attachments.Items[0].AnnotationId).IsEqualTo(AttachmentAnnotationSamples.AnnotationObject);
    }

    /// <summary>Verifies that following the icon opens the attachments panel with the file selected, without opening or saving it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FollowingTheIcon_SelectsTheFileInThePanel()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("clip-follow.pdf", AttachmentAnnotationSamples.Create())]);
        var tab = main.SelectedTab!;
        var link = tab.TryGetDocument()!.GetLinks(0).Single(static l => l.Target.Kind == LinkTargetKind.AttachedFile);

        tab.Navigate(link.Target);

        await Assert.That(tab.SidebarMode).IsEqualTo(SidebarMode.Attachments);
        await Assert.That(tab.SidebarVisible).IsTrue();
        await Assert.That(tab.Attachments.Selected?.Name).IsEqualTo(AttachmentAnnotationSamples.FileName);
    }

    /// <summary>Verifies that the file on the page can be saved with its own contents.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FileOnThePage_CanBeSaved()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("clip-save.pdf", AttachmentAnnotationSamples.Create())]);
        var tab = main.SelectedTab!;
        var path = Path.Combine(test.Directory, "saved-notes.txt");

        var saved = await tab.Attachments.SaveToAsync(tab.Attachments.Items[0], path);

        await Assert.That(saved).IsTrue();
        await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo(AttachmentAnnotationSamples.FileText);
    }
}
