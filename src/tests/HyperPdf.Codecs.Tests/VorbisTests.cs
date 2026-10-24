// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Tests;

/// <summary>
/// Checks Ogg Vorbis reading and decoding against FFmpeg's decode of the same files, kept next to each file as 16 bit FLAC. FFmpeg
/// ends the sound a block earlier or later than the stream's last granule position says, so the sound is compared up to the shorter
/// of the two and the length is checked against the stream's own end.
/// </summary>
public sealed class VorbisTests
{
    /// <summary>The largest difference allowed between a decoded sample and the reference: two steps of 16 bit sound.</summary>
    private const float Tolerance = 2F / 32_768F;

    /// <summary>The largest value a 16 bit sample holds, as a fraction of full scale.</summary>
    private const float LargestSixteenBit = 32_767F / 32_768F;

    /// <summary>The seconds into the file the seek test goes to.</summary>
    private const double SeekSeconds = 0.25;

    /// <summary>The samples at the end of the sound the seek test compares.</summary>
    private const int TailSamples = 2_000;

    /// <summary>The most FFmpeg's length may differ from the stream's, in samples.</summary>
    private const int LengthSlack = 512;

    /// <summary>The most channels a test file has, which scales the slack in a count of interleaved samples.</summary>
    private const int StereoChannels = 2;

    /// <summary>The seed of the random bytes, so a failure can be repeated.</summary>
    private const int Seed = 20_261_027;

    /// <summary>The bytes of random data the not-an-Ogg-file test uses.</summary>
    private const int RandomLength = 5_000;

    /// <summary>Checks that the decoded sound agrees with the reference to within the tolerance.</summary>
    /// <param name="name">The file name without its extension.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("vorbis-stereo-44100")]
    [Arguments("vorbis-mono-22050")]
    [Arguments("vorbis-stereo-48000-low")]
    public async Task DecodedSound_AgreesWithTheReference(string name)
    {
        var decoded = Decode($"{name}.ogg");
        var reference = Decode($"{name}.reference.flac");

        await Assert.That(Math.Abs(decoded.Count - reference.Count)).IsLessThanOrEqualTo(LengthSlack * StereoChannels);
        await Assert.That(MaxDifference(decoded.Samples, reference.Samples)).IsLessThanOrEqualTo(Tolerance);
    }

    /// <summary>Checks that the sound is as long as the container says.</summary>
    /// <param name="name">The file name without its extension.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("vorbis-stereo-44100")]
    [Arguments("vorbis-mono-22050")]
    public async Task Length_MatchesTheDuration(string name)
    {
        using var container = Open($"{name}.ogg");
        var track = container.Tracks[0];
        var decoded = Decode($"{name}.ogg");

        var seconds = (double)decoded.Count / track.Channels / track.SampleRate;
        await Assert.That(container.Duration.TotalSeconds).IsEqualTo(seconds).Within(1D / track.SampleRate);
    }

    /// <summary>Checks that decoding from a seek reaches the same sound as decoding straight through.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Seek_ReachesTheSameSound()
    {
        const string name = "vorbis-stereo-44100.ogg";
        var whole = Decode(name);

        using var container = Open(name);
        using var decoder = AudioDecoders.Create(container.Tracks[0])!;
        await Assert.That(container.Seek(TimeSpan.FromSeconds(SeekSeconds))).IsTrue();
        var pcm = new PcmBuffer();
        var packet = new MediaPacket();
        while (container.ReadPacket(packet))
        {
            decoder.Decode(packet, pcm);
        }

        var tail = TailSamples * container.Tracks[0].Channels;
        await Assert.That(pcm.Samples[^tail..].SequenceEqual(whole.Samples[^tail..])).IsTrue();
    }

    /// <summary>Checks that bytes that are not an Ogg file are not opened as one.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NotOgg_IsNotOpened()
    {
        await using var stream = new MemoryStream(RandomData.Bytes(Seed, RandomLength));
        await Assert.That(MediaContainers.Open(stream)).IsNull();
    }

    /// <summary>Gets the path of a test file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The full path.</returns>
    private static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    /// <summary>Opens a test file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The container.</returns>
    /// <exception cref="InvalidDataException">The file did not open.</exception>
    private static MediaContainer Open(string name) =>
        MediaContainers.Open(PathOf(name)) ?? throw new InvalidDataException($"{name} did not open.");

    /// <summary>Decodes a whole file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>All the sound.</returns>
    private static PcmBuffer Decode(string name)
    {
        using var container = Open(name);
        using var decoder = AudioDecoders.Create(container.Tracks[0])!;
        var packet = new MediaPacket();
        var pcm = new PcmBuffer();
        while (container.ReadPacket(packet))
        {
            decoder.Decode(packet, pcm);
        }

        return pcm;
    }

    /// <summary>Finds the largest difference between a decoded sound and a 16 bit reference, over the length of the shorter.</summary>
    /// <param name="decoded">The decoded sound.</param>
    /// <param name="reference">The reference.</param>
    /// <returns>The largest difference once the decoded sound is cut to what 16 bits hold.</returns>
    private static float MaxDifference(ReadOnlySpan<float> decoded, ReadOnlySpan<float> reference)
    {
        var largest = 0F;
        for (var index = 0; index < decoded.Length && index < reference.Length; index++)
        {
            var clipped = Math.Clamp(decoded[index], -1F, LargestSixteenBit);
            largest = Math.Max(largest, Math.Abs(clipped - reference[index]));
        }

        return largest;
    }
}
