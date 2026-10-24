// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Matroska;

namespace HyperPdf.Codecs.Tests;

/// <summary>Checks that Matroska and WebM files open with the right tracks, play their sound, seek, and that laced blocks are split.</summary>
public sealed class MatroskaContainerTests
{
    /// <summary>The file that holds H.264 pictures, AAC sound and subtitles.</summary>
    private const string MixedFile = "av-h264-aac-text.mkv";

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

    /// <summary>The width of the pictures in the mixed file.</summary>
    private const int PictureWidth = 160;

    /// <summary>The seconds the mixed file lasts at least.</summary>
    private const double MinimumDuration = 1.5;

    /// <summary>The least ratio of sound to error, in decibels, a decode may have against the reference.</summary>
    private const double MinimumSignalToError = 80D;

    /// <summary>The ratio of an amplitude to a decibel, for turning an energy ratio into decibels.</summary>
    private const double DecibelsPerDecade = 10D;

    /// <summary>The samples the decoded length may differ from the reference by.</summary>
    private const int EndSlack = 4_096;

    /// <summary>The seconds to seek to in the seek test.</summary>
    private const double SeekTarget = 1.0;

    /// <summary>The seconds in a timestamp unit used by the hand-made blocks.</summary>
    private const double Millisecond = 0.001;

    /// <summary>The number of the track used by the hand-made blocks.</summary>
    private const int BlockTrackNumber = 1;

    /// <summary>The bytes of a block header with a one byte track number: the number, the timestamp and the flags.</summary>
    private const int BlockHeader = 4;

    /// <summary>The sample rate of the hand-made track.</summary>
    private const int TrackRate = 48_000;

    /// <summary>The channels of the hand-made track.</summary>
    private const int TrackChannels = 2;

    /// <summary>The length in seconds of a frame of the hand-made track.</summary>
    private const double FrameSeconds = 0.02;

    /// <summary>The size of each frame of the hand-made blocks.</summary>
    private const int FrameSize = 10;

    /// <summary>The number of frames in the hand-made laced blocks.</summary>
    private const int LacedFrames = 3;

    /// <summary>Checks the tracks of the mixed file.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MixedFile_HasTheThreeTracks()
    {
        using var container = Open(MixedFile);

        await Assert.That(container.Tracks.Count).IsEqualTo(TrackCount);
        await Assert.That(container.Tracks[VideoTrack].Codec).IsEqualTo(MediaCodec.H264);
        await Assert.That(container.Tracks[VideoTrack].Width).IsEqualTo(PictureWidth);
        await Assert.That(container.Tracks[SoundTrack].Codec).IsEqualTo(MediaCodec.Aac);
        await Assert.That(container.Tracks[TextTrack].Codec).IsEqualTo(MediaCodec.Text);
        await Assert.That(container.Duration.TotalSeconds).IsGreaterThan(MinimumDuration);
    }

    /// <summary>Checks that every picture of the mixed file is given and the sound and text come with them.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MixedFile_GivesEveryPicture()
    {
        using var container = Open(MixedFile);
        var counts = new int[container.Tracks.Count];
        var packet = new MediaPacket();
        while (container.ReadPacket(packet))
        {
            counts[packet.Track]++;
        }

        await Assert.That(counts[VideoTrack]).IsEqualTo(VideoPackets);
        await Assert.That(counts[SoundTrack]).IsGreaterThan(0);
        await Assert.That(counts[TextTrack]).IsGreaterThan(0);
    }

    /// <summary>Checks that a seek moves reading to a later place.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MixedFile_SeekMovesForward()
    {
        using var container = Open(MixedFile);
        var packet = new MediaPacket();
        _ = container.ReadPacket(packet);
        var first = packet.Time;

        await Assert.That(container.Seek(TimeSpan.FromSeconds(SeekTarget))).IsTrue();
        _ = container.ReadPacket(packet);

        await Assert.That(packet.Time).IsGreaterThan(first);
    }

