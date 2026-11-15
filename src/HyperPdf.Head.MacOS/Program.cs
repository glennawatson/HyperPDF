// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.Versioning;
using HyperPdf.Core.Settings;
using HyperPdf.Head.MacOS.Cocoa;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Services;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Head.MacOS;

/// <summary>Starts Hyper PDF Viewer on macOS.</summary>
[SupportedOSPlatform("macos")]
internal static class Program
{
    /// <summary>Opens the window, shows the documents named on the command line and runs until the window closes.</summary>
    /// <param name="args">The files or addresses to open.</param>
    [STAThread]
    private static void Main(string[] args)
    {
        // A second launch with files hands them to the viewer that is already running and ends.
        using var instance = SingleInstance.Start(args);
        if (instance is null)
        {
            return;
        }

        var preference = RenderBackendChoices.Choose(new SettingsStore().Load().RenderBackend);
        using var accessibility = new MacAccessibilityBridge();
        using var window = new CocoaWindow(preference, accessibility);
        var context = ViewerSession.UseWindowThread(window);
        using var services = AppServices.CreateDefault(DesktopPlatforms.Detect(), new ViewerRenderHead(window), SpellCheckers.Create);
        using var session = new ViewerSession(window, services, context);
        session.Follow(instance.Requests);
        session.Open(args);
        session.Run();
    }
}
