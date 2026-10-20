// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;
using HyperPdf.Core.Attachments;
using HyperPdf.TestAssets;
using ReactiveUI.Primitives;
using TUnit.Assertions.Enums;

namespace HyperPdf.App.Tests;

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

    /// <summary>The file a portfolio opens at, which its sort puts first.</summary>
    private const string PortfolioFirst = "second.txt";

    /// <summary>The files of the test portfolio in the order its sort keys ask for.</summary>
    private static readonly string[] PortfolioOrder = [PortfolioFirst, "first.txt"];

    /// <summary>Gets a two-file portfolio that sorts by name, descending, shows tiles and opens at the file that sorts first.</summary>
    private static byte[] PortfolioPdf => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(first.txt) 4 0 R (second.txt) 5 0 R] >> >> /Collection 8 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
        "<< /Type /Filespec /F (first.txt) /EF << /F 6 0 R >> >>",
        "<< /Type /Filespec /F (second.txt) /EF << /F 7 0 R >> >>",
        MiniPdf.Stream("/Type /EmbeddedFile", "one"),
        MiniPdf.Stream("/Type /EmbeddedFile", "two"),
        "<< /Type /Collection /D (second.txt) /View /T /Schema << /Name << /Subtype /S /N (Name) /O 0 >> >> /Sort << /S [/F] /A [false] >> >>");

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

    /// <summary>Verifies a portfolio opens with its files in the sort order, its opening file selected and the panel showing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsPortfolioInItsOwnOrder()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "portfolio.pdf");
        await File.WriteAllBytesAsync(path, PortfolioPdf);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        var attachments = tab.Attachments;

        await Assert.That(attachments.ShowsPortfolio).IsTrue();
        await Assert.That(attachments.Items.Select(static item => item.Name)).IsEquivalentTo(PortfolioOrder, CollectionOrdering.Matching);
        await Assert.That(attachments.Selected?.Name).IsEqualTo(PortfolioFirst);
        await Assert.That(attachments.PortfolioText).IsEqualTo($"PDF portfolio: 2 files, tiles view. It opens at {PortfolioFirst}.");
        await Assert.That(tab.SidebarMode).IsEqualTo(SidebarMode.Attachments);
        await Assert.That(tab.SidebarVisible).IsTrue();
    }

    /// <summary>Verifies a portfolio is described in words for each view.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DescribesPortfolioViews()
    {
        PortfolioSummary details = new("D", 1, null);
        PortfolioSummary hidden = new("H", PortfolioOrder.Length, null);

        await Assert.That(AttachmentsViewModel.DescribePortfolio(details)).IsEqualTo("PDF portfolio: 1 file, details view.");
        await Assert.That(AttachmentsViewModel.DescribePortfolio(hidden)).IsEqualTo("PDF portfolio: 2 files, hidden view.");
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
