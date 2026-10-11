// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Platform;
using PdfViewerLite.Platform.Linux;
using ReactiveUI.Avalonia;

namespace PdfViewerLite.App;

/// <summary>The application entry point.</summary>
public static class Program
{
    /// <summary>The argument that skips forwarding to a running instance.</summary>
    private const string NewInstanceArgument = "--new-instance";

    /// <summary>Gets the desktop integration.</summary>
    internal static IDesktopPlatform Platform { get; private set; } = new FallbackPlatform();

    /// <summary>Gets the single running window claim, when this process holds it.</summary>
    internal static ISingleInstance? InstanceHost { get; private set; }

    /// <summary>Gets the documents passed on the command line, as absolute paths or URIs.</summary>
    internal static IReadOnlyList<string> StartupDocuments { get; private set; } = [];

    /// <summary>Gets whether startup must forward to, or claim, the one running viewer.</summary>
    internal static bool NeedsSingleInstancePreflight { get; private set; }

    /// <summary>Starts the application, or hands the documents to an already running instance.</summary>
    /// <param name="args">Files or URIs to open.</param>
    /// <returns>The exit code.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (Array.IndexOf(args, TextRecognitionCheck.Argument) >= 0)
        {
            return TextRecognitionCheck.Run(Console.Out);
        }

        var newInstance = Array.IndexOf(args, NewInstanceArgument) >= 0;
        StartupDocuments = NormalizeArguments(args);
        Platform = DesktopPlatforms.Detect();
        NeedsSingleInstancePreflight = !newInstance;

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            (Avalonia.Application.Current as App)?.Release();
            InstanceHost?.Dispose();
        }
    }

    /// <summary>Builds the Avalonia application. Also used by the designer.</summary>
    /// <returns>The application builder.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AppBuilder BuildAvaloniaApp() =>
        BuildAvaloniaApp(
            OperatingSystem.IsLinux(),
            Environment.GetEnvironmentVariable("DISPLAY"),
            Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

    /// <summary>Builds the application for the supplied desktop session.</summary>
    /// <param name="isLinux">Whether the app runs on Linux.</param>
    /// <param name="x11Display">The DISPLAY environment value.</param>
    /// <param name="waylandDisplay">The WAYLAND_DISPLAY environment value.</param>
    /// <returns>The application builder with the selected desktop backend.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static AppBuilder BuildAvaloniaApp(bool isLinux, string? x11Display, string? waylandDisplay) =>
        AppBuilder.Configure<App>()
            .UseDesktopPlatform(isLinux, x11Display, waylandDisplay)
            .With(new X11PlatformOptions { WmClass = AppIdentity.WindowClass, RenderingMode = [X11RenderingMode.Vulkan, X11RenderingMode.Egl, X11RenderingMode.Glx, X11RenderingMode.Software] })
            .LogToTrace()
            .UseReactiveUI(static _ => { });

    /// <summary>Forwards startup documents to a running viewer, or claims its single-instance endpoint.</summary>
    /// <returns>Whether this process should show its own window.</returns>
    internal static async Task<bool> StartSingleInstanceAsync()
    {
        if (await Platform.TryForwardAsync(new(StartupDocuments, Platform.GetLaunchActivationToken())).ConfigureAwait(false))
        {
            return false;
        }

        InstanceHost = await Platform.TryClaimSingleInstanceAsync().ConfigureAwait(false);
        return true;
    }

    /// <summary>Turns command line arguments into absolute paths or URIs, dropping options.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The documents.</returns>
    private static List<string> NormalizeArguments(string[] args)
    {
        var documents = new List<string>(args.Length);
        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg) || arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            documents.Add(arg.Contains("://", StringComparison.Ordinal) ? arg : Path.GetFullPath(arg));
        }

        return documents;
    }
}
