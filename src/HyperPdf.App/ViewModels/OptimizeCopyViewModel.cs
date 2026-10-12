// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Optimizing;
using HyperPdf.Core.Platform;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// The "Save Optimised Copy" window: a size preset, accessibility options, a save button that asks where to save, a
/// progress bar with Cancel, and a plain-words summary. The open file is never overwritten; the copy is written to a
/// temporary file and moved into place once it is complete, so a cancel or a failure leaves no half-written file.
/// </summary>
[DebuggerDisplay("OptimizeCopyViewModel: {FileName}")]
public sealed partial class OptimizeCopyViewModel : ReactiveObject, IDisposable
{
    /// <summary>The ending of the temporary file the copy is written to.</summary>
    private const string TemporarySuffix = ".optimising";

    /// <summary>Why a chosen destination was refused.</summary>
    private const string OverwriteMessage = "That would replace the file you have open. Choose a different name for the copy.";

    /// <summary>The status shown before anything has happened.</summary>
    private const string ReadyStatus = "Ready. Choose your settings, then Save a Copy.";

    /// <summary>The optimiser the open document provides.</summary>
    private readonly IDocumentOptimizer _optimizer;

    /// <summary>The path of the open document, which must never be overwritten.</summary>
    private readonly string _sourcePath;

    /// <summary>Whether a run is not in progress, so settings can change and a copy can start.</summary>
    private readonly IObservable<bool> _canSave;

    /// <summary>Whether a run is in progress, so it can be cancelled.</summary>
    private readonly IObservable<bool> _canCancel;

    /// <summary>Stops the run in progress; <see langword="null"/> when none is.</summary>
    private CancellationTokenSource? _cancel;

    /// <summary>Initializes a new instance of the <see cref="OptimizeCopyViewModel"/> class.</summary>
    /// <param name="optimizer">The open document's optimiser.</param>
    /// <param name="sourcePath">The path of the open document.</param>
    /// <param name="language">The language suggested for a document that has none, such as "en-AU".</param>
    public OptimizeCopyViewModel(IDocumentOptimizer optimizer, string sourcePath, string language)
    {
        ArgumentNullException.ThrowIfNull(optimizer);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        _optimizer = optimizer;
        _sourcePath = sourcePath;
        FileName = Path.GetFileName(sourcePath);
        Language = language ?? string.Empty;
        SelectedPreset = Presets[1];
        _canSave = this.WhenChanged(static vm => vm.IsRunning).Select(static running => !running);
        _canCancel = this.WhenChanged(static vm => vm.IsRunning);
    }

    /// <summary>Gets the size choices.</summary>
    public IReadOnlyList<OptimizePresetChoice> Presets { get; } =
    [
        new(OptimizePreset.Smaller, "Smaller file", "The smallest file. Pictures are scaled to about 150 dots per inch and compressed harder, so fine detail in photos and scans may soften."),
        new(OptimizePreset.Balanced, "Balanced", "A good size with little visible change. Pictures are scaled to about 200 dots per inch. Best for most files."),
        new(OptimizePreset.KeepQuality, "Keep quality", "No picture loses detail. The file shrinks only by lossless means, such as better compression and removing repeats, so it may stay large."),
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
    [Reactive]
    public partial string StatusText { get; private set; } = ReadyStatus;

    /// <summary>Gets what went wrong, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? Error { get; private set; }

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
    public void Dispose()
    {
        // Cancelling first lets a run in progress stop; the run disposes the source again, which is harmless.
        Volatile.Read(ref _cancel)?.Cancel();
        _cancel?.Dispose();
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
        Error = null;
        var path = await SaveInteraction.Handle(SuggestedName).ToTask().ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (IsSameFile(path, _sourcePath))
        {
            Error = OverwriteMessage;
            return;
        }

        var cancel = new CancellationTokenSource();
        Volatile.Write(ref _cancel, cancel);
        IsRunning = true;
        ProgressPercent = 0;
        ResultText = string.Empty;
        try
        {
            var report = await WriteAsync(path, cancel.Token).ConfigureAwait(true);
            ShowResult(report, path);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled. No file was saved.";
            ProgressPercent = 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Error = $"Could not save the copy: {ex.Message}";
            StatusText = "The copy was not saved.";
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
        var progress = new Progress<OptimizeProgress>(OnProgress);
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
    }

    /// <summary>Shows the progress of the run.</summary>
    /// <param name="progress">The step and its progress.</param>
    private void OnProgress(OptimizeProgress progress)
    {
        if (!IsRunning)
        {
            return;
        }

        ProgressPercent = OptimizeReportText.OverallPercent(progress);
        StatusText = progress.Total > 0
            ? string.Create(CultureInfo.CurrentCulture, $"{OptimizeReportText.DescribeStep(progress.Step)}: {progress.Completed} of {progress.Total}")
            : OptimizeReportText.DescribeStep(progress.Step);
    }

    /// <summary>Shows a finished copy's summary.</summary>
    /// <param name="report">The report.</param>
    /// <param name="path">Where the copy was saved.</param>
    private void ShowResult(OptimizeReport report, string path)
    {
        Report = report;
        SavedPath = path;
        ProgressPercent = OptimizeReportText.OverallPercent(new(OptimizeStep.Done, 0, 0));
        StatusText = "Done. The copy is saved.";
        ResultText = OptimizeReportText.Join(OptimizeReportText.Lines(report, path));
    }
}
