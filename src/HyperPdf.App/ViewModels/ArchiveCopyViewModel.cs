// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Archiving;
using HyperPdf.Core.Platform;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// The "Save PDF/A Copy" window: a PDF/A level, a save button that asks where to save, a busy bar with Cancel, and a
/// plain-words summary of what changed or why the copy could not be made. The open file is never overwritten; the copy
/// is written to a temporary file and moved into place only once it is complete and meets the level.
/// </summary>
[DebuggerDisplay("ArchiveCopyViewModel: {FileName}")]
public sealed partial class ArchiveCopyViewModel : ReactiveObject, ICopyWindowRequest
{
    /// <summary>The ending of the temporary file the copy is written to.</summary>
    private const string TemporarySuffix = ".archiving";

    /// <summary>Why a chosen destination was refused.</summary>
    private const string OverwriteMessage = "That would replace the file you have open. Choose a different name for the copy.";

    /// <summary>The status shown before anything has happened.</summary>
    private const string ReadyStatus = "Ready. Choose a level, then Save a Copy.";

    /// <summary>The index of the suggested level, PDF/A-2b.</summary>
    private const int SuggestedLevel = 1;

    /// <summary>The bar's value once a copy is saved.</summary>
    private const double FullBar = 100;

    /// <summary>The writer the open document provides.</summary>
    private readonly IArchiveCopyWriter _writer;

    /// <summary>The path of the open document, which must never be overwritten.</summary>
    private readonly string _sourcePath;

    /// <summary>Whether a run is not in progress, so the level can change and a copy can start.</summary>
    private readonly IObservable<bool> _canSave;

    /// <summary>Whether a run is in progress, so it can be cancelled.</summary>
    private readonly IObservable<bool> _canCancel;

    /// <summary>Stops the run in progress; <see langword="null"/> when none is.</summary>
    private CancellationTokenSource? _cancel;

    /// <summary>Initializes a new instance of the <see cref="ArchiveCopyViewModel"/> class.</summary>
    /// <param name="writer">The open document's PDF/A copy writer.</param>
    /// <param name="sourcePath">The path of the open document.</param>
    public ArchiveCopyViewModel(IArchiveCopyWriter writer, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        _writer = writer;
        _sourcePath = sourcePath;
        FileName = Path.GetFileName(sourcePath);
        SelectedLevel = Levels[SuggestedLevel];
        _canSave = this.WhenChanged(static vm => vm.IsRunning).Select(static running => !running);
        _canCancel = this.WhenChanged(static vm => vm.IsRunning);
    }

    /// <summary>Gets the level choices, oldest first, with PDF/A-2b suggested.</summary>
    public IReadOnlyList<ArchiveLevelChoice> Levels { get; } =
    [
        new(ArchiveLevel.PdfA1B, "PDF/A-1b", "The oldest level. Use it only when a system asks for PDF/A-1. It does not allow see-through effects or attached files."),
        new(ArchiveLevel.PdfA2B, "PDF/A-2b (suggested)", "The pages will look the same in years to come. Allows see-through effects and layers. Best for most files."),
        new(ArchiveLevel.PdfA2U, "PDF/A-2u", "As PDF/A-2b, and all text can be searched and copied."),
        new(ArchiveLevel.PdfA2A, "PDF/A-2a", "As PDF/A-2u, and the file must have a reading structure (tags) for screen readers."),
        new(ArchiveLevel.PdfA3B, "PDF/A-3b", "As PDF/A-2b, and any kind of file may be attached."),
        new(ArchiveLevel.PdfA3U, "PDF/A-3u", "As PDF/A-3b, and all text can be searched and copied."),
        new(ArchiveLevel.PdfA3A, "PDF/A-3a", "As PDF/A-3u, and the file must have a reading structure (tags) for screen readers."),
        new(ArchiveLevel.PdfA4, "PDF/A-4", "The newest level, based on PDF 2.0. Attached files must also be PDF/A."),
        new(ArchiveLevel.PdfA4E, "PDF/A-4e", "PDF/A-4 for engineering files, which may hold 3D models."),
        new(ArchiveLevel.PdfA4F, "PDF/A-4f", "PDF/A-4 with attached files of any kind."),
    ];

