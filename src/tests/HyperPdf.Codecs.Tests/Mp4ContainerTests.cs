// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Tests;

/// <summary>Checks that MP4 files open with the right tracks, give their packets in order, and seek to a key packet.</summary>
public sealed class Mp4ContainerTests
{
    /// <summary>The file that holds H.264 pictures, AAC sound and timed text.</summary>
    private const string MixedFile = "av-h264-aac-text.mp4";

    /// <summary>The number of tracks in the mixed file.</summary>
    private const int TrackCount = 3;

    /// <summary>The number of the picture track in the mixed file.</summary>
    private const int VideoTrack = 0;

    /// <summary>The number of the sound track in the mixed file.</summary>
    private const int SoundTrack = 1;

    /// <summary>The number of the text track in the mixed file.</summary>
    private const int TextTrack = 2;

    /// <summary>The pictures in the mixed file.</summary>
    private const int VideoPackets = 30;

    /// <summary>The frames of sound in the mixed file.</summary>
    private const int SoundPackets = 66;

    /// <summary>The cues in the mixed file.</summary>
    private const int TextPackets = 3;

    /// <summary>The width of the pictures in the mixed file.</summary>
    private const int PictureWidth = 160;

    /// <summary>The height of the pictures in the mixed file.</summary>
    private const int PictureHeight = 120;

    /// <summary>The seconds the mixed file lasts at least.</summary>
    private const double MinimumDuration = 1.5;

    /// <summary>The least ratio of sound to error, in decibels, a decode may have against the reference.</summary>
    private const double MinimumSignalToError = 80D;

    /// <summary>The ratio of an amplitude to a decibel, for turning an energy ratio into decibels.</summary>
    private const double DecibelsPerDecade = 10D;

    /// <summary>The samples the decoded length may differ from the reference by.</summary>
    private const int EndSlack = 2_048;

    /// <summary>The seconds to seek to in the seek test.</summary>
    private const double SeekTarget = 1.0;

    /// <summary>The most a key picture may be before the seek target, in seconds: the gap between key pictures and a little more.</summary>
    private const double SeekReach = 1.1;

    /// <summary>Checks the tracks of the mixed file.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MixedFile_HasTheThreeTracks()
    {
        using var container = Open(MixedFile);

        await Assert.That(container.Tracks.Count).IsEqualTo(TrackCount);
        await Assert.That(container.Tracks[VideoTrack].Kind).IsEqualTo(MediaTrackKind.Video);
        await Assert.That(container.Tracks[VideoTrack].Codec).IsEqualTo(MediaCodec.H264);
        await Assert.That(container.Tracks[VideoTrack].Width).IsEqualTo(PictureWidth);
        await Assert.That(container.Tracks[VideoTrack].Height).IsEqualTo(PictureHeight);
        await Assert.That(container.Tracks[VideoTrack].Configuration.Length).IsGreaterThan(0);
        await Assert.That(container.Tracks[SoundTrack].Codec).IsEqualTo(MediaCodec.Aac);
        await Assert.That(container.Tracks[TextTrack].Kind).IsEqualTo(MediaTrackKind.Caption);
        await Assert.That(container.Tracks[TextTrack].Codec).IsEqualTo(MediaCodec.Text);
        await Assert.That(container.Duration.TotalSeconds).IsGreaterThan(MinimumDuration);
    }

    /// <summary>Checks that every packet of the mixed file is given once, with times that do not go backwards in the sound track.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MixedFile_GivesEveryPacketOnce()
    {
        using var container = Open(MixedFile);
        var counts = new int[container.Tracks.Count];
        var lastSound = -1D;
        var packet = new MediaPacket();
        while (container.ReadPacket(packet))
        {
            counts[packet.Track]++;
            if (packet.Track != SoundTrack)
            {
                continue;
            }

            await Assert.That(packet.Time).IsGreaterThanOrEqualTo(lastSound);
            lastSound = packet.Time;
        }

        await Assert.That(counts[VideoTrack]).IsEqualTo(VideoPackets);
        await Assert.That(counts[SoundTrack]).IsEqualTo(SoundPackets);
        await Assert.That(counts[TextTrack]).IsEqualTo(TextPackets);
    }

