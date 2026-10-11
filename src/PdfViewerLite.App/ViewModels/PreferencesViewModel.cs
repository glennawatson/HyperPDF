// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Layout;
using PdfViewerLite.Core.Settings;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// The comfort settings (docs/COMFORT.md, rule 12). Each choice applies at once and is saved; the list indexes match the
/// explicit values of the settings enums.
/// </summary>
[DebuggerDisplay("PreferencesViewModel: Preferences")]
public sealed partial class PreferencesViewModel : ReactiveObject, IDisposable
{
    /// <summary>The label of choices that follow the desktop setting.</summary>
    private const string FollowDesktop = "Follow the desktop";

    /// <summary>The interface text sizes offered, in points; index 0 follows the desktop.</summary>
    private static readonly double[] FontSizes = [0, 9, 10, 11, 12, 14, 16];

    /// <summary>The engines in the order they are offered: MeloTTS first, as the default.</summary>
    private static readonly SpeechEngineChoice[] SpeechEngineChoices = [SpeechEngineChoice.OnDevice, SpeechEngineChoice.Kokoro, SpeechEngineChoice.Azure];

    /// <summary>The services.</summary>
    private readonly AppServices _services;

    /// <summary>Subscriptions writing each choice to the settings.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Initializes a new instance of the <see cref="PreferencesViewModel"/> class.</summary>
    /// <param name="services">The application services.</param>
    public PreferencesViewModel(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        OcrLanguages = new(services);
        Load();
        Follow();
    }

    /// <summary>Gets the colour scheme choices.</summary>
    public static IReadOnlyList<string> ColorSchemeOptions { get; } = [FollowDesktop, "Calm", "High contrast", "Dark", "Light"];

    /// <summary>Gets the page colour choices.</summary>
    public static IReadOnlyList<string> PageToneOptions { get; } = ["Match the colour scheme", "White", "Soft paper", "Calm night", "Dark", FollowDesktop];

    /// <summary>Gets the opening zoom choices in enum order.</summary>
    public static IReadOnlyList<string> OpeningZoomOptions { get; } = ["100%", "Fit width", "Fit page"];

    /// <summary>Gets the tool bar choices.</summary>
    public static IReadOnlyList<string> ToolbarOptions { get; } = ["Text beside icons", "Icons only"];

    /// <summary>Gets the file change choices.</summary>
    public static IReadOnlyList<string> FileChangeOptions { get; } = ["Reload automatically", "Ask before reloading"];

    /// <summary>Gets the motion choices.</summary>
    public static IReadOnlyList<string> MotionOptions { get; } = [FollowDesktop, "Reduced", "Normal"];

    /// <summary>Gets the text cursor choices.</summary>
    public static IReadOnlyList<string> CaretOptions { get; } = [FollowDesktop, "Steady", "Blinking"];

    /// <summary>Gets the interface text size choices.</summary>
    public static IReadOnlyList<string> FontSizeOptions { get; } = [FollowDesktop, "9 pt", "10 pt", "11 pt", "12 pt", "14 pt", "16 pt"];

    /// <summary>Gets the Read Aloud voice choices, in the order of <see cref="SpeechEngineChoices"/>.</summary>
    public static IReadOnlyList<string> SpeechEngineOptions { get; } =
    [
        "On this computer: MeloTTS, natural and private (Australian, British, American, Indian)",
        "On this computer: Kokoro (American, British)",
        "Azure AI Speech with your own key",
    ];

    /// <summary>Gets the text recognition languages: which are used, and their packs to download or remove.</summary>
    public OcrLanguagesViewModel OcrLanguages { get; }

    /// <summary>Gets or sets the Read Aloud engine index.</summary>
    [Reactive(nameof(UsesAzure))]
    public partial int SpeechEngine { get; set; }

    /// <summary>Gets a value indicating whether Azure AI Speech is chosen, which shows its key and region.</summary>
    public bool UsesAzure => _services.Settings.SpeechEngine == SpeechEngineChoice.Azure;

    /// <summary>Gets or sets the Azure Speech key.</summary>
    [Reactive]
    public partial string AzureKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the timestamp server used when signing with a certificate; empty for none.</summary>
    [Reactive]
    public partial string TimestampServer { get; set; } = string.Empty;

