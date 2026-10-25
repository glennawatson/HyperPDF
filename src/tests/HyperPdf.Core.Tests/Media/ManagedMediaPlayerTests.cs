// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdf.Core.Media;
using ReactiveUI.Primitives;

namespace HyperPdf.Core.Tests.Media;

/// <summary>Tests for <see cref="ManagedMediaPlayer"/>, which plays sound and video with decoders written in C#.</summary>
public sealed class ManagedMediaPlayerTests
{
    /// <summary>The samples a second of the test sound.</summary>
    private const int Rate = 44_100;

    /// <summary>The length of the test sound, in seconds.</summary>
    private const double Seconds = 1;

    /// <summary>The length of the AAC test sound, in seconds.</summary>
    private const double AacSeconds = 1.5;

    /// <summary>How far from the length the sound written may be, in seconds.</summary>
    private const double Tolerance = 0.1;

    /// <summary>The bytes in a sample.</summary>
    private const int SampleBytes = 2;

    /// <summary>The frequency of the test tone.</summary>
    private const double Tone = 440;

    /// <summary>The bytes of a made-up file that no decoder reads.</summary>
    private const int JunkBytes = 2_000;

    /// <summary>The bytes of a stereo frame of 16 bit sound.</summary>
    private const int FrameBytes = 4;

    /// <summary>The number of channels of the test sound.</summary>
    private const int Stereo = 2;

    /// <summary>The format code of whole number sound.</summary>
    private const int WholeNumberFormat = 1;

    /// <summary>A full turn in radians.</summary>
    private const double TwoPi = Math.Tau;

    /// <summary>The bits in a sample.</summary>
    private const int SampleBits = 16;

    /// <summary>The loudness of the test tone.</summary>
    private const double Loudness = 0.3;

    /// <summary>The width of the pictures of the video test file.</summary>
    private const int PictureWidth = 160;

    /// <summary>The height of the pictures of the video test file.</summary>
    private const int PictureHeight = 120;

    /// <summary>The pictures in the video test file.</summary>
    private const int PictureCount = 30;

    /// <summary>The fewest of those pictures a run must show when none is far behind the sound.</summary>
    private const int FewestShown = 24;

    /// <summary>The fewest pictures of the H.265 test file a run must show; a slow runner drops the first ones as late.</summary>
    private const int FewestHevcShown = 1;

    /// <summary>The most pictures a run at reduced motion shows in the two second clip.</summary>
    private const int MostReduced = 3;

    /// <summary>The most pictures a run that starts at half way shows.</summary>
    private const int MostAfterSeek = 20;

    /// <summary>The bytes in a pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The opaque value of the alpha byte.</summary>
    private const byte Opaque = 255;

    /// <summary>The step in seconds between the positions where captions are looked for.</summary>
    private const double CaptionStep = 0.25;

    /// <summary>The length of the video test file, in seconds.</summary>
    private const double VideoSeconds = 2;

    /// <summary>The position the seek test starts from, in seconds.</summary>
    private const double SeekTo = 1;

    /// <summary>The test file with H.264 pictures, AAC sound and text.</summary>
    private const string VideoFile = "av-h264-aac-text.mp4";

