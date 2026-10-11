// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests for <see cref="FocusModeViewModel"/>.</summary>
public sealed class FocusModeViewModelTests
{
    /// <summary>The article's pages.</summary>
    private const int Pages = 2;

    /// <summary>Reading aloud marks the sentence on the block in Focus Mode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarksTheSentenceBeingRead()
    {
        var speech = new FakeSpeech(true, true);
        using var test = new TestServices(new Services.FallbackPlatform(), speech);
        var path = Path.Combine(test.Directory, "article.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateArticle(Pages));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        var changes = 0;
        using var probe = tab.ReadAloud.MarksChanged.SubscribeSafe(_ => changes++, static _ => { });
        tab.FocusMode.IsOn = true;
        await tab.FocusMode.Pages[0].LoadAsync();

        tab.ReadAloud.IsOpen = true;

        await Assert.That(await UiWait.UntilAsync(() => !tab.ReadAloud.SpokenRange.IsEmpty)).IsTrue();
        await Assert.That(changes).IsGreaterThan(0);
        await Assert.That(tab.FocusMode.Pages[0].Blocks[0].Spoken.IsEmpty).IsFalse();
        tab.ReadAloud.IsOpen = false;
    }
}
