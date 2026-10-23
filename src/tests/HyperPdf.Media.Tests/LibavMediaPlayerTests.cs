// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Media;
using ReactiveUI.Primitives;

namespace HyperPdf.Media.Tests;

/// <summary>
/// Tests for <see cref="LibavMediaPlayer"/> against the FFmpeg libraries. They skip with a reason when the libraries or the
/// <c>ffmpeg</c> program that makes the test clips are missing, and no media file is stored with the tests.
/// </summary>
public sealed class LibavMediaPlayerTests
{
    /// <summary>The file name of the clip with sound and pictures.</summary>
    private const string FullClip = "full.mp4";

    /// <summary>The width of the clips, in pixels.</summary>
    private const int ClipWidth = 160;

    /// <summary>The height of the clips, in pixels.</summary>
    private const int ClipHeight = 120;

    /// <summary>The length of the clips, in seconds.</summary>
    private const double ClipSeconds = 3;

    /// <summary>How far from the length the sound written may be, in seconds.</summary>
    private const double Tolerance = 0.15;

    /// <summary>The fewest pictures a three second clip at ten pictures a second shows when none is dropped.</summary>
    private const int FewestPictures = 24;

    /// <summary>Where the seek test goes, in seconds.</summary>
    private const double SeekTarget = 2;

    /// <summary>The most pictures the seek test may show after going two seconds in.</summary>
    private const int MostAfterSeek = 14;

    /// <summary>The position after which the pause test pauses, in seconds.</summary>
    private const double PauseAfter = 0.6;

    /// <summary>How long the pause test waits to see that nothing moves, in milliseconds.</summary>
    private const int QuietTime = 400;

    /// <summary>The most pictures at reduced motion for a three second clip.</summary>
    private const int MostReducedPictures = 4;

    /// <summary>The second at which the captions test asks for the second caption.</summary>
    private const double SecondCaption = 1.5;

    /// <summary>How long to wait for the player.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>The size of the pictures of the clips.</summary>
    private static readonly FrameSize ClipSize = new(ClipWidth, ClipHeight);

