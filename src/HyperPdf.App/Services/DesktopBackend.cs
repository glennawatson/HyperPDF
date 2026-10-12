// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.App.Services;

/// <summary>Selects native Wayland when a Linux session has no X11 display.</summary>
internal static class DesktopBackend
{
    /// <summary>Checks whether the session needs the native Wayland backend.</summary>
    /// <param name="isLinux">Whether the app runs on Linux.</param>
    /// <param name="x11Display">The DISPLAY environment value.</param>
    /// <param name="waylandDisplay">The WAYLAND_DISPLAY environment value.</param>
    /// <returns>True when Linux exposes only a Wayland display.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool ShouldUseWayland(bool isLinux, string? x11Display, string? waylandDisplay) =>
        isLinux && string.IsNullOrWhiteSpace(x11Display) && !string.IsNullOrWhiteSpace(waylandDisplay);
}
