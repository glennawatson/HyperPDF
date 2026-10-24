// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Exporting;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Printing;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Output;

/// <summary>
/// The "Export Pages" window: a file format, which pages, a resolution for pictures, a save button that asks where to
/// save, a busy bar with Cancel, and a plain-words summary. The open file is never changed. A format that holds one page
/// per file writes a numbered file for each page chosen.
/// </summary>
[DebuggerDisplay("ExportPagesViewModel: {FileName}")]
public sealed partial class ExportPagesViewModel : ReactiveObject, ICopyWindowRequest
{
    /// <summary>The ending of the temporary file each export is written to.</summary>
    private const string TemporarySuffix = ".export";

    /// <summary>The status shown before anything has happened.</summary>
    private const string ReadyStatus = "Ready. Choose a format and pages, then Export.";

    /// <summary>The bar's value once every file is saved.</summary>
    private const double FullBar = 100;

    /// <summary>Why a chosen destination was refused.</summary>
    private const string OverwriteMessage = "That would replace the file you have open. Choose a different name.";

    /// <summary>The digits in the page number of a numbered file, so the files sort in page order.</summary>
    private const int NumberWidth = 3;

    /// <summary>The exporter the open document provides.</summary>
    private readonly IPageFileExporter _exporter;

    /// <summary>The path of the open document, which must never be overwritten.</summary>
    private readonly string _sourcePath;

    /// <summary>The pages in the open document.</summary>
    private readonly int _pageCount;

    /// <summary>The zero based page on screen when the window opened.</summary>
    private readonly int _currentPage;

    /// <summary>Whether a run is not in progress, so an export can start.</summary>
    private readonly IObservable<bool> _canSave;

    /// <summary>Whether a run is in progress, so it can be cancelled.</summary>
    private readonly IObservable<bool> _canCancel;

