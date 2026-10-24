// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdf.Codecs.Checksums;

namespace HyperPdf.Codecs.Tests;

/// <summary>
/// Checks FLAC reading and decoding. Each file was made by FFmpeg, and the whole number samples FFmpeg decodes from it were hashed
/// with SHA-256 when the test was written, so a match means this decoder gives the same samples as an independent one.
/// </summary>
public sealed class FlacTests
{
    /// <summary>The seed of the random data, so a failure can be repeated.</summary>
    private const int Seed = 20_261_024;

    /// <summary>The bytes of the random data the check tests use.</summary>
    private const int DataLength = 1_031;

    /// <summary>The bits of a sample FFmpeg writes for sound of 16 bits or fewer.</summary>
    private const int NarrowBits = 16;

    /// <summary>The bits of a sample FFmpeg writes for wider sound.</summary>
    private const int WideBits = 32;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The seconds into the file the seek test goes to.</summary>
    private const double SeekSeconds = 0.5;

    /// <summary>The bit by bit polynomial of the 16 bit check.</summary>
    private const int Polynomial = 0x8005;

    /// <summary>The top bit of the 16 bit register.</summary>
    private const int TopBit = 0x8000;

    /// <summary>The mask of 16 bits.</summary>
    private const int Mask16 = 0xFFFF;

    /// <summary>How far from the end of the file the byte flipped in the damage test is; the start of the file is padding, so the end is sound.</summary>
    private const int DamagedFromEnd = 4_000;

    /// <summary>The names of the test files.</summary>
    private static readonly string[] Files =
    [
        "sine-16-stereo-44100.flac",
        "noise-16-mono-22050.flac",
        "silence-16-mono-8000.flac",
        "sine-24-stereo-48000.flac",
        "fixed-16-stereo-96000.flac",
        "six-16-32000.flac",
    ];

    /// <summary>The test files as test cases.</summary>
    /// <returns>The file names.</returns>
    public static IEnumerable<string> FileNames() => Files;

    /// <summary>Checks the decoded samples of a file against the SHA-256 of what FFmpeg decodes from the same file.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="hash">The SHA-256 of FFmpeg's whole number samples, in hex.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("sine-16-stereo-44100.flac", "4fb8905ef2d427f490c8927ea24b09e9c0c53547ac096f7fc7e6c370fa36d956")]
    [Arguments("noise-16-mono-22050.flac", "fb5d563de61b95e5e0ee9782071e777017551d7f329f7ae6a0902bd7ec54d35e")]
    [Arguments("silence-16-mono-8000.flac", "e61f41d57db208c5f92a35c4ce7198570924a3fc87eeba83441fceee5d6a2865")]
    [Arguments("fixed-16-stereo-96000.flac", "26560bc9d8aceb6c8666824962c1cdd05c8f7e3e81e64a6ea283ed6b7af650c2")]
    [Arguments("six-16-32000.flac", "e0b483069b62b06b249c71dc65a05c773af0d1cf9b0edd06af7ebd2586b783ea")]
    [Arguments("sine-24-stereo-48000.flac", "b717ddd3e90ec1ad4a342cb9ec0709190088ec9d75943709b2b30c126fb65f49")]
    public async Task DecodedSamples_MatchWhatFfmpegDecodes(string name, string hash)
    {
        using var container = Open(name);
        var track = container.Tracks[0];
        using var decoder = AudioDecoders.Create(track)!;
        var pcm = DecodeAll(container, decoder);

        var actual = Convert.ToHexStringLower(SHA256.HashData(ToIntegerBytes(pcm.Samples, track.BitsPerSample)));
        await Assert.That(actual).IsEqualTo(hash);
    }

    /// <summary>Checks the number of samples decoded against the length in the stream information.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodDataSource(nameof(FileNames))]
    public async Task DecodedLength_MatchesTheDuration(string name)
    {
        using var container = Open(name);
        var track = container.Tracks[0];
        using var decoder = AudioDecoders.Create(track)!;
        var pcm = DecodeAll(container, decoder);

        var seconds = (double)pcm.Count / track.Channels / track.SampleRate;
        await Assert.That(seconds).IsEqualTo(container.Duration.TotalSeconds).Within(1D / track.SampleRate);
    }