    /// <summary>How long to wait for the player.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>Verifies opening reads the sound and plays nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpeningReadsTheFileAndPlaysNothing()
    {
        using var folder = new TemporaryFolder();
        var path = folder.WriteFile("tone.wav", MakeWave());
        var sound = new SimulatedSound();
        using var player = new ManagedMediaPlayer(() => sound, false);

        await Assert.That(await player.OpenAsync(path, CancellationToken.None)).IsTrue();

        await Assert.That(player.Info.HasAudio).IsTrue();
        await Assert.That(player.Info.HasVideo).IsFalse();
        await Assert.That(player.Snapshot.Duration.TotalSeconds).IsEqualTo(Seconds).Within(Tolerance);
        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Ready);
        await Assert.That(sound.WrittenSeconds).IsEqualTo(0D);
    }

    /// <summary>Verifies a file plays to its end and all its sound reaches the output at the output's rate.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlaysToTheEnd()
    {
        using var folder = new TemporaryFolder();
        var path = folder.WriteFile("tone.wav", MakeWave());
        var sound = new SimulatedSound();
        using var player = new ManagedMediaPlayer(() => sound, false);
        _ = await player.OpenAsync(path, CancellationToken.None);

        await PlayToTheEndAsync(player);

        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Ended);
        await Assert.That(sound.WrittenSeconds).IsEqualTo(Seconds).Within(Tolerance);
    }

    /// <summary>Verifies AAC sound in an MP4 file plays to its end.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AacInMp4PlaysToTheEnd()
    {
        var sound = new SimulatedSound();
        using var player = new ManagedMediaPlayer(() => sound, false);
        _ = await player.OpenAsync(Fixture("aac-lc-stereo-44100.m4a"), CancellationToken.None);

        await PlayToTheEndAsync(player);

        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Ended);
        await Assert.That(sound.WrittenSeconds).IsEqualTo(AacSeconds).Within(Tolerance);
    }

    /// <summary>Verifies a file no decoder reads is refused with a reason.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownFormatIsRefused()
    {
        using var folder = new TemporaryFolder();
        var path = folder.WriteFile("noise.bin", new byte[JunkBytes]);
        using var player = new ManagedMediaPlayer(static () => new SimulatedSound(), false);

        var opened = await player.OpenAsync(path, CancellationToken.None);

        await Assert.That(opened).IsFalse();
        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Failed);
        await Assert.That(player.Problem).IsNotNull();
    }

    /// <summary>Verifies a file with pictures, sound and captions opens with all three.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VideoFile_OpensWithPicturesSoundAndCaptions()
    {
        using var player = new ManagedMediaPlayer(static () => new SimulatedSound(), false);

        await Assert.That(await player.OpenAsync(Fixture(VideoFile), CancellationToken.None)).IsTrue();

        await Assert.That(player.Info.HasVideo).IsTrue();
        await Assert.That(player.Info.HasAudio).IsTrue();
        await Assert.That(player.Info.HasCaptions).IsTrue();
        FrameSize expected = new(PictureWidth, PictureHeight);
        await Assert.That(player.FrameSize).IsEqualTo(expected);
        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Ready);
    }

    /// <summary>Verifies the captions of a file are shown at some position of it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VideoFile_ShowsACaptionAtSomePosition()
    {
        using var player = new ManagedMediaPlayer(static () => new SimulatedSound(), false);
        _ = await player.OpenAsync(Fixture(VideoFile), CancellationToken.None);
        var found = false;

        for (var at = 0.0; at < VideoSeconds; at += CaptionStep)
        {
            player.Seek(TimeSpan.FromSeconds(at));
            found |= player.Snapshot.Caption.Length > 0;
        }

        await Assert.That(found).IsTrue();
    }

    /// <summary>Verifies a file with pictures and sound plays to its end, showing nearly every picture, with the sound all written and the last picture a real one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VideoFile_PlaysPicturesAndSoundToTheEnd()
    {
        var sound = new SimulatedSound();
        using var player = new ManagedMediaPlayer(() => sound, false);
        _ = await player.OpenAsync(Fixture(VideoFile), CancellationToken.None);

        await PlayToTheEndAsync(player);

        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Ended);
        await Assert.That(player.Snapshot.FrameVersion).IsGreaterThanOrEqualTo(FewestShown).And.IsLessThanOrEqualTo(PictureCount);
        await Assert.That(sound.WrittenSeconds).IsEqualTo(AacSeconds).Within(Tolerance);
        var picture = new byte[player.FrameSize.ByteCount];
        await Assert.That(player.TryCopyFrame(picture)).IsTrue();
        await Assert.That(IsOpaqueAndNotBlack(picture)).IsTrue();
    }

    /// <summary>Verifies H.265 pictures with no sound play to their end by the wall clock.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task H265File_PlaysToTheEnd()
    {
        using var player = new ManagedMediaPlayer(static () => new SimulatedSound(), false);
        _ = await player.OpenAsync(Fixture("h265-b.mp4"), CancellationToken.None);

        await PlayToTheEndAsync(player);

        await Assert.That(player.Info.HasVideo).IsTrue();
        await Assert.That(player.Info.HasAudio).IsFalse();
        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Ended);
        await Assert.That(player.Snapshot.FrameVersion).IsGreaterThanOrEqualTo(FewestHevcShown);
        var picture = new byte[player.FrameSize.ByteCount];
        await Assert.That(player.TryCopyFrame(picture)).IsTrue();
        await Assert.That(IsOpaqueAndNotBlack(picture)).IsTrue();
    }

    /// <summary>Verifies a run that starts half way through shows only the pictures from there.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VideoFile_StartsFromTheSoughtPosition()
    {
        var sound = new SimulatedSound();
        using var player = new ManagedMediaPlayer(() => sound, false);
        _ = await player.OpenAsync(Fixture(VideoFile), CancellationToken.None);
        player.Seek(TimeSpan.FromSeconds(SeekTo));

        await PlayToTheEndAsync(player);

        await Assert.That(player.Snapshot.State).IsEqualTo(MediaPlayerState.Ended);
        await Assert.That(player.Snapshot.FrameVersion).IsGreaterThan(0).And.IsLessThanOrEqualTo(MostAfterSeek);
    }

    /// <summary>Verifies reduced motion shows about one picture a second.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReducedMotion_ShowsFewPictures()
    {
        using var player = new ManagedMediaPlayer(static () => new SimulatedSound(), true);
        _ = await player.OpenAsync(Fixture(VideoFile), CancellationToken.None);

        await PlayToTheEndAsync(player);

        await Assert.That(player.Snapshot.FrameVersion).IsGreaterThan(0).And.IsLessThanOrEqualTo(MostReduced);
    }

    /// <summary>Finds a test file beside the tests.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The full path.</returns>
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    /// <summary>Tells whether a picture has an opaque alpha byte in every pixel and some colour.</summary>
    /// <param name="picture">The picture as blue, green, red and alpha bytes.</param>
    /// <returns><see langword="true"/> when the picture is a real one.</returns>
    private static bool IsOpaqueAndNotBlack(byte[] picture)
    {
        var colour = false;
        for (var i = 0; i < picture.Length; i += PixelBytes)
        {
            if (picture[i + PixelBytes - 1] != Opaque)
            {
                return false;
            }

            colour |= picture.AsSpan(i, PixelBytes - 1).ContainsAnyExcept((byte)0);
        }

        return colour;
    }

    /// <summary>Makes a one second 16 bit stereo WAVE file holding a tone.</summary>
    /// <returns>The file's bytes.</returns>
    private static byte[] MakeWave()
    {
        const int Frames = (int)(Rate * Seconds);
        var data = new byte[Frames * FrameBytes];
        for (var frame = 0; frame < Frames; frame++)
        {
            var value = (short)(Math.Sin(TwoPi * Tone * frame / Rate) * Loudness * short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(frame * FrameBytes), value);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan((frame * FrameBytes) + SampleBytes), value);
        }

        return WaveFile.Build(WholeNumberFormat, Stereo, Rate, SampleBits, data);
    }

    /// <summary>Plays a file and waits for it to end.</summary>
    /// <param name="player">The player, with a file open.</param>
    /// <returns>A task.</returns>
    private static async Task PlayToTheEndAsync(ManagedMediaPlayer player)
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
}
