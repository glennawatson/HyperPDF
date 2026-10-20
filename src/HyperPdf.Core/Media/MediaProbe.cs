// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Media;

/// <summary>Reads the text <c>ffprobe</c> prints for a file.</summary>
public static class MediaProbe
{
    /// <summary>The key that starts each stream.</summary>
    private const string TypeKey = "codec_type";

    /// <summary>The key of a stream's width.</summary>
    private const string WidthKey = "width";

    /// <summary>The key of a stream's height.</summary>
    private const string HeightKey = "height";

    /// <summary>The key of the file's length in seconds.</summary>
    private const string DurationKey = "duration";

    /// <summary>The value of <c>codec_type</c> for a picture.</summary>
    private const string Video = "video";

    /// <summary>The value of <c>codec_type</c> for sound.</summary>
    private const string Audio = "audio";

    /// <summary>The value of <c>codec_type</c> for captions.</summary>
    private const string Subtitle = "subtitle";

    /// <summary>The longest length that is read, so a damaged value cannot make a time that overflows.</summary>
    private const double MaxSeconds = 86_400.0 * 30;

    /// <summary>Reads the <c>key=value</c> lines printed with <c>-show_entries format=duration:stream=codec_type,width,height -of default=nw=1</c>.</summary>
    /// <param name="output">The printed text.</param>
    /// <returns>What the file holds.</returns>
    public static MediaInfo Parse(ReadOnlySpan<char> output)
    {
        var state = default(ProbeState);
        foreach (var line in output.EnumerateLines())
        {
            state = ReadLine(state, line);
        }

        return state.Info;
    }

    /// <summary>Reads one <c>key=value</c> line.</summary>
    /// <param name="state">What was read so far.</param>
    /// <param name="line">The line.</param>
    /// <returns>What was read, with the line added.</returns>
    private static ProbeState ReadLine(ProbeState state, ReadOnlySpan<char> line)
    {
        var split = line.IndexOf('=');
        if (split <= 0)
        {
            return state;
        }

        var key = line[..split].Trim();
        var value = line[(split + 1)..].Trim();
        return key.SequenceEqual(TypeKey) ? ReadType(state.Info, value) : state with { Info = ReadValue(state.Info, key, value, state.InVideo) };
    }

    /// <summary>Reads the start of a stream.</summary>
    /// <param name="info">What was read so far.</param>
    /// <param name="type">The kind of stream.</param>
    /// <returns>What was read, with the stream added.</returns>
    private static ProbeState ReadType(MediaInfo info, ReadOnlySpan<char> type)
    {
        var video = type.SequenceEqual(Video);
        var added = info with { HasVideo = info.HasVideo || video, HasAudio = info.HasAudio || type.SequenceEqual(Audio), HasCaptions = info.HasCaptions || type.SequenceEqual(Subtitle) };
        return new(added, video);
    }

    /// <summary>Reads one value that is not a stream type.</summary>
    /// <param name="info">What was read so far.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <param name="inVideo">Whether the value belongs to a video stream.</param>
    /// <returns>What was read, with the value added.</returns>
    private static MediaInfo ReadValue(MediaInfo info, ReadOnlySpan<char> key, ReadOnlySpan<char> value, bool inVideo)
    {
        if (inVideo && info.Width == 0 && key.SequenceEqual(WidthKey))
        {
            return info with { Width = ReadPixels(value) };
        }

        if (inVideo && info.Height == 0 && key.SequenceEqual(HeightKey))
        {
            return info with { Height = ReadPixels(value) };
        }

        return key.SequenceEqual(DurationKey) && TryReadSeconds(value, out var duration) ? info with { Duration = duration } : info;
    }

    /// <summary>Reads a size in pixels.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The size, or zero when the text is not a number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadPixels(ReadOnlySpan<char> value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var pixels) ? pixels : 0;

    /// <summary>Reads a length in seconds.</summary>
    /// <param name="value">The text.</param>
    /// <param name="duration">Receives the length.</param>
    /// <returns><see langword="true"/> when the text is a length within bounds.</returns>
    private static bool TryReadSeconds(ReadOnlySpan<char> value, out TimeSpan duration)
    {
        duration = default;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || seconds is < 0 or > MaxSeconds)
        {
            return false;
        }

        duration = TimeSpan.FromSeconds(seconds);
        return true;
    }

    /// <summary>What was read from the printed text so far.</summary>
    /// <param name="Info">What the file holds.</param>
    /// <param name="InVideo">Whether the stream being read is a video stream.</param>
    private readonly record struct ProbeState(MediaInfo Info, bool InVideo);
}
