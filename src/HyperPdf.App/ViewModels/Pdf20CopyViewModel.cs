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
/// Writes a PDF 2.0 copy of the open document: asks where to save it, makes it while showing progress that can be
/// cancelled, and keeps a plain-words summary of what changed or why the copy was refused. The open file is never replaced.
/// </summary>
[DebuggerDisplay("Pdf20CopyViewModel: {FileName}")]
public sealed partial class Pdf20CopyViewModel : ReactiveObject, ICopyWindowRequest
{
    /// <summary>The ending of the temporary file the copy is written to.</summary>
    private const string TemporarySuffix = ".pdf20";

    /// <summary>Why a chosen destination was refused.</summary>
    private const string OverwriteMessage = "That would replace the file you have open. Choose a different name for the copy.";

    /// <summary>The status shown before anything has happened.</summary>
    private const string ReadyStatus = "Ready. Choose Save Copy to make a PDF 2.0 file.";

    /// <summary>The bar's value once a copy is saved.</summary>
    private const double FullBar = 100;

    /// <summary>The writer the open document provides.</summary>
    private readonly IPdf20CopyWriter _writer;

    /// <summary>The path of the open document, which must never be overwritten.</summary>
    private readonly string _sourcePath;

    /// <summary>Whether a run is not in progress, so a copy can start.</summary>
    private readonly IObservable<bool> _canSave;

    /// <summary>Whether a run is in progress, so it can be cancelled.</summary>
    private readonly IObservable<bool> _canCancel;

    /// <summary>Stops the run in progress; <see langword="null"/> when none is.</summary>
    private CancellationTokenSource? _cancel;

    /// <summary>Initializes a new instance of the <see cref="Pdf20CopyViewModel"/> class.</summary>
    /// <param name="writer">The open document's PDF 2.0 copy writer.</param>
    /// <param name="sourcePath">The path of the open document.</param>
    public Pdf20CopyViewModel(IPdf20CopyWriter writer, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        _writer = writer;
        _sourcePath = sourcePath;
        FileName = Path.GetFileName(sourcePath);
        _canSave = this.WhenChanged(static vm => vm.IsRunning).Select(static running => !running);
        _canCancel = this.WhenChanged(static vm => vm.IsRunning);
    }

    /// <summary>Gets the name of the open document.</summary>
    public string FileName { get; }

    /// <summary>Gets the interaction asking where to save the copy; the input is the suggested file name.</summary>
    public Interaction<string, string?> SaveInteraction { get; } = new();

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
    public partial Pdf20CopyResult? Result { get; private set; }

    /// <summary>Gets the path of the last saved copy, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? SavedPath { get; private set; }

    /// <summary>Gets the file name suggested for the copy.</summary>
    public string SuggestedName => $"{Path.GetFileNameWithoutExtension(FileName)} (PDF 2.0).pdf";

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

    /// <summary>Asks where to save, then writes the PDF 2.0 copy there.</summary>
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
        StatusText = "Making a PDF 2.0 copy. This can take a while for long files.";
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
    private async Task<Pdf20CopyResult> WriteAsync(string path, CancellationToken cancellationToken)
    {
        var temporary = path + TemporarySuffix;
        try
        {
            Pdf20CopyResult result;
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous))
            {
                // The conversion reads and rewrites the whole document before its first await, so the app starts it on
                // the pool to keep the window responsive; the library cannot hop threads itself.
                result = await Task.Run(() => _writer.WritePdf20CopyAsync(stream, cancellationToken), cancellationToken).ConfigureAwait(true);
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
    private void ShowResult(Pdf20CopyResult result, string path)
    {
        Result = result;
        SavedPath = result.Converted ? path : null;
        ProgressPercent = result.Converted ? FullBar : 0;
        StatusText = result.Converted ? "Done. The copy is saved." : "The copy could not be made. See the reasons below.";
        ResultText = OptimizeReportText.Join(Pdf20CopyText.Lines(result, path));
    }
}
