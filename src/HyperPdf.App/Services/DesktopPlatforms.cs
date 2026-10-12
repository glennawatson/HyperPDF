// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Platform;
using HyperPdf.Engine;
using HyperPdf.Platform.Linux;
using HyperPdf.Platform.MacOS;
using HyperPdf.Platform.Windows;

namespace HyperPdf.App.Services;

/// <summary>Chooses the desktop integration for the platform the app runs on.</summary>
public static class DesktopPlatforms
{
    /// <summary>Gets the integration for this platform.</summary>
    /// <returns>Windows, macOS, or KDE Plasma (and the freedesktop parts) on Linux and FreeBSD; otherwise none.</returns>
    public static IDesktopPlatform Detect()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsPlatform(new HyperPdfEngine());
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacPlatform();
        }

        return OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD() ? new KdePlatform() : new FallbackPlatform();
    }
}