    /// <summary>Owns the run in progress until the window closes.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Initializes a new instance of the <see cref="ExportPagesViewModel"/> class.</summary>
    /// <param name="exporter">The open document's page exporter.</param>
    /// <param name="sourcePath">The path of the open document.</param>
    /// <param name="pageCount">The pages in the open document.</param>
    /// <param name="currentPage">The zero based page on screen.</param>
    public ExportPagesViewModel(IPageFileExporter exporter, string sourcePath, int pageCount, int currentPage)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);
        _exporter = exporter;
        _sourcePath = sourcePath;
        _pageCount = pageCount;
        _currentPage = Math.Clamp(currentPage, 0, pageCount - 1);
        FileName = Path.GetFileName(sourcePath);
        SelectedFormat = PageFileFormats.All[0];
        SelectedScope = Scopes[0];
        Dpi = PageFileExportOptions.DefaultDpi;
        IncludeMarkup = PageFileExportOptions.Default.IncludeMarkup;
        PagesText = (_currentPage + 1).ToString(CultureInfo.InvariantCulture);
        _canSave = this.WhenChanged(static vm => vm.IsRunning).Select(static running => !running);
        _canCancel = this.WhenChanged(static vm => vm.IsRunning);
        _subscriptions.Add(this.WhenChanged(static vm => vm.Run).OwnLatest().SubscribeSafeErrors(ErrorTrace.Write));
    }

    /// <summary>Gets the file formats to choose from.</summary>
    public IReadOnlyList<PageFileFormatInfo> Formats => PageFileFormats.All;

    /// <summary>Gets the choices of which pages to export.</summary>
    public IReadOnlyList<ExportPagesScopeChoice> Scopes { get; } =
    [
        new(ExportPagesScopeKind.CurrentPage, "The page on screen"),
        new(ExportPagesScopeKind.AllPages, "All pages"),
        new(ExportPagesScopeKind.Range, "Pages I type, such as 1-3, 7"),
    ];

    /// <summary>Gets the name of the open document.</summary>
    public string FileName { get; }

    /// <summary>Gets the interaction asking where to save; the input is the suggested file name.</summary>
    public Interaction<string, string?> SaveInteraction { get; } = new();

    /// <summary>Gets or sets the chosen file format.</summary>
    [Reactive]
    public partial PageFileFormatInfo SelectedFormat { get; set; }

    /// <summary>Gets or sets the chosen pages.</summary>
    [Reactive]
    public partial ExportPagesScopeChoice SelectedScope { get; set; }

    /// <summary>Gets or sets the pages typed for the range choice, such as <c>1-3, 7</c>.</summary>
    [Reactive]
    public partial string PagesText { get; set; }

    /// <summary>Gets or sets the resolution of pictures in dots to the inch.</summary>
    [Reactive]
    public partial int Dpi { get; set; }

    /// <summary>Gets or sets a value indicating whether comments, form fields and other marks are drawn.</summary>
    [Reactive]
    public partial bool IncludeMarkup { get; set; }

    /// <summary>Gets a value indicating whether an export is running.</summary>
    [Reactive]
    public partial bool IsRunning { get; private set; }

    /// <summary>Gets how full the bar is, from 0 to 100.</summary>
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

    /// <summary>Gets the file name suggested for the export.</summary>
    public string SuggestedName => Path.GetFileNameWithoutExtension(FileName) + SelectedFormat.Extension;

    /// <summary>Gets or sets the run in progress, which disposing cancels, or <see langword="null"/> when none is.</summary>
    [Reactive]
    private partial CancellationDisposable? Run { get; set; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Works out the zero based pages the window describes.</summary>
    /// <param name="pages">Receives the pages.</param>
    /// <returns><see langword="true"/> when the choice names at least one page in the document.</returns>
    public bool TryGetPages(List<int> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        pages.Clear();
        switch (SelectedScope.Kind)
        {
            case ExportPagesScopeKind.CurrentPage:
            {
                pages.Add(_currentPage);
                return true;
            }

            case ExportPagesScopeKind.AllPages:
            {
                for (var page = 0; page < _pageCount; page++)
                {
                    pages.Add(page);
                }

                return true;
            }

            default:
            {
                return PageRanges.TryParse(PagesText, _pageCount, pages);
            }
        }
    }

    /// <summary>Makes the path of one page's file when a format holds one page per file.</summary>
    /// <param name="path">The path the person chose.</param>
    /// <param name="page">The zero based page.</param>
    /// <returns>The path with the page number before the ending.</returns>
    internal static string NumberedPath(string path, int page)
    {
        var extension = Path.GetExtension(path);
        var number = (page + 1).ToString(CultureInfo.InvariantCulture).PadLeft(NumberWidth, '0');
        return string.Concat(path.AsSpan(0, path.Length - extension.Length), "-", number, extension);
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
            // A leftover temporary file does no harm and a later export replaces it.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }

    /// <summary>Stops the export in progress.</summary>
    [ReactiveCommand(CanExecute = nameof(_canCancel))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Cancel() => Run = null;

    /// <summary>Stops any export in progress and asks the window to close.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Close() => Cancel();

    /// <summary>Asks where to save, then writes the pages there.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canSave))]
    private async Task SaveAsync()
    {
        Error = null;
        var pages = new List<int>();
        if (!TryGetPages(pages))
        {
            Error = $"Type pages between 1 and {_pageCount}, such as 1-3, 7.";
            return;
        }

        if (SelectedFormat.IsRaster && (Dpi < PageFileExportOptions.MinimumDpi || Dpi > PageFileExportOptions.MaximumDpi))
        {
            Error = $"Choose a resolution between {PageFileExportOptions.MinimumDpi} and {PageFileExportOptions.MaximumDpi} dots to the inch.";
            return;
        }

        var path = await SaveInteraction.Handle(SuggestedName).ToTask().ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        await RunAsync(path, pages).ConfigureAwait(true);
    }

    /// <summary>Writes the chosen pages, showing progress and how the run ended.</summary>
    /// <param name="path">The path the person chose.</param>
    /// <param name="pages">The zero based pages.</param>
    /// <returns>A task.</returns>
    private async Task RunAsync(string path, List<int> pages)
    {
        var run = new CancellationDisposable();
        Run = run;
        IsRunning = true;
        ProgressPercent = 0;
        ResultText = string.Empty;
        StatusText = "Exporting. This can take a while for long files.";
        try
        {
            ShowResult(await ExportAllAsync(path, pages, run.Token).ConfigureAwait(true));
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled. Files finished before you cancelled stay saved.";
        }
        catch (Exception ex) when (CopyFailure.IsExpected(ex) || ex is ArgumentException)
        {
            Error = $"Could not export: {CopyFailure.Describe(ex)}";
            StatusText = "The export did not finish.";
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

    /// <summary>Writes one file, or a numbered file for each page when the format holds one page.</summary>
    /// <param name="path">The path the person chose.</param>
    /// <param name="pages">The zero based pages.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What the files hold.</returns>
    private async Task<ExportPagesSummary> ExportAllAsync(string path, List<int> pages, CancellationToken cancellationToken)
    {
        var format = SelectedFormat;
        var options = new PageFileExportOptions(Dpi, IncludeMarkup);
        var perPage = !format.AllowsSeveralPages && pages.Count > 1;
        var jobs = perPage ? pages.Count : 1;
        var skipped = 0;
        var approximated = 0;
        for (var job = 0; job < jobs; job++)
        {
            var target = perPage ? NumberedPath(path, pages[job]) : path;
            if (IsSameFile(target, _sourcePath))
            {
                return ExportPagesSummary.Refused(OverwriteMessage);
            }

            var chosen = perPage ? pages.GetRange(job, 1) : pages;
            var result = await WriteAsync(target, format.Format, chosen, options, cancellationToken).ConfigureAwait(true);
            if (!result.Written)
            {
                return ExportPagesSummary.Refused(result.Problem ?? "The file could not be written.");
            }

            skipped += result.Skipped;
            approximated += result.Approximated;
            ProgressPercent = FullBar * (job + 1) / jobs;
        }

        return new(jobs, skipped, approximated, path, null);
    }

    /// <summary>Writes one file to a temporary name, then moves it into place when it was made.</summary>
    /// <param name="path">Where the file goes.</param>
    /// <param name="format">The format.</param>
    /// <param name="pages">The zero based pages in the file.</param>
    /// <param name="options">How to write them.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>What the export did.</returns>
    private async Task<PageFileExportResult> WriteAsync(string path, PageFileFormat format, List<int> pages, PageFileExportOptions options, CancellationToken cancellationToken)
    {
        var temporary = path + TemporarySuffix;
        try
        {
            PageFileExportResult result;
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous))
            {
                // Drawing a page reads and renders before the first await, so it starts on the pool to keep the window responsive.
                result = await Task.Run(() => _exporter.ExportAsync(pages, format, options, stream, cancellationToken).AsTask(), cancellationToken).ConfigureAwait(true);
            }

            if (result.Written)
            {
                FileReplacement.Replace(temporary, path);
            }
            else
            {
                TryDelete(temporary);
            }

            return result;
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    /// <summary>Shows a finished run's summary.</summary>
    /// <param name="summary">What the run did.</param>
    private void ShowResult(ExportPagesSummary summary)
    {
        if (summary.Problem is { } problem)
        {
            Error = problem;
            StatusText = "The export did not finish.";
            return;
        }

        ProgressPercent = FullBar;
        StatusText = "Done. The export is saved.";
        ResultText = ExportPagesText.Describe(summary);
    }
}
