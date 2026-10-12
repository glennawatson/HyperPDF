// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Redaction;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// The "Apply Redactions" window. It says what will be removed for good, asks the person to confirm they understand it
/// cannot be undone once saved, then writes a new file to a temporary name and moves it into place. The open file is never
/// overwritten and keeps its marks, so a cancel or a failure loses nothing.
/// </summary>
[DebuggerDisplay("RedactionViewModel: {FileName}, {AreaCount} areas")]
public sealed partial class RedactionViewModel : ReactiveObject, IDisposable
{
    /// <summary>The ending of the temporary file the copy is written to.</summary>
    private const string TemporarySuffix = ".redacting";

    /// <summary>Why a chosen destination was refused.</summary>
    private const string OverwriteMessage = "That would replace the file you have open. Choose a different name for the redacted copy.";

    /// <summary>The status shown before anything has happened.</summary>
    private const string ReadyStatus = "Ready. Tick the box to confirm, then Apply and Save As.";

    /// <summary>The warning shown above the confirmation.</summary>
    private const string Warning =
        "Redaction removes the marked text, pictures and drawings from the new file for good. "
        + "After you save it, the removed content cannot be brought back. Your open file is not changed, so keep it if you may need the original.";

    /// <summary>The redactor the open document provides.</summary>
    private readonly IDocumentRedactor _redactor;

    /// <summary>The path of the open document, which must never be overwritten.</summary>
    private readonly string _sourcePath;

    /// <summary>Whether the apply command can run: confirmed, not running and something marked.</summary>
    private readonly IObservable<bool> _canApply;

    /// <summary>Whether a run is in progress, so it can be cancelled.</summary>
    private readonly IObservable<bool> _canCancel;

    /// <summary>Stops the run in progress; <see langword="null"/> when none is.</summary>
    private CancellationTokenSource? _cancel;

    /// <summary>Initializes a new instance of the <see cref="RedactionViewModel"/> class.</summary>
    /// <param name="redactor">The open document's redactor.</param>
    /// <param name="sourcePath">The path of the open document.</param>
    /// <param name="areaCount">The areas marked for redaction.</param>
    /// <param name="pageCount">The pages that hold marks.</param>
    /// <param name="hasUnsavedChanges">Whether the open document has changes that are not saved yet.</param>
    public RedactionViewModel(IDocumentRedactor redactor, string sourcePath, int areaCount, int pageCount, bool hasUnsavedChanges)
    {
        ArgumentNullException.ThrowIfNull(redactor);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        _redactor = redactor;
        _sourcePath = sourcePath;
        FileName = Path.GetFileName(sourcePath);
        AreaCount = areaCount;
        SelectedImage = ImageOptions[1];
        SelectedLineArt = LineArtOptions[1];
        Summary = areaCount == 0
            ? "Nothing is marked. Use Mark Area to Redact or Mark Text to Redact first."
            : string.Create(CultureInfo.CurrentCulture, $"{areaCount} marked {(areaCount == 1 ? "area" : "areas")} on {pageCount} {(pageCount == 1 ? "page" : "pages")} will be removed.");
        SourceNote = hasUnsavedChanges ? "Marks and comments you have not saved yet are included in the redacted copy." : null;
        _canApply = this.WhenChanged(static vm => vm.IsRunning, static vm => vm.Understands, static (running, understood) => !running && understood);
        _canCancel = this.WhenChanged(static vm => vm.IsRunning);
    }

    /// <summary>Gets the choices for pictures under a mark.</summary>
    public IReadOnlyList<RedactionImageOption> ImageOptions { get; } =
    [
        new(RedactionImageChoice.Keep, "Leave pictures alone", "Pictures are not changed, even under a mark."),
        new(RedactionImageChoice.BlankPart, "Blank the covered part", "Only the part of a picture under a mark is blanked. The rest of the picture stays."),
        new(RedactionImageChoice.Remove, "Remove the whole picture", "A picture a mark touches is removed completely."),
    ];

    /// <summary>Gets the choices for drawings under a mark.</summary>
    public IReadOnlyList<RedactionLineArtOption> LineArtOptions { get; } =
    [
        new(RedactionLineArtChoice.Keep, "Leave drawings alone", "Lines and shapes are not changed, even under a mark."),
        new(RedactionLineArtChoice.RemoveCovered, "Remove drawings that are fully covered", "A line or shape is removed only when a mark covers all of it."),
        new(RedactionLineArtChoice.RemoveTouched, "Remove every drawing a mark touches", "Any line or shape a mark touches is removed, which can include backgrounds."),
    ];

    /// <summary>Gets the name of the open document.</summary>
    public string FileName { get; }

    /// <summary>Gets the number of areas marked.</summary>
    public int AreaCount { get; }

    /// <summary>Gets the plain-words count of what will be removed.</summary>
    public string Summary { get; }

    /// <summary>Gets a note about the open document, or <see langword="null"/>.</summary>
    public string? SourceNote { get; }

