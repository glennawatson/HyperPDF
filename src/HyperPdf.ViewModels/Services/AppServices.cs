// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Ocr;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Speech;
using HyperPdf.Core.Spelling;
using HyperPdf.Core.Text.Fonts;
using HyperPdf.Core.Theming;
using HyperPdf.Http.Remote;
using HyperPdf.Media;
using HyperPdf.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Services;

/// <summary>The composition root: every long lived service, created once at start up.</summary>
[DebuggerDisplay("AppServices")]
public sealed partial class AppServices : ReactiveObject, IDisposable
{
    /// <summary>Bytes in a megabyte.</summary>
    private const long BytesPerMegabyte = 1024 * 1024;

    /// <summary>The smallest permitted tile cache, in megabytes.</summary>
    private const int MinCacheMegabytes = 32;

    /// <summary>Owns everything the services create, and releases it all in the order it was added.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>The installed fonts offered in this application's text controls.</summary>
    private readonly Lazy<FontCatalog> _fontCatalog;

    /// <summary>The latest desktop palette, if any.</summary>
    private DesktopPalette? _palette;

    /// <summary>The settings the speech engine was made for.</summary>
    private SpeechEngineKey _speechEngineFor;

    /// <summary>The sound output, created when first used.</summary>
    private IAudioOutput? _audio;

    /// <summary>How media plays, made when first needed.</summary>
    private MediaPlaybackFactory? _mediaPlayback;

    /// <summary>The third-party notices, created on first use.</summary>
    private LicenceNotices? _licenceNotices;

    /// <summary>The spell checker, created on first use.</summary>
    private ISpellChecker? _spellChecker;

    /// <summary>The tab reading aloud, so only one speaks at a time.</summary>
    private ReadAloudViewModel? _reader;

    /// <summary>Initializes a new instance of the <see cref="AppServices"/> class.</summary>
    /// <param name="settingsStore">The settings store.</param>
    /// <param name="engine">The document engine.</param>
    /// <param name="platform">The desktop integration.</param>
    /// <param name="head">The UI toolkit's side of rendering.</param>
    public AppServices(SettingsStore settingsStore, IDocumentEngine engine, IDesktopPlatform platform, IRenderHead head)
        : this(settingsStore, engine, platform, head, new Lazy<FontCatalog>(static () => FontCatalog.Scan(FontCatalog.SystemDirectories())))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AppServices"/> class with a shared font catalog.</summary>
    /// <param name="settingsStore">The settings store.</param>
    /// <param name="engine">The document engine.</param>
    /// <param name="platform">The desktop integration.</param>
    /// <param name="head">The UI toolkit's side of rendering.</param>
    /// <param name="catalog">The installed fonts, scanned on first use.</param>
    public AppServices(SettingsStore settingsStore, IDocumentEngine engine, IDesktopPlatform platform, IRenderHead head, Lazy<FontCatalog> catalog)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(catalog);
        _fontCatalog = catalog;
        Head = head;
        Platform = platform;
        SettingsStore = settingsStore;
        Settings = settingsStore.Load();
        SignatureMarkStore = SignatureMarkStore.Beside(settingsStore);
        SignatureMarks = SignatureMarkStore.Load();
        Engine = engine;
        RenderHub = new(Math.Max(MinCacheMegabytes, Settings.TileCacheMegabytes) * BytesPerMegabyte, head);
        Pool = new(engine, Math.Max(1, Settings.MaxOpenDocuments));
        Downloader = new(Path.Combine(Path.GetTempPath(), "hyperpdf-downloads"));
        CurrentTheme = ThemeResolver.Resolve(Settings, null);
        Speech = SpeechSetup.CreateDefault(platform);
        FocusAnnouncements = platform.CreateFocusAnnouncementCheck();
        Theme = this.WhenChanged(static services => services.CurrentTheme);
        _subscriptions.Add(RenderHub);
        _subscriptions.Add(Pool);
        if (ThemeSource is IDisposable themeSource)
        {
            _subscriptions.Add(themeSource);
        }

        _subscriptions.Add(FocusAnnouncements);

        // The speech engine in use is open until settings choose another or the services end.
        _subscriptions.Add(this.WhenChanged(static services => services.SpeechEngine).OwnLatest().SubscribeSafeErrors(ErrorTrace.Write));
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

    /// <summary>Gets the UI toolkit's side of rendering.</summary>
    public IRenderHead Head { get; }

    /// <summary>Gets the screen fonts retained for this application.</summary>
    public PageFontFamilies PageFontFamilies { get; } = new();

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
    public Func<ISpellChecker> CreateSpellChecker { get; init; } = static () => NullSpellChecker.Instance;

    /// <summary>Gets the spell checker for form fields, made on first use.</summary>
    public ISpellChecker SpellChecker => _spellChecker ??= Own(CreateSpellChecker());

    /// <summary>Gets the installed fonts, scanning on first use.</summary>
    public FontCatalog FontCatalog => _fontCatalog.Value;

