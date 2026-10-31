// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.ViewModels.Rendering;
using HyperPdfLibrary;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>Edits, reorders, imports and extracts the pages in one open document.</summary>
[DebuggerDisplay("PageManagementViewModel: {Status}")]
public sealed partial class PageManagementViewModel : ReactiveObject, IDisposable, ILocalisationChildren
{
    /// <summary>The clockwise page rotation in degrees.</summary>
    private const int ClockwiseDegrees = 90;

    /// <summary>The counterclockwise page rotation in degrees.</summary>
    private const int CounterclockwiseDegrees = -90;

    /// <summary>The page file extension used for extracted pages.</summary>
    private const string ExtractedPagesSuffix = "-pages.pdf";

    /// <summary>The temporary extension used while extracted pages are being written.</summary>
    private const string TemporarySuffix = ".extracting";

    /// <summary>The buffer size used by asynchronous extraction writes.</summary>
    private const int FileBufferSize = 65_536;

    /// <summary>The owner that supplies the open document and refreshes its page layout.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Cancelled when the owning tab closes, which stops an in-flight operation; its source is owned by <see cref="_subscriptions"/>.</summary>
    private readonly CancellationToken _lifetime;

    /// <summary>Owns the selection subscription.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Whether a page edit can run.</summary>
    private readonly IObservable<bool> _canEdit;

    /// <summary>Whether a selected page operation can run.</summary>
    private readonly IObservable<bool> _canEditSelection;

    /// <summary>Whether the selection can move earlier.</summary>
    private readonly IObservable<bool> _canMoveEarlier;

    /// <summary>Whether the selection can move later.</summary>
    private readonly IObservable<bool> _canMoveLater;

    /// <summary>Whether the selection can be deleted.</summary>
    private readonly IObservable<bool> _canDelete;

    /// <summary>Whether an edit can be undone.</summary>
    private readonly IObservable<bool> _canUndo;

    /// <summary>Whether an edit can be redone.</summary>
    private readonly IObservable<bool> _canRedo;

    /// <summary>Initializes a new instance of the <see cref="PageManagementViewModel"/> class.</summary>
    /// <param name="owner">The tab holding the document.</param>
    public PageManagementViewModel(DocumentTabViewModel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;

        // Closing the tab first stops an operation that is still running.
        var lifetime = new CancellationDisposable();
        _lifetime = lifetime.Token;
        _subscriptions.Add(lifetime);
        _canEdit = this.WhenChanged(static vm => vm.CanEdit);
        _canEditSelection = this.WhenChanged(static vm => vm.CanEdit, static vm => vm.SelectedPages, static (canEdit, pages) => canEdit && pages.Count > 0);
        _canMoveEarlier = this.WhenChanged(static vm => vm.CanMoveEarlier);
        _canMoveLater = this.WhenChanged(static vm => vm.CanMoveLater);
        _canDelete = this.WhenChanged(static vm => vm.CanDelete);
        _canUndo = this.WhenChanged(static vm => vm.CanUndo);
        _canRedo = this.WhenChanged(static vm => vm.CanRedo);

        // The sidebar's two-way selection binding either replaces the collection or edits it in place; both select the
        // pages it holds. The first value is the collection the view model starts with, which holds nothing yet.
        _subscriptions.Add(
            this.WhenChanged(static vm => vm.SelectedThumbnails).SwitchMap(static thumbnails => thumbnails.Edits).Skip(1).SubscribeSafe(SelectThumbnailPages, ErrorTrace.Write));
        RefreshState();
    }

    /// <summary>Gets a number that changes each time an edit changes the page order or geometry, so the tab reads its pages again.</summary>
    [Reactive]
    public partial int StructureVersion { get; private set; }

    /// <summary>Gets the selected zero-based page indexes in document order.</summary>
    [Reactive]
    public partial IReadOnlyList<int> SelectedPages { get; private set; } = Array.Empty<int>();