    /// <summary>Gets the warning that applying cannot be undone once the copy is saved.</summary>
    public string WarningText => Warning;

    /// <summary>Gets the interaction asking where to save the copy; the input is the suggested file name.</summary>
    public Interaction<string, string?> SaveInteraction { get; } = new();

    /// <summary>Gets or sets what happens to pictures under a mark.</summary>
    [Reactive]
    public partial RedactionImageOption? SelectedImage { get; set; }

    /// <summary>Gets or sets what happens to drawings under a mark.</summary>
    [Reactive]
    public partial RedactionLineArtOption? SelectedLineArt { get; set; }

    /// <summary>Gets or sets a value indicating whether invisible text under a mark, such as a scan's text layer, is removed too.</summary>
    [Reactive]
    public partial bool RemoveHiddenText { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether links, comments and form fields under a mark are removed.</summary>
    [Reactive]
    public partial bool RemoveLinksAndComments { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the document information and metadata are removed.</summary>
    [Reactive]
    public partial bool ScrubMetadata { get; set; }

    /// <summary>Gets or sets a value indicating whether the person confirmed they understand the removal cannot be undone once saved.</summary>
    [Reactive]
    public partial bool Understands { get; set; }

    /// <summary>Gets a value indicating whether a copy is being written.</summary>
    [Reactive]
    public partial bool IsRunning { get; private set; }

    /// <summary>Gets what the run is doing now, or how it ended.</summary>
    [Reactive]
    public partial string StatusText { get; private set; } = ReadyStatus;

    /// <summary>Gets what went wrong, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? Error { get; private set; }

    /// <summary>Gets the plain-words summary of the last finished run; empty before one finishes.</summary>
    [Reactive]
    public partial string ResultText { get; private set; } = string.Empty;

    /// <summary>Gets the report of the last finished run, or <see langword="null"/>.</summary>
    [Reactive]
    public partial RedactionReport? Report { get; private set; }

    /// <summary>Gets the path of the last saved copy, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? SavedPath { get; private set; }

    /// <summary>Gets the settings the window currently describes.</summary>
    public RedactionSettings Settings =>
        new(SelectedImage?.Choice ?? RedactionImageChoice.BlankPart, SelectedLineArt?.Choice ?? RedactionLineArtChoice.RemoveCovered, RemoveHiddenText, RemoveLinksAndComments, ScrubMetadata);

    /// <summary>Gets the file name suggested for the copy.</summary>
    public string SuggestedName => $"{Path.GetFileNameWithoutExtension(FileName)} (redacted).pdf";

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

    /// <summary>Stops the run in progress.</summary>
    [ReactiveCommand(CanExecute = nameof(_canCancel))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Cancel() => Volatile.Read(ref _cancel)?.Cancel();

    /// <summary>Stops any run in progress and asks the window to close.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Close() => Cancel();

    /// <summary>Asks where to save, then applies the redactions and writes the copy there.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canApply))]
    private async Task ApplyAsync()
    {
        Error = null;
        if (AreaCount == 0)
        {
            Error = "Nothing is marked, so there is nothing to redact.";
            return;
        }

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
        ResultText = string.Empty;
        StatusText = "Removing the marked content…";
        try
        {
            var report = await WriteAsync(path, cancel.Token).ConfigureAwait(true);
            ShowResult(report, path);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled. No file was saved.";
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Error = $"Could not save the redacted copy: {ex.Message}";
            StatusText = "The redacted copy was not saved.";
        }
        finally
        {
            Volatile.Write(ref _cancel, null);
            cancel.Dispose();
            IsRunning = false;
        }
    }

    /// <summary>Writes the copy to a temporary file, then moves it into place.</summary>
    /// <param name="path">Where the copy goes.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The report.</returns>
    private async Task<RedactionReport> WriteAsync(string path, CancellationToken cancellationToken)
    {
        var temporary = path + TemporarySuffix;
        var settings = Settings;
        try
        {
            RedactionReport report;
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous))
            {
                report = await _redactor.ApplyAsync(stream, settings, cancellationToken).ConfigureAwait(true);
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

    /// <summary>Shows a finished run's summary.</summary>
    /// <param name="report">The report.</param>
    /// <param name="path">Where the copy was saved.</param>
    private void ShowResult(RedactionReport report, string path)
    {
        Report = report;
        SavedPath = path;
        StatusText = "Done. The redacted copy is saved.";
        var removed = string.Create(
            CultureInfo.CurrentCulture,
            $"Removed {report.Characters} text characters, {report.Pictures} pictures, {report.Drawings} drawings and {report.Annotations} links, comments or form fields from {report.Pages} pages.");
        var saved = string.Create(CultureInfo.CurrentCulture, $"The redacted copy is saved as {path}. Your open file was not changed.");
        ResultText = string.Join('\n', removed, saved, "Open the copy and check it before you share it.");
    }
}
