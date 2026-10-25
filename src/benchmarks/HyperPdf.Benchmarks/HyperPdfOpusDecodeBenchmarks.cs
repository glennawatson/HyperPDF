// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperCodecs;

namespace HyperPdf.Benchmarks;

/// <summary>Decodes about a second of Opus sound coded by each layer: CELT, SILK, and both together.</summary>
public class HyperPdfOpusDecodeBenchmarks
{
    /// <summary>The CELT file, a stereo music clip.</summary>
    private const string CeltFile = "opus-stereo-music.opus";

    /// <summary>The SILK file, a mono wideband speech clip.</summary>
    private const string SilkFile = "silk-mono-wb-20ms.opus";

    /// <summary>The hybrid file, a mono speech clip with the CELT layer on top.</summary>
    private const string HybridFile = "opus-mono-voip.opus";

    /// <summary>The packets of the CELT file.</summary>
    private OpusClip? _celt;

    /// <summary>The packets of the SILK file.</summary>
    private OpusClip? _silk;

    /// <summary>The packets of the hybrid file.</summary>
    private OpusClip? _hybrid;

    /// <summary>Reads the packets of the files.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _celt = OpusClip.Load(CeltFile);
        _silk = OpusClip.Load(SilkFile);
        _hybrid = OpusClip.Load(HybridFile);
    }

    /// <summary>Decodes the CELT file.</summary>
    /// <returns>The samples decoded.</returns>
    [Benchmark]
    public int DecodeCelt() => _celt!.Decode();

    /// <summary>Decodes the SILK file.</summary>
    /// <returns>The samples decoded.</returns>
    [Benchmark]
    public int DecodeSilk() => _silk!.Decode();

    /// <summary>Decodes the hybrid file.</summary>
    /// <returns>The samples decoded.</returns>
    [Benchmark]
    public int DecodeHybrid() => _hybrid!.Decode();

    /// <summary>The packets of one file and the decoder and buffers that decode them.</summary>
    private sealed class OpusClip
    {
        /// <summary>The track.</summary>
        private readonly MediaTrack _track;

        /// <summary>The packets.</summary>
        private readonly List<MediaPacket> _packets;

        /// <summary>The decoded sound, which is cleared and filled again each time.</summary>
        private readonly PcmBuffer _pcm = new();

        /// <summary>Initializes a new instance of the <see cref="OpusClip"/> class.</summary>
        /// <param name="track">The track.</param>
        /// <param name="packets">The packets.</param>
        private OpusClip(MediaTrack track, List<MediaPacket> packets)
        {
            _track = track;
            _packets = packets;
        }

        /// <summary>Reads every packet of a file.</summary>
        /// <param name="name">The file name in the fixtures folder next to the benchmarks.</param>
        /// <returns>The clip.</returns>
        /// <exception cref="InvalidDataException">The file did not open.</exception>
        internal static OpusClip Load(string name)
        {
            using var container = MediaContainers.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", name))
                ?? throw new InvalidDataException($"{name} did not open.");
            var packets = new List<MediaPacket>();
            while (true)
            {
                var packet = new MediaPacket();
                if (!container.ReadPacket(packet))
                {
                    break;
                }

                packets.Add(packet);
            }

            return new(container.Tracks[0], packets);
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
}
