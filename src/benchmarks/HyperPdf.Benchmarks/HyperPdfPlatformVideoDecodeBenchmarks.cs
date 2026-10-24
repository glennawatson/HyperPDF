// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Codecs;
using HyperPdf.Media.Native;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Decodes the same files with the C# decoder and with the decoder the system provides (the graphics hardware where there is one), keeping the
/// decoder from run to run so the cost of starting it is not counted. Set HYPERPDF_BIG_CLIPS to a folder holding larger H.264 or H.265 files
/// to measure them too.
/// </summary>
public class HyperPdfPlatformVideoDecodeBenchmarks
{
    /// <summary>The name of the environment variable that names a folder of larger files.</summary>
    private const string BigClipsVariable = "HYPERPDF_BIG_CLIPS";

    /// <summary>The prefix of a source that lives in the folder of larger files.</summary>
    private const string BigPrefix = "big:";

    /// <summary>The coded pictures, read once.</summary>
    private List<MediaPacket>? _packets;

    /// <summary>The decoded pictures, cleared as they are handed back.</summary>
    private List<VideoFrame>? _frames;

    /// <summary>The decoder under test, kept between runs.</summary>
    private VideoDecoder? _decoder;

    /// <summary>Gets or sets the file under test.</summary>
    [ParamsSource(nameof(Sources))]
    public string Source { get; set; } = "h264-b-cabac-deblock.mp4";

    /// <summary>Gets or sets a value indicating whether to use the system's decoder instead of the C# one.</summary>
    [Params(false, true)]
    public bool System { get; set; }

    /// <summary>Gets the files to measure: three small fixtures, and any files in the folder named by the environment variable.</summary>
    /// <returns>The sources.</returns>
    public static IEnumerable<string> Sources()
    {
        yield return "h264-b-cabac-deblock.mp4";
        yield return "h265-b.mp4";
        var folder = Environment.GetEnvironmentVariable(BigClipsVariable);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(folder, "*.mp4").Order(StringComparer.Ordinal))
        {
            yield return BigPrefix + Path.GetFileName(path);
        }
    }

    /// <summary>Reads the coded pictures of the file and makes the decoder.</summary>
    /// <exception cref="InvalidDataException">The file did not open.</exception>
    /// <exception cref="NotSupportedException">No decoder handles the track.</exception>
    [GlobalSetup]
    public void Setup()
    {
        var path = Source.StartsWith(BigPrefix, StringComparison.Ordinal)
            ? Path.Combine(Environment.GetEnvironmentVariable(BigClipsVariable)!, Source[BigPrefix.Length..])
            : Path.Combine(AppContext.BaseDirectory, "Fixtures", Source);
        using var container = MediaContainers.Open(path) ?? throw new InvalidDataException($"{Source} did not open.");
        var index = container.FindTrack(MediaTrackKind.Video);
        var track = container.Tracks[index];
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
        _decoder = (System ? PlatformVideoDecoders.Create(track) : VideoDecoders.Create(track)) ?? throw new NotSupportedException("No decoder for the track.");
    }

    /// <summary>Lets go of the decoder.</summary>
    [GlobalCleanup]
    public void Cleanup() => _decoder?.Dispose();

    /// <summary>Decodes every picture.</summary>
    /// <returns>The number of pictures.</returns>
    [Benchmark]
    public int DecodeAll()
    {
        var decoder = _decoder!;
        decoder.Reset();
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
