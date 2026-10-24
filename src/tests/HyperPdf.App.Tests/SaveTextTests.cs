// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Archiving;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Tests for <see cref="SaveText"/> and the note a tab shows when a save removed a trap network.</summary>
public sealed class SaveTextTests
{
    /// <summary>The first page's warning.</summary>
    private const string First = "HyperPDF removed the trap network on page 1.";

    /// <summary>A save that changed nothing the reader needs to know shows no note.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlainSaveHasNoNote() => await Assert.That(SaveText.Notice(new(true, null))).IsNull();

    /// <summary>The trap network note gives the first page's warning and counts the others.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TrapNetworkNoteCountsOtherPages()
    {
        using (Assert.Multiple())
        {
            await Assert.That(SaveText.TrapNetworkNotice([First])).IsEqualTo(First);
            await Assert.That(SaveText.TrapNetworkNotice([First, "Second."])).IsEqualTo($"{First} The same applies to one other page.");
            await Assert.That(SaveText.TrapNetworkNotice([First, "Second.", "Third."])).IsEqualTo($"{First} The same applies to 2 other pages.");
        }
    }

    /// <summary>A save that removed a PDF/A claim and a trap network gives both, the claim first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClaimAndTrapNetworkNotesAreJoined()
    {
        var removal = new ArchiveClaimRemoval(ArchiveLevel.PdfA2B, ["Fonts shall be embedded."]);
        var outcome = new DocumentSaveOutcome(true, null, removal) { TrapNetworkWarnings = [First] };

        var notice = SaveText.Notice(outcome);

        await Assert.That(notice).IsEqualTo($"{ArchiveText.ClaimRemovedNotice(removal)}\n{First}");
    }

    /// <summary>Saving a file whose trap network is older than its page tells the reader the network was removed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SaveWarnsWhenItRemovesATrapNetwork()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var file = TrapNetworkPdf.Build(TrapNetworkPdf.Dated("D:20210101000000Z"));
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("trapped.pdf", file)]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();

        var saved = await tab.SaveAsync(Path.Combine(test.Directory, "trapped-saved.pdf"), CancellationToken.None);

        await Assert.That(saved).IsTrue();
        await Assert.That(tab.Notice).StartsWith("HyperPDF removed the trap network on page 1, because the page is dated later than its traps.");
    }
}
