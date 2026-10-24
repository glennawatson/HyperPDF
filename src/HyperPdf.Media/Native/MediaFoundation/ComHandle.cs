// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Media.Native.MediaFoundation;

/// <summary>Owns one reference to a COM object and lets go of it when disposed, or when the handle is finalized if it never was.</summary>
[DebuggerDisplay("ComHandle: {handle}")]
internal sealed unsafe class ComHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new instance of the <see cref="ComHandle"/> class that owns nothing.</summary>
    internal ComHandle()
        : base(true)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ComHandle"/> class that owns a reference.</summary>
    /// <param name="instance">The COM object, whose reference the handle takes over.</param>
    internal ComHandle(void* instance)
        : base(true) => SetHandle((nint)instance);

    /// <summary>Gets the COM object.</summary>
    internal void* Pointer => (void*)handle;

    /// <inheritdoc/>
    protected override bool ReleaseHandle()
    {
        Com.Release((void*)handle);
        return true;
    }
}
