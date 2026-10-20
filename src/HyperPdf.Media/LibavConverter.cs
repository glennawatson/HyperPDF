// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using FFmpeg.AutoGen.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Media;

/// <summary>A sound converter: the native resampler, freed automatically.</summary>
[DebuggerDisplay("LibavConverter: {IsInvalid}")]
internal sealed unsafe class LibavConverter : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new instance of the <see cref="LibavConverter"/> class.</summary>
    /// <param name="context">The initialised resampler.</param>
    private LibavConverter(SwrContext* context)
        : base(true) => SetHandle((nint)context);

    /// <summary>Gets the native resampler.</summary>
    internal SwrContext* Pointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (SwrContext*)handle;
    }

    /// <summary>Makes a converter from a decoded frame's format to interleaved float stereo.</summary>
    /// <param name="frame">A decoded frame, which holds the input format.</param>
    /// <param name="rate">The samples each second to convert to.</param>
    /// <param name="channels">The channels to convert to.</param>
    /// <returns>The converter, or <see langword="null"/> when the format cannot be converted.</returns>
    internal static LibavConverter? Create(LibavFrame frame, int rate, int channels)
    {
        var layout = default(AVChannelLayout);
        ffmpeg.av_channel_layout_default(&layout, channels);
        SwrContext* context = null;
        var input = (AVSampleFormat)frame.Pointer->format;
        var created = ffmpeg.swr_alloc_set_opts2(&context, &layout, AVSampleFormat.AV_SAMPLE_FMT_FLT, rate, &frame.Pointer->ch_layout, input, frame.Pointer->sample_rate, 0, null);
        if (created >= 0 && ffmpeg.swr_init(context) >= 0)
        {
            return new(context);
        }

        ffmpeg.swr_free(&context);
        return null;
    }

    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        fixed (nint* location = &handle)
        {
            ffmpeg.swr_free((SwrContext**)location);
        }

        return true;
    }
}
