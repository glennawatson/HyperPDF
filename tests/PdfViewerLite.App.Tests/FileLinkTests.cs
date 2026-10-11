// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests links to other files and opening attachments: PDFs open in tabs, other files only after asking, programs never.</summary>
public sealed class FileLinkTests
{
    /// <summary>The page of the other PDF the first link opens.</summary>
    private const int LinkedPage = 2;

    /// <summary>The pages in the other PDF.</summary>
    private const int OtherPages = 4;

    /// <summary>The tabs once the other PDF opens beside the first.</summary>
    private const int TwoTabs = 2;

    /// <summary>Verifies a link to another PDF opens it in a new tab at the linked page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensLinkedPdfAtItsPage()
    {
        using var test = new TestServices();
        var other = Path.Combine(test.Directory, TestPdf.LinkedDocumentName);
        await File.WriteAllBytesAsync(other, TestPdf.Create(OtherPages));
        using var main = await OpenLinksAsync(test);
        var tab = main.SelectedTab!;

        tab.Navigate(Target(tab, LinkTargetKind.OtherDocument));

        await Assert.That(await UiWait.UntilAsync(() => main.Tabs.Count == TwoTabs)).IsTrue();
        await Assert.That(main.SelectedTab!.FilePath).IsEqualTo(other);
        await Assert.That(main.SelectedTab.CurrentPageIndex).IsEqualTo(LinkedPage);
    }

    /// <summary>Verifies a link to another kind of file asks first, and opens it only when the reader agrees.</summary>
    /// <param name="agree">The reader's answer.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task AsksBeforeOpeningOtherFiles(bool agree)
    {
        using var test = new TestServices();
        var notes = Path.Combine(test.Directory, TestPdf.LaunchedFileName);
        await File.WriteAllTextAsync(notes, TestPdf.AttachmentText);
        using var main = await OpenLinksAsync(test);
        var tab = main.SelectedTab!;
        string? asked = null;
        string? launched = null;
        using var handler = tab.ConfirmOpenFileInteraction.RegisterHandler(context =>
        {
            asked = context.Input;
            context.SetOutput(agree);
        });
        using var launches = tab.FileLaunchRequests.SubscribeSafe(path => launched = path, static _ => { });

        tab.Navigate(Target(tab, LinkTargetKind.LaunchFile));

        await Assert.That(await UiWait.UntilAsync(() => asked is not null)).IsTrue();
        await Assert.That(asked).IsEqualTo(TestPdf.LaunchedFileName);

        // Opening the same file directly returns once the answer is acted on, so a refusal needs no wait for a launch that never comes.
        await Assert.That(await tab.OpenFileAsync(notes, 0)).IsEqualTo(agree);
        await Assert.That(launched).IsEqualTo(agree ? notes : null);
    }

    /// <summary>Verifies a missing file and a program are explained instead of opened, and nothing is asked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExplainsMissingFilesAndRefusesPrograms()
    {
        using var test = new TestServices();
        using var main = await OpenLinksAsync(test);
        var tab = main.SelectedTab!;
        var asked = false;
        using var handler = tab.ConfirmOpenFileInteraction.RegisterHandler(context =>
        {
            asked = true;
            context.SetOutput(true);
        });

        tab.Navigate(Target(tab, LinkTargetKind.LaunchFile));
        await Assert.That(await UiWait.UntilAsync(() => tab.Notice is not null)).IsTrue();
        await Assert.That(tab.Notice).Contains("was not found");

        var program = Path.Combine(test.Directory, "setup.exe");
        await File.WriteAllTextAsync(program, "MZ");
        await Assert.That(await tab.OpenFileAsync(program, 0)).IsFalse();
        await Assert.That(tab.Notice).Contains("never starts programs");
        await Assert.That(asked).IsFalse();
    }

    /// <summary>Verifies a link into an embedded document shows the attachments and says what to do.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PointsEmbeddedLinksToAttachments()
    {
        using var test = new TestServices();
        using var main = await OpenLinksAsync(test);
        var tab = main.SelectedTab!;

        tab.Navigate(Target(tab, LinkTargetKind.EmbeddedDocument));

        await Assert.That(tab.SidebarMode).IsEqualTo(SidebarMode.Attachments);
        await Assert.That(tab.SidebarVisible).IsTrue();
        await Assert.That(tab.Notice).Contains("Attachments");
    }

    /// <summary>Verifies Open Attachment writes the file out and opens it after asking.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensAttachmentAfterAsking()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "attached.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateWithAttachment());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        string? launched = null;
        using var handler = tab.ConfirmOpenFileInteraction.RegisterHandler(static context => context.SetOutput(true));
        using var launches = tab.FileLaunchRequests.SubscribeSafe(file => launched = file, static _ => { });

        tab.Attachments.Selected = tab.Attachments.Items[0];
        _ = await tab.Attachments.OpenSelectedCommand.Execute().ToTask();

        await Assert.That(await UiWait.UntilAsync(() => launched is not null)).IsTrue();
        await Assert.That(Path.GetFileName(launched)).IsEqualTo(TestPdf.AttachmentName);
        await Assert.That(await File.ReadAllTextAsync(launched!)).IsEqualTo(TestPdf.AttachmentText);
        File.Delete(launched!);
    }

    /// <summary>Verifies a document with content that cannot be shown says so, and the message can be put away.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WarnsAboutUnsupportedContent()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "unsupported.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateWithUnsupportedContent());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;

        await Assert.That(await UiWait.UntilAsync(() => tab.ContentWarning is not null)).IsTrue();
        const UnsupportedContent everything = UnsupportedContent.XfaForm | UnsupportedContent.JavaScript | UnsupportedContent.Multimedia
            | UnsupportedContent.ThreeD | UnsupportedContent.Portfolio;
        await Assert.That(tab.UnsupportedContent).IsEqualTo(everything);
        await Assert.That(tab.ContentWarning).Contains("an XFA form");
        _ = await tab.DismissContentWarningCommand.Execute().ToTask();
        await Assert.That(tab.ContentWarning).IsNull();
    }

    /// <summary>Opens the document of file links.</summary>
    /// <param name="test">The test services.</param>
    /// <returns>The window's view model, showing the document.</returns>
    private static async Task<MainViewModel> OpenLinksAsync(TestServices test)
    {
        var path = Path.Combine(test.Directory, "links.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateWithFileLinks());
        var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        return main;
    }

    /// <summary>Finds the link of a kind on the first page.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The link's target.</returns>
    private static LinkTarget Target(DocumentTabViewModel tab, LinkTargetKind kind) =>
        tab.TryGetDocument()!.GetLinks(0).Select(static link => link.Target).Single(target => target.Kind == kind);
}
