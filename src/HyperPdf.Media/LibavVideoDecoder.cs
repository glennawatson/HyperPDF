// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using FFmpeg.AutoGen.Abstractions;
using HyperPdf.Core.Media;

namespace HyperPdf.Media;

/// <summary>Decodes the pictures of a stream and scales them to blue, green, red and alpha bytes at the size the panel draws.</summary>
[DebuggerDisplay("LibavVideoDecoder: {Size}")]
internal sealed unsafe class LibavVideoDecoder : IDisposable
{
    /// <summary>The seconds in one tick of the stream's time stamps.</summary>
    private readonly double _timeBase;

    /// <summary>The decoder.</summary>
    private readonly LibavCodec _codec;

    /// <summary>The frame the decoder fills.</summary>
    private readonly LibavFrame _frame = new();

    /// <summary>The scaler.</summary>
    private readonly LibavScaler _scaler;

    /// <summary>Initializes a new instance of the <see cref="LibavVideoDecoder"/> class.</summary>
    /// <param name="codec">The opened decoder.</param>
    /// <param name="timeBase">The seconds in one tick of the stream's time stamps.</param>
    /// <param name="size">The size of the scaled pictures.</param>
    private LibavVideoDecoder(LibavCodec codec, double timeBase, FrameSize size)
    {
        _codec = codec;
        _timeBase = timeBase;
        _scaler = new(size);
        Size = size;
    }

    /// <summary>Gets the size of the scaled pictures.</summary>
    internal FrameSize Size { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        _scaler.Dispose();
        _frame.Dispose();
        _codec.Dispose();
    }

    /// <summary>Opens a decoder for a stream.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="timeBase">The seconds in one tick of the stream's time stamps.</param>
    /// <param name="size">The size to scale the pictures to.</param>
    /// <returns>The decoder, or <see langword="null"/> when the video uses a format this build of FFmpeg cannot decode.</returns>
    internal static LibavVideoDecoder? Create(AVStream* stream, double timeBase, FrameSize size) =>
        LibavCodec.Open(stream) is { } codec ? new(codec, timeBase, size) : null;

    /// <summary>Gives the decoder a packet.</summary>
    /// <param name="packet">The packet, or <see langword="null"/> to say the file has ended.</param>
    /// <returns><see langword="true"/> when the decoder took the packet.</returns>
    internal bool Send(LibavPacket? packet) => ffmpeg.avcodec_send_packet(_codec.Pointer, packet is null ? null : packet.Pointer) >= 0;

    /// <summary>Takes the next decoded picture.</summary>
    /// <param name="start">Receives when the picture shows, in seconds from the start of the stream.</param>
    /// <returns><see langword="true"/> when a picture was decoded; it stays until <see cref="Release"/> is called.</returns>
    internal bool Receive(out double start)
    {
        start = 0;
        if (ffmpeg.avcodec_receive_frame(_codec.Pointer, _frame.Pointer) < 0)
        {
            return false;
        }

        var stamp = _frame.Pointer->best_effort_timestamp;
        start = stamp == long.MinValue ? 0 : stamp * _timeBase;
        return true;
    }

    /// <summary>Scales the decoded picture into a buffer.</summary>
    /// <param name="destination">Receives <see cref="FrameSize.ByteCount"/> bytes.</param>
    /// <returns><see langword="true"/> when the picture was scaled.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Scale(Span<byte> destination) => _scaler.Scale(_frame, destination);

    /// <summary>Lets go of the decoded picture so the decoder can reuse it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Release() => ffmpeg.av_frame_unref(_frame.Pointer);
}
