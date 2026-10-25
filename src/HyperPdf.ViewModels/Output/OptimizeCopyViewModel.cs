// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Optimizing;
using HyperPdf.Core.Platform;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Output;

/// <summary>
/// The "Save Optimised Copy" window: a size preset, accessibility options, a save button that asks where to save, a
/// progress bar with Cancel, and a plain-words summary. The open file is never overwritten; the copy is written to a
/// temporary file and moved into place once it is complete, so a cancel or a failure leaves no half-written file.
/// </summary>
[DebuggerDisplay("OptimizeCopyViewModel: {FileName}")]
public sealed partial class OptimizeCopyViewModel : ReactiveObject, ICopyWindowRequest, ILocalisationChildren
{
    /// <summary>The ending of the temporary file the copy is written to.</summary>
    private const string TemporarySuffix = ".optimising";

    /// <summary>The bytes in a megabyte, for telling people how big the model is.</summary>
    private const long BytesPerMegabyte = 1_000_000;

    /// <summary>Half a megabyte, added before dividing so the size rounds to the nearest megabyte.</summary>
    private const long HalfMegabyte = BytesPerMegabyte / 2;

    /// <summary>How pictures are described, or <see langword="null"/> when the window offers no such choice.</summary>
    private readonly PictureDescriptionSetup? _pictureDescription;

    /// <summary>The optimiser the open document provides.</summary>
    private readonly IDocumentOptimizer _optimizer;

    /// <summary>The path of the open document, which must never be overwritten.</summary>
    private readonly string _sourcePath;

    /// <summary>Whether a run is not in progress, so settings can change and a copy can start.</summary>
    private readonly IObservable<bool> _canSave;

    /// <summary>Whether a run is in progress, so it can be cancelled.</summary>
    private readonly IObservable<bool> _canCancel;

    /// <summary>Owns what the window's lifetime ends.</summary>
    private readonly MultipleDisposable _lifetime = [];

    /// <summary>The completed report and destination retained for language changes.</summary>
    private (OptimizeReport Report, string Path)? _resultDescription;

    /// <summary>Stops the run in progress; <see langword="null"/> when none is.</summary>
    private CancellationTokenSource? _cancel;

    /// <summary>The resource identity of the displayed StatusText.</summary>
    private LocalizedMessage _localizedStatusText = LocalizedMessage.Get(UiText, "Ready. Choose your settings, then Save a Copy.");

    /// <summary>The resource identity of the displayed Error.</summary>
    private LocalizedMessage _localizedError;

