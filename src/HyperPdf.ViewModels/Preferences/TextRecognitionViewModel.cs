// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Ocr;
using HyperPdf.Core.Reactive;
using HyperPdf.Http.Ocr;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Preferences;

/// <summary>
/// Recognises the text on a tab's scanned pages, one page at a time off the UI thread, so the pages become searchable
/// and their text can be selected and copied. English ships with the app, so the first press just works. When the
/// first scanned page reads poorly, which usually means another language, nothing is written yet and the language bar
/// asks which language the document is in; a language not on this computer downloads in the same click.
/// </summary>
[DebuggerDisplay("TextRecognitionViewModel: Recognizing={IsRunning}, LanguageBar={IsLanguageBarOpen}")]
public sealed partial class TextRecognitionViewModel : ReactiveObject, IDisposable, ILocalisationChildren
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

    /// <summary>Owns the subscription following the language choice, and the recognition and download in progress.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>The languages the next run uses.</summary>
    private List<string> _chosen;

    /// <summary>Whether the language bar's index is being set from code rather than picked.</summary>
    private bool _settingIndex;

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
        LanguageActionText = DescribeAction(services.Ocr, CollectionsMarshal.AsSpan(_chosen));

        // A language the reader picks is the one the next run uses; the bar setting its own index is not a pick. The
        // filter reads the view model's own flag: a constructor cannot hand 'this' to a state-passing operator.
        _subscriptions.Add(this.WhenChanged(static vm => vm.LanguageIndex).Skip(1).Where(_ => !_settingIndex).SubscribeSafe(UseLanguage, ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static vm => vm.RecognitionRun).OwnLatest().SubscribeSafeErrors(ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static vm => vm.DownloadRun).OwnLatest().SubscribeSafeErrors(ErrorTrace.Write));
    }

    /// <summary>Gets the languages offered in the language bar, English first.</summary>
    public static string[] LanguageNames { get; } = BuildLanguageNames();

    /// <summary>Gets the interaction that shows the window that manages the languages; the output says whether the window was accepted.</summary>
    public Interaction<OcrLanguagesViewModel, bool> ManageLanguagesInteraction { get; } = new();

    /// <summary>Gets the latest page recognition wrote text on; the tab redraws the page it names.</summary>
    [Reactive]
    public partial PageChange LastPageChange { get; private set; }

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

    /// <summary>Gets or sets the recognition in progress, which disposing stops, or <see langword="null"/> when none is.</summary>
    [Reactive]
    private partial CancellationDisposable? RecognitionRun { get; set; }

    /// <summary>Gets or sets the download in progress, which disposing stops, or <see langword="null"/> when none is.</summary>
    [Reactive]
    private partial CancellationDisposable? DownloadRun { get; set; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        LocalisationRefresh.RefreshCore(_owner, visited);
        LocalisationRefresh.RefreshCore(_services, visited);
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
            return UiText.Get("Every page already has text, so there was nothing to recognise.");
        }

        var pages = recognized == 1 ? UiText.Get("1 page") : UiText.Format("{0} pages", recognized);
        var prefix = stopped ? UiText.Get("Stopped. ") : string.Empty;
        return recognized == 0
            ? UiText.Format("{0}No text was found on the scanned pages.", prefix)
            : UiText
            .Format("{0}Text recognised on {1}. You can now search, select and copy it. Save to keep it.", prefix, pages);
    }

    /// <summary>Describes the language bar's button for a choice of languages.</summary>
    /// <param name="setup">The text recognition setup.</param>
    /// <param name="codes">The chosen language codes.</param>
    /// <returns>"Download …" when packs are missing, otherwise "Recognise in …".</returns>
    internal static string DescribeAction(OcrSetup setup, ReadOnlySpan<string> codes)
    {
        var needed = setup.PacksNeededFor([..codes], []);
        if (needed.Count > 0)
        {
            var missing = CollectionsMarshal.AsSpan(needed);
            return UiText.Format("Download {0} ({1})", OcrLanguagesViewModel.DescribePacks(missing), OcrLanguageCatalog.DescribeSize(OcrLanguagePackDownloader.TotalBytes(missing)));
        }

        var names = new List<OcrLanguagePack>(codes.Length);
        foreach (var code in codes)
        {
            if (OcrLanguageCatalog.Find(code) is { } pack)
            {
                names.Add(pack);
            }
        }

        return UiText.Format("Recognise in {0}", OcrLanguagesViewModel.DescribePacks(CollectionsMarshal.AsSpan(names)));
    }

    /// <summary>Gets a language's place in <see cref="LanguageNames"/>.</summary>
    /// <param name="code">The language code.</param>
    /// <returns>The index; English when the language is not offered.</returns>
    private static int IndexOf(string code)
    {
        var packs = OcrLanguageCatalog.Packs;
        for (var i = 0; i < packs.Length; i++)
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
        var names = new string[packs.Length];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = packs[i].Name;
        }

        return names;
    }

    /// <summary>Recognises one page and writes its words, unless the page must be checked first and reads too poorly to trust.</summary>
    /// <param name="document">The document.</param>
    /// <param name="writer">The document's text layer writer.</param>
    /// <param name="engine">The recogniser.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="words">A reusable list for the words.</param>
    /// <param name="check">Whether to check how well the page reads before writing.</param>
    /// <param name="cancellationToken">Stops the work between the strips rendered.</param>
    /// <returns>What happened.</returns>
    private static OcrPageResult RecognizeOne(IDocument document, ITextLayerWriter writer, IOcrEngine engine, int pageIndex, List<OcrWord> words, bool check, CancellationToken cancellationToken)
    {
        // The words go back to the page they were read from, even if pages are inserted, deleted or moved meanwhile.
        var pageToken = writer.GetPageToken(pageIndex);
        var status = OcrRunner.RecognizeWords(document, engine, pageIndex, words, cancellationToken);
        if (status != OcrPageStatus.Recognized)
        {
            return new(pageIndex, status, 0);
        }

        return check && OcrRunner.AverageConfidence(words) < UnsureConfidence ? new(pageIndex, OcrPageStatus.Unsure, 0) : OcrRunner.WriteWords(writer, pageIndex, pageToken, words);
    }

    /// <summary>Recognises the pages in the chosen languages, asking first only if the first page reads poorly.</summary>
    /// <returns>A signal that ends once the pages are recognised, stopped or refused.</returns>
    [ReactiveCommand(CanExecute = nameof(_isIdle))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> Recognize() => Run(false);

    /// <summary>Opens the window that lists the languages text can be recognised in, to tick them, download them and remove the ones that are not used.</summary>
    /// <returns>A signal that ends when the window is closed.</returns>
    [ReactiveCommand]
    private IObservable<RxVoid> ManageLanguages()
    {
        var languages = new OcrLanguagesViewModel(_services);
        return ManageLanguagesInteraction.Handle(languages).ToSignal().Select(static _ => RxVoid.Default).Finally(languages.Dispose);
    }

    /// <summary>Opens the language bar so a language can be picked before recognising.</summary>
    [ReactiveCommand(CanExecute = nameof(_isIdle))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ChooseLanguage() =>
        OpenLanguageBar(UiText.Get("Choose the language of this document. Languages not on this computer download in the same click."));

    /// <summary>Downloads the picked language if needed, then recognises the text in it.</summary>
    /// <returns>A signal that ends once the pages are recognised, or the language could not be downloaded.</returns>
    [ReactiveCommand(CanExecute = nameof(_isIdle))]
    private IObservable<RxVoid> ConfirmLanguage()
    {
        _services.Settings.OcrLanguage = OcrLanguageCatalog.Format(CollectionsMarshal.AsSpan(_chosen));
        _services.SaveSettings();
        var needed = _services.Ocr.PacksNeededFor([.._chosen], []);
        return (needed.Count > 0 ? Download(CollectionsMarshal.AsSpan(needed)) : Signal.Return(true))
            .SwitchMap(ready => ready ? RunConfirmed() : Signal.Return(RxVoid.Default));
    }

    /// <summary>Closes the language bar and recognises the text in the language the person picked.</summary>
    /// <returns>A cold signal that ends once the pages are recognised, stopped or refused.</returns>
    private IObservable<RxVoid> RunConfirmed()
    {
        IsLanguageBarOpen = false;
        return Run(true);
    }

    /// <summary>Hides the language bar, stopping any download. Nothing has been written.</summary>
    [ReactiveCommand]
    private void CloseLanguageBar()
    {
        DownloadRun = null;
        IsLanguageBarOpen = false;
    }

    /// <summary>Stops after the page being recognised.</summary>
    [ReactiveCommand(CanExecute = nameof(_isRunning))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Stop() => RecognitionRun = null;

    /// <summary>Makes a language the one the next run uses.</summary>
    /// <param name="index">The language's position in <see cref="LanguageNames"/>.</param>
    private void UseLanguage(int index)
    {
        var packs = OcrLanguageCatalog.Packs;
        _chosen = [packs[Math.Clamp(index, 0, packs.Length - 1)].Code];
        LanguageActionText = DescribeAction(_services.Ocr, CollectionsMarshal.AsSpan(_chosen));
    }

    /// <summary>Shows the language bar with the current choice.</summary>
    /// <param name="message">Why it opened.</param>
    private void OpenLanguageBar(string message)
    {
        _settingIndex = true;
        LanguageIndex = IndexOf(_chosen[0]);
        _settingIndex = false;
        LanguageBarText = message;
        LanguageActionText = DescribeAction(_services.Ocr, CollectionsMarshal.AsSpan(_chosen));
        IsLanguageBarOpen = true;
    }

    /// <summary>Downloads packs, explaining any failure in the language bar.</summary>
    /// <param name="packs">The packs.</param>
    /// <returns>A cold signal giving <see langword="true"/> when they downloaded; unsubscribing stops the download.</returns>
    private IObservable<bool> Download(ReadOnlySpan<OcrLanguagePack> packs)
    {
        var download = new CancellationDisposable();
        DownloadRun = download;
        IsDownloading = true;
        IsBusy = true;
        DownloadProgress = 0;
        LanguageBarText = UiText.Format("Downloading {0}. It stays on this computer for next time.", OcrLanguagesViewModel.DescribePacks([..packs]));
        var progress = new Progress<double>(fraction => DownloadProgress = fraction);

        return _services.Ocr.Download([..packs], progress)
            .Select(static _ => true)
            .ThrowWhenCancelled(download.Token)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Catch<bool, Exception>(DownloadFailed)
            .Finally(() => EndDownload(download));
    }

    /// <summary>Explains why the download failed in the language bar, or passes on a failure that is not expected.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A signal giving <see langword="false"/> for an expected failure, or failing with the same error.</returns>
    private IObservable<bool> DownloadFailed(Exception error)
    {
        switch (error)
        {
            case OperationCanceledException:
                {
                    LanguageBarText = UiText.Get("The download stopped. Nothing was half saved.");
                    break;
                }

            case HttpRequestException:
                {
                    LanguageBarText = UiText.Format("The language could not be downloaded ({0}). Check the internet connection, then press the button again.", error.Message);
                    break;
                }

            case InvalidDataException:
                {
                    LanguageBarText = UiText.Get("The language arrived damaged and was thrown away. Press the button to try again.");
                    break;
                }

            case IOException or UnauthorizedAccessException:
                {
                    LanguageBarText = UiText.Format("The language could not be saved: {0}", error.Message);
                    break;
                }

            default:
                {
                    return Signal.Throw<bool>(error);
                }
        }

        return Signal.Return(false);
    }

    /// <summary>Puts the language bar's controls back once the download is over.</summary>
    /// <param name="download">The download that ended.</param>
    private void EndDownload(CancellationDisposable download)
    {
        if (ReferenceEquals(DownloadRun, download))
        {
            DownloadRun = null;
        }

        IsDownloading = false;
        IsBusy = false;
    }

    /// <summary>Checks the chosen languages are on this computer, then recognises the pages and reports the outcome.</summary>
    /// <param name="confirmed">Whether the person picked the language, so a poor first page is not questioned.</param>
    /// <returns>A cold signal that ends once the pages are recognised, stopped or refused.</returns>
    private IObservable<RxVoid> Run(bool confirmed)
    {
        if (_owner.TryGetDocument() is not { } document || DocumentFeature<ITextLayerWriter>.Get(document) is not
            {
            } writer || !CheckReady())
        {
            return Signal.Return(RxVoid.Default);
        }

        var recognition = new CancellationDisposable();
        RecognitionRun = recognition;
        IsRunning = true;
        IsBusy = true;
        ProgressText = UiText.Get("Starting text recognition…");
        return Signal.Start(() => _services.CreateOcrEngine(), TaskPoolSequencer.Default)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SwitchMap(engine => Signal.Using(() => engine, recogniser => RecognizePages(document, writer, recogniser, confirmed, recognition.Token)))
            .Catch<RxVoid, Exception>(RunFailed)
            .Finally(() => EndRun(recognition));
    }

    /// <summary>Says why recognition stopped, or passes on a failure that is not expected.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A signal that ends for an expected failure, or fails with the same error.</returns>
    private IObservable<RxVoid> RunFailed(Exception error)
    {
        switch (error)
        {
            case OperationCanceledException:
                {
                    _owner.SetNotice(LocalizedMessage.Get(UiText, "Text recognition stopped."));
                    break;
                }

            case InvalidOperationException:
                {
                    // The engine throws ObjectDisposedException, a kind of InvalidOperationException, once its document is closed.
                    _owner.SetNotice(LocalizedMessage.Get(UiText, "Text recognition stopped because the document was closed."));
                    break;
                }

            default:
                {
                    return Signal.Throw<RxVoid>(error);
                }
        }

        return Signal.Return(RxVoid.Default);
    }

    /// <summary>Puts the controls back once the recognition run is over.</summary>
    /// <param name="recognition">The run that ended.</param>
    private void EndRun(CancellationDisposable recognition)
    {
        if (ReferenceEquals(RecognitionRun, recognition))
        {
            RecognitionRun = null;
        }

        IsRunning = false;
        IsBusy = false;
        Progress = 0;
        ProgressText = string.Empty;
    }

    /// <summary>Recognises the pages one after another and says how it went.</summary>
    /// <param name="document">The document.</param>
    /// <param name="writer">The document's text layer writer.</param>
    /// <param name="engine">The recogniser.</param>
    /// <param name="confirmed">Whether the language was picked, so a poor first page is written anyway.</param>
    /// <param name="cancellation">Stops before the next page.</param>
    /// <returns>A cold signal that ends once the pages are done, stopped or refused.</returns>
    private IObservable<RxVoid> RecognizePages(IDocument document, ITextLayerWriter writer, IOcrEngine engine, bool confirmed, CancellationToken cancellation)
    {
        if (!engine.IsAvailable)
        {
            _owner.SetNotice(OcrSetup.EngineUnavailableMessage);
            return Signal.Return(RxVoid.Default);
        }

        var state = new RecognitionState(!confirmed);
        return LazySequence.Concat(PageRuns(document, writer, engine, state, cancellation))
            .TakeWhile(_ => !state.Done)
            .Count()
            .Select(_ => Reported(document, state.Run, cancellation));
    }

    /// <summary>Makes the recognition of each page, one at a time as the caller moves along.</summary>
    /// <param name="document">The document.</param>
    /// <param name="writer">The document's text layer writer.</param>
    /// <param name="engine">The recogniser.</param>
    /// <param name="state">What has been recognised so far.</param>
    /// <param name="cancellation">Stops before the next page.</param>
    /// <returns>A cold signal per page.</returns>
    private IEnumerable<IObservable<RxVoid>> PageRuns(IDocument document, ITextLayerWriter writer, IOcrEngine engine, RecognitionState state, CancellationToken cancellation)
    {
        var pageCount = document.PageCount;
        for (var page = 0; page < pageCount && !cancellation.IsCancellationRequested; page++)
        {
            var index = page;
            yield return Signal.Defer(() => OnePage(document, writer, engine, state, index, pageCount, cancellation));
        }
    }

    /// <summary>Prepares a page, recognises it on the pool and writes what was read.</summary>
    /// <param name="document">The document.</param>
    /// <param name="writer">The document's text layer writer.</param>
    /// <param name="engine">The recogniser.</param>
    /// <param name="state">What has been recognised so far.</param>
    /// <param name="index">The page.</param>
    /// <param name="pageCount">The number of pages.</param>
    /// <param name="cancellation">Stops the work.</param>
    /// <returns>A cold signal that ends once the page is done.</returns>
    private IObservable<RxVoid> OnePage(IDocument document, ITextLayerWriter writer, IOcrEngine engine, RecognitionState state, int index, int pageCount, CancellationToken cancellation)
    {
        Progress = (double)index / pageCount;
        ProgressText = UiText.Format("Recognising text: page {0} of {1}", index + 1, pageCount);
        var check = state.CheckFirst;

        return document.PreparePage(index).ThrowWhenCancelled(cancellation)
            .SwitchMap(_ => Signal.Start(() => RecognizeOne(document, writer, engine, index, state.Words, check, cancellation), TaskPoolSequencer.Default))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Select(result => PageDone(state, result, index));
    }

    /// <summary>Counts a page that was recognised, or ends the run when it cannot go on.</summary>
    /// <param name="state">What has been recognised so far.</param>
    /// <param name="result">What happened to the page.</param>
    /// <param name="index">The page.</param>
    /// <returns>The completion value.</returns>
    private RxVoid PageDone(RecognitionState state, OcrPageResult result, int index)
    {
        switch (result.Status)
        {
            case OcrPageStatus.Recognized:
                {
                    state.CheckFirst = false;
                    state.Run = state.Run with { Recognized = state.Run.Recognized + 1 };
                    LastPageChange = LastPageChange.Next(index);
                    break;
                }

            case OcrPageStatus.AlreadyHasText:
                {
                    state.Run = state.Run with { AlreadyText = state.Run.AlreadyText + 1 };
                    break;
                }

            case OcrPageStatus.NotRendered:
                {
                    state.Run = state.Run with { Closed = true };
                    state.Done = true;
                    break;
                }

            case OcrPageStatus.Unsure:
                {
                    state.Run = state.Run with { Unsure = true };
                    state.Done = true;
                    break;
                }

            default:
                {
                    break;
                }
        }

        return RxVoid.Default;
    }

    /// <summary>Says how the run went, or opens the language bar when the first page read poorly.</summary>
    /// <param name="document">The document.</param>
    /// <param name="run">What was recognised.</param>
    /// <param name="cancellation">Tells whether the run was stopped.</param>
    /// <returns>The completion value.</returns>
    private RxVoid Reported(IDocument document, RecognitionRun run, CancellationToken cancellation)
    {
        if (run.Unsure)
        {
            OpenLanguageBar(
                UiText.Format("The first scanned page does not read well as {0}, ", OcrLanguagesViewModel.DescribePacks([..CollectionsMarshal.AsSpan(ChosenPacks())]))
                + UiText.Get("so nothing has been changed yet. Choose the language of this document."));
            return RxVoid.Default;
        }

        _owner.Search.Refresh();
        _owner.InvalidateReading();
        _owner.SetNotice(
            run.Closed
                ? LocalizedMessage.Get(UiText, "Text recognition stopped because the document was closed.")
                : LocalizedMessage.Literal(Describe(run.Recognized, run.AlreadyText, document.PageCount, cancellation.IsCancellationRequested)));
        return RxVoid.Default;
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
            _owner.SetNotice(OcrSetup.EngineUnavailableMessage);
            return false;
        }

        _chosen = OcrLanguageCatalog.Parse(_services.Settings.OcrLanguage);
        var unknown = new List<string>();
        var needed = setup.PacksNeededFor([.._chosen], unknown);
        if (unknown.Count > 0)
        {
            _ = _chosen.RemoveAll(unknown.Contains);
            if (_chosen.Count == 0)
            {
                _chosen.Add(OcrLanguageCatalog.DefaultLanguage);
            }

            OpenLanguageBar(UiText.Get("The language set for text recognition is not one the app knows. Choose the language of this document."));
            return false;
        }

        if (needed.Count > 0)
        {
            OpenLanguageBar(UiText.Format("This document is set to {0}, which is not on this computer yet.", OcrLanguagesViewModel.DescribePacks([..CollectionsMarshal.AsSpan(ChosenPacks())])));
            return false;
        }

        try
        {
            setup.GatherLanguages([.._chosen]);
        }
        catch (IOException ex)
        {
            _owner.SetNotice(LocalizedMessage.Format(UiText, "The chosen languages could not be prepared: {0}", ex.Message));
            return false;
        }

        IsLanguageBarOpen = false;
        return true;
    }
}
