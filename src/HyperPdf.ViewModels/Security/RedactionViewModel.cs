// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Redaction;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Security;

/// <summary>
/// The "Apply Redactions" window. It says what will be removed for good, asks the person to confirm they understand it
/// cannot be undone once saved, then writes a new file to a temporary name and moves it into place. The open file is never
/// overwritten and keeps its marks, so a cancel or a failure loses nothing.
/// </summary>
[DebuggerDisplay("RedactionViewModel: {FileName}, {AreaCount} areas")]
public sealed partial class RedactionViewModel : ReactiveObject, ICopyWindowRequest, ILocalisationChildren
{
    /// <summary>The ending of the temporary file the copy is written to.</summary>
    private const string TemporarySuffix = ".redacting";

    /// <summary>The redactor the open document provides.</summary>
    private readonly IDocumentRedactor _redactor;

    /// <summary>The path of the open document, which must never be overwritten.</summary>
    private readonly string _sourcePath;

    /// <summary>Whether the apply command can run: confirmed, not running and something marked.</summary>
    private readonly IObservable<bool> _canApply;

    /// <summary>Whether a run is in progress, so it can be cancelled.</summary>
    private readonly IObservable<bool> _canCancel;

    /// <summary>Owns the run in progress until the window closes.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>The completed report and destination retained for language changes.</summary>
    private (RedactionReport Report, string Path)? _resultDescription;

    /// <summary>The resource identity of the displayed StatusText.</summary>
    private LocalizedMessage _localizedStatusText = LocalizedMessage.Get(UiText, "Ready. Tick the box to confirm, then Apply and Save As.");

