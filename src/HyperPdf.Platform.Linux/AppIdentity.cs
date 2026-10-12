// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Platform.Linux;

/// <summary>Identifiers shared by the desktop entry, D-Bus name and window class.</summary>
public static class AppIdentity
{
    /// <summary>Gets the reverse-DNS application identifier, also the desktop file name and D-Bus name.</summary>
    public static string ApplicationId => "net.glennwatson.HyperPDF";

    /// <summary>Gets the D-Bus object path the application is exported at.</summary>
    public static string ObjectPath => "/net/glennwatson/HyperPDF";

    /// <summary>Gets the X11 window class, matched by <c>StartupWMClass</c> in the desktop file.</summary>
    public static string WindowClass => "hyperpdf";
}
