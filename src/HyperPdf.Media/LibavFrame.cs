// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using FFmpeg.AutoGen.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Media;

/// <summary>A decoded frame of sound or picture: the native frame, freed automatically.</summary>
[DebuggerDisplay("LibavFrame: {IsInvalid}")]
internal sealed unsafe class LibavFrame : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new instance of the <see cref="LibavFrame"/> class with an empty frame.</summary>
    internal LibavFrame()
        : base(true) => SetHandle((nint)ffmpeg.av_frame_alloc());

    /// <summary>Gets the native frame.</summary>
    internal AVFrame* Pointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (AVFrame*)handle;
    }

    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        fixed (nint* location = &handle)
        {
            ffmpeg.av_frame_free((AVFrame**)location);
        }

        return true;
    }
}