    /// <summary>Checks that sound in a Matroska or WebM file decodes to the reference sound.</summary>
    /// <param name="name">The file.</param>
    /// <param name="reference">The reference file.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("vorbis-stereo-44100.webm", "vorbis-stereo-44100.webm.reference.flac")]
    [Arguments("opus-stereo-music.webm", "opus-stereo-music.reference.flac")]
    [Arguments("aac-stereo-44100.mkv", "aac-stereo-44100.mkv.reference.flac")]
    [Arguments("flac-stereo-44100.mkv", "flac-stereo-44100.mkv.reference.flac")]
    [Arguments("mp3-stereo-44100.mkv", "mp3-stereo-44100.mkv.reference.flac")]
    public async Task Sound_DecodesToTheReference(string name, string reference)
    {
        var decoded = Decode(name);
        var expected = Decode(reference);

        await Assert.That(Math.Abs(decoded.Count - expected.Count)).IsLessThanOrEqualTo(EndSlack);
        await Assert.That(SignalToError(decoded.Samples, expected.Samples)).IsGreaterThanOrEqualTo(MinimumSignalToError);
    }

    /// <summary>Checks that a block with no lacing gives one frame that fills the block.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Block_WithoutLacing_GivesOneFrame()
    {
        var block = MakeBlock(0, FrameSize);
        var frames = Split(block, block.Length);

        await Assert.That(frames.Count).IsEqualTo(1);
        await Assert.That(frames[0].Length).IsEqualTo(FrameSize);
        await Assert.That(frames[0].Offset).IsEqualTo(block.Length - FrameSize);
    }

    /// <summary>Checks that Xiph, fixed size and EBML lacing give the frames the block holds.</summary>
    /// <param name="flags">The lacing flag bits.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(0x02)]
    [Arguments(0x04)]
    [Arguments(0x06)]
    public async Task Block_WithLacing_GivesEveryFrame(int flags)
    {
        var block = MakeLacedBlock(flags);
        var frames = Split(block, block.Length);

        await Assert.That(frames.Count).IsEqualTo(LacedFrames);
        await Assert.That(frames.TrueForAll(static frame => frame.Length == FrameSize)).IsTrue();
        await Assert.That(frames[1].Time - frames[0].Time).IsEqualTo(FrameSeconds).Within(Millisecond);
        await Assert.That(frames[^1].Offset + FrameSize).IsEqualTo(block.Length);
    }

    /// <summary>Checks that files that are not Matroska are left alone.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Garbage_DoesNotOpen()
    {
        await using var stream = new MemoryStream(new byte[256]);

        await Assert.That(MediaContainers.Open(stream)).IsNull();
        await Assert.That(stream.Position).IsEqualTo(0);
    }

    /// <summary>Makes a block header and a payload: the track number, a timestamp of zero, the flags, then bytes.</summary>
    /// <param name="flags">The flags byte.</param>
    /// <param name="payload">The bytes after the header.</param>
    /// <returns>The block.</returns>
    private static byte[] MakeBlock(int flags, int payload)
    {
        var block = new byte[BlockHeader + payload];
        block[0] = 0x80 | BlockTrackNumber;
        block[BlockHeader - 1] = (byte)flags;
        return block;
    }

    /// <summary>Makes a laced block of three frames of equal size.</summary>
    /// <param name="flags">The lacing flag bits.</param>
    /// <returns>The block.</returns>
    private static byte[] MakeLacedBlock(int flags)
    {
        var lengths = flags switch
        {
            0x02 => new byte[] { LacedFrames - 1, FrameSize, FrameSize },
            0x06 => new byte[] { LacedFrames - 1, 0x80 | FrameSize, 0xBF },
            _ => new byte[] { LacedFrames - 1 },
        };
        var block = new byte[BlockHeader + lengths.Length + (LacedFrames * FrameSize)];
        block[0] = 0x80 | BlockTrackNumber;
        block[BlockHeader - 1] = (byte)flags;
        lengths.CopyTo(block, BlockHeader);
        return block;
    }

    /// <summary>Splits a hand-made block.</summary>
    /// <param name="block">The block.</param>
    /// <param name="length">The length of the block.</param>
    /// <returns>The frames.</returns>
    private static List<MatroskaFrame> Split(byte[] block, int length)
    {
        var track = new MediaTrack(0, MediaTrackKind.Audio, MediaCodec.Opus, TrackRate, TrackChannels, 0, 0, 0, default);
        var tracks = new List<MatroskaTrack> { new(BlockTrackNumber, track, FrameSeconds, 0) };
        var frames = new List<MatroskaFrame>();
        var timing = new MatroskaBlockTiming(0, Millisecond, 0, true);
        _ = MatroskaBlocks.Split(block, 0, length, in timing, tracks, frames, true);
        return frames;
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
