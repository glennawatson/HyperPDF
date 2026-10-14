// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;
using HyperPdf.TestAssets;

namespace HyperPdf.App.Tests;

/// <summary>Tests for <see cref="FocusModeViewModel"/>.</summary>
public sealed class FocusModeViewModelTests
{
    /// <summary>The article's pages.</summary>
    private const int Pages = 2;

    /// <summary>The name of the generated article.</summary>
    private const string ArticleName = "article.pdf";

    /// <summary>Reading aloud marks the sentence on the block in Focus Mode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarksTheSentenceBeingRead()
    {
        var speech = new FakeSpeech(true, true);
        using var test = new TestServices(new Services.FallbackPlatform(), speech);
        var path = Path.Combine(test.Directory, ArticleName);
        await File.WriteAllBytesAsync(path, TestPdf.CreateArticle(Pages));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        var marksBefore = tab.ReadAloud.MarksVersion;
        tab.FocusMode.IsOn = true;
        await tab.FocusMode.Pages[0].LoadAsync();

        tab.ReadAloud.IsOpen = true;

        await Assert.That(await UiWait.UntilAsync(() => !tab.ReadAloud.SpokenRange.IsEmpty)).IsTrue();
        await Assert.That(tab.ReadAloud.MarksVersion).IsGreaterThan(marksBefore);
        await Assert.That(tab.FocusMode.Pages[0].Blocks[0].Spoken.IsEmpty).IsFalse();
        tab.ReadAloud.IsOpen = false;
    }

    /// <summary>A page asked for while the document is closed is not marked empty for good; it loads once the document is back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageAskedForWhileTheDocumentIsClosedLoadsLater()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, ArticleName);
        await File.WriteAllBytesAsync(path, TestPdf.CreateArticle(Pages));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        var available = false;
        var page = new FocusPageViewModel(0, "Page 1", () => available ? tab.GetReadingDocument() : null);

        await page.LoadAsync();
        var emptyWhileClosed = page.IsEmpty;
        available = true;
        await page.LoadAsync();

        using (Assert.Multiple())
        {
            await Assert.That(emptyWhileClosed).IsFalse();
            await Assert.That(page.IsEmpty).IsFalse();
            await Assert.That(page.Blocks.Count).IsGreaterThan(0);
        }
    }

    /// <summary>Recognised or changed page text makes Focus Mode load its pages again instead of keeping the old blocks.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangedPageTextMakesFocusModeLoadAgain()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, ArticleName);
        await File.WriteAllBytesAsync(path, TestPdf.CreateArticle(Pages));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        tab.FocusMode.IsOn = true;
        var before = tab.FocusMode.Pages[0];
        await before.LoadAsync();

        tab.InvalidateReading();

        using (Assert.Multiple())
        {
            await Assert.That(tab.FocusMode.Pages[0]).IsNotSameReferenceAs(before);
            await Assert.That(tab.FocusMode.Pages.Count).IsEqualTo(Pages);
        }

        await tab.FocusMode.Pages[0].LoadAsync();
        await Assert.That(tab.FocusMode.Pages[0].Blocks.Count).IsGreaterThan(0);
    }
}