    /// <summary>The resource identity of the displayed Error.</summary>
    private LocalizedMessage _localizedError;

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
            ? UiText.Get("Nothing is marked. Use Mark Area to Redact or Mark Text to Redact first.")
            : UiText.Format(
            "{0} marked {1} on {2} {3} will be removed.",
            areaCount,
            (areaCount == 1
                ? UiText.Get("area")
                : UiText
            .Get("areas")),
            pageCount,
            (pageCount == 1 ? UiText.Get("page") : UiText.Get("pages")));
        SourceNote = hasUnsavedChanges ? UiText.Get("Marks and comments you have not saved yet are included in the redacted copy.") : null;
        _canApply = this.WhenChanged(static vm => vm.IsRunning, static vm => vm.Understands, static (running, understood) => !running && understood);
        _canCancel = this.WhenChanged(static vm => vm.IsRunning);
        _subscriptions.Add(this.WhenChanged(static vm => vm.Run).OwnLatest().SubscribeSafeErrors(ErrorTrace.Write));
    }

    /// <summary>Gets the choices for pictures under a mark.</summary>
    public IReadOnlyList<RedactionImageOption> ImageOptions { get; } =
    [

        new(RedactionImageChoice.Keep, "Leave pictures alone", "Pictures are not changed, even under a mark."),
        new(RedactionImageChoice.BlankPart, "Blank the covered part", "Only the part of a picture under a mark is blanked. The rest of the picture stays."),
        new(RedactionImageChoice.Remove, "Remove the whole picture", "A picture a mark touches is removed completely.")
    ];

    /// <summary>Gets the choices for drawings under a mark.</summary>
    public IReadOnlyList<RedactionLineArtOption> LineArtOptions { get; } =
    [

        new(RedactionLineArtChoice.Keep, "Leave drawings alone", "Lines and shapes are not changed, even under a mark."),
        new(RedactionLineArtChoice.RemoveCovered, "Remove drawings that are fully covered", "A line or shape is removed only when a mark covers all of it."),
        new(
            RedactionLineArtChoice.RemoveTouched,
            "Remove every drawing a mark touches",
            "Any line or shape a mark touches is removed, which can include backgrounds.")
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
    public string StatusText { get => _localizedStatusText.Value ?? string.Empty; private set => SetStatusText(LocalizedMessage.Literal(value)); }

    /// <summary>Gets what went wrong, or <see langword="null"/>.</summary>
    public string? Error { get => _localizedError.Value; private set => SetError(LocalizedMessage.Literal(value)); }

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
        new(
        SelectedImage?.Choice ?? RedactionImageChoice.BlankPart,
        SelectedLineArt?.Choice ?? RedactionLineArtChoice.RemoveCovered,
        RemoveHiddenText,
        RemoveLinksAndComments,
        ScrubMetadata);

    /// <summary>Gets the file name suggested for the copy.</summary>
    public string SuggestedName => $"{Path.GetFileNameWithoutExtension(FileName)} (redacted).pdf";

    /// <summary>Gets the warning shown above the confirmation.</summary>
    private static string Warning => UiText.Get("Redaction removes the marked text, pictures and drawings from the new file for good. ")
        + UiText.Get("After you save it, the removed content cannot be brought back. Your open file is not changed, so keep it if you may need the " + "original.");

    /// <summary>Gets or sets the run in progress, which disposing cancels, or <see langword="null"/> when none is.</summary>
    [Reactive]
    private partial CancellationDisposable? Run { get; set; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

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
        RefreshResultText();
        this.RaisePropertyChanged(nameof(WarningText));
        this.RaisePropertyChanged(nameof(StatusText));
        this.RaisePropertyChanged(nameof(Error));
        LocalisationRefresh.RefreshCore(ImageOptions, visited);
        LocalisationRefresh.RefreshCore(LineArtOptions, visited);
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
    private void Cancel() => Run = null;

    /// <summary>Stops any run in progress and asks the window to close.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Close() => Cancel();

    /// <summary>Asks where to save, then applies the redactions and writes the copy there.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canApply))]
    private async Task ApplyAsync()
    {
        SetError(LocalizedMessage.Literal(null));
        if (AreaCount == 0)
        {
            SetError(LocalizedMessage.Get(UiText, "Nothing is marked, so there is nothing to redact."));
            return;
        }

        var path = await SaveInteraction.Handle(SuggestedName).ToTask().ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (IsSameFile(path, _sourcePath))
        {
            SetError(LocalizedMessage.Get(UiText, "That would replace the file you have open. Choose a different name for the redacted copy."));
            return;
        }

        var run = new CancellationDisposable();
        Run = run;
        IsRunning = true;
        _resultDescription = null;
        ResultText = string.Empty;
        SetStatusText(LocalizedMessage.Get(UiText, "Removing the marked content…"));
        try
        {
            var report = await WriteAsync(path, run.Token).ConfigureAwait(true);
            ShowResult(report, path);
        }
        catch (OperationCanceledException)
        {
            SetStatusText(LocalizedMessage.Get(UiText, "Cancelled. No file was saved."));
        }
        catch (Exception ex) when (CopyFailure.IsExpected(ex))
        {
            SetError(LocalizedMessage.Format(UiText, "Could not save the redacted copy: {0}", CopyFailure.Message(ex)));
            SetStatusText(LocalizedMessage.Get(UiText, "The redacted copy was not saved."));
        }
        finally
        {
            if (ReferenceEquals(Run, run))
            {
                Run = null;
            }

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
                // Redaction rewrites the pages before its first await, so the app starts it on the pool to keep Cancel alive.
                report = await Task.Run(() => _redactor.ApplyAsync(stream, settings, cancellationToken), cancellationToken).ConfigureAwait(true);
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
        SetStatusText(LocalizedMessage.Get(UiText, "Done. The redacted copy is saved."));
        _resultDescription = (report, path);
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
        var removed = UiText.Format(
            "Removed {0} text characters, {1} pictures, {2} drawings and {3} links, comments or form fields from {4} pages.",
            report.Characters,
            report.Pictures,
            report.Drawings,
            report.Annotations,
            report.Pages);
        var saved = UiText.Format("The redacted copy is saved as {0}. Your open file was not changed.", path);
        var check = UiText.Get("Open the copy and check it before you share it.");
        ResultText = report switch
        {
            { ClaimRemoved: { } removal } => string.Join('\n', removed, saved, ArchiveText.ClaimRemovedNotice(removal), check),
            { ClaimRepaired: { } repair } => string.Join('\n', removed, saved, ArchiveText.ClaimRepairedNotice(repair), check),
            _ => string.Join('\n', removed, saved, check),
        };
    }
}