    /// <summary>Gets the installed fonts if the scan has completed.</summary>
    public FontCatalog? LoadedFontCatalog => _fontCatalog.IsValueCreated ? _fontCatalog.Value : null;

    /// <summary>Gets the sound output shared by every tab.</summary>
    public IAudioOutput Audio => _audio ??= Own(Speech.CreateAudio());

    /// <summary>Gets how media plays on this computer, which every tab shares.</summary>
    public MediaPlaybackFactory MediaPlayback => _mediaPlayback ??= new(Platform.CreateStreamingAudioOutput);

    /// <summary>Gets the resolved theme, re-resolved when the settings or the desktop palette change; the current value is replayed on subscription.</summary>
    public IObservable<ResolvedTheme> Theme { get; }

    /// <summary>
    /// Gets the request to open a document as a tab, for example a signed copy. The window that was made last answers it,
    /// so a document opens once.
    /// </summary>
    public Interaction<string, RxVoid> OpenDocumentInteraction { get; } = new();

    /// <summary>Gets the current resolved theme.</summary>
    [Reactive]
    public partial ResolvedTheme CurrentTheme { get; private set; }

    /// <summary>Gets a number that changes each time user settings are applied, including changes that leave the theme unchanged.</summary>
    [Reactive]
    public partial int SettingsVersion { get; private set; }

    /// <summary>Gets the notices owned by this application lifetime.</summary>
    internal LicenceNotices LicenceNotices => _licenceNotices ??= new(Head.NoticesAssembly);

    /// <summary>Gets the tint over fillable form fields the settings choose.</summary>
    internal FormHighlight FormHighlight => ReadHighlight(this);

    /// <summary>Gets the page sharpness choices the settings hold.</summary>
    internal RenderSharpness Sharpness => Settings.Sharpness;

    /// <summary>Gets the speech engine in use, kept while the settings that chose it stay the same, or <see langword="null"/> before one is needed.</summary>
    [Reactive]
    internal partial ISpeechEngine? SpeechEngine { get; private set; }

    /// <summary>Creates the services for the current platform.</summary>
    /// <param name="platform">The desktop integration for the running operating system.</param>
    /// <param name="head">The UI toolkit's side of rendering.</param>
    /// <param name="createSpellChecker">Makes the spell checker the desktop provides.</param>
    /// <returns>The services.</returns>
    public static AppServices CreateDefault(IDesktopPlatform platform, IRenderHead head, Func<ISpellChecker> createSpellChecker)
    {
        AppServices? services = null;

        var catalog = new Lazy<FontCatalog>(static () => FontCatalog.Scan(FontCatalog.SystemDirectories()));
        var engine = new HighlightedHyperPdfEngine(() => ReadHighlight(services), () => services?.Settings.Sharpness ?? RenderSharpness.Default, catalog);
        services = new(new SettingsStore(), engine, platform, head, catalog) { CreateSpellChecker = createSpellChecker };
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
        var wanted = new SpeechEngineKey(Settings.SpeechEngine, Settings.AzureSpeechKey, Settings.AzureSpeechRegion);
        if (SpeechEngine is not { } engine || _speechEngineFor != wanted)
        {
            // The new engine replaces the one before, which the ownership chain in the constructor then releases.
            engine = Speech.CreateEngine(Settings, Speech.VoiceDirectory);
            SpeechEngine = engine;
            _speechEngineFor = wanted;
        }

        return engine;
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
    public void RequestOpen(string path) => _ = OpenDocumentInteraction.WhenHandled(path).SubscribeSafeErrors(ErrorTrace.Write);

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
        SettingsVersion++;
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
        try
        {
            _subscriptions.Dispose();
        }
        finally
        {
            Downloader.Dispose();
            Ocr.Dispose();
            Speech.Dispose();
        }
    }

    /// <summary>Reads the tint over fillable form fields from the settings.</summary>
    /// <param name="services">The services, or <see langword="null"/> before they exist.</param>
    /// <returns>The tint; the default one before the settings exist.</returns>
    private static FormHighlight ReadHighlight(AppServices? services) => services is null
        ? FormHighlight.Default
        : new(services.Settings.FormHighlightColor, (byte)Math.Clamp(services.Settings.FormHighlightAlpha, 0, byte.MaxValue));

    /// <summary>Resolves the theme again; the reactive property tells subscribers only when the theme changed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RefreshTheme() => CurrentTheme = ThemeResolver.Resolve(Settings, _palette);

    /// <summary>Hands a service to the owner so it is released with the rest, when it has anything to release.</summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="service">The service.</param>
    /// <returns>The same service.</returns>
    private T Own<T>(T service)
    {
        if (service is IDisposable disposable)
        {
            _subscriptions.Add(disposable);
        }

        return service;
    }

    /// <summary>The settings a speech engine was made for.</summary>
    /// <param name="Choice">The engine chosen.</param>
    /// <param name="Key">The Azure speech key.</param>
    /// <param name="Region">The Azure speech region.</param>
    private readonly record struct SpeechEngineKey(SpeechEngineChoice Choice, string Key, string Region);
}
