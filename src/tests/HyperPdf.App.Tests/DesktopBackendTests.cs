// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.Services;

namespace HyperPdf.App.Tests;

/// <summary>Tests desktop backend selection without changing the process environment.</summary>
public sealed class DesktopBackendTests
{
    /// <summary>Verifies that Wayland is used only for Linux sessions without an X11 display.</summary>
    /// <param name="isLinux">Whether the session runs on Linux.</param>
    /// <param name="x11Display">The X11 display value.</param>
    /// <param name="waylandDisplay">The Wayland display value.</param>
    /// <param name="expected">Whether native Wayland should be selected.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(true, null, "wayland-0", true)]
    [Arguments(true, "", "wayland-0", true)]
    [Arguments(true, " ", "wayland-0", true)]
    [Arguments(true, ":0", "wayland-0", false)]
    [Arguments(true, ":0", null, false)]
    [Arguments(true, null, null, false)]
    [Arguments(true, null, "", false)]
    [Arguments(true, null, " ", false)]
    [Arguments(false, null, "wayland-0", false)]
    public async Task ShouldUseWayland_SelectsSessionBackend(bool isLinux, string? x11Display, string? waylandDisplay, bool expected) =>
        await Assert.That(DesktopBackend.ShouldUseWayland(isLinux, x11Display, waylandDisplay)).IsEqualTo(expected);
}
