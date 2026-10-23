// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Media;
using ReactiveUI.Primitives;

namespace HyperPdf.Core.Tests.Media;

/// <summary>
/// Tests for <see cref="FfmpegMediaPlayer"/> against the FFmpeg programs on the computer. They skip with a reason when FFmpeg is not
/// installed, and they make their own short clips with FFmpeg so no media file is stored with the tests.
/// </summary>
public sealed class FfmpegMediaPlayerTests
{
    /// <summary>The width of the clips, in pixels.</summary>
    private const int ClipWidth = 160;

    /// <summary>The height of the clips, in pixels.</summary>
    private const int ClipHeight = 120;

    /// <summary>The seconds in the second caption's time of the captions test.</summary>
    private const double SecondCaptionTime = 1.5;

    /// <summary>A position before the start, in seconds.</summary>
    private const int BeforeTheStart = -4;

    /// <summary>The fewest pictures a clip of three seconds shows at normal motion.</summary>
    private const int FewestNormalPictures = 5;

    /// <summary>The position after which the pause test pauses, in milliseconds.</summary>
    private const int PauseAfter = 300;

    /// <summary>How long the pause test waits to see that nothing moves, in milliseconds.</summary>
    private const int QuietTime = 400;

    /// <summary>The least position the pause test must have reached, in milliseconds.</summary>
    private const int LeastPaused = 250;

    /// <summary>The most pictures a clip of three seconds shows at reduced motion.</summary>
    private const int MostReducedPictures = 4;

