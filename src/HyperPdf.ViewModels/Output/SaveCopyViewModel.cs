// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Platform;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Output;

/// <summary>
/// The unified "Save a Copy" window: lets the person pick a copy format (Standard PDF, Optimised, PDF/A, PDF 2.0 or
/// Flattened), adjust its settings, and write the copy with progress, cancellation and a plain-words summary. The open
/// file is never overwritten.
/// </summary>
[DebuggerDisplay("SaveCopyViewModel: {FileName} ({Format})")]
public sealed partial class SaveCopyViewModel : ReactiveObject, ICopyWindowRequest, ILocalisationChildren
{
    /// <summary>The ending of the temporary file a standard copy is written to.</summary>
    private const string TemporarySuffix = ".saving";

    /// <summary>The bar's value once a copy is saved.</summary>
    private const double FullBar = 100;

    /// <summary>The index of the Optimized format in <see cref="Formats"/>.</summary>
    private const int OptimizedIndex = 1;

    /// <summary>The index of the Archive format in <see cref="Formats"/>.</summary>
    private const int ArchiveIndex = 2;

    /// <summary>The index of the Pdf20 format in <see cref="Formats"/>.</summary>
    private const int Pdf20Index = 3;

    /// <summary>The index of the Flattened format in <see cref="Formats"/>.</summary>
    private const int FlattenedIndex = 4;

    /// <summary>The ready status resource key.</summary>
    private const string ReadyStatusKey = "Ready. Choose a format and settings, then Save Copy.";

    /// <summary>The path of the open document, which must never be overwritten.</summary>
    private readonly string _sourcePath;

    /// <summary>The open document's editor, when available, used to write a standard copy with current edits.</summary>
    private readonly IAnnotationEditor? _editor;

    /// <summary>The open document's source, when available, so its unsaved flag is preserved after writing a copy.</summary>
    private readonly DocumentSource? _source;

    /// <summary>Whether a run is not in progress, so settings can change and a copy can start.</summary>
    private readonly IObservable<bool> _canSave;

    /// <summary>Whether a run is in progress, so it can be cancelled.</summary>
    private readonly IObservable<bool> _canCancel;

    /// <summary>Owns the sub-viewmodels and subscriptions until the window closes.</summary>
    private readonly MultipleDisposable _lifetime = [];

    /// <summary>Whether a standard copy is currently being written.</summary>
    private bool _standardRunning;

    /// <summary>The progress bar value of the standard copy.</summary>
    private double _standardProgress;

    /// <summary>The status text of the standard copy.</summary>
    private LocalizedMessage _standardStatus = LocalizedMessage.Get(UiText, ReadyStatusKey);

    /// <summary>The error text of the standard copy, or <see langword="null"/>.</summary>
    private LocalizedMessage _standardError;

    /// <summary>The summary text of the standard copy.</summary>
    private (string Path, DocumentSaveOutcome Outcome)? _standardDescription;

    /// <summary>The saved path of the standard copy, or <see langword="null"/>.</summary>
    private string? _standardSavedPath;

    /// <summary>The resource identity of the displayed StatusText.</summary>
    private LocalizedMessage _localizedStatusText = LocalizedMessage.Get(UiText, ReadyStatusKey);

    /// <summary>The resource identity of the displayed Error.</summary>
    private LocalizedMessage _localizedError;

    /// <summary>Initializes a new instance of the <see cref="SaveCopyViewModel"/> class.</summary>
    /// <param name="optimize">The optimised copy view model.</param>
    /// <param name="archive">The PDF/A copy view model.</param>
    /// <param name="pdf20">The PDF 2.0 copy view model.</param>
    /// <param name="flatten">The flattened copy view model.</param>
    /// <param name="sourcePath">The path of the open document.</param>
    /// <param name="editor">The open document's annotation editor, if any.</param>
    /// <param name="source">The open document's source, if any.</param>
    public SaveCopyViewModel(
        OptimizeCopyViewModel optimize,
        ArchiveCopyViewModel archive,
        Pdf20CopyViewModel pdf20,
        FlattenCopyViewModel flatten,
        string sourcePath,
        IAnnotationEditor? editor,
        DocumentSource? source)
    {
        ArgumentNullException.ThrowIfNull(optimize);
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(pdf20);
        ArgumentNullException.ThrowIfNull(flatten);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        Optimize = optimize;
        Archive = archive;
        Pdf20 = pdf20;
        Flatten = flatten;
        _sourcePath = sourcePath;
        _editor = editor;
        _source = source;
        FileName = Path.GetFileName(sourcePath);
        SelectedFormat = Formats[0];
        _canSave = this.WhenChanged(static vm => vm.IsRunning).Select(static running => !running);
        _canCancel = this.WhenChanged(static vm => vm.IsRunning);
        _lifetime.Add(this.WhenChanged(static vm => vm.Run).OwnLatest().SubscribeSafeErrors(ErrorTrace.Write));
        _lifetime.Add(optimize);
        _lifetime.Add(archive);
        _lifetime.Add(pdf20);
        _lifetime.Add(flatten);
        _lifetime.Add(Scope.Create(Cancel));
        _lifetime.Add(optimize.SaveInteraction.RegisterHandler(ForwardSaveAsync));
        _lifetime.Add(archive.SaveInteraction.RegisterHandler(ForwardSaveAsync));
        _lifetime.Add(pdf20.SaveInteraction.RegisterHandler(ForwardSaveAsync));
        _lifetime.Add(flatten.SaveInteraction.RegisterHandler(ForwardSaveAsync));
        WatchSubViewModels();
    }

