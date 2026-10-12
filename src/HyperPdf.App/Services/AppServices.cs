// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.App.Rendering;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Ocr;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Speech;
using HyperPdf.Core.Spelling;
using HyperPdf.Core.Theming;
using HyperPdf.Http.Remote;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Services;

/// <summary>The composition root: every long lived service, created once at start up.</summary>
[DebuggerDisplay("AppServices")]
public sealed class AppServices : IDisposable
{
    /// <summary>Bytes in a megabyte.</summary>
    private const long BytesPerMegabyte = 1024 * 1024;

    /// <summary>The smallest permitted tile cache, in megabytes.</summary>
    private const int MinCacheMegabytes = 32;

    /// <summary>The resolved theme, re-published when the settings or the desktop palette change.</summary>
    private readonly BehaviorSignal<ResolvedTheme> _theme;

    /// <summary>Documents the app is asked to open as tabs, for example a signed copy.</summary>
    private readonly Signal<string> _openRequests = new();

    /// <summary>Emits when user settings are applied, including changes that leave the theme unchanged.</summary>
    private readonly Signal<RxVoid> _settingsApplied = new();

    /// <summary>The latest desktop palette, if any.</summary>
    private DesktopPalette? _palette;

    /// <summary>The speech engine, kept while the settings that chose it stay the same.</summary>
    private ISpeechEngine? _speechEngine;

    /// <summary>The settings the speech engine was made for.</summary>
    private (SpeechEngineChoice Choice, string Key, string Region) _speechEngineFor;

    /// <summary>The sound output, created when first used.</summary>
    private IAudioOutput? _audio;

    /// <summary>The spell checker, created on first use.</summary>
    private ISpellChecker? _spellChecker;

    /// <summary>The tab reading aloud, so only one speaks at a time.</summary>
    private ReadAloudViewModel? _reader;

    /// <summary>Initializes a new instance of the <see cref="AppServices"/> class.</summary>
    /// <param name="settingsStore">The settings store.</param>
    /// <param name="engine">The document engine.</param>
    /// <param name="platform">The desktop integration.</param>
    public AppServices(SettingsStore settingsStore, IDocumentEngine engine, IDesktopPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(platform);
        Platform = platform;
        SettingsStore = settingsStore;
        Settings = settingsStore.Load();
        SignatureMarkStore = SignatureMarkStore.Beside(settingsStore);
        SignatureMarks = SignatureMarkStore.Load();
        Engine = engine;
        Pool = new(engine, Math.Max(1, Settings.MaxOpenDocuments));
        RenderHub = new(Math.Max(MinCacheMegabytes, Settings.TileCacheMegabytes) * BytesPerMegabyte);
        Downloader = new(Path.Combine(Path.GetTempPath(), "hyperpdf-downloads"));
        _theme = new(ThemeResolver.Resolve(Settings, null));
        Speech = SpeechSetup.CreateDefault(platform);
        FocusAnnouncements = platform.CreateFocusAnnouncementCheck();
        Theme = new(_theme);
        OpenRequests = new(_openRequests);
        SettingsApplied = new(_settingsApplied);
    }

    /// <summary>Gets the settings store.</summary>
    public SettingsStore SettingsStore { get; }

    /// <summary>Gets the user settings.</summary>
    public AppSettings Settings { get; }

    /// <summary>Gets the store for the remembered signature and initials.</summary>
    public SignatureMarkStore SignatureMarkStore { get; }

    /// <summary>Gets the signature and initials the user chose to remember.</summary>
    public SavedSignatureMarks SignatureMarks { get; }

    /// <summary>Gets the document engine, for documents outside the pool such as print previews.</summary>
    public IDocumentEngine Engine { get; }

    /// <summary>Gets the document pool.</summary>
    public DocumentPool Pool { get; }

    /// <summary>Gets the render hub.</summary>
    public RenderHub RenderHub { get; }

    /// <summary>Gets the desktop integration.</summary>
    public IDesktopPlatform Platform { get; }

    /// <summary>Gets the check that keyboard focus changes reach the screen reader.</summary>
    public IFocusAnnouncementCheck FocusAnnouncements { get; }

    /// <summary>Gets the recent documents store.</summary>
    public IRecentDocumentStore RecentDocuments => Platform.RecentDocuments;

    /// <summary>Gets the file manager launcher.</summary>
    public IFileManagerLauncher FileManager => Platform.FileManager;

    /// <summary>Gets the desktop theme source, if any.</summary>
    public IDesktopThemeSource? ThemeSource => Platform.ThemeSource;

    /// <summary>Gets the remote document downloader.</summary>
    public RemoteDocumentDownloader Downloader { get; }

    /// <summary>Gets how the app reads aloud; tests replace it with fakes.</summary>
    public SpeechSetup Speech { get; init; }

    /// <summary>Gets how the app recognises text and fetches language packs; tests replace it with fakes.</summary>
    public OcrSetup Ocr { get; init; } = OcrSetup.CreateDefault();

    /// <summary>Gets how the spell checker is made; tests replace it with fakes.</summary>
    public Func<ISpellChecker> CreateSpellChecker { get; init; } = SpellCheckers.Create;

