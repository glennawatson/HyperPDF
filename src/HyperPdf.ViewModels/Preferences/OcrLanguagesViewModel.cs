// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Ocr;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Preferences;

/// <summary>
/// The text recognition languages in Preferences: every language pack offered, which are on this computer, and which
/// are used. Ticking a language uses it at once; a language not yet downloaded is downloaded here or offered the next
/// time text is recognised. Packs download one at a time with steady progress and can be stopped.
/// </summary>
[DebuggerDisplay("OcrLanguagesViewModel: Downloading={IsDownloading}")]
public sealed partial class OcrLanguagesViewModel : ReactiveObject, IDisposable, ILocalisationChildren
{
    /// <summary>The application services.</summary>
    private readonly AppServices _services;

    /// <summary>Subscriptions following each language's tick box.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Emits whether a download is running; enables Stop.</summary>
    private readonly IObservable<bool> _isDownloading;

    /// <summary>Language codes in the settings that the catalogue does not offer, kept so they are not lost.</summary>
    private readonly List<string> _otherCodes = [];

    /// <summary>Stops the download in progress, or <see langword="null"/> when none runs; the download owns its cancellation source.</summary>
    private Action? _stopDownload;

    /// <summary>The resource identity of the displayed StatusText.</summary>
    private LocalizedMessage _localizedStatusText = LocalizedMessage.Literal(string.Empty);

    /// <summary>Initializes a new instance of the <see cref="OcrLanguagesViewModel"/> class.</summary>
    /// <param name="services">The application services.</param>
    public OcrLanguagesViewModel(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        foreach (var code in OcrLanguageCatalog.Parse(services.Settings.OcrLanguage))
        {
            if (OcrLanguageCatalog.Find(code) is null)
            {
                _otherCodes.Add(code);
            }
        }

        IsEngineInstalled = services.Ocr.IsEngineInstalled();
        Note = IsEngineInstalled
            ? UiText.Get("English is included. Other languages download from the Tesseract project on GitHub when you choose them, ")
            + UiText.Format("and are kept on this computer in {0}. Recognised text never leaves this computer.", services.Ocr.LanguageDirectory)
            : OcrSetup.EngineUnavailableText;
        _isDownloading = this.WhenChanged(static vm => vm.IsDownloading);

        // Closing the window stops a download in progress.
        _subscriptions.Add(Scope.Create(StopDownload));
    }

    /// <summary>Gets every language offered, English first, created when first read.</summary>
    public IReadOnlyList<OcrLanguageItemViewModel> Items => field ??= CreateItems();

    /// <summary>Gets a value indicating whether the text recogniser can run on this computer.</summary>
    public bool IsEngineInstalled { get; }

    /// <summary>Gets a note on where packs come from and where they are kept, or that the recogniser could not start.</summary>
    public string Note { get; }

    /// <summary>Gets a value indicating whether a pack is downloading.</summary>
    [Reactive]
    public partial bool IsDownloading { get; private set; }

    /// <summary>Gets the download progress from 0 to 1.</summary>
    [Reactive]
    public partial double DownloadProgress { get; private set; }

