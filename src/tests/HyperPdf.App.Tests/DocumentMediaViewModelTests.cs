// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Media;
using HyperPdf.Media;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks the media panel: sound and video are listed, saved on request, and opened with the system player only after the reader agrees.</summary>
public sealed class DocumentMediaViewModelTests
{
    /// <summary>The sound, screen clip and rich media asset of the test document.</summary>
    private const int MediaCount = 3;

    /// <summary>The position of the screen clip.</summary>
    private const int ClipPosition = 1;

    /// <summary>A bar value this close to the playing position is the bar following the player, in seconds.</summary>
    private const double FollowStep = 0.2;

    /// <summary>The place the bar is moved to, in seconds.</summary>
    private const double SeekSeconds = 1.5;

    /// <summary>How long to wait to see that nothing starts by itself, in milliseconds.</summary>
    private const int SettleMilliseconds = 400;

    /// <summary>How long to wait to see that nothing starts by itself.</summary>
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(SettleMilliseconds);

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

    /// <summary>When media cannot be played the player says so, its buttons stay off, and saving still works.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlayerExplainsThatMediaCannotBePlayed()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tab = await OpenAsync(test, main, TestPdf.CreateMediaDocument());
        using var media = new DocumentMediaViewModel(tab, new MediaPlaybackFactory(static () => false, static _ => null));
        media.Refresh();
        media.Selected = media.Items[ClipPosition];

        await Assert.That(media.Player.IsAvailable).IsFalse();
        await Assert.That(media.Player.Status).Contains("cannot be played");
        await Assert.That(await media.Player.PlayCommand.CanExecute.FirstAsync()).IsFalse();
        await Assert.That(media.SaveCommand is not null).IsTrue();
    }

    /// <summary>Nothing plays until Play is pressed. Then the video plays in the panel with a picture, and choosing other media puts the player away.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlaysTheChosenClipOnlyAfterPlayIsPressed()
    {
        var playback = new MediaPlaybackFactory(static () => NullStreamingAudioOutput.Instance);
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tab = await OpenAsync(test, main, TestPdf.CreateMediaDocument(await ReadClipAsync()));
        using var media = new DocumentMediaViewModel(tab, playback);
        media.Refresh();

        media.Selected = media.Items[ClipPosition];
        await Task.Delay(SettleTime);

        await Assert.That(media.Player.State).IsEqualTo(MediaPlayerState.Idle);
        await Assert.That(media.Player.FrameVersion).IsEqualTo(0);
        await Assert.That(await media.Player.PlayCommand.CanExecute.FirstAsync()).IsTrue();

        _ = await media.Player.PlayCommand.Execute().ToTask();
        await Assert.That(await UiWait.UntilAsync(() => media.Player.FrameVersion > 0)).IsTrue();

        await Assert.That(media.Player.HasPicture).IsTrue();
        await Assert.That(media.Player.FrameSize.IsEmpty).IsFalse();
        var picture = new byte[media.Player.FrameSize.ByteCount];
        await Assert.That(media.Player.TryCopyFrame(picture)).IsTrue();
        await Assert.That(await media.Player.StopCommand.CanExecute.FirstAsync()).IsTrue();

        media.Selected = media.Items[0];
        await Assert.That(media.Player.State).IsEqualTo(MediaPlayerState.Idle);
        await Assert.That(media.Player.HasPicture).IsFalse();
    }

    /// <summary>Moving the position bar and letting go goes to that place; a bar that only follows the player does not seek.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovingThePositionBarSeeks()
    {
        var playback = new MediaPlaybackFactory(static () => NullStreamingAudioOutput.Instance);
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tab = await OpenAsync(test, main, TestPdf.CreateMediaDocument(await ReadClipAsync()));
        using var media = new DocumentMediaViewModel(tab, playback);
        media.Refresh();
        media.Selected = media.Items[ClipPosition];
        _ = await media.Player.PlayCommand.Execute().ToTask();
        _ = await media.Player.PauseCommand.Execute().ToTask();
        await Assert.That(await UiWait.UntilAsync(() => media.Player.State == MediaPlayerState.Paused)).IsTrue();
        var paused = media.Player.PositionSeconds;

        media.Player.NoteSeek(paused + FollowStep);
        media.Player.CompleteSeek();
        await Assert.That(media.Player.PositionSeconds).IsEqualTo(paused);

        media.Player.NoteSeek(SeekSeconds);
        media.Player.CompleteSeek();

        await Assert.That(media.Player.PositionSeconds).IsEqualTo(SeekSeconds);
        await Assert.That(await UiWait.UntilAsync(() => media.Player.State == MediaPlayerState.Paused)).IsTrue();
    }

    /// <summary>Reads the short H.264 video with sound that the tests embed in documents.</summary>
    /// <returns>The bytes of the video.</returns>
    private static Task<byte[]> ReadClipAsync() => File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "av-h264-aac-text.mp4"));

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