    /// <summary>Gets the selected thumbnail objects for the sidebar's two-way selection binding.</summary>
    [Reactive]
    public partial IList SelectedThumbnails { get; set; } = new ObservableCollection<object>();

    /// <summary>Gets a value indicating whether a page-management operation can run.</summary>
    [Reactive]
    public partial bool CanEdit { get; private set; }

    /// <summary>Gets a value indicating whether the selected pages can be deleted while keeping one page.</summary>
    [Reactive]
    public partial bool CanDelete { get; private set; }

    /// <summary>Gets a value indicating whether a change can be undone.</summary>
    [Reactive]
    public partial bool CanUndo { get; private set; }

    /// <summary>Gets a value indicating whether a change can be redone.</summary>
    [Reactive]
    public partial bool CanRedo { get; private set; }

    /// <summary>Gets a value indicating whether the selection can move earlier.</summary>
    [Reactive]
    public partial bool CanMoveEarlier { get; private set; }

    /// <summary>Gets a value indicating whether the selection can move later.</summary>
    [Reactive]
    public partial bool CanMoveLater { get; private set; }

    /// <summary>Gets a value indicating whether a page operation is in progress.</summary>
    [Reactive]
    public partial bool IsBusy { get; private set; }

    /// <summary>Gets the current page-management status.</summary>
    [Reactive]
    public partial string Status { get; private set; } = string.Empty;

    /// <summary>Gets the text for the available undo action.</summary>
    [Reactive]
    public partial string UndoText { get; private set; } = UiText.Get("Undo");

    /// <summary>Gets the text for the available redo action.</summary>
    [Reactive]
    public partial string RedoText { get; private set; } = UiText.Get("Redo");

    /// <summary>Gets the interaction asking which PDF files to insert.</summary>
    public Interaction<RxVoid, string[]> InsertInteraction { get; } = new();

    /// <summary>Gets the interaction asking which PDF files to merge at the end.</summary>
    public Interaction<RxVoid, string[]> MergeFilesInteraction { get; } = new();

    /// <summary>Gets the interaction asking which picture files to add as pages.</summary>
    public Interaction<RxVoid, string[]> InsertImagesInteraction { get; } = new();

    /// <summary>Gets the interaction asking where to save the selected pages.</summary>
    public Interaction<string, string?> ExtractInteraction { get; } = new();

    /// <summary>Sets the selected pages, dropping invalid indexes and sorting duplicates into document order.</summary>
    /// <param name="pages">Zero-based page indexes.</param>
    public void SelectPages(IEnumerable<int> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var count = _owner.PageCount;
        var indexes = new SortedSet<int>();
        foreach (var page in pages)
        {
            if ((uint)page < (uint)count)
            {
                _ = indexes.Add(page);
            }
        }

        var selected = new int[indexes.Count];
        indexes.CopyTo(selected);
        var thumbnails = new object[selected.Length];
        for (var index = 0; index < selected.Length; index++)
        {
            thumbnails[index] = _owner.Thumbnails[selected[index]];
        }

        if (!SameIndexes(SelectedPages, selected))
        {
            SelectedPages = selected;
        }

        if (!SameThumbnails(SelectedThumbnails, thumbnails))
        {
            SelectedThumbnails = new ObservableCollection<object>(thumbnails);
        }

        RefreshState();
    }

    /// <summary>Refreshes selection and command state after the document's page structure changes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Refresh() => SelectPages(SelectedPages);

    /// <summary>Selects the page of the thumbnail the reader has settled on, unless the reader has picked several pages or that page is already selected.</summary>
    /// <param name="thumbnail">The thumbnail, or <see langword="null"/> when there is none.</param>
    public void Follow(ThumbnailItemViewModel? thumbnail)
    {
        if (thumbnail is not null && SelectedPages.Count <= 1 && !(SelectedPages.Count == 1 && SelectedPages[0] == thumbnail.PageIndex))
        {
            SelectPages([thumbnail.PageIndex]);
        }
    }

