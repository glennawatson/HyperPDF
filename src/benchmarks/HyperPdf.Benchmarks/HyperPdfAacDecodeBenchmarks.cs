// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperCodecs;

namespace HyperPdf.Benchmarks;

/// <summary>Decodes about a second and a half of AAC Low Complexity sound, and opens MP4 files.</summary>
public class HyperPdfAacDecodeBenchmarks
{
    /// <summary>The stereo music clip.</summary>
    private const string MusicFile = "aac-lc-stereo-44100.m4a";

    /// <summary>The low bitrate clip that uses noise substitution and intensity stereo.</summary>
    private const string NoiseFile = "aac-lc-noise-24k.m4a";

    /// <summary>The file with pictures, sound and text.</summary>
    private const string MixedFile = "av-h264-aac-text.mp4";

    /// <summary>The fragmented file.</summary>
    private const string FragmentedFile = "aac-lc-stereo-fragmented.mp4";

    /// <summary>The music clip's packets.</summary>
    private SoundClip? _music;

    /// <summary>The noise clip's packets.</summary>
    private SoundClip? _noise;

    /// <summary>Reads the packets of the files.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _music = SoundClip.Load(MusicFile);
        _noise = SoundClip.Load(NoiseFile);
    }

    /// <summary>Decodes the music clip.</summary>
    /// <returns>The samples decoded.</returns>
    [Benchmark]
    public int DecodeMusic() => _music!.Decode();

    /// <summary>Decodes the noise clip.</summary>
    /// <returns>The samples decoded.</returns>
    [Benchmark]
    public int DecodeNoise() => _noise!.Decode();

    /// <summary>Opens the file with pictures, sound and text.</summary>
    /// <returns>The number of tracks.</returns>
    [Benchmark]
    public int OpenMixed() => OpenCount(MixedFile);

    /// <summary>Opens the fragmented file.</summary>
    /// <returns>The number of tracks.</returns>
    [Benchmark]
    public int OpenFragmented() => OpenCount(FragmentedFile);

    /// <summary>Opens a file and counts its tracks.</summary>
    /// <param name="name">The file name in the fixtures folder next to the benchmarks.</param>
    /// <returns>The number of tracks.</returns>
    /// <exception cref="InvalidDataException">The file did not open.</exception>
    private static int OpenCount(string name)
    {
        using var container = MediaContainers.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", name))
            ?? throw new InvalidDataException($"{name} did not open.");
        return container.Tracks.Count;
    }
}
