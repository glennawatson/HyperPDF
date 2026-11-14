// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Head.Linux.X11;

/// <summary>Owns an Xlib display connection and closes it when released.</summary>
internal sealed class X11DisplayHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new instance of the <see cref="X11DisplayHandle"/> class.</summary>
    public X11DisplayHandle()
        : base(true)
    {
    }

    /// <inheritdoc/>
    protected override bool ReleaseHandle() => NativeMethods.XCloseDisplay(handle) >= 0;
}
