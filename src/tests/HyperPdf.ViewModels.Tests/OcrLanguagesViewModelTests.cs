// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels.Preferences;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the window that ticks, downloads and removes the languages text can be recognised in.</summary>
public sealed class OcrLanguagesViewModelTests
{
    /// <summary>How long the test waits for a download to finish.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>How often the test looks whether the download is finished.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(25);

    /// <summary>Pressing a language that is not on this computer ticks it, saves the choice and downloads it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PressingALanguageTicksItAndDownloadsIt()
    {
        using var test = new TestServices(new FakeOcr(true));
        using var languages = new OcrLanguagesViewModel(test.Services);
        var other = languages.Items.First(static item => item.CanDownload);

        languages.Pick(other);

        await Assert.That(other.IsSelected).IsTrue();
        await Assert.That(test.Services.Settings.OcrLanguage).Contains(other.Pack.Code);
        await Assert.That(await WaitForAsync(() => other.IsDownloaded)).IsTrue();
    }

    /// <summary>Removing the languages that are not ticked deletes their packs and keeps the ticked ones.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntickedDownloadedLanguagesAreRemoved()
    {
        using var test = new TestServices(new FakeOcr(true));
        using var languages = new OcrLanguagesViewModel(test.Services);
        var kept = languages.Items.First(static item => item.CanDownload);
        var dropped = languages.Items.First(item => item.CanDownload && item != kept);
        languages.Pick(kept);

        // A language cannot be downloaded while another download runs, so the second is picked once the first is done.
        await Assert.That(await WaitForAsync(() => kept.IsDownloaded && !languages.IsDownloading)).IsTrue();
        languages.Pick(dropped);
        await Assert.That(await WaitForAsync(() => dropped.IsDownloaded && !languages.IsDownloading)).IsTrue();
        languages.Pick(dropped);

        _ = await languages.RemoveUntickedCommand.Execute().ToTask();

        await Assert.That(dropped.IsDownloaded).IsFalse();
        await Assert.That(kept.IsDownloaded).IsTrue();
    }

    /// <summary>Waits until a condition holds, looking at a steady interval.</summary>
    /// <param name="condition">The condition.</param>
    /// <returns><see langword="true"/> when it held in time.</returns>
    private static async Task<bool> WaitForAsync(Func<bool> condition)
    {
        using var cancel = new CancellationTokenSource(Patience);
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (!condition())
            {
                _ = await timer.WaitForNextTickAsync(cancel.Token);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return condition();
        }
    }
}
