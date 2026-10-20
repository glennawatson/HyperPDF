// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using FFmpeg.AutoGen.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Media;

/// <summary>An opened decoder: the native codec context, freed automatically.</summary>
[DebuggerDisplay("LibavCodec: {IsInvalid}")]
internal sealed unsafe class LibavCodec : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new instance of the <see cref="LibavCodec"/> class.</summary>
    /// <param name="context">The opened codec context.</param>
    private LibavCodec(AVCodecContext* context)
        : base(true) => SetHandle((nint)context);

    /// <summary>Gets the native codec context.</summary>
    internal AVCodecContext* Pointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (AVCodecContext*)handle;
    }

    /// <summary>Opens the decoder for a stream.</summary>
    /// <param name="stream">The stream.</param>
    /// <returns>The decoder, or <see langword="null"/> when this build of FFmpeg cannot decode the stream's format.</returns>
    internal static LibavCodec? Open(AVStream* stream)
    {
        var decoder = ffmpeg.avcodec_find_decoder(stream->codecpar->codec_id);
        if (decoder is null)
        {
            return null;
        }

        var context = ffmpeg.avcodec_alloc_context3(decoder);
        if (ffmpeg.avcodec_parameters_to_context(context, stream->codecpar) >= 0 && ffmpeg.avcodec_open2(context, decoder, null) >= 0)
        {
            return new(context);
        }

        ffmpeg.avcodec_free_context(&context);
        return null;
    }

    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        fixed (nint* location = &handle)
        {
            ffmpeg.avcodec_free_context((AVCodecContext**)location);
        }

        return true;
    }
}