    /// <summary>How long to wait for the player.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>Verifies opening reads the size, length and captions and plays nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpeningReadsTheFileAndDoesNotPlay()
    {
        using var clip = await ClipFolder.CreateAsync(true);
        using var player = new FfmpegMediaPlayer(clip.Tools, false);

        await Assert.That(await player.OpenAsync(clip.Path, CancellationToken.None)).IsTrue();

        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Ready);
        await Assert.That(player.Info.HasVideo).IsTrue();
        await Assert.That(player.Info.HasCaptions).IsTrue();
        await Assert.That(player.FrameSize.Width).IsEqualTo(ClipWidth);
        await Assert.That(player.FrameSize.Height).IsEqualTo(ClipHeight);
        await Assert.That(player.Snapshot.Duration).IsGreaterThan(TimeSpan.FromSeconds(1));
        await Assert.That(player.Snapshot.Position).IsEqualTo(TimeSpan.Zero);
        await Assert.That(player.Snapshot.FrameVersion).IsEqualTo(0);
    }

    /// <summary>Verifies the captions follow the position of a file that is not playing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CaptionsFollowTheSeekPosition()
    {
        using var clip = await ClipFolder.CreateAsync(true);
        using var player = new FfmpegMediaPlayer(clip.Tools, false);
        _ = await player.OpenAsync(clip.Path, CancellationToken.None);

        await Assert.That(player.Snapshot.Caption).IsEqualTo("Hello");
        player.Seek(TimeSpan.FromSeconds(SecondCaptionTime));

        await Assert.That(player.Snapshot.Caption).IsEqualTo("World");
        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Ready);
        player.Seek(TimeSpan.FromSeconds(BeforeTheStart));
        await Assert.That(player.Snapshot.Position).IsEqualTo(TimeSpan.Zero);
    }

    /// <summary>Verifies a file plays pictures to its end.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlaysPicturesToTheEnd()
    {
        using var clip = await ClipFolder.CreateAsync(false);
        using var player = new FfmpegMediaPlayer(clip.Tools, false);
        _ = await player.OpenAsync(clip.Path, CancellationToken.None);

        await PlayToTheEndAsync(player);

        var picture = new byte[player.FrameSize.ByteCount];
        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Ended);
        await Assert.That(player.Snapshot.FrameVersion).IsGreaterThan(FewestNormalPictures);
        await Assert.That(player.TryCopyFrame(picture)).IsTrue();
        await Assert.That(Array.Exists(picture, static value => value != 0)).IsTrue();
        await Assert.That(player.Snapshot.Position).IsEqualTo(player.Snapshot.Duration);
    }

    /// <summary>Verifies pausing keeps the position, and stopping goes back to the start.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PauseKeepsThePositionAndStopRewinds()
    {
        using var clip = await ClipFolder.CreateAsync(false);
        using var player = new FfmpegMediaPlayer(clip.Tools, false);
        _ = await player.OpenAsync(clip.Path, CancellationToken.None);
        using var moved = new SemaphoreSlim(0);
        using var subscription = player.Changes.SubscribeSafe(
            snapshot =>
            {
                if (snapshot.State == MediaPlayerState.Playing && snapshot.Position > TimeSpan.FromMilliseconds(PauseAfter))
                {
                    _ = moved.Release();
                }
            },
            static _ => { });

        player.Play();
        await Assert.That(await moved.WaitAsync(Timeout)).IsTrue();
        player.Pause();
        var paused = player.Snapshot.Position;
        await Task.Delay(TimeSpan.FromMilliseconds(QuietTime));

        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Paused);
        await Assert.That(player.Snapshot.Position).IsEqualTo(paused);
        await Assert.That(paused).IsGreaterThan(TimeSpan.FromMilliseconds(LeastPaused));
        player.Stop();
        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Ready);
        await Assert.That(player.Snapshot.Position).IsEqualTo(TimeSpan.Zero);
    }

    /// <summary>Verifies reduced motion shows far fewer pictures than normal motion.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReducedMotionShowsFewerPictures()
    {
        using var clip = await ClipFolder.CreateAsync(false);
        var normal = await CountPicturesAsync(clip, false);
        var reduced = await CountPicturesAsync(clip, true);

        await Assert.That(reduced).IsLessThan(normal);
        await Assert.That(reduced).IsLessThanOrEqualTo(MostReducedPictures);
    }

    /// <summary>Verifies a file that is not media fails with a plain message and cannot be played.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonMediaFails()
    {
        using var clip = await ClipFolder.CreateAsync(false);
        var text = Path.Combine(clip.Directory, "notes.txt");
        await File.WriteAllTextAsync(text, "this is not a sound or a picture");
        using var player = new FfmpegMediaPlayer(clip.Tools, false);

        await Assert.That(await player.OpenAsync(text, CancellationToken.None)).IsFalse();

        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Failed);
        await Assert.That(player.Problem).IsNotNull();
        player.Play();
        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Failed);
    }

    /// <summary>Plays a file and waits for it to end.</summary>
    /// <param name="player">The player, with a file open.</param>
    /// <returns>A task.</returns>
    private static async Task PlayToTheEndAsync(FfmpegMediaPlayer player)
    {
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = player.Changes.SubscribeSafe(
            snapshot =>
            {
                if (snapshot.State == MediaPlayerState.Ended)
                {
                    _ = ended.TrySetResult();
                }
            },
            static _ => { });
        player.Play();
        await ended.Task.WaitAsync(Timeout);
    }

    /// <summary>Plays a clip to its end and counts the pictures shown.</summary>
    /// <param name="clip">The clip.</param>
    /// <param name="reduceMotion">Whether the player was asked for less motion.</param>
    /// <returns>The number of pictures.</returns>
    private static async Task<int> CountPicturesAsync(ClipFolder clip, bool reduceMotion)
    {
        using var player = new FfmpegMediaPlayer(clip.Tools, reduceMotion);
        _ = await player.OpenAsync(clip.Path, CancellationToken.None);
        await PlayToTheEndAsync(player);
        return player.Snapshot.FrameVersion;
    }

    /// <summary>A folder holding a short clip made with FFmpeg.</summary>
    private sealed class ClipFolder : IDisposable
    {
        /// <summary>The caption file used for the captioned clip.</summary>
        private const string Captions = "1\n00:00:00,000 --> 00:00:01,000\nHello\n\n2\n00:00:01,000 --> 00:00:03,000\nWorld\n";

        /// <summary>Initializes a new instance of the <see cref="ClipFolder"/> class.</summary>
        /// <param name="directory">The folder.</param>
        /// <param name="tools">The FFmpeg programs.</param>
        private ClipFolder(string directory, MediaTools tools)
        {
            Directory = directory;
            Tools = tools;
            Path = System.IO.Path.Combine(directory, "clip.mp4");
        }

        /// <summary>Gets the folder.</summary>
        internal string Directory { get; }

        /// <summary>Gets the FFmpeg programs.</summary>
        internal MediaTools Tools { get; }

        /// <summary>Gets the full path of the clip.</summary>
        internal string Path { get; }

        /// <inheritdoc />
        public void Dispose() => System.IO.Directory.Delete(Directory, true);

        /// <summary>Makes a clip of three seconds, 160 by 120 pixels, ten pictures a second, optionally with captions.</summary>
        /// <param name="captions">Whether to add a caption stream.</param>
        /// <returns>The folder; the test is skipped when FFmpeg is not installed.</returns>
        internal static async Task<ClipFolder> CreateAsync(bool captions)
        {
            var tools = MediaToolLocator.Find();
            if (tools is null)
            {
                Skip.Test("FFmpeg is not installed on this computer.");
            }

            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"clip-{Guid.NewGuid():N}");
            _ = System.IO.Directory.CreateDirectory(directory);
            var clip = new ClipFolder(directory, tools!);
            var plain = captions ? System.IO.Path.Combine(directory, "plain.mp4") : clip.Path;
            await RunAsync(tools!.Ffmpeg, "-v", "error", "-f", "lavfi", "-i", "testsrc=duration=3:size=160x120:rate=10", "-c:v", "mpeg4", "-y", plain);
            if (captions)
            {
                var srt = System.IO.Path.Combine(directory, "captions.srt");
                await File.WriteAllTextAsync(srt, Captions);
                await RunAsync(tools.Ffmpeg, "-v", "error", "-i", plain, "-i", srt, "-map", "0", "-map", "1", "-c", "copy", "-c:s", "mov_text", "-y", clip.Path);
            }

            return clip;
        }

        /// <summary>Runs a program to its end.</summary>
        /// <param name="program">The program.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>A task.</returns>
        private static async Task RunAsync(string program, params string[] arguments)
        {
            var start = new ProcessStartInfo(program) { UseShellExecute = false, RedirectStandardError = true };
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start)!;
            _ = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
        }
    }
}
