// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.App.Services;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Ocr;
using HyperPdf.Http.Ocr;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// Recognises the text on a tab's scanned pages, one page at a time off the UI thread, so the pages become searchable
/// and their text can be selected and copied. English ships with the app, so the first press just works. When the
/// first scanned page reads poorly, which usually means another language, nothing is written yet and the language bar
/// asks which language the document is in; a language not on this computer downloads in the same click.
/// </summary>
[DebuggerDisplay("TextRecognitionViewModel: Recognizing={IsRunning}, LanguageBar={IsLanguageBarOpen}")]
public sealed partial class TextRecognitionViewModel : ReactiveObject, IDisposable
{
    /// <summary>
    /// Below this mean word confidence, out of 100, the first page probably is not in the chosen language. Clean English
    /// scans read above 85 with the English data; text in another script reads far lower.
    /// </summary>
    private const float UnsureConfidence = 60;

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>The application services.</summary>
    private readonly AppServices _services;

    /// <summary>Emits whether recognition is running; enables Stop.</summary>
    private readonly IObservable<bool> _isRunning;

    /// <summary>Emits whether nothing is running or downloading; enables Recognize and the language bar's button.</summary>
    private readonly IObservable<bool> _isIdle;

    /// <summary>Subscriptions following the language choice.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>The languages the next run uses.</summary>
    private List<string> _chosen;

    /// <summary>Whether the language bar's index is being set from code rather than picked.</summary>
    private bool _settingIndex;

    /// <summary>Cancels the running recognition.</summary>
    private CancellationTokenSource? _cancellation;

    /// <summary>Cancels the running download.</summary>
    private CancellationTokenSource? _download;