    /// <summary>Checks that the text packets hold the cue text, without the length that precedes it in the file.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MixedFile_TextPacketsHoldTheCueText()
    {
        using var container = Open(MixedFile);
        var packet = new MediaPacket();
        var text = new List<string>();
        while (container.ReadPacket(packet))
        {
            if (packet.Track == TextTrack && packet.Length > 0)
            {
                text.Add(System.Text.Encoding.UTF8.GetString(packet.Data));
            }
        }

        await Assert.That(text.Count).IsGreaterThan(0);
        await Assert.That(text.TrueForAll(static cue => cue[0] >= ' ')).IsTrue();
    }

    /// <summary>Checks that a seek lands on a key picture at or before the target, and reading carries on from there.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MixedFile_SeekLandsOnAKeyPicture()
    {
        using var container = Open(MixedFile);
        await Assert.That(container.Seek(TimeSpan.FromSeconds(SeekTarget))).IsTrue();
        var packet = new MediaPacket();
        var found = false;
        while (!found && container.ReadPacket(packet))
        {
            found = packet.Track == VideoTrack;
        }

        await Assert.That(found).IsTrue();
        await Assert.That(packet.IsKey).IsTrue();
        await Assert.That(packet.Time).IsLessThanOrEqualTo(SeekTarget);
        await Assert.That(packet.Time).IsGreaterThan(SeekTarget - SeekReach);
    }

    /// <summary>Checks that sound in an MP4 file decodes to the reference sound.</summary>
    /// <param name="name">The MP4 file.</param>
    /// <param name="reference">The reference file.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("flac-stereo-44100.mp4", "flac-stereo-44100.mp4.reference.flac")]
    [Arguments("mp3-stereo-44100.mp4", "mp3-stereo-44100.mp4.reference.flac")]
    [Arguments("opus-stereo-music.mp4", "opus-stereo-music.reference.flac")]
    public async Task Sound_DecodesToTheReference(string name, string reference)
    {
        var decoded = Decode(name);
        var expected = Decode(reference);

        await Assert.That(Math.Abs(decoded.Count - expected.Count)).IsLessThanOrEqualTo(EndSlack);
        await Assert.That(SignalToError(decoded.Samples, expected.Samples)).IsGreaterThanOrEqualTo(MinimumSignalToError);
    }

    /// <summary>Checks that files that are not MP4 are left alone.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Garbage_DoesNotOpen()
    {
        await using var stream = new MemoryStream(new byte[256]);

        await Assert.That(MediaContainers.Open(stream)).IsNull();
        await Assert.That(stream.Position).IsEqualTo(0);
    }

    /// <summary>Opens a test file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The container.</returns>
    /// <exception cref="InvalidDataException">The file did not open.</exception>
    private static MediaContainer Open(string name) =>
        MediaContainers.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)) ?? throw new InvalidDataException($"{name} did not open.");

    /// <summary>Decodes the first sound track of a file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>All the sound.</returns>
    private static PcmBuffer Decode(string name)
    {
        using var container = Open(name);
        var index = container.FindTrack(MediaTrackKind.Audio);
        using var decoder = AudioDecoders.Create(container.Tracks[index])!;
        var packet = new MediaPacket();
        var pcm = new PcmBuffer();
        while (container.ReadPacket(packet))
        {
            if (packet.Track == index)
            {
                decoder.Decode(packet, pcm);
            }
        }

        return pcm;
    }

    /// <summary>Works out how far above the error the reference sound is, over the samples both sounds have.</summary>
    /// <param name="decoded">The decoded sound.</param>
    /// <param name="reference">The reference.</param>
    /// <returns>The ratio of the sound's energy to the error's energy, in decibels.</returns>
    private static double SignalToError(ReadOnlySpan<float> decoded, ReadOnlySpan<float> reference)
    {
        var count = Math.Min(decoded.Length, reference.Length);
        double signal = 0;
        double error = 0;
        for (var index = 0; index < count; index++)
        {
            var difference = decoded[index] - reference[index];
            signal += reference[index] * reference[index];
            error += difference * difference;
        }

        return error == 0 ? double.PositiveInfinity : DecibelsPerDecade * Math.Log10(signal / error);
    }
}
