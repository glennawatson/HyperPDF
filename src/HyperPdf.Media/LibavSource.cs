// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using FFmpeg.AutoGen.Abstractions;
using HyperPdf.Core.Media;

namespace HyperPdf.Media;

/// <summary>An open media file: its streams and its packets, read one at a time.</summary>
[DebuggerDisplay("LibavSource: video {VideoIndex}, audio {AudioIndex}, captions {CaptionIndex}")]
internal sealed unsafe class LibavSource : IDisposable
{
    /// <summary>The value of a time stamp that is not known.</summary>
    private const long NoTimestamp = long.MinValue;

    /// <summary>The opened container.</summary>
    private readonly LibavFormat _format;

    /// <summary>Initializes a new instance of the <see cref="LibavSource"/> class.</summary>
    /// <param name="format">The opened container.</param>
    private LibavSource(LibavFormat format)
    {
        _format = format;
        var context = format.Pointer;
        VideoIndex = ffmpeg.av_find_best_stream(context, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
        AudioIndex = ffmpeg.av_find_best_stream(context, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, null, 0);
        CaptionIndex = ffmpeg.av_find_best_stream(context, AVMediaType.AVMEDIA_TYPE_SUBTITLE, -1, -1, null, 0);
        Duration = context->duration > 0 ? TimeSpan.FromSeconds((double)context->duration / ffmpeg.AV_TIME_BASE) : TimeSpan.Zero;
        Origin = context->start_time == NoTimestamp ? 0 : (double)context->start_time / ffmpeg.AV_TIME_BASE;
    }

    /// <summary>Gets the index of the best video stream, or a negative number.</summary>
    internal int VideoIndex { get; }

    /// <summary>Gets the index of the best audio stream, or a negative number.</summary>
    internal int AudioIndex { get; }

    /// <summary>Gets the index of the best caption stream, or a negative number.</summary>
    internal int CaptionIndex { get; }

    /// <summary>Gets how long the file plays, or zero when it does not say.</summary>
    internal TimeSpan Duration { get; }

    /// <summary>Gets the time stamp in seconds at which the file starts, which positions are counted from.</summary>
    internal double Origin { get; }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _format.Dispose();

    /// <summary>Opens a file.</summary>
    /// <param name="path">The full path of the file.</param>
    /// <returns>The file, or <see langword="null"/> when it cannot be read.</returns>
    internal static LibavSource? Open(string path)
    {
        AVFormatContext* context = null;
        if (ffmpeg.avformat_open_input(&context, path, null, null) < 0)
        {
            return null;
        }

        var format = new LibavFormat(context);
        if (ffmpeg.avformat_find_stream_info(context, null) >= 0)
        {
            return new(format);
        }

        format.Dispose();
        return null;
    }

    /// <summary>Gets a stream.</summary>
    /// <param name="index">The stream's index.</param>
    /// <returns>The stream.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal AVStream* Stream(int index) => _format.Pointer->streams[index];

    /// <summary>Works out the size the pictures are scaled to.</summary>
    /// <returns>The size, or an empty size when the file has no picture.</returns>
    internal FrameSize PictureSize()
    {
        if (VideoIndex < 0)
        {
            return default;
        }

        var parameters = Stream(VideoIndex)->codecpar;
        return FfmpegArguments.Fit(parameters->width, parameters->height);
    }

    /// <summary>Opens a decoder for the best sound stream.</summary>
    /// <returns>The decoder, or <see langword="null"/> when there is no sound or this build of FFmpeg cannot decode it.</returns>
    internal LibavAudioDecoder? OpenSoundDecoder() =>
        AudioIndex < 0 ? null : LibavAudioDecoder.Create(Stream(AudioIndex), TimeBase(AudioIndex));

    /// <summary>Opens a decoder for the best picture stream.</summary>
    /// <param name="size">The size to scale the pictures to.</param>
    /// <returns>The decoder, or <see langword="null"/> when there is no picture or this build of FFmpeg cannot decode it.</returns>
    internal LibavVideoDecoder? OpenPictureDecoder(FrameSize size) =>
        VideoIndex < 0 || size.IsEmpty ? null : LibavVideoDecoder.Create(Stream(VideoIndex), TimeBase(VideoIndex), size);

    /// <summary>Gets the length of one tick of a stream's time stamps, in seconds.</summary>
    /// <param name="index">The stream's index.</param>
    /// <returns>The seconds in one tick.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal double TimeBase(int index) => ffmpeg.av_q2d(_format.Pointer->streams[index]->time_base);

    /// <summary>Reads the next packet of any stream.</summary>
    /// <param name="packet">Receives the packet.</param>
    /// <returns><see langword="false"/> at the end of the file or on a read error.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Read(LibavPacket packet) => ffmpeg.av_read_frame(_format.Pointer, packet.Pointer) >= 0;

    /// <summary>Moves to the key frame at or before a position.</summary>
    /// <param name="position">The position.</param>
    /// <returns><see langword="true"/> when the file moved.</returns>
    internal bool SeekTo(TimeSpan position)
    {
        var target = (long)((position.TotalSeconds + Origin) * ffmpeg.AV_TIME_BASE);
        return ffmpeg.avformat_seek_file(_format.Pointer, -1, long.MinValue, target, target, 0) >= 0;
    }
}
