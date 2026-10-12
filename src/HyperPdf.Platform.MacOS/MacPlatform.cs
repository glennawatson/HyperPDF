// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Speech;
using HyperPdf.Platform.MacOS.Audio;

namespace HyperPdf.Platform.MacOS;

/// <summary>
/// The macOS desktop: the light or dark appearance and accent colour, the Finder, the Dock's recent items, one window per
/// user through a Unix domain socket, printing through CUPS and Preview's print dialog, and sound through CoreAudio.
/// Documents opened from the Finder arrive through Avalonia's activation events.
/// </summary>
[DebuggerDisplay("MacPlatform: {Name}")]
public sealed class MacPlatform : IDesktopPlatform
{
    /// <summary>The application name, used for the socket and the data folder.</summary>
    private const string ApplicationName = "HyperPDF";

    /// <summary>The socket name for this user.</summary>
    private static readonly string InstanceName = PipeSingleInstance.NameFor(ApplicationName);

    /// <summary>Initializes a new instance of the <see cref="MacPlatform"/> class.</summary>
    public MacPlatform() => RecentDocuments = new MacRecentDocumentStore(Path.Combine(DataDirectory, "recent.json"));

    /// <summary>Gets the app's data folder, <c>~/Library/Application Support/HyperPDF</c>.</summary>
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", ApplicationName);

    /// <inheritdoc/>
    public string Name => "macOS";

    /// <inheritdoc/>
    public IDesktopThemeSource? ThemeSource { get; } = new MacThemeSource();

    /// <inheritdoc/>
    public IFileManagerLauncher FileManager { get; } = new FinderFileManagerLauncher();

    /// <inheritdoc/>
    public IRecentDocumentStore RecentDocuments { get; }

    /// <inheritdoc/>
    public IPrintService Printer { get; } = new MacPrintService();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAudioOutput CreateAudioOutput() => new AudioQueueOutput();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? GetLaunchActivationToken() => null;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> TryForwardAsync(OpenRequest request) => PipeSingleInstance.TryForwardAsync(InstanceName, request);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<ISingleInstance?> TryClaimSingleInstanceAsync() => Task.FromResult<ISingleInstance?>(PipeSingleInstance.TryStart(InstanceName));
}