    /// <summary>Verifies opening reads the streams, size, length and captions, and plays nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpeningReadsTheFileAndPlaysNothing()
    {
        using var clips = ClipFolder.Create();
        var path = await clips.MakeAsync(FullClip, true, true, true);
        var sounds = new CountingFactory();
        using var player = new LibavMediaPlayer(sounds.Create, false);

        await Assert.That(await player.OpenAsync(path, CancellationToken.None)).IsTrue();

        await Assert.That(player.Info.HasVideo).IsTrue();
        await Assert.That(player.Info.HasAudio).IsTrue();
        await Assert.That(player.Info.HasCaptions).IsTrue();
        await Assert.That(player.FrameSize).IsEqualTo(ClipSize);
        await Assert.That(player.Snapshot.Duration.TotalSeconds).IsEqualTo(ClipSeconds).Within(Tolerance);
        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Ready);
        await Assert.That(player.Snapshot.FrameVersion).IsEqualTo(0);
        await Assert.That(sounds.Count).IsEqualTo(0);
    }

    /// <summary>Verifies a clip with sound and pictures plays to its end, with all its sound written and nearly every picture shown.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlaysSoundAndPicturesToTheEnd()
    {
        using var clips = ClipFolder.Create();
        var path = await clips.MakeAsync(FullClip, true, true, false);
        var sound = new SimulatedSound();
        using var player = new LibavMediaPlayer(() => sound, false);
        _ = await player.OpenAsync(path, CancellationToken.None);

        await PlayToTheEndAsync(player);

        var picture = new byte[player.FrameSize.ByteCount];
        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Ended);
        await Assert.That(sound.WrittenSeconds).IsEqualTo(ClipSeconds).Within(Tolerance);
        await Assert.That(player.Snapshot.FrameVersion).IsGreaterThanOrEqualTo(FewestPictures);
        await Assert.That(player.TryCopyFrame(picture)).IsTrue();
        await Assert.That(Array.Exists(picture, static value => value != 0)).IsTrue();
        await Assert.That(player.Snapshot.Position).IsEqualTo(player.Snapshot.Duration);
    }

    /// <summary>Verifies the position follows what is heard: the sound written minus the output's latency, and never goes backward.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PositionFollowsWhatIsHeard()
    {
        using var clips = ClipFolder.Create();
        var path = await clips.MakeAsync(FullClip, true, true, false);
        var sound = new SimulatedSound();
        using var player = new LibavMediaPlayer(() => sound, false);
        _ = await player.OpenAsync(path, CancellationToken.None);
        var previous = TimeSpan.Zero;
        var backward = 0;
        var worst = 0.0;
        using var subscription = player.Changes.SubscribeSafe(
            snapshot =>
            {
                if (snapshot.State != MediaPlayerState.Playing)
                {
                    return;
                }

                backward += snapshot.Position < previous ? 1 : 0;
                previous = snapshot.Position;
                worst = Math.Max(worst, Math.Abs(snapshot.Position.TotalSeconds - (sound.WrittenSeconds - sound.Latency.TotalSeconds)));
            },
            static _ => { });

        await PlayToTheEndAsync(player);

        await Assert.That(backward).IsEqualTo(0);
        await Assert.That(worst).IsLessThan(Tolerance);
    }

    /// <summary>Verifies going to a position and playing starts there: only the rest of the sound is written, and the captions follow.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeekThenPlayStartsAtThePosition()
    {
        using var clips = ClipFolder.Create();
        var path = await clips.MakeAsync(FullClip, true, true, true);
        var sound = new SimulatedSound();
        using var player = new LibavMediaPlayer(() => sound, false);
        _ = await player.OpenAsync(path, CancellationToken.None);

        player.Seek(TimeSpan.FromSeconds(SecondCaption));
        await Assert.That(player.Snapshot.Caption).IsEqualTo("World");
        player.Seek(TimeSpan.FromSeconds(SeekTarget));
        await PlayToTheEndAsync(player);

        await Assert.That(sound.WrittenSeconds).IsEqualTo(ClipSeconds - SeekTarget).Within(Tolerance);
        await Assert.That(player.Snapshot.FrameVersion).IsLessThanOrEqualTo(MostAfterSeek);
        await Assert.That(player.Snapshot.FrameVersion).IsGreaterThan(0);
    }

    /// <summary>Verifies pausing keeps the position, playing again carries on, and stopping goes back to the start.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PauseKeepsThePositionAndPlayCarriesOn()
    {
        using var clips = ClipFolder.Create();
        var path = await clips.MakeAsync(FullClip, true, true, false);
        var sound = new SimulatedSound();
        using var player = new LibavMediaPlayer(() => sound, false);
        _ = await player.OpenAsync(path, CancellationToken.None);
        using var moved = new SemaphoreSlim(0);
        using var subscription = player.Changes.SubscribeSafe(
            snapshot =>
            {
                if (snapshot.State == MediaPlayerState.Playing && snapshot.Position.TotalSeconds > PauseAfter)
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
        await PlayToTheEndAsync(player);
        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Ended);
        player.Stop();
        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Ready);
        await Assert.That(player.Snapshot.Position).IsEqualTo(TimeSpan.Zero);
    }

    /// <summary>Verifies a file with sound only plays to its end and has no picture.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SoundOnlyFilePlays()
    {
        using var clips = ClipFolder.Create();
        var path = await clips.MakeAsync("tone.m4a", true, false, false);
        var sound = new SimulatedSound();
        using var player = new LibavMediaPlayer(() => sound, false);

        await Assert.That(await player.OpenAsync(path, CancellationToken.None)).IsTrue();
        await Assert.That(player.Info.HasVideo).IsFalse();
        await PlayToTheEndAsync(player);

        await Assert.That(sound.WrittenSeconds).IsEqualTo(ClipSeconds).Within(Tolerance);
        await Assert.That(player.FrameSize.IsEmpty).IsTrue();
    }

    /// <summary>Verifies a file with pictures only plays by the wall clock, and so does a file whose sound output cannot start.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PicturesPlayWithoutSound()
    {
        using var clips = ClipFolder.Create();
        var silent = await clips.MakeAsync("silent.mp4", false, true, false);
        var full = await clips.MakeAsync(FullClip, true, true, false);
        using var first = new LibavMediaPlayer(static () => new SimulatedSound(), false);
        using var second = new LibavMediaPlayer(static () => new UnavailableSound(), false);
        _ = await first.OpenAsync(silent, CancellationToken.None);
        _ = await second.OpenAsync(full, CancellationToken.None);

        await PlayToTheEndAsync(first);
        await PlayToTheEndAsync(second);

        await Assert.That(first.Snapshot.FrameVersion).IsGreaterThanOrEqualTo(FewestPictures);
        await Assert.That(second.Snapshot.FrameVersion).IsGreaterThanOrEqualTo(FewestPictures);
    }

    /// <summary>Verifies reduced motion shows far fewer pictures.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReducedMotionShowsFewerPictures()
    {
        using var clips = ClipFolder.Create();
        var path = await clips.MakeAsync(FullClip, true, true, false);
        using var player = new LibavMediaPlayer(static () => new SimulatedSound(), true);
        _ = await player.OpenAsync(path, CancellationToken.None);

        await PlayToTheEndAsync(player);

        await Assert.That(player.Snapshot.FrameVersion).IsGreaterThan(0);
        await Assert.That(player.Snapshot.FrameVersion).IsLessThanOrEqualTo(MostReducedPictures);
    }

    /// <summary>Verifies a file that is not media fails with a plain message.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonMediaFails()
    {
        using var clips = ClipFolder.Create();
        var text = Path.Combine(clips.Directory, "notes.txt");
        await File.WriteAllTextAsync(text, "this is not a sound or a picture");
        using var player = new LibavMediaPlayer(static () => new SimulatedSound(), false);

        await Assert.That(await player.OpenAsync(text, CancellationToken.None)).IsFalse();

        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Failed);
        await Assert.That(player.Problem).IsNotNull();
        player.Play();
        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Failed);
    }

    /// <summary>Plays a file and waits for it to end.</summary>
    /// <param name="player">The player, with a file open.</param>
    /// <returns>A task.</returns>
    private static async Task PlayToTheEndAsync(LibavMediaPlayer player)
    {
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = player.Changes.SubscribeSafe(
            snapshot =>
            {
                if (snapshot.State is MediaPlayerState.Ended or MediaPlayerState.Failed)
                {
                    _ = ended.TrySetResult();
                }
            },
            static _ => { });
        player.Play();
        await ended.Task.WaitAsync(Timeout);
    }

    /// <summary>Makes sound outputs and counts them.</summary>
    private sealed class CountingFactory
    {
        /// <summary>Gets how many outputs were made.</summary>
        internal int Count { get; private set; }

        /// <summary>Makes an output.</summary>
        /// <returns>The output.</returns>
        internal SimulatedSound Create()
        {
            Count++;
            return new();
        }
    }

    /// <summary>A sound output that cannot start, as on a computer with no sound.</summary>
    private sealed class UnavailableSound : IStreamingAudioOutput
    {
        /// <inheritdoc />
        public bool IsAvailable => false;

        /// <inheritdoc />
        public TimeSpan Latency => TimeSpan.Zero;

        /// <inheritdoc />
        public bool Start(int sampleRate, int channels) => false;

        /// <inheritdoc />
        public bool Write(ReadOnlySpan<float> samples, CancellationToken cancellationToken) => false;

        /// <inheritdoc />
        public void Flush()
        {
        }

        /// <inheritdoc />
        public void Close()
        {
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