    /// <summary>Gets the spell checker for form fields, made on first use.</summary>
    public ISpellChecker SpellChecker => _spellChecker ??= CreateSpellChecker();

    /// <summary>Gets the sound output shared by every tab.</summary>
    public IAudioOutput Audio => _audio ??= Speech.CreateAudio();

    /// <summary>Gets the resolved theme; the current value is replayed on subscription.</summary>
    public AsObservableSignal<ResolvedTheme> Theme{ get; }

    /// <summary>Gets the documents the app is asked to open as tabs.</summary>
    public AsObservableSignal<string> OpenRequests{ get; }

    /// <summary>Gets the current resolved theme.</summary>
    public ResolvedTheme CurrentTheme => _theme.Value;

    /// <summary>Gets notifications when user settings are applied.</summary>
    internal AsObservableSignal<RxVoid> SettingsApplied{ get; }

    /// <summary>Creates the services for the current platform.</summary>
    /// <param name="platform">The desktop integration, normally from <see cref="DesktopPlatforms.Detect"/>.</param>
    /// <returns>The services.</returns>
    public static AppServices CreateDefault(IDesktopPlatform platform)
    {
        AppServices? services = null;

        var engine = new HighlightedHyperPdfEngine(() => ReadHighlight(services));
        services = new(new SettingsStore(), engine, platform);
        return services;
    }

    /// <summary>Creates a text recogniser for the configured languages, using downloaded packs first; dispose it when done.</summary>
    /// <returns>The recogniser, which reports whether Tesseract and the language data were found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IOcrEngine CreateOcrEngine() => Ocr.CreateEngine(OcrLanguageCatalog.Format(OcrLanguageCatalog.Parse(Settings.OcrLanguage)), Ocr.LanguageDirectory);

    /// <summary>Gets the speech engine the settings choose, reusing it while they stay the same.</summary>
    /// <returns>The engine.</returns>
    public ISpeechEngine GetSpeechEngine()
    {
        var wanted = (Settings.SpeechEngine, Settings.AzureSpeechKey, Settings.AzureSpeechRegion);
        if (_speechEngine is null || _speechEngineFor != wanted)
        {
            _speechEngine?.Dispose();
            _speechEngine = Speech.CreateEngine(Settings, Speech.VoiceDirectory);
            _speechEngineFor = wanted;
        }

        return _speechEngine;
    }

    /// <summary>Records which tab is reading aloud, pausing the one that was.</summary>
    /// <param name="reader">The tab's reader.</param>
    public void ClaimSpeech(ReadAloudViewModel reader)
    {
        if (_reader is not null && !ReferenceEquals(_reader, reader))
        {
            _reader.Pause();
        }

        _reader = reader;
    }

    /// <summary>Asks the window to open a document as a tab.</summary>
    /// <param name="path">The document.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RequestOpen(string path) => _openRequests.OnNext(path);

    /// <summary>Records a new desktop palette and re-resolves the theme. Call on the UI thread.</summary>
    /// <param name="palette">The palette, or <see langword="null"/> when the desktop has none.</param>
    public void SetDesktopPalette(DesktopPalette? palette)
    {
        _palette = palette;
        RefreshTheme();
    }

    /// <summary>Re-resolves the theme after <see cref="Settings"/> changed and saves the settings. Call on the UI thread.</summary>
    public void ApplySettings()
    {
        RefreshTheme();
        _settingsApplied.OnNext(RxVoid.Default);
        SaveSettings();
    }

    /// <summary>Saves the settings, ignoring IO failures.</summary>
    public void SaveSettings()
    {
        try
        {
            SettingsStore.Save(Settings);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Could not save settings: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Could not save settings: {ex.Message}");
        }
    }

    /// <summary>Saves the remembered signature and initials, ignoring IO failures.</summary>
    public void SaveSignatureMarks()
    {
        try
        {
            SignatureMarkStore.Save(SignatureMarks);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Could not save signatures: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Could not save signatures: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _theme.Dispose();
        _openRequests.Dispose();
        _settingsApplied.Dispose();
        RenderHub.Dispose();
        Pool.Dispose();
        (ThemeSource as IDisposable)?.Dispose();
        _speechEngine?.Dispose();
        _audio?.Dispose();
        (_spellChecker as IDisposable)?.Dispose();
        FocusAnnouncements.Dispose();
    }

    /// <summary>Reads the tint over fillable form fields from the settings.</summary>
    /// <param name="services">The services, or <see langword="null"/> before they exist.</param>
    /// <returns>The tint; the default one before the settings exist.</returns>
    private static FormHighlight ReadHighlight(AppServices? services) => services is null
        ? FormHighlight.Default
        : new(services.Settings.FormHighlightColor, (byte)Math.Clamp(services.Settings.FormHighlightAlpha, 0, byte.MaxValue));

    /// <summary>Publishes the theme when it changed.</summary>
    private void RefreshTheme()
    {
        var theme = ThemeResolver.Resolve(Settings, _palette);
        if (theme != _theme.Value)
        {
            _theme.OnNext(theme);
        }
    }
}
