// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperCodecs;

namespace HyperPdf.Benchmarks;

/// <summary>The packets of the first sound track of a file, read once, and the buffer that decoding them fills again each time.</summary>
internal sealed class SoundClip
{
    /// <summary>The track.</summary>
    private readonly MediaTrack _track;

    /// <summary>The packets.</summary>
    private readonly List<MediaPacket> _packets;

    /// <summary>The decoded sound, which is cleared and filled again each time.</summary>
    private readonly PcmBuffer _pcm = new();

    /// <summary>Initializes a new instance of the <see cref="SoundClip"/> class.</summary>
    /// <param name="track">The track.</param>
    /// <param name="packets">The packets.</param>
    private SoundClip(MediaTrack track, List<MediaPacket> packets)
    {
        _track = track;
        _packets = packets;
    }

    /// <summary>Reads every packet of the first sound track of a file.</summary>
    /// <param name="name">The file name in the fixtures folder next to the benchmarks.</param>
    /// <returns>The clip.</returns>
    /// <exception cref="InvalidDataException">The file did not open.</exception>
    internal static SoundClip Load(string name)
    {
        using var container = MediaContainers.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", name))
            ?? throw new InvalidDataException($"{name} did not open.");
        var index = container.FindTrack(MediaTrackKind.Audio);
        var packets = new List<MediaPacket>();
        while (true)
        {
            var packet = new MediaPacket();
            if (!container.ReadPacket(packet))
            {
                break;
            }

            if (packet.Track == index)
            {
                packets.Add(packet);
            }
        }

        return new(container.Tracks[index], packets);
    }

    /// <summary>Decodes every packet with a new decoder.</summary>
    /// <returns>The samples decoded.</returns>
    internal int Decode()
    {
        using var decoder = AudioDecoders.Create(_track)!;
        _pcm.Clear();
        foreach (var packet in _packets)
        {
            decoder.Decode(packet, _pcm);
        }

        return _pcm.Count;
    }
}
