// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Head.Windows.Win32;

/// <summary>The Win32 TRACKMOUSEEVENT request.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TrackMouseRequest
{
    /// <summary>Initializes a new instance of the <see cref="TrackMouseRequest"/> struct for the next pointer exit.</summary>
    /// <param name="window">The client window.</param>
    internal unsafe TrackMouseRequest(nint window)
    {
        Size = (uint)sizeof(TrackMouseRequest);
        Flags = 0x02;
        Window = window;
    }

    /// <summary>Gets the native structure size.</summary>
    internal uint Size { get; }

    /// <summary>Gets the requested notifications.</summary>
    internal uint Flags { get; }

    /// <summary>Gets the client window handle.</summary>
    internal nint Window { get; }

    /// <summary>Gets the hover timeout, unused for leave notifications.</summary>
    internal uint HoverTime { get; }
}
