// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Speech;

namespace HyperPdf.Core.Platform;

/// <summary>
/// Everything HyperPDF asks of the desktop it runs on: its colours, file manager, recent documents, printing and
/// the single running window. KDE Plasma is the first implementation; other desktops add their own.
/// </summary>
public interface IDesktopPlatform
{
    /// <summary>Gets the desktop's name, for diagnostics.</summary>
    string Name { get; }

    /// <summary>Gets the desktop's colour scheme and font, or <see langword="null"/> when it publishes none.</summary>
    IDesktopThemeSource? ThemeSource { get; }

    /// <summary>Gets the file manager launcher.</summary>
    IFileManagerLauncher FileManager { get; }

    /// <summary>Gets the shared list of recently used documents.</summary>
    IRecentDocumentStore RecentDocuments { get; }

    /// <summary>Gets the print service.</summary>
    IPrintService Printer { get; }

    /// <summary>Creates the sound output Read Aloud plays through; dispose it when done.</summary>
    /// <returns>The output, which reports whether sound can be played.</returns>
    IAudioOutput CreateAudioOutput();

    /// <summary>Gets the window activation token the launcher handed this process, if any.</summary>
    /// <returns>The token.</returns>
    string? GetLaunchActivationToken();

    /// <summary>Asks an already running instance to open documents.</summary>
    /// <param name="request">The documents and activation token.</param>
    /// <returns><see langword="true"/> when a running instance took the request, so this process can exit.</returns>
    Task<bool> TryForwardAsync(OpenRequest request);

    /// <summary>Creates the check that keyboard focus changes reach the screen reader; dispose it when done.</summary>
    /// <returns>The check; platforms whose bridge announces every change return one that never acts.</returns>
    IFocusAnnouncementCheck CreateFocusAnnouncementCheck() => NoFocusAnnouncementCheck.Instance;

    /// <summary>Claims the single running window for this process.</summary>
    /// <returns>The claim, or <see langword="null"/> when the desktop has no such mechanism or another process holds it.</returns>
    Task<ISingleInstance?> TryClaimSingleInstanceAsync();
}