    /// <summary>Moves the selected pages to an index in the document after removing the selection.</summary>
    /// <param name="destination">The destination index after removal.</param>
    /// <returns>A task completing after the move.</returns>
    public Task MoveAsync(int destination) => SelectedPages.Count == 0 || (uint)destination > (uint)(_owner
        .PageCount - SelectedPages.Count)
        ? Task.CompletedTask
        : RunEditAsync(
        static (manager, request, report, token) => manager
        .ApplyAsync(request, report, token),
        new(PageEditKind.Move, CopySelectedPages(), destination),
        UiText.Get("Could not move pages."));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        RefreshLabelsAndStatus(GetManager(), SelectedPages.Count);
        LocalisationRefresh.RefreshCore(_owner, visited);
    }

    /// <summary>Determines whether two paths name the same file.</summary>
    /// <param name="first">The first path.</param>
    /// <param name="second">The second path.</param>
    /// <returns>True when both paths identify the same file.</returns>
    private static bool SamePath(string first, string second)
    {
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), comparison);
    }

    /// <summary>Removes a partial extraction, ignoring errors because the destination is still safe.</summary>
    /// <param name="path">The temporary file.</param>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Compares two page-index selections without an iterator allocation.</summary>
    /// <param name="current">The current selection.</param>
    /// <param name="next">The proposed selection.</param>
    /// <returns>True when each index matches.</returns>
    private static bool SameIndexes(IReadOnlyList<int> current, int[] next)
    {
        if (current.Count != next.Length)
        {
            return false;
        }

        for (var index = 0; index < next.Length; index++)
        {
            if (current[index] != next[index])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Compares thumbnail references without an iterator allocation.</summary>
    /// <param name="current">The current thumbnail list.</param>
    /// <param name="next">The proposed thumbnails.</param>
    /// <returns>True when each object reference matches.</returns>
    private static bool SameThumbnails(IList current, object[] next)
    {
        if (current.Count != next.Length)
        {
            return false;
        }

        for (var index = 0; index < next.Length; index++)
        {
            if (!ReferenceEquals(current[index], next[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Says what a page edit or copy could not keep: separation pages and trap networks.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The notice, or <see langword="null"/> when everything was kept.</returns>
    private static string? CopyNotice(PageEditReport report)
    {
        var separations = SeparationText.ChangeNotice(report.SeparationChanges);
        if (report.TrapNetworkWarnings.Count == 0)
        {
            return separations;
        }

        var traps = SaveText.TrapNetworkNotice(report.TrapNetworkWarnings);
        return separations is null ? traps : $"{separations} {traps}";
    }

    /// <summary>Copies page indexes from memory without an iterator allocation.</summary>
    /// <param name="pages">The page indexes.</param>
    /// <returns>A copy of the page indexes.</returns>
    private static int[] CopyPages(ReadOnlyMemory<int> pages)
    {
        var copy = new int[pages.Length];
        pages.Span.CopyTo(copy);
        return copy;
    }

    /// <summary>Gets a history command label.</summary>
    /// <param name="label">The edit label.</param>
    /// <param name="command">The command name, used alone when there is no edit.</param>
    /// <param name="withLabel">The command name with the edit's place in it, so each language orders the words itself.</param>
    /// <returns>The command label.</returns>
    private static string GetHistoryText(string? label, string command, string withLabel) =>
        label is null ? command : UiText.Format(withLabel, UiText.Get(label));

    /// <summary>Checks whether an operation failure can be shown as a user notice.</summary>
    /// <param name="exception">The operation failure.</param>
    /// <returns>True for expected file, parser or stale-document failures.</returns>
    private static bool IsExpectedFailure(Exception exception) => exception is
        PdfException or InvalidDataException or IOException or UnauthorizedAccessException or
        NotSupportedException or InvalidOperationException or ArgumentOutOfRangeException;

    /// <summary>Reads each picture file in order, skipping the ones that cannot be read.</summary>
    /// <param name="head">The render head that decodes pictures.</param>
    /// <param name="paths">The picture file paths.</param>
    /// <param name="cancellationToken">Stops reading between files.</param>
    /// <returns>The pictures that were read.</returns>
    private static PageImageSource[] ReadImages(IRenderHead head, string[] paths, CancellationToken cancellationToken)
    {
        var images = new List<PageImageSource>(paths.Length);
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (head.LoadImage(path) is { } decoded)
            {
                images.Add(new(decoded.Pixels, decoded.Width, decoded.Height));
            }
        }

        return [.. images];
    }

    /// <summary>Creates an array containing a contiguous range of page indexes.</summary>
    /// <param name="start">The first page index.</param>
    /// <param name="count">The number of indexes.</param>
    /// <returns>The page indexes.</returns>
    private static int[] CopyRange(int start, int count)
    {
        var pages = new int[count];
        for (var index = 0; index < count; index++)
        {
            pages[index] = start + index;
        }

        return pages;
    }

    /// <summary>Selects the pages of the thumbnails the thumbnail control selected, in document order.</summary>
    /// <param name="thumbnails">The selected thumbnail items.</param>
    private void SelectThumbnailPages(IList thumbnails)
    {
        var indexes = new List<int>(thumbnails.Count);
        foreach (var thumbnail in thumbnails)
        {
            if (thumbnail is ThumbnailItemViewModel item)
            {
                indexes.Add(item.PageIndex);
            }
        }

        SelectPages(indexes);
    }

    /// <summary>Copies the selected indexes into an operation request.</summary>
    /// <returns>A copy of the page indexes.</returns>
    private int[] CopySelectedPages()
    {
        var copy = new int[SelectedPages.Count];
        for (var index = 0; index < copy.Length; index++)
        {
            copy[index] = SelectedPages[index];
        }

        return copy;
    }

    /// <summary>Deletes the selected pages.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canDelete))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task DeleteAsync() => RunSelectedEditAsync(PageEditKind.Delete, 0, UiText.Get("Could not delete pages."));

    /// <summary>Turns the selected pages clockwise by 90 degrees.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canEditSelection))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task RotateClockwiseAsync() => RunSelectedEditAsync(PageEditKind.Rotate, ClockwiseDegrees, UiText.Get("Could not rotate pages."));

    /// <summary>Turns the selected pages counterclockwise by 90 degrees.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canEditSelection))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task RotateCounterclockwiseAsync() => RunSelectedEditAsync(PageEditKind.Rotate, CounterclockwiseDegrees, UiText.Get("Could not rotate pages."));

    /// <summary>Copies the selected pages after the last selected page.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canEditSelection))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task DuplicateAsync() => RunSelectedEditAsync(PageEditKind.Duplicate, SelectedPages[^1] + 1, UiText.Get("Could not duplicate pages."));

    /// <summary>Moves the selected pages one position earlier.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canMoveEarlier))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task MoveEarlierAsync() => MoveAsync(SelectedPages[0] - 1);

    /// <summary>Moves the selected pages one position later.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canMoveLater))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task MoveLaterAsync() => MoveAsync(SelectedPages[0] + 1);

    /// <summary>Asks for PDF files and inserts them after the selected pages or current page.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canEdit))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task InsertFilesAsync() => RunAsync(
        async (manager, token) =>
{
    var paths = await InsertInteraction.Handle(RxVoid.Default).ToTask(token).WaitAsync(token).ConfigureAwait(true);
    if (paths.Length > 0)
    {
        var index = SelectedPages.Count > 0 ? SelectedPages[0] : Math.Clamp(_owner.CurrentPageIndex, 0, _owner.PageCount);
        var previousCount = _owner.PageCount;
        var report = new PageEditReport();

        // The library reads and rewrites the pages before its first await, so the app starts it on the pool.
        await Task.Run(() => manager.InsertAsync(index, paths, report, token).AsTask(), token).ConfigureAwait(true);
        ShowSeparationNotice(report);
        return new(true, () => CopyRange(index, Math.Max(0, _owner.PageCount - previousCount)));
    }

    return default;
    },
        UiText.Get("Could not insert pages."),
        true);

    /// <summary>Asks for picture files and inserts each as a page after the selected pages or current page.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canEdit))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task InsertImagesAsync() => RunAsync(
        async (manager, token) =>
{
    var paths = await InsertImagesInteraction.Handle(RxVoid.Default).ToTask(token).WaitAsync(token).ConfigureAwait(true);
    if (paths.Length == 0)
    {
        return default;
    }

    var head = _owner.Services.Head;
    var images = await Task.Run(() => ReadImages(head, paths, token), token).ConfigureAwait(true);
    if (images.Length == 0)
    {
        _owner.SetNotice(LocalizedMessage.Get(UiText, "None of the chosen files could be read as pictures."));
        return default;
    }

    var index = SelectedPages.Count > 0 ? SelectedPages[^1] + 1 : Math.Clamp(_owner.CurrentPageIndex + 1, 0, _owner.PageCount);
    await Task.Run(() => manager.InsertImagesAsync(index, images, token).AsTask(), token).ConfigureAwait(true);
    if (images.Length < paths.Length)
    {
        _owner.SetNotice(LocalizedMessage.Get(UiText, "Some of the chosen files could not be read as pictures and were skipped."));
    }

    return new(true, () => CopyRange(index, images.Length));
    },
        UiText.Get("Could not add pictures as pages."),
        true);

    /// <summary>Asks for PDF files and appends every page to the document.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canEdit))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task MergeFilesAsync() => RunAsync(
        async (manager, token) =>
{
    var paths = await MergeFilesInteraction.Handle(RxVoid.Default).ToTask(token).WaitAsync(token).ConfigureAwait(true);
    if (paths.Length > 0)
    {
        var index = _owner.PageCount;
        var previousCount = _owner.PageCount;
        var report = new PageEditReport();
        await Task.Run(() => manager.InsertAsync(index, paths, report, token).AsTask(), token).ConfigureAwait(true);
        ShowSeparationNotice(report);
        return new(true, () => CopyRange(index, Math.Max(0, _owner.PageCount - previousCount)));
    }

    return default;
    },
        UiText.Get("Could not merge pages."),
        true);

    /// <summary>Asks where to save the selected pages, then writes them as a new PDF.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canEditSelection))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task ExtractAsync() => RunAsync(
        async (manager, token) =>
{
    var suggestedName = Path.GetFileNameWithoutExtension(_owner.FileName) + ExtractedPagesSuffix;
    var destination = await ExtractInteraction.Handle(suggestedName).ToTask(token).WaitAsync(token).ConfigureAwait(true);
    if (string.IsNullOrWhiteSpace(destination))
    {
        return default;
    }

    if (SamePath(destination, _owner.FilePath))
    {
        _owner.SetNotice(LocalizedMessage.Get(UiText, "Choose a different file so the open document is not replaced."));
        return default;
    }

    var temporary = destination + TemporarySuffix + Guid.NewGuid().ToString("N");
    try
    {
        var report = new PageEditReport();
        await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, FileBufferSize, FileOptions.Asynchronous))
        {
            var pages = CopySelectedPages();
            await Task.Run(() => manager.ExtractAsync(pages, stream, report, token).AsTask(), token).ConfigureAwait(true);
        }

        File.Move(temporary, destination, true);
        _owner.SetNotice(
                CopyNotice(report) is { } separations
                    ? LocalizedMessage.Format(
                        UiText,
                        "Extracted {0} pages. {1}",
                        SelectedPages.Count,
                        separations)
                    : LocalizedMessage.Format(UiText, "Extracted {0} pages.", SelectedPages.Count));
        return default;
    }
    finally
    {
        TryDelete(temporary);
    }
    },
        UiText.Get("Could not extract pages."),
        false);

    /// <summary>Reverts the latest page edit.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canUndo))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task UndoAsync() => RunHistoryAsync(true);

    /// <summary>Reapplies the latest undone page edit.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canRedo))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task RedoAsync() => RunHistoryAsync(false);

    /// <summary>Applies an operation to the current selection.</summary>
    /// <param name="kind">The edit to apply.</param>
    /// <param name="value">The rotation or insertion destination.</param>
    /// <param name="failureMessage">The notice shown when the operation fails.</param>
    /// <returns>A task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task RunSelectedEditAsync(PageEditKind kind, int value, string failureMessage) => RunEditAsync(
        static (
            manager,
            request,
            report,
            token) => manager.ApplyAsync(request, report, token),
        new(kind, CopySelectedPages(), value),
        failureMessage);

    /// <summary>Runs an edit that carries a page request, then says what it could not keep.</summary>
    /// <param name="edit">The edit operation.</param>
    /// <param name="request">The selected pages and operation.</param>
    /// <param name="failureMessage">The notice shown when the operation fails.</param>
    /// <returns>A task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task RunEditAsync(
        Func<IPageManager, PageEditRequest, PageEditReport, CancellationToken, ValueTask> edit,
        PageEditRequest request,
        string failureMessage) =>
        RunAsync(
        async (manager, token) =>
{
    var oldSelection = CopyPages(request.Pages);
    var report = new PageEditReport();
    await Task.Run(() => edit(manager, request, report, token).AsTask(), token).ConfigureAwait(true);
    ShowSeparationNotice(report);
    return new(true, () => SelectionAfterEdit(request.Kind, oldSelection, request.Value) ?? Array.Empty<int>());
    },
        failureMessage,
        true);

    /// <summary>Runs an undo or redo operation.</summary>
    /// <param name="undo">Whether to undo rather than redo.</param>
    /// <returns>A task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task RunHistoryAsync(bool undo) => RunAsync(
        async (manager, token) =>
{
    var changed = await Task.Run(() => undo ? manager.UndoAsync(token).AsTask() : manager.RedoAsync(token).AsTask(), token).ConfigureAwait(true);
    return new(changed, () => SelectedPages);
    },
        undo ? UiText.Get("Could not undo the page edit.") : UiText.Get("Could not redo the page edit."),
        true);

    /// <summary>Runs one backend operation and keeps the UI state on the caller's context.</summary>
    /// <param name="operation">The operation.</param>
    /// <param name="failureMessage">The notice shown for expected file or parser failures.</param>
    /// <param name="refreshDocument">Whether a successful operation changed the open document.</param>
    /// <returns>A task.</returns>
    private async Task RunAsync(Func<IPageManager, CancellationToken, Task<PageManagementOperationResult>> operation, string failureMessage, bool refreshDocument)
    {
        if (IsBusy || GetManager() is not { } manager)
        {
            RefreshState();
            return;
        }

        IsBusy = true;
        RefreshState();
        try
        {
            var result = await operation(manager, _lifetime).ConfigureAwait(true);
            if (result.Changed && refreshDocument)
            {
                StructureVersion++;
                if (result.SelectionAfterRefresh is { } selection)
                {
                    SelectPages(selection());
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            _owner.SetNotice(LocalizedMessage.Literal($"{failureMessage} {ex.Message}"));
        }
        finally
        {
            IsBusy = false;
            RefreshState();
        }
    }

    /// <summary>Tells the reader when an edit could not keep the colour separations of a printed page together.</summary>
    /// <param name="report">The edit's report.</param>
    private void ShowSeparationNotice(PageEditReport report)
    {
        if (CopyNotice(report) is { } notice)
        {
            _owner.SetNotice(LocalizedMessage.Literal(notice));
        }
    }

    /// <summary>Refreshes the command state and labels from the current document.</summary>
    private void RefreshState()
    {
        var manager = GetManager();
        var pageCount = _owner.PageCount;
        var selected = SelectedPages.Count;
        RefreshCommandAvailability(manager, pageCount, selected);
        RefreshLabelsAndStatus(manager, selected);
    }

    /// <summary>Refreshes the command states for the document and selection.</summary>
    /// <param name="manager">The page manager.</param>
    /// <param name="pageCount">The number of pages.</param>
    /// <param name="selected">The selected page count.</param>
    private void RefreshCommandAvailability(IPageManager? manager, int pageCount, int selected)
    {
        CanEdit = manager is not null && !IsBusy;
        RefreshDeleteAvailability(selected, pageCount);
        RefreshHistoryAvailability(manager);
        RefreshMovementAvailability(selected, pageCount);
    }

    /// <summary>Refreshes whether the selected pages can be deleted.</summary>
    /// <param name="selected">The selected page count.</param>
    /// <param name="pageCount">The number of pages.</param>
    private void RefreshDeleteAvailability(int selected, int pageCount) =>
        CanDelete = CanEdit && selected > 0 && selected < pageCount;

    /// <summary>Refreshes undo and redo command availability.</summary>
    /// <param name="manager">The page manager.</param>
    private void RefreshHistoryAvailability(IPageManager? manager)
    {
        CanUndo = CanEdit && manager?.UndoLabel is not null;
        CanRedo = CanEdit && manager?.RedoLabel is not null;
    }

    /// <summary>Refreshes whether the selected pages can move in either direction.</summary>
    /// <param name="selected">The selected page count.</param>
    /// <param name="pageCount">The number of pages.</param>
    private void RefreshMovementAvailability(int selected, int pageCount)
    {
        CanMoveEarlier = CanEdit && selected > 0 && SelectedPages[0] > 0;
        CanMoveLater = CanEdit && selected > 0 && SelectedPages[^1] < pageCount - 1;
    }

    /// <summary>Refreshes the labels and status for the document and selection.</summary>
    /// <param name="manager">The page manager.</param>
    /// <param name="selected">The selected page count.</param>
    private void RefreshLabelsAndStatus(IPageManager? manager, int selected)
    {
        UndoText = GetHistoryText(manager?.UndoLabel, UiText.Get("Undo"), "Undo {0}");
        RedoText = GetHistoryText(manager?.RedoLabel, UiText.Get("Redo"), "Redo {0}");
        Status = GetStatus(manager, selected);
    }

    /// <summary>Gets the current status for the document, activity and selection.</summary>
    /// <param name="manager">The page manager.</param>
    /// <param name="selected">The selected page count.</param>
    /// <returns>The status text.</returns>
    private string GetStatus(IPageManager? manager, int selected)
    {
        if (manager is null)
        {
            return UiText.Get("Page management requires the HyperPDF engine.");
        }

        if (IsBusy)
        {
            return UiText.Get("Updating pages…");
        }

        if (selected == 0)
        {
            return UiText.Get("Select one or more pages.");
        }

        return UiText.Format("{0} {1} selected.", selected, (selected == 1 ? UiText.Get("page") : UiText.Get("pages")));
    }

    /// <summary>Gets the selection to show after a page edit.</summary>
    /// <param name="kind">The page edit.</param>
    /// <param name="pages">The original selection.</param>
    /// <param name="value">The edit's destination or rotation.</param>
    /// <returns>The updated page indexes.</returns>
    private int[]? SelectionAfterEdit(PageEditKind kind, int[] pages, int value) => kind switch
    {
        PageEditKind.Rotate => pages,
        PageEditKind.Duplicate or PageEditKind.Move => CopyRange(value, pages.Length),
        PageEditKind.Delete => [Math.Min(pages[0], Math.Max(0, _owner.PageCount - 1))],
        _ => null,
    };

    /// <summary>Gets a manager exposed by the current open document.</summary>
    /// <returns>The page manager, or null when page management is unavailable.</returns>
    private IPageManager? GetManager() => DocumentFeature<IPageManagementSource>.Get(_owner.TryGetDocument()) is { } source ? source.PageManager : null;
}
