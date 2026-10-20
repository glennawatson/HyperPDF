// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;
using HyperPdf.TestAssets;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks the media panel: sound and video are listed, saved on request, and opened with the system player only after the reader agrees.</summary>
public sealed class DocumentMediaViewModelTests
{
    /// <summary>The sound, screen clip and rich media asset of the test document.</summary>
    private const int MediaCount = 3;

    /// <summary>The position of the screen clip.</summary>
    private const int ClipPosition = 1;

    /// <summary>The media panel offers the document's sound and video, and a document without any hides it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsTheDocumentsMedia()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tab = await OpenAsync(test, main, TestPdf.CreateMediaDocument());

        await Assert.That(tab.Media.HasMedia).IsTrue();
        await Assert.That(tab.Media.Items.Count).IsEqualTo(MediaCount);

        using var plainMain = new MainViewModel(test.Services);
        var plain = await OpenAsync(test, plainMain, TestPdf.Create(1));

        await Assert.That(plain.Media.HasMedia).IsFalse();
    }

    /// <summary>Saving writes the embedded bytes to the chosen path and says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesMediaWhereTheReaderChooses()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tab = await OpenAsync(test, main, TestPdf.CreateMediaDocument());
        var target = Path.Combine(test.Directory, "saved.mp4");

        var saved = await tab.Media.SaveToAsync(tab.Media.Items[ClipPosition], target);

        await Assert.That(saved).IsTrue();
        await Assert.That(await File.ReadAllTextAsync(target)).IsEqualTo("MP4DATA!");
        await Assert.That(tab.Notice).IsEqualTo("Saved intro.mp4.");
    }

    /// <summary>Opening asks first: nothing reaches the system player when the reader says no, and the file is handed over when they say yes.</summary>
    /// <param name="agree">Whether the reader agrees.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task OpensWithThePlayerOnlyAfterConfirmation(bool agree)
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tab = await OpenAsync(test, main, TestPdf.CreateMediaDocument());
        string? asked = null;
        string? launched = null;
        using var confirm = tab.Links.ConfirmOpenFileInteraction.RegisterHandler(context =>
        {
            asked = context.Input;
            context.SetOutput(agree);
        });
        using var launch = tab.Links.LaunchFileInteraction.RegisterHandler(context =>
        {
            launched = context.Input;
            context.SetOutput(RxVoid.Default);
        });

        var opened = await tab.Media.OpenAsync(tab.Media.Items[ClipPosition]);

        await Assert.That(opened).IsEqualTo(agree);
        await Assert.That(asked).IsEqualTo("intro.mp4");
        await Assert.That(launched is not null).IsEqualTo(agree);
    }

    /// <summary>Opens a document and waits for it to load.</summary>
    /// <param name="test">The services.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="bytes">The document.</param>
    /// <returns>The tab.</returns>
    private static async Task<DocumentTabViewModel> OpenAsync(TestServices test, MainViewModel main, byte[] bytes)
    {
        var path = Path.Combine(test.Directory, $"{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, bytes);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        return tab;
    }
}