    /// <summary>Gets the copy format choices.</summary>
    public IReadOnlyList<SaveCopyFormatChoice> Formats { get; } =
    [

        new(SaveCopyFormat.Standard, "Standard PDF", "Saves a copy of the document as it is now, keeping your annotations, form entries and page changes."),
        new(SaveCopyFormat.Optimized, "Optimised", "Saves a smaller or more accessible copy, or removes thumbnails and hidden extras."),
        new(SaveCopyFormat.Archive, "PDF/A (long-term archive)", "Saves a PDF/A copy made to keep its look for many years."),
        new(SaveCopyFormat.Pdf20, "PDF 2.0", "Saves a PDF 2.0 file, moving details from retired PDF features when possible."),
        new(SaveCopyFormat.Flattened, "Flattened", "Draws form fields, comments and other marks into the page so they can no longer be changed.")
    ];

    /// <summary>Gets the name of the open document.</summary>
    public string FileName { get; }

    /// <summary>Gets the optimised copy options and state.</summary>
    public OptimizeCopyViewModel Optimize { get; }

    /// <summary>Gets the PDF/A copy options and state.</summary>
    public ArchiveCopyViewModel Archive { get; }

    /// <summary>Gets the PDF 2.0 copy state.</summary>
    public Pdf20CopyViewModel Pdf20 { get; }

    /// <summary>Gets the flattened copy options and state.</summary>
    public FlattenCopyViewModel Flatten { get; }

    /// <summary>Gets the interaction asking where to save the copy; the input is the suggested file name.</summary>
    public Interaction<string, string?> SaveInteraction { get; } = new();

    /// <summary>Gets or sets the chosen copy format; none selected means Standard PDF.</summary>
    [Reactive(
        nameof(Format),
        nameof(IsStandardFormat),
        nameof(IsOptimizeFormat),
        nameof(IsArchiveFormat),
        nameof(IsPdf20Format),
        nameof(IsFlattenFormat),
        nameof(SuggestedName))]
    public partial SaveCopyFormatChoice? SelectedFormat { get; set; }

    /// <summary>Gets the chosen format enum value.</summary>
    public SaveCopyFormat Format => SelectedFormat?.Format ?? SaveCopyFormat.Standard;

    /// <summary>Gets a value indicating whether Standard PDF is selected.</summary>
    public bool IsStandardFormat => Format == SaveCopyFormat.Standard;

    /// <summary>Gets a value indicating whether Optimised is selected.</summary>
    public bool IsOptimizeFormat => Format == SaveCopyFormat.Optimized;

    /// <summary>Gets a value indicating whether PDF/A is selected.</summary>
    public bool IsArchiveFormat => Format == SaveCopyFormat.Archive;

    /// <summary>Gets a value indicating whether PDF 2.0 is selected.</summary>
    public bool IsPdf20Format => Format == SaveCopyFormat.Pdf20;

    /// <summary>Gets a value indicating whether Flattened is selected.</summary>
    public bool IsFlattenFormat => Format == SaveCopyFormat.Flattened;

    /// <summary>Gets the file name suggested for the selected format.</summary>
    public string SuggestedName => Format switch
    {
        SaveCopyFormat.Optimized => Optimize.SuggestedName,
        SaveCopyFormat.Archive => Archive.SuggestedName,
        SaveCopyFormat.Pdf20 => Pdf20.SuggestedName,
        SaveCopyFormat.Flattened => Flatten.SuggestedName,
        _ => $"{Path.GetFileNameWithoutExtension(FileName)} (copy).pdf",
    };

    /// <summary>Gets a value indicating whether a copy is being written.</summary>
    [Reactive]
    public partial bool IsRunning { get; private set; }

