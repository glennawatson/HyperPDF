// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.Head.MacOS;
using HyperPdf.Head.MacOS.Cocoa;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Accessibility;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.CocoaWindowRun;

/// <summary>
/// Opens a document in the viewer inside a real Cocoa window and reports what happened, one line for each step. AppKit only
/// works on a process's main thread, so this runs as its own process instead of inside a test.
/// </summary>
internal static class Program
{
    /// <summary>The text put on the pasteboard and read back.</summary>
    private const string ClipboardSample = "HyperPDF pasteboard check";

    /// <summary>The longest the viewer may take to load and draw the document, in seconds.</summary>
    private const double LoadSeconds = 90;

    /// <summary>The longest one wait for window events, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The frames with no new tile to ask for that show the document is fully drawn.</summary>
    private const int SettledFrames = 3;

    /// <summary>Runs the check.</summary>
    /// <param name="args">The document to open and a folder for the settings.</param>
    /// <returns>0 when every step worked, 1 when one did not.</returns>
    [STAThread]
    private static int Main(string[] args) => Check(args, Console.Out);

    /// <summary>Checks the arguments and the operating system, then runs the window.</summary>
    /// <param name="args">The document to open and a folder for the settings.</param>
    /// <param name="report">Receives one line for each step.</param>
    /// <returns>0 when every step worked, 1 when one did not.</returns>
    private static int Check(string[] args, TextWriter report)
    {
        if (!OperatingSystem.IsMacOS())
        {
            report.WriteLine("skipped: Cocoa windows only run on macOS");
            return 0;
        }

        if (args.Length <= 1)
        {
            report.WriteLine("usage: <document> <settings folder>");
            return 1;
        }

        return Run(args[0], args[1], report);
    }

    /// <summary>Opens the window, runs the viewer in it and reports each step.</summary>
    /// <param name="document">The document to open.</param>
    /// <param name="folder">A folder for the settings.</param>
    /// <param name="report">Receives one line for each step.</param>
    /// <returns>0 when every step worked.</returns>
    [SupportedOSPlatform("macos")]
    private static int Run(string document, string folder, TextWriter report)
    {
        using var accessibility = new MacAccessibilityBridge();
        using var window = new CocoaWindow(RenderBackendPreference.Gpu, accessibility);
        report.WriteLine($"window: {window.ClientSize.Width} x {window.ClientSize.Height}");
        report.WriteLine($"backend: {window.Backend.GetType().Name}");
        window.SetTitle("HyperPDF Cocoa window run");
        window.SetClipboardText(ClipboardSample);
        var copied = string.Equals(ReadPasteboard(), ClipboardSample, StringComparison.Ordinal);
        report.WriteLine($"clipboard: {(copied ? "ok" : "mismatch")}");

        var context = ViewerSession.UseWindowThread(window);
        using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
        using var session = new ViewerSession(window, services, context);
        session.Open([document]);
        window.Show();
        var drawn = PumpUntilDrawn(session);
        report.WriteLine($"viewer: {(drawn ? "drawn" : "timed out")}, pages {session.Main.SelectedTab?.PageCount ?? 0}, controls {session.Chrome.Bars.Count}");

        // The first question from VoiceOver makes the viewer build its tree, which the next turn of the loop does.
        _ = session.Accessibility.Get(session.Accessibility.RootId);
        _ = session.PumpOnce(PumpMilliseconds);
        var read = session.Accessibility.Get(ViewerNodes.DocumentId) is not null;
        report.WriteLine($"accessibility: {(read ? "document published" : "no document")}");
        var worked = copied && drawn && read;

        // The last line is the verdict the test reads: an exit code alone cannot tell a failed step from a crash.
        report.WriteLine(worked ? "result: ok" : "result: failed");
        return worked ? 0 : 1;
    }

    /// <summary>Turns the viewer's loop and draws frames until the document is loaded and several frames in a row need no new tile.</summary>
    /// <param name="session">The viewer session.</param>
    /// <returns><see langword="true"/> when the document was drawn in time.</returns>
    private static bool PumpUntilDrawn(ViewerSession session)
    {
        var started = Stopwatch.GetTimestamp();
        var settled = 0;
        while (settled < SettledFrames)
        {
            if (Stopwatch.GetElapsedTime(started).TotalSeconds > LoadSeconds)
            {
                return false;
            }

            _ = session.PumpOnce(PumpMilliseconds);
            session.Canvas.DrawFrame();
            var loaded = session.Main.SelectedTab is { IsLoaded: true } && session.Canvas.State.Layout.PageCount > 0;
            settled = loaded && session.Canvas.State.Render.RequestsThisFrame == 0 ? settled + 1 : 0;
        }

        return true;
    }

    /// <summary>Reads the text on the general pasteboard, as another application does.</summary>
    /// <returns>The text, or <see langword="null"/> when the pasteboard holds none.</returns>
    [SupportedOSPlatform("macos")]
    private static string? ReadPasteboard()
    {
        var pool = NativeMethods.AutoreleasePoolPush();
        var pasteboard = NativeMethods.Send(NativeMethods.GetClass("NSPasteboard"), Selectors.GeneralPasteboard);
        var text = NativeMethods.Send(pasteboard, NativeMethods.Selector("stringForType:"), Cocoa.Text("public.utf8-plain-text"));
        var read = text == 0 ? null : Marshal.PtrToStringUTF8(NativeMethods.Send(text, Selectors.Utf8String));
        NativeMethods.AutoreleasePoolPop(pool);
        return read;
    }
}
