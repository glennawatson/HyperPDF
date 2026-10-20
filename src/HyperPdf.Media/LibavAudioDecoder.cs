// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using FFmpeg.AutoGen.Abstractions;

namespace HyperPdf.Media;

/// <summary>Decodes the sound of a stream and converts it to interleaved 32-bit float stereo at a fixed rate, which is what the sound output plays.</summary>
[DebuggerDisplay("LibavAudioDecoder: {OutputRate} Hz")]
internal sealed unsafe class LibavAudioDecoder : IDisposable
{
    /// <summary>The samples each second of the converted sound.</summary>
    internal const int OutputRate = 48_000;

    /// <summary>The channels of the converted sound.</summary>
    internal const int OutputChannels = 2;

    /// <summary>The extra samples reserved when sizing the buffer, for the converter's own delay.</summary>
    private const int Slack = 256;

    /// <summary>The samples each channel of the first buffer holds at least, which is more than the frame of any common codec, so it is made once.</summary>
    private const int MinimumCapacity = 4_864;

    /// <summary>The seconds in one tick of the stream's time stamps.</summary>
    private readonly double _timeBase;

    /// <summary>The decoder.</summary>
    private readonly LibavCodec _codec;

    /// <summary>The frame the decoder fills.</summary>
    private readonly LibavFrame _frame = new();

    /// <summary>The converter, made when the first sound arrives.</summary>
    private LibavConverter? _converter;

    /// <summary>The converted samples of the latest frame.</summary>
    private float[] _samples = [];

    /// <summary>Where the next frame is expected to start when it has no time stamp, in seconds.</summary>
    private double _expected;

    /// <summary>Initializes a new instance of the <see cref="LibavAudioDecoder"/> class.</summary>
    /// <param name="codec">The opened decoder.</param>
    /// <param name="timeBase">The seconds in one tick of the stream's time stamps.</param>
    private LibavAudioDecoder(LibavCodec codec, double timeBase)
    {
        _codec = codec;
        _timeBase = timeBase;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _converter?.Dispose();
        _frame.Dispose();
        _codec.Dispose();
    }

    /// <summary>Opens a decoder for a stream.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="timeBase">The seconds in one tick of the stream's time stamps.</param>
    /// <returns>The decoder, or <see langword="null"/> when the sound uses a format this build of FFmpeg cannot decode.</returns>
    internal static LibavAudioDecoder? Create(AVStream* stream, double timeBase) =>
        LibavCodec.Open(stream) is { } codec ? new(codec, timeBase) : null;

    /// <summary>Gives the decoder a packet.</summary>
    /// <param name="packet">The packet, or <see langword="null"/> to say the file has ended.</param>
    /// <returns><see langword="true"/> when the decoder took the packet.</returns>
    internal bool Send(LibavPacket? packet) => ffmpeg.avcodec_send_packet(_codec.Pointer, packet is null ? null : packet.Pointer) >= 0;

    /// <summary>Takes the next decoded sound, converted.</summary>
    /// <param name="samples">Receives the interleaved samples, valid until the next call.</param>
    /// <param name="start">Receives where the sound starts, in seconds from the start of the stream.</param>
    /// <returns><see langword="true"/> when a frame was decoded.</returns>
    internal bool Receive(out ReadOnlySpan<float> samples, out double start)
    {
        samples = default;
        start = 0;
        if (ffmpeg.avcodec_receive_frame(_codec.Pointer, _frame.Pointer) < 0)
        {
            return false;
        }

        var stamp = _frame.Pointer->best_effort_timestamp;
        start = stamp == long.MinValue ? _expected : stamp * _timeBase;
        var count = Convert();
        _expected = start + ((double)count / OutputRate);
        samples = _samples.AsSpan(0, count * OutputChannels);
        ffmpeg.av_frame_unref(_frame.Pointer);
        return true;
    }

    /// <summary>Converts the decoded frame to the output format.</summary>
    /// <returns>The samples converted for each channel.</returns>
    private int Convert()
    {
        _converter ??= LibavConverter.Create(_frame, OutputRate, OutputChannels);
        if (_converter is null)
        {
            return 0;
        }

        var capacity = ffmpeg.swr_get_out_samples(_converter.Pointer, _frame.Pointer->nb_samples) + Slack;
        if (_samples.Length < capacity * OutputChannels)
        {
            _samples = new float[Math.Max(capacity, MinimumCapacity) * OutputChannels];
        }

        fixed (float* output = _samples)
        {
            var converted = ffmpeg.swr_convert(_converter.Pointer, (byte**)&output, capacity, _frame.Pointer->extended_data, _frame.Pointer->nb_samples);
            return Math.Max(0, converted);
        }
    }
}