    /// <summary>Gets a value indicating whether the indeterminate busy bar should be shown instead of the percentage bar.</summary>
    [Reactive]
    public partial bool IsBusy { get; private set; }

    /// <summary>Gets how full the progress bar is, from 0 to 100.</summary>
    [Reactive]
    public partial double ProgressPercent { get; private set; }

    /// <summary>Gets what the run is doing now, or how it ended.</summary>
    public string StatusText { get => _localizedStatusText.Value ?? string.Empty; private set => SetStatusText(LocalizedMessage.Literal(value)); }

    /// <summary>Gets what went wrong, or <see langword="null"/>.</summary>
    public string? Error { get => _localizedError.Value; private set => SetError(LocalizedMessage.Literal(value)); }

    /// <summary>Gets the plain-words summary of the last finished run; empty before one finishes.</summary>
    [Reactive]
    public partial string ResultText { get; private set; } = string.Empty;

    /// <summary>Gets the path of the last saved copy, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? SavedPath { get; private set; }

    /// <summary>The standard copy run in progress, which disposing cancels, or <see langword="null"/> when none is.</summary>
    [Reactive]
    private partial CancellationDisposable? Run { get; set; }

    /// <summary>Gets the completed summary in the current language.</summary>
    private string StandardResultText => _standardDescription is { } description ? BuildStandardResult(description.Path, description.Outcome) : string.Empty;

