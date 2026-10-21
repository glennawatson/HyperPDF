// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Flattening;
using HyperPdf.Core.Platform;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Output;

/// <summary>
/// The "Save Flattened Copy" window: what to flatten, which look to keep, a save button that asks where to save, a busy
/// bar with Cancel, and a plain-words summary of what became page content or why the copy was refused. The open file is
/// never replaced; the copy is written to a temporary file and moved into place only once it is complete.
/// </summary>
[DebuggerDisplay("FlattenCopyViewModel: {FileName}")]
public sealed partial class FlattenCopyViewModel : ReactiveObject, ICopyWindowRequest
{
    /// <summary>The ending of the temporary file the copy is written to.</summary>
    private const string TemporarySuffix = ".flatten";

    /// <summary>Why a chosen destination was refused.</summary>
    private const string OverwriteMessage = "That would replace the file you have open. Choose a different name for the copy.";

    /// <summary>The status shown before anything has happened.</summary>
    private const string ReadyStatus = "Ready. Choose what to flatten, then Save Copy.";

    /// <summary>The index of the suggested scope, form fields and all markup.</summary>
    private const int SuggestedScope = 2;

    /// <summary>The bar's value once a copy is saved.</summary>
    private const double FullBar = 100;

    /// <summary>The writer the open document provides.</summary>
    private readonly IFlattenCopyWriter _writer;

    /// <summary>The path of the open document, which must never be overwritten.</summary>
    private readonly string _sourcePath;

    /// <summary>Whether a run is not in progress, so a copy can start.</summary>
    private readonly IObservable<bool> _canSave;

    /// <summary>Whether a run is in progress, so it can be cancelled.</summary>
    private readonly IObservable<bool> _canCancel;

    /// <summary>Owns the run in progress until the window closes.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Initializes a new instance of the <see cref="FlattenCopyViewModel"/> class.</summary>
    /// <param name="writer">The open document's flattened copy writer.</param>
    /// <param name="sourcePath">The path of the open document.</param>
    public FlattenCopyViewModel(IFlattenCopyWriter writer, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        _writer = writer;
        _sourcePath = sourcePath;
        FileName = Path.GetFileName(sourcePath);
        SelectedScope = Scopes[SuggestedScope];
        SelectedLook = Looks[0];
        _canSave = this.WhenChanged(static vm => vm.IsRunning).Select(static running => !running);
        _canCancel = this.WhenChanged(static vm => vm.IsRunning);
        _subscriptions.Add(this.WhenChanged(static vm => vm.Run).OwnLatest().SubscribeSafeErrors(ErrorTrace.Write));
    }

    /// <summary>Gets the choices of what to flatten.</summary>
    public IReadOnlyList<FlattenScopeChoice> Scopes { get; } =
    [
        new(FlattenCopyScope.FormFields, "Form fields only", "Filled-in fields, check boxes and buttons become part of the page. Comments, highlights and links stay as they are."),
        new(FlattenCopyScope.CommentsAndMarkup, "Comments and markup only", "Comments, highlights, stamps and drawings become part of the page. Form fields and links stay as they are."),
        new(FlattenCopyScope.EverythingButLinks, "Form fields and all markup (suggested)", "Everything you can see on top of the page becomes part of it. Links stay clickable."),
        new(FlattenCopyScope.Everything, "Everything, including link outlines", "As above, and the outlines of links are drawn on the page too."),
    ];

    /// <summary>Gets the choices of which look to keep.</summary>
    public IReadOnlyList<FlattenLookChoice> Looks { get; } =
    [
        new(FlattenCopyLook.Screen, "As it looks on screen", "Keeps what you see on screen. Items marked not to show on screen are left out."),
        new(FlattenCopyLook.Print, "As it prints", "Keeps what would print. Items not marked to print are left out."),
    ];

    /// <summary>Gets the name of the open document.</summary>
    public string FileName { get; }

    /// <summary>Gets the interaction asking where to save the copy; the input is the suggested file name.</summary>
    public Interaction<string, string?> SaveInteraction { get; } = new();

    /// <summary>Gets or sets the scope choice; none selected means form fields and all markup.</summary>
    [Reactive]
    public partial FlattenScopeChoice? SelectedScope { get; set; }

    /// <summary>Gets or sets the look choice; none selected means the screen look.</summary>
    [Reactive]
    public partial FlattenLookChoice? SelectedLook { get; set; }

    /// <summary>Gets or sets a value indicating whether turned pages are saved upright, with the turn built into their content.</summary>
    [Reactive]
    public partial bool TurnPagesUpright { get; set; }

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
    public partial FlattenCopyResult? Result { get; private set; }

    /// <summary>Gets the path of the last saved copy, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? SavedPath { get; private set; }

    /// <summary>Gets the options the window currently describes.</summary>
    public FlattenCopyOptions Options => new(
        SelectedScope?.Scope ?? FlattenCopyScope.EverythingButLinks,
        SelectedLook?.Look ?? FlattenCopyLook.Screen,
        TurnPagesUpright);

    /// <summary>Gets the file name suggested for the copy.</summary>
    public string SuggestedName => $"{Path.GetFileNameWithoutExtension(FileName)} (flattened).pdf";

    /// <summary>Gets or sets the run in progress, which disposing cancels, or <see langword="null"/> when none is.</summary>
    [Reactive]
    private partial CancellationDisposable? Run { get; set; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

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
    private void Cancel() => Run = null;

    /// <summary>Stops any copy in progress and asks the window to close.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Close() => Cancel();

    /// <summary>Asks where to save, then writes the flattened copy there.</summary>
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

        var run = new CancellationDisposable();
        Run = run;
        IsRunning = true;
        ProgressPercent = 0;
        ResultText = string.Empty;
        StatusText = "Making a flattened copy. This can take a while for long files.";
        try
        {
            ShowResult(await WriteAsync(path, Options, run.Token).ConfigureAwait(true), path);
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
            if (ReferenceEquals(Run, run))
            {
                Run = null;
            }

            IsRunning = false;
        }
    }

    /// <summary>Writes the copy to a temporary file, off the UI thread, then moves it into place when it was made.</summary>
    /// <param name="path">Where the copy goes.</param>
    /// <param name="options">What to flatten.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The result.</returns>
    private async Task<FlattenCopyResult> WriteAsync(string path, FlattenCopyOptions options, CancellationToken cancellationToken)
    {
        var temporary = path + TemporarySuffix;
        try
        {
            FlattenCopyResult result;
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous))
            {
                // Flattening reads and rewrites the pages before its first await, so the app starts it on the pool to
                // keep the window responsive; the library cannot hop threads itself.
                result = await Task.Run(() => _writer.WriteFlattenedCopyAsync(stream, options, cancellationToken), cancellationToken).ConfigureAwait(true);
            }

            if (!result.Flattened)
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
    private void ShowResult(FlattenCopyResult result, string path)
    {
        Result = result;
        SavedPath = result.Flattened ? path : null;
        ProgressPercent = result.Flattened ? FullBar : 0;
        StatusText = result.Flattened ? "Done. The copy is saved." : "The copy could not be made. See the reasons below.";
        ResultText = OptimizeReportText.Join(FlattenCopyText.Lines(result, path));
    }
}
