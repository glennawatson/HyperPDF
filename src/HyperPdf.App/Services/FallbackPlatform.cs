// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Speech;

namespace HyperPdf.App.Services;

/// <summary>
/// A desktop with no integration: the app's own colours, no file manager, no shared recent list, no printing, and
/// every launch its own window. Used on platforms without an implementation, and by tests.
/// </summary>
[DebuggerDisplay("FallbackPlatform: {Name}")]
public sealed class FallbackPlatform : IDesktopPlatform
{
    /// <inheritdoc/>
    public string Name => "None";

    /// <inheritdoc/>
    public IDesktopThemeSource? ThemeSource => null;

    /// <inheritdoc/>
    public IFileManagerLauncher FileManager { get; } = new NullFileManagerLauncher();

    /// <inheritdoc/>
    public IRecentDocumentStore RecentDocuments { get; } = new NullRecentDocumentStore();

    /// <inheritdoc/>
    public IPrintService Printer { get; } = new NullPrintService();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAudioOutput CreateAudioOutput() => new NullAudioOutput();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? GetLaunchActivationToken() => null;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> TryForwardAsync(OpenRequest request) => Task.FromResult(false);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<ISingleInstance?> TryClaimSingleInstanceAsync() => Task.FromResult<ISingleInstance?>(null);
}