    /// <summary>Initializes a new instance of the <see cref="TextRecognitionViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    /// <param name="services">The application services.</param>
    public TextRecognitionViewModel(DocumentTabViewModel owner, AppServices services)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(services);
        _owner = owner;
        _services = services;
        _chosen = OcrLanguageCatalog.Parse(services.Settings.OcrLanguage);
        _isRunning = this.WhenChanged(static vm => vm.IsRunning);
        _isIdle = this.WhenChanged(static vm => vm.IsBusy).Select(static busy => !busy);
        LanguageIndex = IndexOf(_chosen[0]);
        LanguageActionText = DescribeAction(services.Ocr, _chosen);
        _subscriptions.Add(this.WhenChanged(static vm => vm.LanguageIndex).Skip(1).SubscribeSafe(_ => OnLanguagePicked(), OnError));
    }

    /// <summary>Gets the languages offered in the language bar, English first.</summary>
    public static IReadOnlyList<string> LanguageNames { get; } = BuildLanguageNames();

    /// <summary>Gets a value indicating whether recognition is running.</summary>
    [Reactive]
    public partial bool IsRunning { get; private set; }

    /// <summary>Gets a value indicating whether recognition is running or a language is downloading.</summary>
    [Reactive]
    public partial bool IsBusy { get; private set; }

    /// <summary>Gets the progress from 0 to 1.</summary>
    [Reactive]
    public partial double Progress { get; private set; }

    /// <summary>Gets the progress as words, for example "Recognising text: page 3 of 12".</summary>
    [Reactive]
    public partial string ProgressText { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the language bar is shown.</summary>
    [Reactive]
    public partial bool IsLanguageBarOpen { get; private set; }

    /// <summary>Gets or sets the language picked in the language bar, an index into <see cref="LanguageNames"/>.</summary>
    [Reactive]
    public partial int LanguageIndex { get; set; }

    /// <summary>Gets what the language bar says: why it opened, or how a download went.</summary>
    [Reactive]
    public partial string LanguageBarText { get; private set; } = string.Empty;

    /// <summary>Gets the language bar button's label, for example "Download German (about 2 MB)" or "Recognise in German".</summary>
    [Reactive]
    public partial string LanguageActionText { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether a language is downloading.</summary>
    [Reactive]
    public partial bool IsDownloading { get; private set; }

    /// <summary>Gets the download progress from 0 to 1.</summary>
    [Reactive]
    public partial double DownloadProgress { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _subscriptions.Dispose();
        _download?.Cancel();
        _download?.Dispose();
        _cancellation?.Cancel();
        _cancellation?.Dispose();
    }

    /// <summary>Describes the outcome of a run.</summary>
    /// <param name="recognized">Pages given text.</param>
    /// <param name="alreadyText">Pages that already had text.</param>
    /// <param name="pageCount">Pages in the document.</param>
    /// <param name="stopped">Whether the user stopped early.</param>
    /// <returns>The message.</returns>
    internal static string Describe(int recognized, int alreadyText, int pageCount, bool stopped)
    {
        if (alreadyText == pageCount)
        {
            return "Every page already has text, so there was nothing to recognise.";
        }

        var pages = recognized == 1 ? "1 page" : $"{recognized} pages";
        var prefix = stopped ? "Stopped. " : string.Empty;
        return recognized == 0
            ? $"{prefix}No text was found on the scanned pages."
            : $"{prefix}Text recognised on {pages}. You can now search, select and copy it. Save to keep it.";
    }

    /// <summary>Describes the language bar's button for a choice of languages.</summary>
    /// <param name="setup">The text recognition setup.</param>
    /// <param name="codes">The chosen language codes.</param>
    /// <returns>"Download …" when packs are missing, otherwise "Recognise in …".</returns>
    internal static string DescribeAction(OcrSetup setup, IReadOnlyList<string> codes)
    {
        var needed = setup.PacksNeededFor(codes, []);
        if (needed.Count > 0)
        {
            return $"Download {OcrLanguagesViewModel.DescribePacks(needed)} ({OcrLanguageCatalog.DescribeSize(OcrLanguagePackDownloader.TotalBytes(needed))})";
        }

        var names = new List<OcrLanguagePack>(codes.Count);
        foreach (var code in codes)
        {
            if (OcrLanguageCatalog.Find(code) is { } pack)
            {
                names.Add(pack);
            }
        }

        return $"Recognise in {OcrLanguagesViewModel.DescribePacks(names)}";
    }

    /// <summary>Gets a language's place in <see cref="LanguageNames"/>.</summary>
    /// <param name="code">The language code.</param>
    /// <returns>The index; English when the language is not offered.</returns>
    private static int IndexOf(string code)
    {
        var packs = OcrLanguageCatalog.Packs;
        for (var i = 0; i < packs.Count; i++)
        {
            if (string.Equals(packs[i].Code, code, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>Builds the language names.</summary>
    /// <returns>The names in catalogue order.</returns>
    private static string[] BuildLanguageNames()
    {
        var packs = OcrLanguageCatalog.Packs;
        var names = new string[packs.Count];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = packs[i].Name;
        }

        return names;
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Recognises one page and writes its words, unless the page must be checked first and reads too poorly to trust.</summary>
    /// <param name="document">The document.</param>
    /// <param name="writer">The document's text layer writer.</param>
    /// <param name="engine">The recogniser.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="words">A reusable list for the words.</param>
    /// <param name="check">Whether to check how well the page reads before writing.</param>
    /// <returns>What happened.</returns>
    private static OcrPageResult RecognizeOne(IDocument document, ITextLayerWriter writer, IOcrEngine engine, int pageIndex, List<OcrWord> words, bool check)
    {
        var status = OcrRunner.RecognizeWords(document, engine, pageIndex, words);
        if (status != OcrPageStatus.Recognized)
        {
            return new(pageIndex, status, 0);
        }

        return check && OcrRunner.AverageConfidence(words) < UnsureConfidence
            ? new(pageIndex, OcrPageStatus.Unsure, 0)
            : OcrRunner.WriteWords(writer, pageIndex, words);
    }

    /// <summary>Recognises the pages in the chosen languages, asking first only if the first page reads poorly.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_isIdle))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task RecognizeAsync() => RunAsync(false);

    /// <summary>Opens the language bar so a language can be picked before recognising.</summary>
    [ReactiveCommand(CanExecute = nameof(_isIdle))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ChooseLanguage() =>
        OpenLanguageBar("Choose the language of this document. Languages not on this computer download in the same click.");

    /// <summary>Downloads the picked language if needed, then recognises the text in it.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_isIdle))]
    private async Task ConfirmLanguageAsync()
    {
        _services.Settings.OcrLanguage = OcrLanguageCatalog.Format(_chosen);
        _services.SaveSettings();
        var needed = _services.Ocr.PacksNeededFor(_chosen, []);
        if (needed.Count > 0 && !await DownloadAsync(needed).ConfigureAwait(true))
        {
            return;
        }

        IsLanguageBarOpen = false;
        await RunAsync(true).ConfigureAwait(true);
    }

    /// <summary>Hides the language bar, stopping any download. Nothing has been written.</summary>
    [ReactiveCommand]
    private void CloseLanguageBar()
    {
        _download?.Cancel();
        IsLanguageBarOpen = false;
    }

    /// <summary>Stops after the page being recognised.</summary>
    [ReactiveCommand(CanExecute = nameof(_isRunning))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Stop() => _cancellation?.Cancel();

    /// <summary>Makes the picked language the one the next run uses.</summary>
    private void OnLanguagePicked()
    {
        if (_settingIndex)
        {
            return;
        }

        var packs = OcrLanguageCatalog.Packs;
        _chosen = [packs[Math.Clamp(LanguageIndex, 0, packs.Count - 1)].Code];
        LanguageActionText = DescribeAction(_services.Ocr, _chosen);
    }

    /// <summary>Shows the language bar with the current choice.</summary>
    /// <param name="message">Why it opened.</param>
    private void OpenLanguageBar(string message)
    {
        _settingIndex = true;
        LanguageIndex = IndexOf(_chosen[0]);
        _settingIndex = false;
        LanguageBarText = message;
        LanguageActionText = DescribeAction(_services.Ocr, _chosen);
        IsLanguageBarOpen = true;
    }

    /// <summary>Downloads packs, explaining any failure in the language bar.</summary>
    /// <param name="packs">The packs.</param>
    /// <returns><see langword="true"/> when they downloaded.</returns>
    private async Task<bool> DownloadAsync(IReadOnlyList<OcrLanguagePack> packs)
    {
        using var download = new CancellationTokenSource();
        _download = download;
        IsDownloading = true;
        IsBusy = true;
        DownloadProgress = 0;
        try
        {
            LanguageBarText = $"Downloading {OcrLanguagesViewModel.DescribePacks(packs)}. It stays on this computer for next time.";
            var progress = new Progress<double>(fraction => DownloadProgress = fraction);
            await _services.Ocr.DownloadAsync(packs, progress, download.Token).ConfigureAwait(true);
            return true;
        }
        catch (OperationCanceledException)
        {
            LanguageBarText = "The download stopped. Nothing was half saved.";
        }
        catch (HttpRequestException ex)
        {
            LanguageBarText = $"The language could not be downloaded ({ex.Message}). Check the internet connection, then press the button again.";
        }
        catch (InvalidDataException)
        {
            LanguageBarText = "The language arrived damaged and was thrown away. Press the button to try again.";
        }
        catch (IOException ex)
        {
            LanguageBarText = $"The language could not be saved: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            LanguageBarText = $"The language could not be saved: {ex.Message}";
        }
        finally
        {
            _download = null;
            IsDownloading = false;
            IsBusy = false;
        }

        return false;
    }

    /// <summary>Checks the chosen languages are on this computer, then recognises the pages and reports the outcome.</summary>
    /// <param name="confirmed">Whether the person picked the language, so a poor first page is not questioned.</param>
    /// <returns>A task.</returns>
    private async Task RunAsync(bool confirmed)
    {
        if (_owner.TryGetDocument() is not { } document || DocumentFeature<ITextLayerWriter>.Get(document) is not
            {
            } writer || !CheckReady())
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsRunning = true;
        IsBusy = true;
        try
        {
            ProgressText = "Starting text recognition…";
            using var engine = await Task.Run(_services.CreateOcrEngine, cancellation.Token).ConfigureAwait(true);
            if (!engine.IsAvailable)
            {
                _owner.Notice = OcrSetup.EngineUnavailableText;
                return;
            }

            var run = await RecognizePagesAsync(document, writer, engine, confirmed, cancellation.Token).ConfigureAwait(true);
            if (run.Unsure)
            {
                OpenLanguageBar($"The first scanned page does not read well as {OcrLanguagesViewModel.DescribePacks(ChosenPacks())}, "
                    + "so nothing has been changed yet. Choose the language of this document.");
                return;
            }

            _owner.Search.Refresh();
            _owner.InvalidateReading();
            _owner.Notice = run.Closed
                ? "Text recognition stopped because the document was closed."
                : Describe(run.Recognized, run.AlreadyText, document.PageCount, cancellation.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            _owner.Notice = "Text recognition stopped.";
        }
        finally
        {
            _cancellation = null;
            IsRunning = false;
            IsBusy = false;
            Progress = 0;
            ProgressText = string.Empty;
        }
    }

    /// <summary>Gets the packs of the chosen languages that the catalogue offers.</summary>
    /// <returns>The packs.</returns>
    private List<OcrLanguagePack> ChosenPacks()
    {
        var packs = new List<OcrLanguagePack>(_chosen.Count);
        foreach (var code in _chosen)
        {
            if (OcrLanguageCatalog.Find(code) is { } pack)
            {
                packs.Add(pack);
            }
        }

        return packs;
    }

    /// <summary>Checks Tesseract loads and every chosen language is on this computer, offering any that are not.</summary>
    /// <returns><see langword="true"/> when recognition can start.</returns>
    private bool CheckReady()
    {
        var setup = _services.Ocr;
        if (!setup.IsEngineInstalled())
        {
            IsLanguageBarOpen = false;
            _owner.Notice = OcrSetup.EngineUnavailableText;
            return false;
        }

        _chosen = OcrLanguageCatalog.Parse(_services.Settings.OcrLanguage);
        var unknown = new List<string>();
        var needed = setup.PacksNeededFor(_chosen, unknown);
        if (unknown.Count > 0)
        {
            _ = _chosen.RemoveAll(unknown.Contains);
            if (_chosen.Count == 0)
            {
                _chosen.Add(OcrLanguageCatalog.DefaultLanguage);
            }

            OpenLanguageBar("The language set for text recognition is not one the app knows. Choose the language of this document.");
            return false;
        }

        if (needed.Count > 0)
        {
            OpenLanguageBar($"This document is set to {OcrLanguagesViewModel.DescribePacks(ChosenPacks())}, which is not on this computer yet.");
            return false;
        }

        try
        {
            setup.GatherLanguages(_chosen);
        }
        catch (IOException ex)
        {
            _owner.Notice = $"The chosen languages could not be prepared: {ex.Message}";
            return false;
        }

        IsLanguageBarOpen = false;
        return true;
    }

    /// <summary>Recognises each page in turn off the UI thread, reporting progress and edits on the UI thread.</summary>
    /// <param name="document">The document.</param>
    /// <param name="writer">The document's text layer writer.</param>
    /// <param name="engine">The recogniser.</param>
    /// <param name="confirmed">Whether the language was picked, so a poor first page is written anyway.</param>
    /// <param name="cancellation">Stops before the next page.</param>
    /// <returns>What happened.</returns>
    private async Task<RecognitionRun> RecognizePagesAsync(IDocument document, ITextLayerWriter writer, IOcrEngine engine, bool confirmed, CancellationToken cancellation)
    {
        var run = default(RecognitionRun);
        var pageCount = document.PageCount;
        var checkFirst = !confirmed;
        List<OcrWord> words = [];
        for (var page = 0; page < pageCount && !cancellation.IsCancellationRequested; page++)
        {
            Progress = (double)page / pageCount;
            ProgressText = $"Recognising text: page {page + 1} of {pageCount}";
            await document.PreparePageAsync(page, cancellation).ConfigureAwait(true);
            var index = page;
            var check = checkFirst;
            var result = await Task.Run(() => RecognizeOne(document, writer, engine, index, words, check), CancellationToken.None).ConfigureAwait(true);
            switch (result.Status)
            {
                case OcrPageStatus.Recognized:
                    {
                        checkFirst = false;
                        run = run with { Recognized = run.Recognized + 1 };
                        _owner.OnPageEdited(index);
                        break;
                    }

                case OcrPageStatus.AlreadyHasText:
                    {
                        run = run with { AlreadyText = run.AlreadyText + 1 };
                        break;
                    }

                case OcrPageStatus.NotRendered:
                    {
                        return run with { Closed = true };
                    }

                case OcrPageStatus.Unsure:
                    {
                        return run with { Unsure = true };
                    }

                default:
                    {
                        break;
                    }
            }
        }

        return run;
    }
}
