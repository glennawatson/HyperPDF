// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Head.Linux;

/// <summary>The window systems the Linux head can use.</summary>
public enum LinuxWindowSystem
{
    /// <summary>Wayland, the preferred system, used whenever a compositor is reachable.</summary>
    Wayland = 0,

    /// <summary>X11, the fallback for sessions with no Wayland compositor.</summary>
    X11 = 1,
}
