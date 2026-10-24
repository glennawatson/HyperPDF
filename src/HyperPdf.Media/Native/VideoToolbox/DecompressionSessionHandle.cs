// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Media.Native.VideoToolbox;

/// <summary>Owns a VideoToolbox decompression session and ends it when disposed, or when the handle is finalized if it never was.</summary>
[DebuggerDisplay("DecompressionSessionHandle: {handle}")]
internal sealed unsafe class DecompressionSessionHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new instance of the <see cref="DecompressionSessionHandle"/> class that owns nothing.</summary>
    internal DecompressionSessionHandle()
        : base(true)
    {
    }

    /// <summary>Gets the session.</summary>
    internal void* Pointer => (void*)handle;

    /// <summary>Takes over a session.</summary>
    /// <param name="session">The session, whose reference the handle takes over.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Adopt(void* session) => SetHandle((nint)session);

    /// <inheritdoc/>
    protected override bool ReleaseHandle()
    {
        NativeMethods.VTDecompressionSessionInvalidate((void*)handle);
        NativeMethods.CFRelease((void*)handle);
        return true;
    }
}
