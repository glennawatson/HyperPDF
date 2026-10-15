// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Speech;
using HyperPdf.Http.Speech;
using HyperPdf.Speech.Kokoro;
using HyperPdf.Speech.Melo;

namespace HyperPdf.App.Services;

/// <summary>How the app reads aloud: where the on-device voice lives, how engines and sound output are made, and how the voice is downloaded.</summary>
/// <param name="VoiceDirectory">The folder holding the on-device voice.</param>
/// <param name="VoiceFilesFor">Gets the files the on-device voice the settings choose needs.</param>
/// <param name="CreateEngine">Creates the engine the settings choose, given the voice folder.</param>
/// <param name="CreateAudio">Creates the sound output.</param>
/// <param name="DownloadVoice">Downloads the missing files of the voice the settings choose into the voice folder, reporting the fraction done.</param>
[DebuggerDisplay("SpeechSetup: {VoiceDirectory}")]
public sealed record SpeechSetup(
    string VoiceDirectory,
    Func<AppSettings, IReadOnlyList<SpeechModelFile>> VoiceFilesFor,
    Func<AppSettings, string, ISpeechEngine> CreateEngine,
    Func<IAudioOutput> CreateAudio,
    Func<AppSettings, IProgress<double>, CancellationToken, Task> DownloadVoice) : IDisposable
{
    /// <summary>Gets the default voice folder, <c>~/.local/share/hyperpdf/voices</c> on Linux.</summary>
    public static string DefaultVoiceDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hyperpdf", "voices");

    /// <summary>Gets the downloads and file facts owned by this setup.</summary>
    private SpeechModelDownloader Downloads { get; init; } = new();

    /// <summary>Creates the default setup: MeloTTS or Kokoro on this computer, or Azure with the person's key, played through the desktop's sound output.</summary>
    /// <param name="platform">The desktop integration, which provides the sound output.</param>
    /// <returns>The setup.</returns>
    public static SpeechSetup CreateDefault(IDesktopPlatform platform)
    {
        var downloads = new SpeechModelDownloader();
        return new(
            DefaultVoiceDirectory,
            FilesFor,
            CreateEngineFor,
            platform.CreateAudioOutput,
            (settings, progress, cancellationToken) => downloads.DownloadAsync(FilesFor(settings), DefaultVoiceDirectory, progress, cancellationToken)) { Downloads = downloads, };
    }

    /// <summary>Gets the files the chosen on-device voice needs: Kokoro's when Kokoro is chosen, otherwise MeloTTS's.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The files.</returns>
    public static IReadOnlyList<SpeechModelFile> FilesFor(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.SpeechEngine == SpeechEngineChoice.Kokoro ? KokoroModel.Files : MeloModel.Files;
    }

    /// <summary>Creates the engine the settings choose.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="voiceDirectory">The on-device voice folder.</param>
    /// <returns>The engine.</returns>
    public static ISpeechEngine CreateEngineFor(AppSettings settings, string voiceDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.SpeechEngine switch
        {
            SpeechEngineChoice.Azure => new AzureSpeechEngine(new(settings.AzureSpeechKey, settings.AzureSpeechRegion)),
            SpeechEngineChoice.Kokoro => new KokoroEngine(voiceDirectory),
            _ => new MeloEngine(voiceDirectory),
        };
    }

    /// <summary>Gets about how many bytes of the chosen voice are still to download.</summary>
    /// <param name="settings">The settings, which choose the voice.</param>
    /// <returns>The approximate size.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long MissingBytesFor(AppSettings settings) => SpeechModelDownloader.TotalBytes(Downloads.Missing(VoiceFilesFor(settings), VoiceDirectory));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Downloads.Dispose();
}
