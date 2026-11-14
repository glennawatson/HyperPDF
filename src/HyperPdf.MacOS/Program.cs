// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.Versioning;
using HyperPdf.Head.MacOS;
using HyperPdf.Head.MacOS.Cocoa;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Services;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.MacOS;

/// <summary>Starts Hyper PDF Viewer on macOS.</summary>
[SupportedOSPlatform("macos")]
internal static class Program
{
    /// <summary>The environment variable that picks the render backend: <c>cpu</c> for the software backend, anything else for the GPU.</summary>
    private const string BackendVariable = "HYPERPDF_RENDER_BACKEND";

    /// <summary>Opens the window, shows the documents named on the command line and runs until the window closes.</summary>
    /// <param name="args">The files or addresses to open.</param>
    [STAThread]
    private static void Main(string[] args)
    {
        var preference = string.Equals(Environment.GetEnvironmentVariable(BackendVariable), "cpu", StringComparison.OrdinalIgnoreCase)
            ? RenderBackendPreference.Cpu
            : RenderBackendPreference.Gpu;
        using var accessibility = new MacAccessibilityBridge();
        using var window = new CocoaWindow(preference, accessibility);
        var context = ViewerSession.UseWindowThread(window);
        using var services = AppServices.CreateDefault(DesktopPlatforms.Detect(), new ViewerRenderHead(window), SpellCheckers.Create);
        using var session = new ViewerSession(window, services, context);
        session.Open(args);
        session.Run();
    }
}
