// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Speech;
using HyperPdf.Http.Speech;
using HyperPdf.Speech.Kokoro;
using HyperPdf.Speech.Melo;

namespace HyperPdf.ViewModels.Services;

/// <summary>How the app reads aloud: where the on-device voice lives, how engines and sound output are made, and how the voice is downloaded.</summary>
/// <param name="VoiceDirectory">The folder holding the on-device voice.</param>
/// <param name="VoiceFilesFor">Gets the files the on-device voice the settings choose needs.</param>
/// <param name="CreateEngine">Creates the engine the settings choose, given the voice folder.</param>
/// <param name="CreateAudio">Creates the sound output.</param>
/// <param name="DownloadVoice">Downloads the missing files of the voice the settings choose into the voice folder, reporting the fraction done.</param>
[DebuggerDisplay("SpeechSetup: {VoiceDirectory}")]
public sealed record SpeechSetup(
    string VoiceDirectory,
    Func<AppSettings, SpeechModelFile[]> VoiceFilesFor,
    Func<AppSettings, string, ISpeechEngine> CreateEngine,
    Func<IAudioOutput> CreateAudio,
    Func<AppSettings, IProgress<double>, IObservable<RxVoid>> DownloadVoice) : IDisposable
{
    /// <summary>Gets the default voice folder, <c>~/.local/share/hyperpdf/voices</c> on Linux.</summary>
    public static string DefaultVoiceDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hyperpdf", "voices");

    /// <summary>
    /// Gets every voice a person can pick, whichever engine speaks it: the two engines that run on this computer first, then
    /// Azure. One list means a voice is picked without first picking its engine somewhere else.
    /// </summary>
    public static SpeechVoiceChoice[] AllVoices { get; } = ListAllVoices();

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
            (settings, progress) => downloads.Download([..FilesFor(settings)], DefaultVoiceDirectory, progress))
        { Downloads = downloads, };
    }

    /// <summary>Gets the files the chosen on-device voice needs: Kokoro's when Kokoro is chosen, otherwise MeloTTS's.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The files.</returns>
    public static SpeechModelFile[] FilesFor(AppSettings settings)
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
    public long MissingBytesFor(AppSettings settings) => SpeechModelDownloader.TotalBytes(CollectionsMarshal.AsSpan(Downloads.Missing(VoiceFilesFor(settings), VoiceDirectory)));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Downloads.Dispose();

    /// <summary>Lists the voices of every engine, each with the engine that speaks it.</summary>
    /// <returns>The voices.</returns>
    private static SpeechVoiceChoice[] ListAllVoices()
    {
        var all = new List<SpeechVoiceChoice>();
        AddVoices(all, SpeechEngineChoice.OnDevice, "MeloTTS", MeloModel.Voices);
        AddVoices(all, SpeechEngineChoice.Kokoro, "Kokoro", KokoroModel.Voices);
        AddVoices(all, SpeechEngineChoice.Azure, "Azure", AzureSpeechEngine.AzureVoices);
        return [.. all];
    }

    /// <summary>Adds the voices of one engine.</summary>
    /// <param name="all">Receives the voices.</param>
    /// <param name="engine">The engine.</param>
    /// <param name="name">The engine's name.</param>
    /// <param name="voices">The engine's voices.</param>
    private static void AddVoices(List<SpeechVoiceChoice> all, SpeechEngineChoice engine, string name, SpeechVoice[] voices)
    {
        foreach (var voice in voices)
        {
            all.Add(new(engine, name, voice));
        }
    }
}
