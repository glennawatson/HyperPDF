// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Media;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the parts of in-app media playback that run on every picture and caption; allocations come from the EventPipe trace.</summary>
public sealed class MediaPlaybackBenchmarks : IDisposable
{
    /// <summary>The captions in the sample file.</summary>
    private const int CueCount = 300;

    /// <summary>The pictures in the sample stream.</summary>
    private const int PictureCount = 30;

    /// <summary>The width of the sample pictures.</summary>
    private const int PictureWidth = 160;

    /// <summary>The height of the sample pictures.</summary>
    private const int PictureHeight = 120;

    /// <summary>The seconds between the start of one caption and the next.</summary>
    private const int CueSpacing = 2;

    /// <summary>The seconds a caption shows.</summary>
    private const double CueLength = 1.5;

    /// <summary>The sample caption file.</summary>
    private string _captionText = string.Empty;

    /// <summary>The sample captions, read once.</summary>
    private List<CaptionCue> _cues = [];

    /// <summary>A stream of raw pictures.</summary>
    private byte[] _pictures = [];

    /// <summary>Receives a copied picture.</summary>
    private byte[] _copy = [];

    /// <summary>The pump that reads the pictures.</summary>
    private FramePump _pump = null!;

    /// <summary>The stream of pictures, read again from the start by each run.</summary>
    private MemoryStream _source = null!;

    /// <summary>The size of the sample pictures.</summary>
    private FrameSize _size;

    /// <summary>Makes the sample captions and pictures.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var text = new StringBuilder();
        for (var i = 0; i < CueCount; i++)
        {
            var start = TimeSpan.FromSeconds(i * CueSpacing);
            var end = start + TimeSpan.FromSeconds(CueLength);
            _ = text.Append(i + 1).Append('\n').Append(start.ToString(@"hh\:mm\:ss\,fff")).Append(" --> ").Append(end.ToString(@"hh\:mm\:ss\,fff"));
            _ = text.Append('\n').Append("A line of words in the caption\n\n");
        }

        _captionText = text.ToString();
        _cues = SrtCaptions.Parse(_captionText);
        _size = new(PictureWidth, PictureHeight);
        _pictures = new byte[_size.ByteCount * PictureCount];
        _copy = new byte[_size.ByteCount];
        _pump = new(_size);
        _source = new(_pictures, false);
    }

    /// <summary>Closes the stream of pictures.</summary>
    [GlobalCleanup]
    public void Dispose() => _source?.Dispose();

    /// <summary>Reads a file of captions.</summary>
    /// <returns>The number of captions.</returns>
    [Benchmark]
    public int ParseCaptions() => SrtCaptions.Parse(_captionText).Count;

    /// <summary>Finds the caption shown near the end of the file.</summary>
    /// <returns>The length of the words.</returns>
    [Benchmark]
    public int FindCaption() => SrtCaptions.TextAt(_cues, TimeSpan.FromSeconds((CueCount * CueSpacing) - 1)).Length;

    /// <summary>Reads a stream of pictures and copies the latest one out, as the view does for each picture.</summary>
    /// <returns>A task that completes with the number of pictures shown.</returns>
    [Benchmark]
    public async Task<int> PumpPictures()
    {
        _source.Position = 0;
        await _pump.RunAsync(_source, static () => { }, CancellationToken.None).ConfigureAwait(false);
        return _pump.TryCopy(_copy) ? _pump.Version : 0;
    }
}
