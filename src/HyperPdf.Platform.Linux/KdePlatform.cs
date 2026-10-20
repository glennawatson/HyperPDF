// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Media;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Speech;
using HyperPdf.Platform.Linux.Accessibility;
using HyperPdf.Platform.Linux.Audio;
using HyperPdf.Platform.Linux.DBus;
using HyperPdf.Platform.Linux.Kde;
using HyperPdf.Platform.Linux.Recent;

namespace HyperPdf.Platform.Linux;

/// <summary>
/// KDE Plasma: colours and font from <c>kdeglobals</c>, Dolphin through <c>org.freedesktop.FileManager1</c>, the shared
/// <c>recently-used.xbel</c>, printing through the XDG portal and a single window claimed on the session bus. The
/// freedesktop parts work on other Linux desktops too.
/// </summary>
[DebuggerDisplay("KdePlatform: {Name}")]
public sealed class KdePlatform : IDesktopPlatform
{
    /// <inheritdoc/>
    public string Name => "KDE Plasma";

    /// <inheritdoc/>
    public IDesktopThemeSource? ThemeSource { get; } = new KdeThemeSource();

    /// <inheritdoc/>
    public IFileManagerLauncher FileManager { get; } = new DBusFileManagerLauncher();

    /// <inheritdoc/>
    public IRecentDocumentStore RecentDocuments { get; } = new XbelRecentDocumentStore();

    /// <inheritdoc/>
    public IPrintService Printer { get; } = new LinuxPrintService();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAudioOutput CreateAudioOutput() => new PulseAudioOutput();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IStreamingAudioOutput CreateStreamingAudioOutput() => new PulseStreamingAudioOutput();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? GetLaunchActivationToken() => SingleInstanceHost.GetLaunchActivationToken();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IFocusAnnouncementCheck CreateFocusAnnouncementCheck() => new AtSpiFocusAnnouncementCheck();

    /// <inheritdoc/>
    public Task<bool> TryForwardAsync(OpenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SingleInstanceHost.TryForwardAsync(request.Uris, request.ActivationToken);
    }

    /// <inheritdoc/>
    public async Task<ISingleInstance?> TryClaimSingleInstanceAsync() => await SingleInstanceHost.TryStartAsync().ConfigureAwait(false);
}
