// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using FFmpeg.AutoGen.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Media;

/// <summary>An open container: the native format context, closed automatically.</summary>
[DebuggerDisplay("LibavFormat: {IsInvalid}")]
internal sealed unsafe class LibavFormat : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new instance of the <see cref="LibavFormat"/> class.</summary>
    /// <param name="context">The opened format context.</param>
    internal LibavFormat(AVFormatContext* context)
        : base(true) => SetHandle((nint)context);

    /// <summary>Gets the native format context.</summary>
    internal AVFormatContext* Pointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (AVFormatContext*)handle;
    }

    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        fixed (nint* location = &handle)
        {
            ffmpeg.avformat_close_input((AVFormatContext**)location);
        }

        return true;
    }
}
