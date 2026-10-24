// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Opus;

namespace HyperPdf.Codecs.Tests;

/// <summary>Checks that Ogg Opus files open, report their sound and give packets the Opus packet reader accepts.</summary>
public sealed class OpusContainerTests
{
    /// <summary>The sample rate Opus decodes to.</summary>
    private const int OpusRate = 48_000;

    /// <summary>The most the container's length may differ from the reference's, in seconds: a frame of the longest size.</summary>
    private const double DurationSlack = 0.0605;

    /// <summary>The stereo channel count.</summary>
    private const int StereoChannels = 2;

    /// <summary>The bytes in an identification header with no mapping table.</summary>
    private const int HeaderBytes = 19;

    /// <summary>The first byte of an Opus identification header, repeated in the tests that build one.</summary>
    private const byte Version = 1;

    /// <summary>The pre-skip a typical encoder writes, in samples at 48 kHz.</summary>
    private const int TypicalPreSkip = 312;

    /// <summary>Files open as Opus tracks with the channels and rate the stream says.</summary>
    /// <param name="name">The file name without its extension.</param>
    /// <param name="channels">The channels.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("opus-stereo-music", 2)]
    [Arguments("opus-mono-voip", 1)]
    [Arguments("opus-stereo-lowdelay", 2)]
    public async Task Open_OpusFile_ReportsTheTrack(string name, int channels)
    {
        using var container = Open($"{name}.opus");
        using var reference = Open($"{name}.reference.flac");

        var track = container.Tracks[0];

        await Assert.That(track.Codec).IsEqualTo(MediaCodec.Opus);
        await Assert.That(track.SampleRate).IsEqualTo(OpusRate);
        await Assert.That(track.Channels).IsEqualTo(channels);
        await Assert.That(container.Duration.TotalSeconds).IsEqualTo(reference.Duration.TotalSeconds).Within(DurationSlack);
    }

    /// <summary>Every packet of a real file is split into frames, and the file's coding mode is what the encoder was asked for.</summary>
    /// <param name="name">The file name without its extension.</param>
    /// <param name="mode">The mode every packet is expected to have.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("opus-stereo-lowdelay", (int)OpusMode.Celt)]
    public async Task Packets_OfARealFile_Parse(string name, int mode)
    {
        var counts = CountPackets($"{name}.opus", (OpusMode)mode);

        await Assert.That(counts.Packets).IsGreaterThan(0);
        await Assert.That(counts.Parsed).IsEqualTo(counts.Packets);
        await Assert.That(counts.InMode).IsEqualTo(counts.Packets);
    }

    /// <summary>A mono or stereo header is read, and one that is damaged is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpusHead_StereoHeader_IsRead()
    {
        var header = Header(channels: StereoChannels, family: 0);

        var ok = OpusHead.TryRead(header, out var head);
        var damaged = OpusHead.TryRead(header.AsSpan(0, header.Length - 1), out _);
        var wrongMarker = OpusHead.TryRead(new byte[header.Length], out _);

        await Assert.That(ok).IsTrue();
        await Assert.That(head!.Channels).IsEqualTo(StereoChannels);
        await Assert.That(head.PreSkip).IsEqualTo(TypicalPreSkip);
        await Assert.That(head.CoupledCount).IsEqualTo(1);
        await Assert.That(damaged).IsFalse();
        await Assert.That(wrongMarker).IsFalse();
    }

    /// <summary>Counts a file's packets, how many parse and how many have a mode.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="mode">The mode to count.</param>
    /// <returns>The counts.</returns>
    private static PacketCounts CountPackets(string name, OpusMode mode)
    {
        using var container = Open(name);
        var packet = new MediaPacket();
        Span<OpusFrame> frames = stackalloc OpusFrame[OpusPacket.MaxFrames];
        var packets = 0;
        var parsed = 0;
        var inMode = 0;
        while (container.ReadPacket(packet))
        {
            packets++;
            if (!OpusPacket.TryParse(packet.Data, frames, out var info))
            {
                continue;
            }

            parsed++;
            inMode += info.Mode == mode ? 1 : 0;
        }

        return new(packets, parsed, inMode);
    }

    /// <summary>Builds an identification header.</summary>
    /// <param name="channels">The channels.</param>
    /// <param name="family">The mapping family.</param>
    /// <returns>The header packet.</returns>
    private static byte[] Header(byte channels, byte family)
    {
        var header = new byte[HeaderBytes];
        "OpusHead"u8.CopyTo(header);
        header[8] = Version;
        header[9] = channels;
        header[10] = (byte)(TypicalPreSkip & 0xFF);
        header[11] = (byte)(TypicalPreSkip >> 8);
        header[18] = family;
        return header;
    }

    /// <summary>Opens a test file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The container.</returns>
    /// <exception cref="InvalidDataException">The file did not open.</exception>
    private static MediaContainer Open(string name) =>
        MediaContainers.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)) ?? throw new InvalidDataException($"{name} did not open.");

    /// <summary>The packets of a file and what became of them.</summary>
    /// <param name="Packets">The packets read.</param>
    /// <param name="Parsed">The packets the packet reader accepted.</param>
    /// <param name="InMode">The packets in the expected mode.</param>
    private readonly record struct PacketCounts(int Packets, int Parsed, int InMode);
}