    /// <summary>Selects the format matching <paramref name="format"/>.</summary>
    /// <param name="format">The format to select.</param>
    public void SelectFormat(SaveCopyFormat format)
    {
        var index = format switch
        {
            SaveCopyFormat.Optimized => OptimizedIndex,
            SaveCopyFormat.Archive => ArchiveIndex,
            SaveCopyFormat.Pdf20 => Pdf20Index,
            SaveCopyFormat.Flattened => FlattenedIndex,
            _ => 0,
        };
        SelectedFormat = Formats[index];
    }

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
    public void SetError(LocalizedMessage message) => this.RaiseAndSetIfChanged(ref _localizedError, message, nameof(Error));

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(StatusText));
        this.RaisePropertyChanged(nameof(Error));
        LocalisationRefresh.RefreshCore(Formats, visited);
        LocalisationRefresh.RefreshCore(Optimize, visited);
        LocalisationRefresh.RefreshCore(Archive, visited);
        LocalisationRefresh.RefreshCore(Pdf20, visited);
        LocalisationRefresh.RefreshCore(Flatten, visited);
        SyncState();
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

    /// <summary>Builds the plain-words summary for a finished standard PDF copy.</summary>
    /// <param name="path">Where the copy was saved.</param>
    /// <param name="outcome">The save report outcome.</param>
    /// <returns>The summary text.</returns>
    private static string BuildStandardResult(string path, in DocumentSaveOutcome outcome)
    {
        var summary = UiText.Format("Saved a standard PDF copy to {0}.", path);
        return SaveText.Notice(outcome) is { } notice ? $"{summary}\n{notice}" : summary;
    }

    /// <summary>Forwards a sub-viewmodel's save picker request to this window's <see cref="SaveInteraction"/>.</summary>
    /// <param name="context">The interaction context holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task ForwardSaveAsync(IInteractionContext<string, string?> context) =>
        context.SetOutput(await SaveInteraction.Handle(context.Input).ToTask().ConfigureAwait(true));

    /// <summary>Subscribes to the format choice and each sub-viewmodel so the window's status and progress stay in sync.</summary>
    private void WatchSubViewModels()
    {
        var sync = this.WhenChanged(static vm => vm.SelectedFormat).Select(static _ => RxVoid.Default)
            .Merge(Optimize.WhenChanged(static vm => vm.IsRunning, static vm => vm.ProgressPercent, static vm => vm.StatusText).Select(static _ => RxVoid.Default))
            .Merge(Optimize.WhenChanged(static vm => vm.Error, static vm => vm.ResultText, static vm => vm.SavedPath).Select(static _ => RxVoid.Default))
            .Merge(Archive.WhenChanged(static vm => vm.IsRunning, static vm => vm.ProgressPercent, static vm => vm.StatusText).Select(static _ => RxVoid.Default))
            .Merge(Archive.WhenChanged(static vm => vm.Error, static vm => vm.ResultText, static vm => vm.SavedPath).Select(static _ => RxVoid.Default))
            .Merge(Pdf20.WhenChanged(static vm => vm.IsRunning, static vm => vm.ProgressPercent, static vm => vm.StatusText).Select(static _ => RxVoid.Default))
            .Merge(Pdf20.WhenChanged(static vm => vm.Error, static vm => vm.ResultText, static vm => vm.SavedPath).Select(static _ => RxVoid.Default))
            .Merge(Flatten.WhenChanged(static vm => vm.IsRunning, static vm => vm.ProgressPercent, static vm => vm.StatusText).Select(static _ => RxVoid.Default))
            .Merge(Flatten.WhenChanged(static vm => vm.Error, static vm => vm.ResultText, static vm => vm.SavedPath).Select(static _ => RxVoid.Default));
        _lifetime.Add(sync.SelectWith(this, static (self, _) => self).SubscribeSafe(static self => self.SyncState(), ErrorTrace.Write));
    }

    /// <summary>Copies the active format's status, progress and result into this view model's properties.</summary>
    private void SyncState()
    {
        switch (Format)
        {
            case SaveCopyFormat.Optimized:
                {
                    ApplyState(Optimize.IsRunning, false, Optimize.ProgressPercent, Optimize.StatusText, Optimize.Error, Optimize.ResultText, Optimize.SavedPath);
                    break;
                }

            case SaveCopyFormat.Archive:
                {
                    ApplyState(Archive.IsRunning, Archive.IsRunning, Archive.ProgressPercent, Archive.StatusText, Archive.Error, Archive.ResultText, Archive.SavedPath);
                    break;
                }

            case SaveCopyFormat.Pdf20:
                {
                    ApplyState(Pdf20.IsRunning, Pdf20.IsRunning, Pdf20.ProgressPercent, Pdf20.StatusText, Pdf20.Error, Pdf20.ResultText, Pdf20.SavedPath);
                    break;
                }

            case SaveCopyFormat.Flattened:
                {
                    ApplyState(Flatten.IsRunning, Flatten.IsRunning, Flatten.ProgressPercent, Flatten.StatusText, Flatten.Error, Flatten.ResultText, Flatten.SavedPath);
                    break;
                }

            default:
                {
                    ApplyState(_standardRunning, _standardRunning, _standardProgress, _standardStatus.Value ?? string.Empty, _standardError.Value, StandardResultText, _standardSavedPath);
                    break;
                }
        }
    }

    /// <summary>Updates the exposed status and progress properties.</summary>
    /// <param name="running">Whether a copy is running.</param>
    /// <param name="busy">Whether the indeterminate bar should show.</param>
    /// <param name="progress">The progress percentage.</param>
    /// <param name="status">The status line.</param>
    /// <param name="error">The error line, if any.</param>
    /// <param name="result">The result summary.</param>
    /// <param name="savedPath">The saved file path, if any.</param>
    private void ApplyState(bool running, bool busy, double progress, string status, string? error, string result, string? savedPath)
    {
        IsRunning = running;
        IsBusy = busy;
        ProgressPercent = progress;
        SetStatusText(LocalizedMessage.Literal(status));
        SetError(LocalizedMessage.Literal(error));
        ResultText = result;
        SavedPath = savedPath;
    }

    /// <summary>Stops the copy in progress.</summary>
    [ReactiveCommand(CanExecute = nameof(_canCancel))]
    private void Cancel()
    {
        Run = null;
        StopSubViewModels();
    }

    /// <summary>Stops any running sub-viewmodel command.</summary>
    private void StopSubViewModels()
    {
        if (Optimize.IsRunning)
        {
            _ = Optimize.CancelCommand.Execute().SubscribeSafeErrors(ErrorTrace.Write);
        }

        if (Archive.IsRunning)
        {
            _ = Archive.CancelCommand.Execute().SubscribeSafeErrors(ErrorTrace.Write);
        }

        if (Pdf20.IsRunning)
        {
            _ = Pdf20.CancelCommand.Execute().SubscribeSafeErrors(ErrorTrace.Write);
        }

        if (Flatten.IsRunning)
        {
            _ = Flatten.CancelCommand.Execute().SubscribeSafeErrors(ErrorTrace.Write);
        }
    }

    /// <summary>Stops any copy in progress and asks the window to close.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Close() => Cancel();

    /// <summary>Asks where to save, then writes the copy in the chosen format.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canSave))]
    private Task SaveAsync() => Format switch
    {
        SaveCopyFormat.Optimized => Optimize.SaveCommand.Execute().ToTask(),
        SaveCopyFormat.Archive => Archive.SaveCommand.Execute().ToTask(),
        SaveCopyFormat.Pdf20 => Pdf20.SaveCommand.Execute().ToTask(),
        SaveCopyFormat.Flattened => Flatten.SaveCommand.Execute().ToTask(),
        _ => SaveStandardAsync(),
    };

    /// <summary>Asks where to save and writes a standard PDF copy.</summary>
    /// <returns>A task.</returns>
    private async Task SaveStandardAsync()
    {
        _standardError = LocalizedMessage.Literal(null);
        SyncState();
        var path = await SaveInteraction.Handle(SuggestedName).ToTask().ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (IsSameFile(path, _sourcePath))
        {
            _standardError = LocalizedMessage.Get(UiText, "That would replace the file you have open. Choose a different name for the copy.");
            SyncState();
            return;
        }

        var run = new CancellationDisposable();
        Run = run;
        _standardRunning = true;
        _standardProgress = 0;
        _standardDescription = null;
        _standardStatus = LocalizedMessage.Get(UiText, "Saving a standard PDF copy.");
        SyncState();
        try
        {
            var outcome = await WriteStandardAsync(path, run.Token).ConfigureAwait(true);
            FinishStandardRun(path, outcome);
        }
        catch (OperationCanceledException)
        {
            _standardStatus = LocalizedMessage.Get(UiText, "Cancelled. No file was saved.");
            _standardProgress = 0;
        }
        catch (Exception ex) when (CopyFailure.IsExpected(ex))
        {
            _standardError = LocalizedMessage.Format(UiText, "Could not save the copy: {0}", CopyFailure.Message(ex));
            _standardStatus = LocalizedMessage.Get(UiText, "The copy was not saved.");
            _standardProgress = 0;
        }
        finally
        {
            if (ReferenceEquals(Run, run))
            {
                Run = null;
            }

            _standardRunning = false;
            SyncState();
        }
    }

    /// <summary>Writes the standard copy to a temporary file and moves it into place.</summary>
    /// <param name="path">Where the copy goes.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The save outcome.</returns>
    private async Task<DocumentSaveOutcome> WriteStandardAsync(string path, CancellationToken cancellationToken)
    {
        if (_editor is not { } editor)
        {
            await CopyRawFileAsync(path, cancellationToken).ConfigureAwait(true);
            return new(true, null);
        }

        var wasDirty = _source?.HasUnsavedChanges ?? editor.HasUnsavedChanges;
        var temporary = path + TemporarySuffix;
        var report = new DocumentSaveReport();
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous };
            bool serialized;
            await using (var stream = new FileStream(temporary, options))
            {
                serialized = await Task.Run(() => editor.SaveAsync(stream, report, cancellationToken).AsTask(), cancellationToken).ConfigureAwait(true);
            }

            if (!serialized)
            {
                TryDelete(temporary);
                return new(false, report.Failure ?? UiText.Get("The document could not be written."));
            }

            FileReplacement.Replace(temporary, path);
            return new(true, null, report.ClaimRemoved)
            {
                ClaimRepaired = report.ClaimRepaired,
                TrapNetworkWarnings = report.TrapNetworkWarnings,
                RequirementWarnings = report.RequirementWarnings,
                RemovedRequirements = report.RemovedRequirements,
            };
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
        finally
        {
            if (wasDirty)
            {
                _source?.MarkSaveUnpublished();
            }
        }
    }

    /// <summary>Copies the source file to a temporary file and moves it into place when no editor is available.</summary>
    /// <param name="path">Where the copy goes.</param>
    /// <param name="cancellationToken">Stops the copy.</param>
    /// <returns>A task.</returns>
    private async Task CopyRawFileAsync(string path, CancellationToken cancellationToken)
    {
        var temporary = path + TemporarySuffix;
        try
        {
            var readOptions = new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.Read, Share = FileShare.ReadWrite, Options = FileOptions.Asynchronous };
            var writeOptions = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous };
            await using (var input = new FileStream(_sourcePath, readOptions))
            await using (var output = new FileStream(temporary, writeOptions))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(true);
            }

            FileReplacement.Replace(temporary, path);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    /// <summary>Applies the outcome of a finished standard copy run.</summary>
    /// <param name="path">Where the copy was to be saved.</param>
    /// <param name="outcome">The outcome.</param>
    private void FinishStandardRun(string path, in DocumentSaveOutcome outcome)
    {
        if (!outcome.Saved)
        {
            _standardError = outcome.Error is { } error ? LocalizedMessage.Format(UiText, "Could not save the copy: {0}", error) : LocalizedMessage.Get(UiText, "Could not save the copy.");
            _standardStatus = LocalizedMessage.Get(UiText, "The copy was not saved.");
            _standardProgress = 0;
            return;
        }

        _standardSavedPath = path;
        _standardProgress = FullBar;
        _standardStatus = LocalizedMessage.Get(UiText, "Done. The copy is saved.");
        _standardDescription = (path, outcome);
    }
}