    /// <summary>Initializes a new instance of the <see cref="OptimizeCopyViewModel"/> class.</summary>
    /// <param name="optimizer">The open document's optimiser.</param>
    /// <param name="sourcePath">The path of the open document.</param>
    /// <param name="language">The language suggested for a document that has none, such as "en-AU".</param>
    public OptimizeCopyViewModel(IDocumentOptimizer optimizer, string sourcePath, string language)
        : this(optimizer, sourcePath, language, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OptimizeCopyViewModel"/> class that can describe pictures.</summary>
    /// <param name="optimizer">The open document's optimiser.</param>
    /// <param name="sourcePath">The path of the open document.</param>
    /// <param name="language">The language suggested for a document that has none, such as "en-AU".</param>
    /// <param name="pictureDescription">How pictures are described, or <see langword="null"/> when the window offers no such choice.</param>
    public OptimizeCopyViewModel(IDocumentOptimizer optimizer, string sourcePath, string language, PictureDescriptionSetup? pictureDescription)
    {
        ArgumentNullException.ThrowIfNull(optimizer);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        _optimizer = optimizer;
        _sourcePath = sourcePath;
        _pictureDescription = pictureDescription;
        FileName = Path.GetFileName(sourcePath);
        Language = language ?? string.Empty;
        SelectedPreset = Presets[1];
        _canSave = this.WhenChanged(static vm => vm.IsRunning).Select(static running => !running);
        _canCancel = this.WhenChanged(static vm => vm.IsRunning);

        // Closing the window stops a run in progress; the run itself disposes its source when it ends.
        _lifetime.Add(Scope.Create(Cancel));
    }

    /// <summary>Gets the size choices.</summary>
    public IReadOnlyList<OptimizePresetChoice> Presets { get; } =
    [

        new(
            OptimizePreset.Smaller,
            "Smaller file",
            "The smallest file. Pictures are scaled to about 150 dots per inch and compressed harder, so fine detail in photos and scans may " + "soften."),
        new(OptimizePreset.Balanced, "Balanced", "A good size with little visible change. Pictures are scaled to about 200 dots per inch. Best for most files."),
        new(
            OptimizePreset.KeepQuality,
            "Keep quality",
            "No picture loses detail. The file shrinks only by lossless means, such as better compression and removing repeats, so it may stay " + "large.")
    ];

    /// <summary>Gets the name of the open document.</summary>
    public string FileName { get; }

    /// <summary>Gets the interaction asking where to save the copy; the input is the suggested file name.</summary>
    public Interaction<string, string?> SaveInteraction { get; } = new();

    /// <summary>Gets or sets the size choice; none selected means balanced.</summary>
    [Reactive]
    public partial OptimizePresetChoice? SelectedPreset { get; set; }

    /// <summary>Gets or sets a value indicating whether a missing language, title and tagging flags are filled in.</summary>
    [Reactive]
    public partial bool FixAccessibility { get; set; } = true;

    /// <summary>Gets or sets the language written when the document has none, such as "en-AU".</summary>
    [Reactive]
    public partial string Language { get; set; }

    /// <summary>Gets or sets a value indicating whether a reading structure is guessed for an untagged document.</summary>
    [Reactive]
    public partial bool AddInferredTags { get; set; }

    /// <summary>Gets a value indicating whether the window can describe pictures, which it can when it was given a model to do it.</summary>
    public bool CanDescribePictures => _pictureDescription is not null;

    /// <summary>Gets what to tell people about describing pictures: that it runs on this computer and what it costs to start.</summary>
    public string PictureDescriptionNote => DescribeModel(_pictureDescription);

    /// <summary>
    /// Gets or sets a value indicating whether the pictures found while a reading structure is guessed are described by a model
    /// on this computer, and the description is written as their alternate text. It needs the guessed structure to be on.
    /// </summary>
    [Reactive]
    public partial bool DescribePictures { get; set; }

    /// <summary>Gets or sets a value indicating whether thumbnails, private data, unused resources and empty lists are removed.</summary>
    [Reactive]
    public partial bool CleanUp { get; set; }

    /// <summary>Gets a value indicating whether a copy is being written.</summary>
    [Reactive]
    public partial bool IsRunning { get; private set; }

    /// <summary>Gets how far along the copy is, from 0 to 100.</summary>
    [Reactive]
    public partial double ProgressPercent { get; private set; }

    /// <summary>Gets what the run is doing now, or how it ended.</summary>
    public string StatusText { get => _localizedStatusText.Value ?? string.Empty; private set => SetStatusText(LocalizedMessage.Literal(value)); }

    /// <summary>Gets what went wrong, or <see langword="null"/>.</summary>
    public string? Error { get => _localizedError.Value; private set => SetError(LocalizedMessage.Literal(value)); }

    /// <summary>Gets the plain-words summary of the last finished copy; empty before one finishes.</summary>
    [Reactive]
    public partial string ResultText { get; private set; } = string.Empty;

    /// <summary>Gets the report of the last finished copy, or <see langword="null"/>.</summary>
    [Reactive]
    public partial OptimizeReport? Report { get; private set; }

    /// <summary>Gets the path of the last saved copy, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? SavedPath { get; private set; }

    /// <summary>Gets the settings the dialog currently describes.</summary>
    public OptimizeSettings Settings => new(SelectedPreset?.Preset ?? OptimizePreset.Balanced, FixAccessibility, Language, AddInferredTags, CleanUp);

    /// <summary>Gets the file name suggested for the copy.</summary>
    public string SuggestedName => $"{Path.GetFileNameWithoutExtension(FileName)} (optimised).pdf";

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _lifetime.Dispose();

    /// <summary>Sets the displayed StatusText while retaining its resource identity.</summary>
    /// <param name="message">The message.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetStatusText(LocalizedMessage message) => this.RaiseAndSetIfChanged(ref _localizedStatusText, message, nameof(StatusText));

    /// <summary>Sets the displayed Error while retaining its resource identity.</summary>
    /// <param name="message">The message.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetError(in LocalizedMessage message) => this.RaiseAndSetIfChanged(ref _localizedError, message, nameof(Error));

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(PictureDescriptionNote));
        RefreshResultText();
        this.RaisePropertyChanged(nameof(StatusText));
        this.RaisePropertyChanged(nameof(Error));
        LocalisationRefresh.RefreshCore(Presets, visited);
    }

    /// <summary>Tells people what describing pictures costs: a one-off download, and that it runs on this computer.</summary>
    /// <param name="setup">How pictures are described, or <see langword="null"/>.</param>
    /// <returns>The note, or empty when pictures cannot be described.</returns>
    private static string DescribeModel(PictureDescriptionSetup? setup)
    {
        if (setup is null)
        {
            return string.Empty;
        }

        var missing = setup.MissingBytes();
        var megabytes = (missing + HalfMegabyte) / BytesPerMegabyte;
        return missing > 0
            ? UiText.Format("Runs on this computer. The first time, it downloads about {0} MB from this project's release.", megabytes)
            : UiText.Get("Runs on this computer. Nothing is sent anywhere.");
    }

    /// <summary>Determines whether two paths name the same file.</summary>
    /// <param name="first">The first path.</param>
    /// <param name="second">The second path.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    private static bool IsSameFile(string first, string second)
    {
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), comparison);
    }

    /// <summary>Deletes a temporary file, ignoring a failure because the file is only a leftover.</summary>
    /// <param name="path">The file.</param>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A leftover temporary file does no harm and a later save replaces it.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }

    /// <summary>Stops the copy in progress.</summary>
    [ReactiveCommand(CanExecute = nameof(_canCancel))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Cancel() => Volatile.Read(ref _cancel)?.Cancel();

    /// <summary>Stops any copy in progress and asks the window to close.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Close() => Cancel();

    /// <summary>Asks where to save, then writes the optimised copy there with progress.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canSave))]
    private async Task SaveAsync()
    {
        SetError(LocalizedMessage.Literal(null));
        var path = await SaveInteraction.Handle(SuggestedName).ToTask().ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (IsSameFile(path, _sourcePath))
        {
            SetError(LocalizedMessage.Get(UiText, "That would replace the file you have open. Choose a different name for the copy."));
            return;
        }

        var cancel = new CancellationTokenSource();
        Volatile.Write(ref _cancel, cancel);
        IsRunning = true;
        ProgressPercent = 0;
        _resultDescription = null;
        ResultText = string.Empty;
        try
        {
            var report = await WriteAsync(path, cancel.Token).ConfigureAwait(true);
            ShowResult(report, path);
        }
        catch (OperationCanceledException)
        {
            SetStatusText(LocalizedMessage.Get(UiText, "Cancelled. No file was saved."));
            ProgressPercent = 0;
        }
        catch (Exception ex) when (CopyFailure.IsExpected(ex))
        {
            SetError(LocalizedMessage.Format(UiText, "Could not save the copy: {0}", CopyFailure.Message(ex)));
            SetStatusText(LocalizedMessage.Get(UiText, "The copy was not saved."));
            ProgressPercent = 0;
        }
        finally
        {
            Volatile.Write(ref _cancel, null);
            cancel.Dispose();
            IsRunning = false;
        }
    }

    /// <summary>Writes the copy to a temporary file, off the UI thread, then moves it into place.</summary>
    /// <param name="path">Where the copy goes.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The report.</returns>
    private async Task<OptimizeReport> WriteAsync(string path, CancellationToken cancellationToken)
    {
        var temporary = path + TemporarySuffix;
        var settings = Settings;

        // Created here, on the UI thread, so each report is posted back to it.
        var progress = new Progress<OptimizeProgress>(ShowProgress);
        var describer = DescribePictures && AddInferredTags && _pictureDescription is { } setup
            ? await PrepareDescriberAsync(setup, cancellationToken).ConfigureAwait(true)
            : null;
        if (describer is not null)
        {
            settings = settings with { FigureDescriber = describer };
        }

        try
        {
            OptimizeReport report;
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous))
            {
                // The optimiser snapshots the document and plans before its first await: nearly all of an 8-page run runs
                // on the caller's thread (HyperPdfOptimizeCopyBenchmarks.TimeBeforeFirstWrite). The library cannot hop threads itself
                // (no Task.Run there, and an await only yields back to this thread), so the app starts it on the pool.
                report = await Task.Run(() => _optimizer.OptimizeAsync(stream, settings, progress, cancellationToken), cancellationToken).ConfigureAwait(true);
            }

            FileReplacement.Replace(temporary, path);
            return report;
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
        finally
        {
            (describer as IDisposable)?.Dispose();
        }
    }

    /// <summary>Downloads the picture model when it is missing and creates the describer.</summary>
    /// <param name="setup">How pictures are described.</param>
    /// <param name="cancellationToken">A token that cancels the download.</param>
    /// <returns>The describer.</returns>
    private async Task<IFigureDescriber> PrepareDescriberAsync(PictureDescriptionSetup setup, CancellationToken cancellationToken)
    {
        if (setup.MissingBytes() > 0)
        {
            SetStatusText(LocalizedMessage.Get(UiText, "Downloading the picture model."));
            await setup.Download(new Progress<double>(static _ => { }), cancellationToken).ConfigureAwait(true);
        }

        return setup.CreateDescriber();
    }

    /// <summary>Shows the progress of the run.</summary>
    /// <param name="progress">The step and its progress.</param>
    private void ShowProgress(OptimizeProgress progress)
    {
        if (!IsRunning)
        {
            return;
        }

        ProgressPercent = OptimizeReportText.OverallPercent(progress);
        SetStatusText(
            progress.Total > 0
                ? LocalizedMessage.Format(
                    UiText,
                    "{0}: {1} of {2}",
                    OptimizeReportText.StepMessage(progress.Step),
                    progress.Completed,
                    progress.Total)
                : OptimizeReportText.StepMessage(progress.Step));
    }

    /// <summary>Shows a finished copy's summary.</summary>
    /// <param name="report">The report.</param>
    /// <param name="path">Where the copy was saved.</param>
    private void ShowResult(OptimizeReport report, string path)
    {
        Report = report;
        SavedPath = path;
        ProgressPercent = OptimizeReportText.OverallPercent(new(OptimizeStep.Done, 0, 0));
        SetStatusText(LocalizedMessage.Get(UiText, "Done. The copy is saved."));
        _resultDescription = (report, path);
        this.RaisePropertyChanged(nameof(PictureDescriptionNote));
        RefreshResultText();
    }

    /// <summary>Rewords the retained report without repeating the operation.</summary>
    private void RefreshResultText()
    {
        if (_resultDescription is not { } description)
        {
            return;
        }

        var(report, path) = description;
        ResultText = OptimizeReportText.Join(OptimizeReportText.Lines(report, path));
    }
}
