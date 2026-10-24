// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Codecs;

namespace HyperPdf.Benchmarks;

/// <summary>Decodes predicted and bi-predicted H.264 and H.265 files, which exercise motion compensation and the loop filters.</summary>
public class HyperPdfVideoDecodeBenchmarks
{
    /// <summary>The track description of the picture track.</summary>
    private MediaTrack? _track;

    /// <summary>The coded pictures, read once.</summary>
    private List<MediaPacket>? _packets;

    /// <summary>The decoded pictures, cleared before each run.</summary>
    private List<VideoFrame>? _frames;

    /// <summary>Gets or sets the file under test.</summary>
    [Params("h264-p-cabac.mp4", "h264-b-cabac-deblock.mp4", "h265-intra.mp4", "h265-p.mp4", "h265-b.mp4", "h265-loop-filters-ctu32.mp4")]
    public string Source { get; set; } = "h264-p-cabac.mp4";

    /// <summary>Reads the coded pictures of the file.</summary>
    /// <exception cref="InvalidDataException">The file did not open.</exception>
    [GlobalSetup]
    public void Setup()
    {
        using var container = MediaContainers.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", Source))
            ?? throw new InvalidDataException($"{Source} did not open.");
        var index = container.FindTrack(MediaTrackKind.Video);
        _track = container.Tracks[index];
        _packets = [];
        var packet = new MediaPacket();
        while (container.ReadPacket(packet))
        {
            if (packet.Track != index)
            {
                continue;
            }

            var copy = new MediaPacket();
            packet.Data.CopyTo(copy.Begin(packet.Track, packet.Length, packet.Time, packet.Duration, packet.IsKey));
            _packets.Add(copy);
        }

        _frames = [];
    }

    /// <summary>Decodes every picture.</summary>
    /// <returns>The number of pictures.</returns>
    /// <exception cref="NotSupportedException">No decoder handles the track.</exception>
    [Benchmark]
    public int DecodeAll()
    {
        using var decoder = VideoDecoders.Create(_track!) ?? throw new NotSupportedException("No decoder for the track.");
        var count = 0;
        foreach (var packet in _packets!)
        {
            decoder.Decode(packet, _frames!);
            count += Recycle(decoder);
        }

        decoder.Flush(_frames!);
        return count + Recycle(decoder);
    }

    /// <summary>Hands the pictures back to the decoder, as the player does once it has shown them.</summary>
    /// <param name="decoder">The decoder.</param>
    /// <returns>The number of pictures.</returns>
    private int Recycle(VideoDecoder decoder)
    {
        var count = _frames!.Count;
        foreach (var frame in _frames)
        {
            decoder.Recycle(frame);
        }

        _frames.Clear();
        return count;
    }
}
