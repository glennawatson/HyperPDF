// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Media;

namespace HyperPdf.Benchmarks;

/// <summary>Reads a caption file of 5,000 cues, and finds the cue shown at a moment near the start and near the end.</summary>
public class HyperPdfSrtCaptionBenchmarks
{
    /// <summary>The cues in the file.</summary>
    private const int Cues = 5000;

    /// <summary>The seconds each cue is shown for.</summary>
    private const int CueSeconds = 2;

    /// <summary>The seconds between the starts of two cues.</summary>
    private const int CueSpacing = 3;

    /// <summary>The seconds a minute holds.</summary>
    private const int MinuteSeconds = 60;

    /// <summary>The seconds an hour holds.</summary>
    private const int HourSeconds = 3600;

    /// <summary>The captions' text.</summary>
    private string _text = string.Empty;

    /// <summary>The captions read from the text.</summary>
    private List<CaptionCue> _cues = [];

    /// <summary>The moment inside the last cue.</summary>
    private TimeSpan _last;

    /// <summary>Builds the file and reads it once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var text = new StringBuilder();
        for (var i = 0; i < Cues; i++)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{i + 1}\n{Time(i * CueSpacing)} --> {Time((i * CueSpacing) + CueSeconds)}\nThe <i>words</i> of caption {i}\nand a second line.\n\n");
        }

        _text = text.ToString();
        _cues = SrtCaptions.Parse(_text);
        _last = TimeSpan.FromSeconds(((Cues - 1) * CueSpacing) + 1);
    }

    /// <summary>Reads the whole file.</summary>
    /// <returns>The cues read.</returns>
    [Benchmark]
    public int Parse() => SrtCaptions.Parse(_text).Count;

    /// <summary>Finds the cue shown during the first cue.</summary>
    /// <returns>The words.</returns>
    [Benchmark]
    public int TextAtStart() => SrtCaptions.TextAt(_cues, TimeSpan.FromSeconds(1)).Length;

    /// <summary>Finds the cue shown during the last cue, which a linear search reaches last.</summary>
    /// <returns>The words.</returns>
    [Benchmark]
    public int TextAtEnd() => SrtCaptions.TextAt(_cues, _last).Length;

    /// <summary>Formats a moment the way a caption file writes it.</summary>
    /// <param name="seconds">The seconds from the start.</param>
    /// <returns>The text.</returns>
    private static string Time(int seconds) =>
        string.Create(CultureInfo.InvariantCulture, $"{seconds / HourSeconds:00}:{seconds % HourSeconds / MinuteSeconds:00}:{seconds % MinuteSeconds:00},000");
}
