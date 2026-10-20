// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using FFmpeg.AutoGen.Abstractions;
using HyperPdf.Core.Media;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Media;

/// <summary>A picture scaler: the native scaling context, freed automatically.</summary>
[DebuggerDisplay("LibavScaler: {IsInvalid}")]
internal sealed unsafe class LibavScaler : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>The bytes in each pixel of the scaled picture.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The scaler's flag for smooth scaling (<c>SWS_BILINEAR</c>).</summary>
    private const int BilinearFlag = 2;

    /// <summary>The size of the scaled pictures.</summary>
    private readonly FrameSize _size;

    /// <summary>A frame that describes the buffer the scaled picture goes into, so scaling needs no arrays.</summary>
    private readonly LibavFrame _target = new();

    /// <summary>Initializes a new instance of the <see cref="LibavScaler"/> class.</summary>
    /// <param name="size">The size of the scaled pictures.</param>
    internal LibavScaler(FrameSize size)
        : base(true)
    {
        _size = size;
        var target = _target.Pointer;
        target->width = size.Width;
        target->height = size.Height;
        target->format = (int)AVPixelFormat.AV_PIX_FMT_BGRA;
        target->linesize[0] = size.Width * BytesPerPixel;
    }

    /// <summary>Scales a decoded picture into a buffer as blue, green, red and alpha bytes.</summary>
    /// <param name="frame">The decoded picture.</param>
    /// <param name="destination">Receives <see cref="FrameSize.ByteCount"/> bytes.</param>
    /// <returns><see langword="true"/> when the picture was scaled.</returns>
    internal bool Scale(LibavFrame frame, Span<byte> destination)
    {
        var picture = frame.Pointer;
        var input = (AVPixelFormat)picture->format;
        var context = ffmpeg.sws_getCachedContext(
            (SwsContext*)handle,
            picture->width,
            picture->height,
            input,
            _size.Width,
            _size.Height,
            AVPixelFormat.AV_PIX_FMT_BGRA,
            BilinearFlag,
            null,
            null,
            null);
        SetHandle((nint)context);
        if (context is null || destination.Length < _size.ByteCount)
        {
            return false;
        }

        fixed (byte* output = destination)
        {
            _target.Pointer->data[0] = output;
            var result = ffmpeg.sws_scale_frame(context, _target.Pointer, picture);
            _target.Pointer->data[0] = null;
            return result >= 0;
        }
    }

    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        ffmpeg.sws_freeContext((SwsContext*)handle);
        return true;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _target.Dispose();
        }

        base.Dispose(disposing);
    }
}
