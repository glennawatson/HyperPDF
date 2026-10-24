// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Codecs;

namespace HyperPdf.Benchmarks;

/// <summary>Opens a Matroska file, reads all its packets, and seeks in it.</summary>
public class HyperPdfMatroskaBenchmarks
{
    /// <summary>The file with pictures, sound and subtitles.</summary>
    private const string MixedFile = "av-h264-aac-text.mkv";

    /// <summary>The seconds to seek to.</summary>
    private const double SeekTarget = 1.0;

    /// <summary>The container, opened once for the read and seek benchmarks.</summary>
    private MediaContainer? _container;

    /// <summary>The packet the read and seek benchmarks fill.</summary>
    private MediaPacket? _packet;

    /// <summary>Opens the file for the read and seek benchmarks.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _container = MediaContainers.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", MixedFile));
        _packet = new();
    }

    /// <summary>Lets go of the file.</summary>
    [GlobalCleanup]
    public void Cleanup() => _container?.Dispose();

    /// <summary>Opens the file and counts its tracks.</summary>
    /// <returns>The number of tracks.</returns>
    /// <exception cref="InvalidDataException">The file did not open.</exception>
    [Benchmark]
    public int Open()
    {
        using var container = MediaContainers.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", MixedFile))
            ?? throw new InvalidDataException($"{MixedFile} did not open.");
        return container.Tracks.Count;
    }

    /// <summary>Reads every packet from the start.</summary>
    /// <returns>The bytes read.</returns>
    [Benchmark]
    public long ReadAll()
    {
        _ = _container!.Seek(TimeSpan.Zero);
        var bytes = 0L;
        while (_container.ReadPacket(_packet!))
        {
            bytes += _packet!.Length;
        }

        return bytes;
    }

    /// <summary>Seeks into the file.</summary>
    /// <returns>Whether the file moved.</returns>
    [Benchmark]
    public bool SeekInside() => _container!.Seek(TimeSpan.FromSeconds(SeekTarget));
}