    /// <summary>Gets the name of the open document.</summary>
    public string FileName { get; }

    /// <summary>Gets the interaction asking where to save the copy; the input is the suggested file name.</summary>
    public Interaction<string, string?> SaveInteraction { get; } = new();

    /// <summary>Gets or sets the level choice; none selected means PDF/A-2b.</summary>
    [Reactive]
    public partial ArchiveLevelChoice? SelectedLevel { get; set; }

    /// <summary>Gets a value indicating whether a copy is being written.</summary>
    [Reactive]
    public partial bool IsRunning { get; private set; }

    /// <summary>Gets how full the bar is, from 0 to 100: full once a copy is saved, empty otherwise; it moves while running.</summary>
    [Reactive]
    public partial double ProgressPercent { get; private set; }

    /// <summary>Gets what the run is doing now, or how it ended.</summary>
    [Reactive]
    public partial string StatusText { get; private set; } = ReadyStatus;

    /// <summary>Gets what went wrong, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? Error { get; private set; }

    /// <summary>Gets the plain-words summary of the last finished run; empty before one finishes.</summary>
    [Reactive]
    public partial string ResultText { get; private set; } = string.Empty;

    /// <summary>Gets the result of the last finished run, or <see langword="null"/>.</summary>
    [Reactive]
    public partial ArchiveCopyResult? Result { get; private set; }

    /// <summary>Gets the path of the last saved copy, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? SavedPath { get; private set; }

    /// <summary>Gets the level the window currently describes.</summary>
    public ArchiveLevel Level => SelectedLevel?.Level ?? ArchiveLevel.PdfA2B;

    /// <summary>Gets the file name suggested for the copy.</summary>
    public string SuggestedName => $"{Path.GetFileNameWithoutExtension(FileName)} (PDF-A).pdf";

    /// <inheritdoc/>
    public void Dispose()
    {
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

    /// <summary>Asks where to save, then writes the PDF/A copy there.</summary>
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
        StatusText = $"Making a {ArchiveText.Label(Level)} copy. This can take a while for long files.";
        try
        {
            ShowResult(await WriteAsync(path, cancel.Token).ConfigureAwait(true), path);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled. No file was saved.";
        }
        catch (Exception ex) when (CopyFailure.IsExpected(ex))
        {
            Error = $"Could not save the copy: {CopyFailure.Describe(ex)}";
            StatusText = "The copy was not saved.";
        }
        finally
        {
            Volatile.Write(ref _cancel, null);
            cancel.Dispose();
            IsRunning = false;
        }
    }

    /// <summary>Writes the copy to a temporary file, off the UI thread, then moves it into place when it was made.</summary>
    /// <param name="path">Where the copy goes.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The result.</returns>
    private async Task<ArchiveCopyResult> WriteAsync(string path, CancellationToken cancellationToken)
    {
        var temporary = path + TemporarySuffix;
        var level = Level;
        try
        {
            ArchiveCopyResult result;
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous))
            {
                // The conversion reads and checks the whole document before its first await, so the app starts it on
                // the pool to keep the window responsive; the library cannot hop threads itself.
                result = await Task.Run(() => _writer.WriteArchiveCopyAsync(stream, level, cancellationToken), cancellationToken).ConfigureAwait(true);
            }

            if (!result.Converted)
            {
                TryDelete(temporary);
                return result;
            }

            FileReplacement.Replace(temporary, path);
            return result;
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    /// <summary>Shows a finished run's summary.</summary>
    /// <param name="result">The result.</param>
    /// <param name="path">Where the copy was to be saved.</param>
    private void ShowResult(ArchiveCopyResult result, string path)
    {
        Result = result;
        SavedPath = result.Converted ? path : null;
        ProgressPercent = result.Converted ? FullBar : 0;
        StatusText = result.Converted ? "Done. The copy is saved." : "The copy could not be made. See the reasons below.";
        ResultText = OptimizeReportText.Join(ArchiveText.Lines(result, path));
    }
}
