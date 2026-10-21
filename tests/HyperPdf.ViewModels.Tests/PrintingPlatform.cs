// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Platform;

namespace HyperPdf.ViewModels.Tests;

/// <summary>A desktop with nothing but a print service and, optionally, a recent documents list.</summary>
/// <param name="printer">The print service.</param>
/// <param name="recent">The recent documents, or none.</param>
internal sealed class PrintingPlatform(IPrintService printer, IRecentDocumentStore? recent = null) : IDesktopPlatform
{
    /// <summary>The integration printing is added to.</summary>
    private readonly FallbackPlatform _fallback = new();

    /// <inheritdoc/>
    public string Name => "Test";

    /// <inheritdoc/>
    public IDesktopThemeSource? ThemeSource => null;

    /// <inheritdoc/>
    public IFileManagerLauncher FileManager => _fallback.FileManager;

    /// <inheritdoc/>
    public IRecentDocumentStore RecentDocuments => recent ?? _fallback.RecentDocuments;

    /// <inheritdoc/>
    public IPrintService Printer => printer;

    /// <inheritdoc/>
    public string? GetLaunchActivationToken() => null;

    /// <inheritdoc/>
    public HyperPdf.Core.Speech.IAudioOutput CreateAudioOutput() => new HyperPdf.Core.Speech.NullAudioOutput();

    /// <inheritdoc/>
    public Task<bool> TryForwardAsync(OpenRequest request) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<ISingleInstance?> TryClaimSingleInstanceAsync() => Task.FromResult<ISingleInstance?>(null);
}
