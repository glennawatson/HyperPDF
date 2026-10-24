// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Media.Native.VideoToolbox;

/// <summary>Owns one reference to a CoreFoundation object (a format description, a buffer, a dictionary) and lets go of it when disposed, or when the handle is finalized if it never was.</summary>
[DebuggerDisplay("CoreFoundationHandle: {handle}")]
internal sealed unsafe class CoreFoundationHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new instance of the <see cref="CoreFoundationHandle"/> class that owns nothing.</summary>
    internal CoreFoundationHandle()
        : base(true)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CoreFoundationHandle"/> class that owns a reference.</summary>
    /// <param name="instance">The object, whose reference the handle takes over.</param>
    internal CoreFoundationHandle(void* instance)
        : base(true) => SetHandle((nint)instance);

    /// <summary>Gets the object.</summary>
    internal void* Pointer => (void*)handle;

    /// <inheritdoc/>
    protected override bool ReleaseHandle()
    {
        NativeMethods.CFRelease((void*)handle);
        return true;
    }
}