    /// <summary>Checks that a seek lands at or before the target and that decoding from there gives the same samples as decoding through.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Seek_LandsBeforeTheTargetAndDecodesTheSameSamples()
    {
        const string name = "sine-16-stereo-44100.flac";
        using var whole = Open(name);
        using var wholeDecoder = AudioDecoders.Create(whole.Tracks[0])!;
        var all = DecodeAll(whole, wholeDecoder);

        using var container = Open(name);
        using var decoder = AudioDecoders.Create(container.Tracks[0])!;
        await Assert.That(container.Seek(TimeSpan.FromSeconds(SeekSeconds))).IsTrue();
        var packet = new MediaPacket();
        await Assert.That(container.ReadPacket(packet)).IsTrue();
        await Assert.That(packet.Time).IsLessThanOrEqualTo(SeekSeconds);

        var output = new PcmBuffer();
        decoder.Decode(packet, output);
        var start = (int)Math.Round(packet.Time * decoder.SampleRate) * decoder.Channels;
        await Assert.That(output.Samples.SequenceEqual(all.Samples.Slice(start, output.Count))).IsTrue();
    }

    /// <summary>Checks that the container skips a damaged frame instead of handing it to the decoder.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DamagedFrame_IsSkippedNotDecoded()
    {
        var bytes = await File.ReadAllBytesAsync(PathOf("sine-16-stereo-44100.flac"));
        bytes[^DamagedFromEnd] ^= 0xFF;
        using var container = MediaContainers.Open(new MemoryStream(bytes))!;
        using var decoder = AudioDecoders.Create(container.Tracks[0])!;
        var packets = 0;
        var packet = new MediaPacket();
        var output = new PcmBuffer();
        while (container.ReadPacket(packet))
        {
            decoder.Decode(packet, output);
            packets++;
        }

        using var clean = Open("sine-16-stereo-44100.flac");
        var cleanPackets = 0;
        while (clean.ReadPacket(packet))
        {
            cleanPackets++;
        }

        await Assert.That(packets).IsLessThan(cleanPackets);
    }

    /// <summary>Checks that bytes that are not a FLAC file are not opened as one.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NotFlac_IsNotOpened()
    {
        var bytes = RandomData.Bytes(Seed, DataLength);
        await using var stream = new MemoryStream(bytes);
        await Assert.That(MediaContainers.Open(stream)).IsNull();
        await Assert.That(stream.Position).IsEqualTo(0L);
    }

    /// <summary>Checks the eight bytes at a time check against a bit by bit one on random data of every length up to a few groups.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Crc16_MatchesTheBitByBitCheck()
    {
        var data = RandomData.Bytes(Seed, DataLength);
        for (var length = 0; length <= data.Length; length++)
        {
            var expected = BitByBit(data.AsSpan(0, length));
            await Assert.That(Crc16.Compute(data.AsSpan(0, length))).IsEqualTo(expected);
        }
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

    /// <summary>Decodes every packet of a container.</summary>
    /// <param name="container">The container.</param>
    /// <param name="decoder">The decoder for its sound track.</param>
    /// <returns>All the samples.</returns>
    private static PcmBuffer DecodeAll(MediaContainer container, AudioDecoder decoder)
    {
        var packet = new MediaPacket();
        var pcm = new PcmBuffer();
        while (container.ReadPacket(packet))
        {
            decoder.Decode(packet, pcm);
        }

        return pcm;
    }

    /// <summary>Turns decoded floats back into whole numbers laid out as FFmpeg writes them: 16 bits wide, or 32 bits with the sample in the top bits.</summary>
    /// <param name="samples">The decoded samples.</param>
    /// <param name="bits">The bits of each sample.</param>
    /// <returns>The samples as little endian whole numbers.</returns>
    private static byte[] ToIntegerBytes(ReadOnlySpan<float> samples, int bits)
    {
        var wide = bits > NarrowBits ? WideBits : NarrowBits;
        var width = wide / ByteBits;
        var scale = (double)(1L << (bits - 1));
        var bytes = new byte[samples.Length * width];
        for (var index = 0; index < samples.Length; index++)
        {
            var value = (long)Math.Round(samples[index] * scale) << (wide - bits);
            for (var part = 0; part < width; part++)
            {
                bytes[(index * width) + part] = (byte)(value >> (part * ByteBits));
            }
        }

        return bytes;
    }

    /// <summary>Computes the check one bit at a time, which is slow and plainly right.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The check.</returns>
    private static ushort BitByBit(ReadOnlySpan<byte> data)
    {
        var crc = 0;
        foreach (var value in data)
        {
            crc ^= value << ByteBits;
            for (var bit = 0; bit < ByteBits; bit++)
            {
                crc = (crc & TopBit) != 0 ? ((crc << 1) ^ Polynomial) & Mask16 : (crc << 1) & Mask16;
            }
        }

        return (ushort)crc;
    }
}
