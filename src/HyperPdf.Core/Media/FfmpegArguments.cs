// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Media;

/// <summary>
/// Builds the arguments of the FFmpeg programs. Each is a list of separate arguments, so a shell never splits a file name.
/// The viewer passes only full paths, which never start with a dash.
/// </summary>
public static class FfmpegArguments
{
    /// <summary>The log level that prints only errors.</summary>
    private const string ErrorsOnly = "error";

    /// <summary>The widest picture decoded, in pixels. A larger video is scaled down.</summary>
    private const int MaxFrameWidth = 640;

    /// <summary>The smallest side of a decoded picture, in pixels.</summary>
    private const int MinSide = 2;

    /// <summary>Builds the arguments that make <c>ffprobe</c> print the length and size of a file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The arguments.</returns>
    public static string[] Probe(string path) =>
        ["-v", ErrorsOnly, "-show_entries", "format=duration:stream=codec_type,width,height", "-of", "default=nw=1", path];

    /// <summary>Builds the arguments that make <c>ffmpeg</c> print the first caption stream as SubRip text.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The arguments.</returns>
    public static string[] Captions(string path) =>
        ["-v", ErrorsOnly, "-i", path, "-map", "0:s:0", "-f", "srt", "pipe:1"];

    /// <summary>Builds the arguments that make <c>ffmpeg</c> write raw pictures at the speed the video plays.</summary>
    /// <param name="path">The file.</param>
    /// <param name="start">Where in the video to begin.</param>
    /// <param name="size">The size of each picture.</param>
    /// <param name="framesPerSecond">How many pictures to write for each second.</param>
    /// <returns>The arguments. Each picture is <c>Width * Height * 4</c> bytes in blue, green, red, alpha order.</returns>
    public static string[] Video(string path, TimeSpan start, FrameSize size, int framesPerSecond) =>
    [
        "-v", ErrorsOnly, "-ss", Seconds(start), "-re", "-i", path, "-an", "-sn",
        "-vf", string.Create(CultureInfo.InvariantCulture, $"fps={Math.Max(1, framesPerSecond)},scale={size.Width}:{size.Height}"),
        "-pix_fmt", "bgra", "-f", "rawvideo", "pipe:1",
    ];

    /// <summary>Builds the arguments that make <c>ffplay</c> play the sound of a file without a window.</summary>
    /// <param name="path">The file.</param>
    /// <param name="start">Where in the file to begin.</param>
    /// <returns>The arguments.</returns>
    public static string[] Audio(string path, TimeSpan start) =>
        ["-nodisp", "-autoexit", "-loglevel", "quiet", "-vn", "-ss", Seconds(start), path];

    /// <summary>Works out the size of the pictures to decode: the video's size, scaled down to fit and made even.</summary>
    /// <param name="width">The video's width.</param>
    /// <param name="height">The video's height.</param>
    /// <returns>The size to decode; empty when the video has no size.</returns>
    public static FrameSize Fit(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return default;
        }

        var scale = Math.Min(1.0, (double)MaxFrameWidth / width);
        return new(Even((int)(width * scale)), Even((int)(height * scale)));
    }

    /// <summary>Writes a time as seconds with the decimal point <c>ffmpeg</c> expects.</summary>
    /// <param name="time">The time.</param>
    /// <returns>The seconds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Seconds(TimeSpan time) =>
        Math.Max(0, time.TotalSeconds).ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Rounds a side down to an even number, because the picture formats FFmpeg scales to need one.</summary>
    /// <param name="side">The side.</param>
    /// <returns>An even side of at least two pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Even(int side) => Math.Max(MinSide, side & ~1);
}
