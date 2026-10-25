// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Automation.Peers;
using Avalonia.Controls.ApplicationLifetimes;
using HyperPdf.Core.Diagnostics;
using HyperPdf.Platform.Linux.Accessibility;
using HyperPdf.Platform.Windows.Accessibility;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Accessibility;

/// <summary>
/// Serves the app's elements to screen readers through the app's own bridge: AT-SPI on Linux, so a screen reader gets heading
/// levels, places in a list and the page text under both X11 and Wayland, and UI Automation on Windows. Nothing runs until the
/// system says assistive technology is on, so ordinary use pays nothing.
/// </summary>
[DebuggerDisplay("AccessibilityHost")]
internal sealed class AccessibilityHost : IDisposable
{
    /// <summary>How often the app checks whether assistive technology has been switched on.</summary>
    private static readonly TimeSpan CheckPeriod = TimeSpan.FromSeconds(5);

    /// <summary>How often the snapshot of the elements is rebuilt while a screen reader is in use.</summary>
    private static readonly TimeSpan RefreshPeriod = TimeSpan.FromMilliseconds(200);

    /// <summary>The application's windows.</summary>
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;

    /// <summary>Stops the waiting loop and the server when the app ends.</summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>Owns what the host starts.</summary>
    private readonly MultipleDisposable _lifetime = [];

    /// <summary>Initializes a new instance of the <see cref="AccessibilityHost"/> class.</summary>
    /// <param name="desktop">The application's windows.</param>
    private AccessibilityHost(IClassicDesktopStyleApplicationLifetime desktop) => _desktop = desktop;

    /// <inheritdoc/>
    public void Dispose()
    {
        _stop.Cancel();
        _lifetime.Dispose();
        _stop.Dispose();
    }

    /// <summary>Starts watching for assistive technology.</summary>
    /// <param name="desktop">The application's windows.</param>
    /// <returns>The host, to dispose when the app ends, or <see langword="null"/> on a system that is neither Linux nor Windows.</returns>
    internal static AccessibilityHost? Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsWindows())
        {
            return null;
        }

        var host = new AccessibilityHost(desktop);
        _ = host.RunAsync(host._stop.Token);
        return host;
    }

    /// <summary>Checks whether a screen reader or another assistive technology is on.</summary>
    /// <returns>Whether it is.</returns>
    private static async Task<bool> IsAssistiveTechnologyOnAsync() =>
        OperatingSystem.IsWindows()
            ? WindowsAccessibilityServer.IsAssistiveTechnologyListening()
            : await LinuxAccessibility.IsAssistiveTechnologyEnabledAsync().ConfigureAwait(false);

    /// <summary>Starts the server for the system the app runs on.</summary>
    /// <param name="tree">The elements to serve.</param>
    /// <returns>The server, to dispose when the app ends, or <see langword="null"/> when the system has no accessibility service.</returns>
    private static async Task<IDisposable?> StartServerAsync(AvaloniaAccessibleTree tree) =>
        OperatingSystem.IsWindows()
            ? WindowsAccessibilityServer.Start(tree)
            : await LinuxAccessibility.StartAsync(tree).ConfigureAwait(false);

    /// <summary>Makes the windows answer UI Automation with the app's own elements. Call it on the UI thread, after the tree has the windows.</summary>
    /// <param name="server">The running server.</param>
    /// <param name="peers">The open windows.</param>
    private static void AttachWindows(IDisposable server, List<WindowPeer> peers)
    {
        if (!OperatingSystem.IsWindows() || server is not WindowsAccessibilityServer windows)
        {
            return;
        }

        foreach (var peer in peers)
        {
            if (peer.Number != 0)
            {
                windows.AttachWindow(peer.Number);
            }
        }
    }

    /// <summary>Waits until assistive technology is on, then starts serving elements.</summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>A task that ends once the server is running or the app ends.</returns>
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(CheckPeriod);
            do
            {
                if (await IsAssistiveTechnologyOnAsync().ConfigureAwait(false))
                {
                    await ServeAsync().ConfigureAwait(false);
                    return;
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // The app ended while waiting.
        }
        catch (Exception ex)
        {
            // Nothing awaits this task, so a failure would otherwise vanish and leave screen readers with no tree and no clue why.
            AppLog.Error(ex, "Serving the app's elements to screen readers failed.");
        }
    }

    /// <summary>
    /// Starts the server at once, with only the application element, so a screen reader sees the app as soon as it starts, and then keeps
    /// the windows up to date from the UI thread as it gets to them.
    /// </summary>
    /// <returns>A task that ends once the server is running.</returns>
    private async Task ServeAsync()
    {
        var tree = new AvaloniaAccessibleTree();
        _lifetime.Add(tree);
        var server = await StartServerAsync(tree).ConfigureAwait(false);
        if (server is null)
        {
            return;
        }

        _lifetime.Add(server);

        // The step reads the tree and the server held here; the refresh must run where the peers live, which is the UI thread.
        _lifetime.Add(Signal.Interval(RefreshPeriod, AvaloniaScheduler.Instance).SubscribeSafe(_ => Refresh(tree, server), AppLog.Error));
    }

    /// <summary>
    /// Rebuilds the snapshot from the open windows, and on Windows makes each window answer with the snapshot's elements. A failure is
    /// logged and the next refresh tries again. Call it on the UI thread.
    /// </summary>
    /// <param name="tree">The tree to refresh.</param>
    /// <param name="server">The running server.</param>
    private void Refresh(AvaloniaAccessibleTree tree, IDisposable server)
    {
        try
        {
            var peers = CollectWindows();
            tree.Refresh(peers);
            AttachWindows(server, peers);
        }
        catch (Exception ex)
        {
            // Reading the peers can fail on any control; one bad refresh must not stop the ones after it.
            AppLog.Error(ex, "Reading the app's elements for screen readers failed.");
        }
    }

    /// <summary>Gets the peers of the open windows. Call it on the UI thread.</summary>
    /// <returns>The windows.</returns>
    private List<WindowPeer> CollectWindows()
    {
        var windows = new List<WindowPeer>(_desktop.Windows.Count);
        foreach (var window in _desktop.Windows)
        {
            windows.Add(new(ControlAutomationPeer.CreatePeerForElement(window), window.IsActive, false, (long?)window.TryGetPlatformHandle()?.Handle ?? 0));
        }

        return windows;
    }
}
