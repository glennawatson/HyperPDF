// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using FFmpeg.AutoGen.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Media;

/// <summary>A packet of compressed sound or picture. Freeing it is automatic, so no code outside this type handles the native memory.</summary>
[DebuggerDisplay("LibavPacket: stream {StreamIndex}")]
internal sealed unsafe class LibavPacket : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new instance of the <see cref="LibavPacket"/> class with an empty packet.</summary>
    internal LibavPacket()
        : base(true) => SetHandle((nint)ffmpeg.av_packet_alloc());

    /// <summary>Gets the native packet.</summary>
    internal AVPacket* Pointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (AVPacket*)handle;
    }

    /// <summary>Gets the index of the stream the packet belongs to.</summary>
    internal int StreamIndex => Pointer->stream_index;

    /// <summary>Gets the time stamp of the packet in its stream's ticks, or <see cref="long.MinValue"/> when it has none.</summary>
    internal long Stamp => Pointer->pts;

    /// <summary>Gets how long the packet lasts in its stream's ticks.</summary>
    internal long Length => Pointer->duration;

    /// <summary>Drops the compressed data the packet holds, so the packet can be filled again.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Unref() => ffmpeg.av_packet_unref(Pointer);

    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        fixed (nint* location = &handle)
        {
            ffmpeg.av_packet_free((AVPacket**)location);
        }

        return true;
    }
}
