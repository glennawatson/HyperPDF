// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests for <see cref="AttachmentsViewModel"/>.</summary>
public sealed class AttachmentsViewModelTests
{
    /// <summary>A size in kilobytes.</summary>
    private const long TwelveKilobytes = 12 * 1024;

    /// <summary>A size in megabytes.</summary>
    private const long ThreeAndAHalfMegabytes = 3_670_016;

    /// <summary>The size of <see cref="ThreeAndAHalfMegabytes"/> in megabytes.</summary>
    private const double ThreeAndAHalf = 3.5;

    /// <summary>A size in bytes.</summary>
    private const long SmallFile = 512;

    /// <summary>Verifies a document's embedded file is listed and saved where the user picks.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesAttachment()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "attached.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateWithAttachment());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        var attachments = tab.Attachments;
        var target = Path.Combine(test.Directory, "saved-notes.txt");
        string? suggested = null;
        using var handler = attachments.SaveInteraction.RegisterHandler(context =>
        {
            suggested = context.Input;
            context.SetOutput(target);
        });

        attachments.Selected = attachments.Items[0];
        _ = await attachments.SaveCommand.Execute().ToTask();

        await Assert.That(attachments.HasAttachments).IsTrue();
        await Assert.That(suggested).IsEqualTo(TestPdf.AttachmentName);
        await Assert.That(await File.ReadAllTextAsync(target)).IsEqualTo(TestPdf.AttachmentText);
        await Assert.That(tab.Notice).IsEqualTo($"Saved {TestPdf.AttachmentName}.");
    }

    /// <summary>Verifies documents without attachments keep the panel hidden.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HidesPanelWithoutAttachments()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("plain.pdf", 1)]);

        await Assert.That(main.SelectedTab!.Attachments.HasAttachments).IsFalse();
        await Assert.That(main.SelectedTab.Attachments.Items.Count).IsEqualTo(0);
    }

    /// <summary>Verifies sizes read naturally.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormatsSizes()
    {
        await Assert.That(AttachmentsViewModel.FormatSize(1)).IsEqualTo("1 byte");
        await Assert.That(AttachmentsViewModel.FormatSize(SmallFile)).IsEqualTo("512 bytes");
        await Assert.That(AttachmentsViewModel.FormatSize(TwelveKilobytes)).IsEqualTo("12 KB");
        await Assert.That(AttachmentsViewModel.FormatSize(ThreeAndAHalfMegabytes)).IsEqualTo(string.Create(System.Globalization.CultureInfo.CurrentCulture, $"{ThreeAndAHalf:0.0} MB"));
    }
}