    /// <summary>Gets the outcome of the last download or removal, for example "German downloaded.".</summary>
    public string StatusText { get => _localizedStatusText.Value ?? string.Empty; private set => SetStatusText(LocalizedMessage.Literal(value)); }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Sets the displayed StatusText while retaining its resource identity.</summary>
    /// <param name="message">The message.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetStatusText(LocalizedMessage message) => this.RaiseAndSetIfChanged(ref _localizedStatusText, message, nameof(StatusText));

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(Items));
        this.RaisePropertyChanged(nameof(StatusText));
        LocalisationRefresh.RefreshCore(_services, visited);
    }

    /// <summary>Describes packs for people, for example "English and German".</summary>
    /// <param name="packs">The packs.</param>
    /// <returns>The description.</returns>
    internal static string DescribePacks(IReadOnlyList<OcrLanguagePack> packs)
    {
        if (packs.Count == 0)
        {
            return string.Empty;
        }

        var names = new string[packs.Count];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = packs[i].Name;
        }

        return names.Length == 1 ? names[0] : UiText.Format("{0} and {1}", string.Join(", ", names, 0, names.Length - 1), names[^1]);
    }

    /// <summary>Downloads packs, then refreshes each language's state.</summary>
    /// <param name="packs">The packs.</param>
    /// <returns>A task.</returns>
    internal async Task DownloadAsync(IReadOnlyList<OcrLanguagePack> packs)
    {
        if (IsDownloading)
        {
            return;
        }

        using var download = new CancellationTokenSource();
        _stopDownload = download.Cancel;
        IsDownloading = true;
        DownloadProgress = 0;
        Refresh();
        try
        {
            SetStatusText(LocalizedMessage.Format(UiText, "Downloading {0}…", DescribePacks(packs)));
            var progress = new Progress<double>(fraction => DownloadProgress = fraction);
            await _services.Ocr.DownloadAsync(packs, progress, download.Token).ConfigureAwait(true);
            SetStatusText(LocalizedMessage.Format(UiText, "{0} downloaded.", DescribePacks(packs)));
        }
        catch (OperationCanceledException)
        {
            SetStatusText(LocalizedMessage.Get(UiText, "The download stopped. Nothing was half saved; download again when you are ready."));
        }
        catch (HttpRequestException ex)
        {
            SetStatusText(LocalizedMessage.Format(UiText, "The language pack could not be downloaded: {0}", ex.Message));
        }
        catch (InvalidDataException ex)
        {
            SetStatusText(LocalizedMessage.Literal(ex.Message));
        }
        catch (IOException ex)
        {
            SetStatusText(LocalizedMessage.Format(UiText, "The language pack could not be saved: {0}", ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            SetStatusText(LocalizedMessage.Format(UiText, "The language pack could not be saved: {0}", ex.Message));
        }
        finally
        {
            _stopDownload = null;
            IsDownloading = false;
            Refresh();
        }
    }

    /// <summary>Deletes a language's downloaded pack.</summary>
    /// <param name="item">The language.</param>
    internal void Remove(OcrLanguageItemViewModel item)
    {
        try
        {
            _services.Ocr.Remove(item.Pack);
            SetStatusText(LocalizedMessage.Format(UiText, "{0} removed.", item.Name));
        }
        catch (IOException ex)
        {
            SetStatusText(LocalizedMessage.Format(UiText, "{0} could not be removed: {1}", item.Name, ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            SetStatusText(LocalizedMessage.Format(UiText, "{0} could not be removed: {1}", item.Name, ex.Message));
        }

        Refresh();
    }

    /// <summary>Describes where a language's data is.</summary>
    /// <param name="downloaded">Whether its pack is downloaded.</param>
    /// <param name="bundled">Whether its data ships with the app.</param>
    /// <param name="onComputer">Whether its data is elsewhere on this computer.</param>
    /// <param name="pack">The pack.</param>
    /// <returns>The status.</returns>
    private static string DescribeState(bool downloaded, bool bundled, bool onComputer, OcrLanguagePack pack)
    {
        if (downloaded)
        {
            return UiText.Get("Downloaded");
        }

        if (bundled)
        {
            return UiText.Get("Included");
        }

        return onComputer ? UiText.Get("On this computer") : UiText.Format("Not downloaded, {0}", OcrLanguageCatalog.DescribeSize(pack.Bytes));
    }

    /// <summary>Stops the download in progress, keeping packs already finished.</summary>
    [ReactiveCommand(CanExecute = nameof(_isDownloading))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void StopDownload() => _stopDownload?.Invoke();

    /// <summary>Creates a row for each language offered, ticking the chosen ones, and follows each tick box.</summary>
    /// <returns>The rows.</returns>
    private OcrLanguageItemViewModel[] CreateItems()
    {
        var chosen = OcrLanguageCatalog.Parse(_services.Settings.OcrLanguage);
        var items = new OcrLanguageItemViewModel[OcrLanguageCatalog.Packs.Count];
        for (var i = 0; i < items.Length; i++)
        {
            var pack = OcrLanguageCatalog.Packs[i];
            items[i] = new(this, pack, chosen.Contains(pack.Code));
        }

        Refresh(items);

        // A tick or untick on any row writes the chosen languages to the settings.
        _subscriptions.Add(
            Signal.Merge(Array.ConvertAll(items, static item => item.WhenChanged(static row => row.IsSelected).Skip(1))).SubscribeSafe(_ => SaveSelection(), ErrorTrace.Write));
        return items;
    }

    /// <summary>Updates each language's status and which buttons it offers.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Refresh() => Refresh(Items);

    /// <summary>Updates the given rows' status and which buttons they offer.</summary>
    /// <param name="items">The rows.</param>
    private void Refresh(IReadOnlyList<OcrLanguageItemViewModel> items)
    {
        var setup = _services.Ocr;
        foreach (var item in items)
        {
            var downloaded = setup.IsDownloaded(item.Pack);
            var bundled = !downloaded && setup.IsBundled(item.Pack.Code);
            var onComputer = !downloaded && !bundled && setup.HasLanguage(item.Pack.Code);
            item.IsDownloaded = downloaded;
            item.CanDownload = !downloaded && !bundled && !onComputer && !IsDownloading;
            item.CanRemove = downloaded && !IsDownloading;
            item.SetStatusText(LocalizedMessage.Literal(DescribeState(downloaded, bundled, onComputer, item.Pack)));
        }
    }

    /// <summary>Writes the ticked languages to the settings, in list order, keeping any languages the list does not offer.</summary>
    private void SaveSelection()
    {
        var codes = new List<string>(Items.Count + _otherCodes.Count);
        foreach (var item in Items)
        {
            if (item.IsSelected)
            {
                codes.Add(item.Pack.Code);
            }
        }

        codes.AddRange(_otherCodes);
        _services.Settings.OcrLanguage = OcrLanguageCatalog.Format(codes);
        SetStatusText(codes.Count == 0 ? LocalizedMessage.Get(UiText, "No language is ticked, so English is used.") : LocalizedMessage.Literal(string.Empty));
        _services.SaveSettings();
    }
}