    /// <summary>Gets or sets the name recorded as the author of new comments; empty uses the computer's user name.</summary>
    [Reactive]
    public partial string CommentAuthor { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether a document opens again at the page last read in it.</summary>
    [Reactive]
    public partial bool ReopenAtLastPage { get; set; }

    /// <summary>Gets or sets a value indicating whether form fields underline misspelled words.</summary>
    [Reactive]
    public partial bool CheckSpelling { get; set; }

    /// <summary>Gets or sets a value indicating whether fonts that cannot be saved in a PDF are offered for typing, shown on screen only.</summary>
    [Reactive]
    public partial bool ShowPreviewOnlyFonts { get; set; }

    /// <summary>Gets or sets the Azure Speech region.</summary>
    [Reactive]
    public partial string AzureRegion { get; set; } = string.Empty;

    /// <summary>Gets or sets the colour scheme index.</summary>
    [Reactive]
    public partial int ColorScheme { get; set; }

    /// <summary>Gets or sets the page colour index.</summary>
    [Reactive]
    public partial int PageTone { get; set; }

    /// <summary>Gets or sets the tool bar style index.</summary>
    [Reactive]
    public partial int Toolbar { get; set; }

    /// <summary>Gets or sets the zoom mode used for newly opened documents.</summary>
    [Reactive]
    public partial int OpeningZoom { get; set; }

    /// <summary>Gets or sets the file change action index.</summary>
    [Reactive]
    public partial int FileChange { get; set; }

    /// <summary>Gets or sets the motion index.</summary>
    [Reactive]
    public partial int Motion { get; set; }

    /// <summary>Gets or sets the text cursor index.</summary>
    [Reactive]
    public partial int Caret { get; set; }

    /// <summary>Gets or sets the interface text size index.</summary>
    [Reactive]
    public partial int FontSize { get; set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _subscriptions.Dispose();
        OcrLanguages.Dispose();
    }

    /// <summary>Asks the window to close; the choices are already applied.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Close()
    {
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Loads each choice from the settings.</summary>
    private void Load()
    {
        var settings = _services.Settings;
        SpeechEngine = Math.Max(0, Array.IndexOf(SpeechEngineChoices, settings.SpeechEngine));
        AzureKey = settings.AzureSpeechKey;
        TimestampServer = settings.TimestampServer;
        CommentAuthor = settings.CommentAuthor;
        ReopenAtLastPage = settings.ReopenAtLastPage;
        CheckSpelling = settings.CheckSpelling;
        ShowPreviewOnlyFonts = settings.ShowPreviewOnlyFonts;
        AzureRegion = settings.AzureSpeechRegion;
        ColorScheme = (int)settings.ColorScheme;
        PageTone = (int)settings.PageTone;
        Toolbar = (int)settings.ToolbarStyle;
        OpeningZoom = (int)settings.DefaultZoomMode;
        FileChange = (int)settings.FileChangeAction;
        Motion = (int)settings.Motion;
        Caret = (int)settings.Caret;
        FontSize = settings.InterfaceFontSizePoints is { } points ? Math.Max(0, Array.IndexOf(FontSizes, points)) : 0;
    }

    /// <summary>Writes later changes of the reading and commenting choices to the settings.</summary>
    /// <param name="settings">The settings.</param>
    private void FollowReading(AppSettings settings)
    {
        Follow(this.WhenChanged(static x => x.CommentAuthor), author => settings.CommentAuthor = author);
        Follow(this.WhenChanged(static x => x.ReopenAtLastPage), reopen => settings.ReopenAtLastPage = reopen);
        Follow(this.WhenChanged(static x => x.CheckSpelling), check => settings.CheckSpelling = check);
        Follow(this.WhenChanged(static x => x.ShowPreviewOnlyFonts), show => settings.ShowPreviewOnlyFonts = show);
    }

    /// <summary>Writes each later change of a choice to the settings, re-themes and saves.</summary>
    private void Follow()
    {
        var settings = _services.Settings;
        Follow(this.WhenChanged(static x => x.SpeechEngine), index =>
        {
            var clamped = Math.Clamp(index, 0, SpeechEngineChoices.Length - 1);
            settings.SpeechEngine = SpeechEngineChoices[clamped];
            if (clamped != index)
            {
                SpeechEngine = clamped;
            }
        });
        Follow(this.WhenChanged(static x => x.AzureKey), key =>
        {
            var trimmed = key.Trim();
            settings.AzureSpeechKey = trimmed;
            AzureKey = trimmed;
        });
        Follow(this.WhenChanged(static x => x.TimestampServer), server =>
        {
            var trimmed = server.Trim();
            settings.TimestampServer = trimmed;
            TimestampServer = trimmed;
        });
        FollowReading(settings);
        Follow(this.WhenChanged(static x => x.AzureRegion), region =>
        {
            var trimmed = region.Trim().ToLowerInvariant();
            settings.AzureSpeechRegion = trimmed;
            AzureRegion = trimmed;
        });
        Follow(this.WhenChanged(static x => x.ColorScheme), index => settings.ColorScheme = (ColorSchemeChoice)index);
        Follow(this.WhenChanged(static x => x.PageTone), index =>
        {
            settings.PageTone = (PageToneChoice)index;
            settings.PageToneEnabled = true;
        });
        Follow(this.WhenChanged(static x => x.Toolbar), index => settings.ToolbarStyle = (ToolbarStyle)index);
        Follow(this.WhenChanged(static x => x.OpeningZoom), index =>
        {
            var clamped = Math.Clamp(index, 0, OpeningZoomOptions.Count - 1);
            settings.DefaultZoomMode = (ZoomMode)clamped;
            if (clamped != index)
            {
                OpeningZoom = clamped;
            }
        });
        Follow(this.WhenChanged(static x => x.FileChange), index => settings.FileChangeAction = (FileChangeAction)index);
        Follow(this.WhenChanged(static x => x.Motion), index => settings.Motion = (MotionPreference)index);
        Follow(this.WhenChanged(static x => x.Caret), index => settings.Caret = (CaretPreference)index);
        Follow(this.WhenChanged(static x => x.FontSize), index =>
        {
            var known = index > 0 && index < FontSizes.Length;
            settings.InterfaceFontSizePoints = known ? FontSizes[index] : null;
            if (!known && index != 0)
            {
                FontSize = 0;
            }
        });
    }

    /// <summary>Applies each later value of a choice, then re-themes and saves.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="source">The choice's changes; the first value is the loaded one and is skipped.</param>
    /// <param name="change">Writes the value to the settings.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Follow<T>(IObservable<T> source, Action<T> change) =>
        _subscriptions.Add(source.Skip(1).SubscribeSafe(
            value =>
            {
                change(value);
                _services.ApplySettings();
            },
            OnError));
}
