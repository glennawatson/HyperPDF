// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Codecs;

namespace HyperPdf.Benchmarks;

/// <summary>Decodes the intra coded pictures of a baseline H.264 file.</summary>
public class HyperPdfH264DecodeBenchmarks
{
    /// <summary>The file of ten intra coded pictures.</summary>
    private const string IntraFile = "h264-intra-baseline.mp4";

    /// <summary>The file of ten intra coded pictures coded with CABAC.</summary>
    private const string CabacFile = "h264-intra-cabac.mp4";

    /// <summary>The track description of the picture track.</summary>
    private MediaTrack? _track;

    /// <summary>The coded pictures, read once.</summary>
    private List<MediaPacket>? _packets;

    /// <summary>The decoded pictures, cleared before each run.</summary>
    private List<VideoFrame>? _frames;

    /// <summary>Gets or sets the file under test.</summary>
    [Params(IntraFile, CabacFile)]
    public string Source { get; set; } = IntraFile;

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
        using var decoder = VideoDecoders.Create(_track!) ?? throw new NotSupportedException("No H.264 decoder.");
        _frames!.Clear();
        foreach (var packet in _packets!)
        {
            decoder.Decode(packet, _frames);
        }

        decoder.Flush(_frames);
        return _frames.Count;
    }
}
