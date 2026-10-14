// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.App.Services;
using HyperPdf.Core.Protection;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// The run of a window that writes a protected or unprotected copy: asks where to save, writes the copy while showing
/// progress that can be cancelled, and keeps a plain-words summary. The open file is never replaced.
/// </summary>
[DebuggerDisplay("ProtectionRunViewModel: {FileName}")]
public abstract partial class ProtectionRunViewModel : ReactiveObject, ICopyWindowRequest
{
    /// <summary>The bar's value once a copy is saved.</summary>
    private const double FullBar = 100;

    /// <summary>The path of the open document, which must never be overwritten.</summary>
    private readonly string _sourcePath;

    /// <summary>Initializes a new instance of the <see cref="ProtectionRunViewModel"/> class.</summary>
    /// <param name="sourcePath">The path of the open document.</param>
    protected ProtectionRunViewModel(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        _sourcePath = sourcePath;
        FileName = Path.GetFileName(sourcePath);
        CanStart = this.WhenChanged(static vm => vm.IsRunning).Select(static running => !running);
        CanStop = this.WhenChanged(static vm => vm.IsRunning);
        Subscriptions.Add(this.WhenChanged(static vm => vm.Run).OwnLatest().SubscribeSafeErrors(ErrorTrace.Write));
    }

    /// <summary>Gets the name of the open document.</summary>
    public string FileName { get; }

    /// <summary>Gets the file name suggested for the copy.</summary>
    public abstract string SuggestedName { get; }

    /// <summary>Gets the interaction asking where to save the copy; the input is the suggested file name.</summary>
    public Interaction<string, string?> SaveInteraction { get; } = new();

    /// <summary>Gets a value indicating whether a copy is being written.</summary>
    [Reactive]
    public partial bool IsRunning { get; private set; }

    /// <summary>Gets how full the bar is, from 0 to 100: full once a copy is saved, empty otherwise.</summary>
    [Reactive]
    public partial double ProgressPercent { get; private set; }

    /// <summary>Gets what the run is doing now, or how it ended.</summary>
    [Reactive]
    public partial string StatusText { get; protected set; } = string.Empty;

    /// <summary>Gets what went wrong, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? Error { get; protected set; }

    /// <summary>Gets the plain-words summary of the last finished run; empty before one finishes.</summary>
    [Reactive]
    public partial string ResultText { get; private set; } = string.Empty;

    /// <summary>Gets the path of the last saved copy, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? SavedPath { get; private set; }

    /// <summary>Gets whether a run is not in progress, so a copy can start.</summary>
    protected IObservable<bool> CanStart { get; }

    /// <summary>Gets whether a run is in progress, so it can be cancelled.</summary>
    protected IObservable<bool> CanStop { get; }

    /// <summary>
    /// Gets what the window owns until it closes: the run in progress, and what a derived window adds, such as
    /// forgetting the passwords it holds.
    /// </summary>
    protected MultipleDisposable Subscriptions { get; } = [];

    /// <summary>Gets or sets the run in progress, which disposing cancels, or <see langword="null"/> when none is.</summary>
    [Reactive]
    private partial CancellationDisposable? Run { get; set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases what the window owns: the run in progress, and what a derived window added to <see cref="Subscriptions"/>.</summary>
    /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Subscriptions.Dispose();
        }
    }

    /// <summary>Asks where to save, then writes the copy there.</summary>
    /// <param name="suggestedName">The file name offered.</param>
    /// <param name="runningText">What the status says while the copy is made.</param>
    /// <param name="action">What the run does, for the refusal line.</param>
    /// <param name="write">Writes the copy.</param>
    /// <returns>A task.</returns>
    protected async Task RunAsync(string suggestedName, string runningText, string action, Func<Stream, CancellationToken, Task<ProtectionResult>> write)
    {
        var path = await SaveInteraction.Handle(suggestedName).ToTask().ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (ProtectedCopyFile.IsSameFile(path, _sourcePath))
        {
            Error = ProtectedCopyFile.OverwriteMessage;
            return;
        }

        var run = new CancellationDisposable();
        Run = run;
        IsRunning = true;
        ProgressPercent = 0;
        ResultText = string.Empty;
        StatusText = runningText;
        try
        {
            ShowResult(await ProtectedCopyFile.WriteAsync(write, path, run.Token).ConfigureAwait(true), path, action);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled. No file was saved.";
        }
        catch (Exception ex) when (CopyFailure.IsExpected(ex) || ex is ArgumentException)
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

    /// <summary>Stops the copy in progress.</summary>
    [ReactiveCommand(CanExecute = nameof(CanStop))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Cancel() => Run = null;

    /// <summary>Stops any copy in progress and asks the window to close.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Close() => Cancel();

    /// <summary>Shows a finished run's summary.</summary>
    /// <param name="result">The result.</param>
    /// <param name="path">Where the copy was to be saved.</param>
    /// <param name="action">What the run does.</param>
    private void ShowResult(ProtectionResult result, string path, string action)
    {
        SavedPath = result.Written ? path : null;
        ProgressPercent = result.Written ? FullBar : 0;
        StatusText = result.Written ? "Done. The copy is saved." : "The copy could not be made. See the reason below.";
        ResultText = OptimizeReportText.Join(ProtectionChecks.Lines(result, path, action));
    }
}
