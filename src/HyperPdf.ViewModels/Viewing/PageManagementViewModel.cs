// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.ViewModels.Markup;
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

    /// <summary>The page number settings, made when first asked for.</summary>
    private PageLabelsViewModel? _labels;

    /// <summary>The settings that take hidden information out of the document, made when first asked for.</summary>
    private SanitizeViewModel? _sanitize;

    /// <summary>The comparison with another document, made when first asked for.</summary>
    private CompareViewModel? _compare;

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
            this.WhenChanged(static vm => vm.SelectedThumbnails).SwitchMap(static thumbnails => thumbnails.CollectionChanges).Skip(1).SubscribeSafe(SelectThumbnailPages, ErrorTrace.Write));
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

    /// <summary>Gets the settings that number a run of pages a different way, such as i, ii, iii for the front matter.</summary>
    public PageLabelsViewModel Labels => _labels ??= new PageLabelsViewModel(_owner);

    /// <summary>Gets the settings that take hidden information out of the document before it is shared.</summary>
    public SanitizeViewModel Sanitize => _sanitize ??= new SanitizeViewModel(_owner);

    /// <summary>Gets the comparison of the open document with another file.</summary>
    public CompareViewModel Compare => _compare ??= new CompareViewModel(_owner, _owner.Services);

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
    /// <returns>A cold signal that ends after the move.</returns>
    public IObservable<RxVoid> Move(int destination) => SelectedPages.Count == 0 || (uint)destination > (uint)(_owner.PageCount - SelectedPages.Count)
        ? Signal.Return(RxVoid.Default)
        : RunEdit(
            static (manager, request, report, token) => manager.ApplyAsync(request, report, token),
            new(PageEditKind.Move, CopySelectedPages(), destination),
            UiText.Get("Could not move pages."));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Tells the tab that the pages' sizes or order changed by an edit made outside this view model, so it reads the page structure again.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void NotifyStructureChanged() => StructureVersion++;

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

    /// <summary>Extracts the pages with the library's async writer, the one task here.</summary>
    /// <param name="manager">The page manager.</param>
    /// <param name="pages">The pages.</param>
    /// <param name="stream">Where the PDF goes.</param>
    /// <param name="report">What the extraction found.</param>
    /// <param name="token">Stops the work.</param>
    /// <returns>A task giving <see langword="true"/> once the pages are written.</returns>
    private static async Task<bool> ExtractPages(IPageManager manager, ReadOnlyMemory<int> pages, Stream stream, PageEditReport report, CancellationToken token)
    {
        await manager.ExtractAsync(pages, stream, report, token).ConfigureAwait(false);
        return true;
    }

    /// <summary>Waits for a library call that gives no answer.</summary>
    /// <param name="call">The library call.</param>
    /// <param name="token">Stops the work.</param>
    /// <returns>A task giving the completion value.</returns>
    private static async Task<RxVoid> Wait(Func<CancellationToken, ValueTask> call, CancellationToken token)
    {
        await call(token).ConfigureAwait(false);
        return RxVoid.Default;
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
    /// <returns>A signal that ends once the pages are deleted or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canDelete))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> Delete() => RunSelectedEdit(PageEditKind.Delete, 0, UiText.Get("Could not delete pages."));

    /// <summary>Turns the selected pages clockwise by 90 degrees.</summary>
    /// <returns>A signal that ends once the pages are turned or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canEditSelection))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> RotateClockwise() => RunSelectedEdit(PageEditKind.Rotate, ClockwiseDegrees, UiText.Get("Could not rotate pages."));

    /// <summary>Turns the selected pages counterclockwise by 90 degrees.</summary>
    /// <returns>A signal that ends once the pages are turned or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canEditSelection))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> RotateCounterclockwise() => RunSelectedEdit(PageEditKind.Rotate, CounterclockwiseDegrees, UiText.Get("Could not rotate pages."));

    /// <summary>Copies the selected pages after the last selected page.</summary>
    /// <returns>A signal that ends once the pages are copied or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canEditSelection))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> Duplicate() => RunSelectedEdit(PageEditKind.Duplicate, SelectedPages[^1] + 1, UiText.Get("Could not duplicate pages."));

    /// <summary>Selects every page of the document.</summary>
    [ReactiveCommand]
    private void SelectAll()
    {
        var pages = new int[_owner.PageCount];
        for (var i = 0; i < pages.Length; i++)
        {
            pages[i] = i;
        }

        SelectPages(pages);
    }

    /// <summary>Adds the page being read to the pages selected, so several pages can be changed at once.</summary>
    [ReactiveCommand]
    private void AddCurrentToSelection()
    {
        var current = _owner.CurrentPageIndex;
        var pages = new List<int>(SelectedPages);
        if (pages.BinarySearch(current) >= 0)
        {
            return;
        }

        pages.Add(current);
        pages.Sort();
        SelectPages(pages);
    }

    /// <summary>Moves the selected pages one position earlier.</summary>
    /// <returns>A signal that ends once the pages are moved or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canMoveEarlier))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> MoveEarlier() => Move(SelectedPages[0] - 1);

    /// <summary>Moves the selected pages one position later.</summary>
    /// <returns>A signal that ends once the pages are moved or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canMoveLater))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> MoveLater() => Move(SelectedPages[0] + 1);

    /// <summary>Asks for PDF files and inserts them after the selected pages or current page.</summary>
    /// <returns>A signal that ends once the files are inserted, the choice was given up or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canEdit))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> InsertFiles() => Run(
        manager => InsertInteraction.Handle(RxVoid.Default).ToSignal()
            .SwitchMap(paths => paths.Length > 0
                ? InsertPaths(manager, paths, SelectedPages.Count > 0 ? SelectedPages[0] : Math.Clamp(_owner.CurrentPageIndex, 0, _owner.PageCount))
                : Signal.Return(default(PageManagementOperationResult))),
        UiText.Get("Could not insert pages."),
        true);

    /// <summary>Asks for picture files and inserts each as a page after the selected pages or current page.</summary>
    /// <returns>A signal that ends once the pictures are inserted, the choice was given up or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canEdit))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> InsertImages() => Run(
        manager => InsertImagesInteraction.Handle(RxVoid.Default).ToSignal()
            .SwitchMap(paths => paths.Length == 0 ? Signal.Return(default(PageManagementOperationResult)) : InsertPictures(manager, paths)),
        UiText.Get("Could not add pictures as pages."),
        true);

    /// <summary>Asks for PDF files and appends every page to the document.</summary>
    /// <returns>A signal that ends once the files are merged, the choice was given up or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canEdit))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> MergeFiles() => Run(
        manager => MergeFilesInteraction.Handle(RxVoid.Default).ToSignal()
            .SwitchMap(paths => paths.Length > 0 ? InsertPaths(manager, paths, _owner.PageCount) : Signal.Return(default(PageManagementOperationResult))),
        UiText.Get("Could not merge pages."),
        true);

    /// <summary>Asks where to save the selected pages, then writes them as a new PDF.</summary>
    /// <returns>A signal that ends once the pages are written, the choice was given up or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canEditSelection))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> Extract() => Run(
        manager => ExtractInteraction.Handle(Path.GetFileNameWithoutExtension(_owner.FileName) + ExtractedPagesSuffix).ToSignal()
            .SwitchMap(destination => ExtractTo(manager, destination)),
        UiText.Get("Could not extract pages."),
        false);

    /// <summary>Reverts the latest page edit.</summary>
    /// <returns>A signal that ends once the edit is reverted or failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canUndo))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> Undo() => RunHistory(true);

    /// <summary>Reapplies the latest undone page edit.</summary>
    /// <returns>A signal that ends once the edit is reapplied or failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canRedo))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> Redo() => RunHistory(false);

    /// <summary>Applies an operation to the current selection.</summary>
    /// <param name="kind">The edit to apply.</param>
    /// <param name="value">The rotation or insertion destination.</param>
    /// <param name="failureMessage">The notice shown when the operation fails.</param>
    /// <returns>A cold signal that ends once the edit is made or failed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> RunSelectedEdit(PageEditKind kind, int value, string failureMessage) => RunEdit(
        static (manager, request, report, token) => manager.ApplyAsync(request, report, token),
        new(kind, CopySelectedPages(), value),
        failureMessage);

    /// <summary>Runs an edit that carries a page request, then says what it could not keep.</summary>
    /// <param name="edit">The edit operation.</param>
    /// <param name="request">The selected pages and operation.</param>
    /// <param name="failureMessage">The notice shown when the operation fails.</param>
    /// <returns>A cold signal that ends once the edit is made or failed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> RunEdit(
        Func<IPageManager, PageEditRequest, PageEditReport, CancellationToken, ValueTask> edit,
        PageEditRequest request,
        string failureMessage) =>
        Run(
            manager =>
            {
                var oldSelection = CopyPages(request.Pages);
                var report = new PageEditReport();
                return Call(token => edit(manager, request, report, token))
                    .Select(_ => Edited(report, request, oldSelection));
            },
            failureMessage,
            true);

    /// <summary>Says what an edit could not keep, and how the selection follows it.</summary>
    /// <param name="report">The edit's report.</param>
    /// <param name="request">The selected pages and operation.</param>
    /// <param name="oldSelection">The pages selected before the edit.</param>
    /// <returns>The result of the operation.</returns>
    private PageManagementOperationResult Edited(PageEditReport report, PageEditRequest request, int[] oldSelection)
    {
        ShowSeparationNotice(report);
        return new(true, () => SelectionAfterEdit(request.Kind, oldSelection, request.Value) ?? Array.Empty<int>());
    }

    /// <summary>Runs an undo or redo operation.</summary>
    /// <param name="undo">Whether to undo rather than redo.</param>
    /// <returns>A cold signal that ends once the edit is reverted or failed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> RunHistory(bool undo) => Run(
        manager => Call(token => undo ? manager.UndoAsync(token) : manager.RedoAsync(token))
            .Select(changed => new PageManagementOperationResult(changed, () => SelectedPages)),
        undo ? UiText.Get("Could not undo the page edit.") : UiText.Get("Could not redo the page edit."),
        true);

    /// <summary>Inserts PDF files at a page and selects the pages they added.</summary>
    /// <param name="manager">The page manager.</param>
    /// <param name="paths">The files.</param>
    /// <param name="index">Where the pages go.</param>
    /// <returns>A cold signal giving the result of the operation.</returns>
    private IObservable<PageManagementOperationResult> InsertPaths(IPageManager manager, string[] paths, int index)
    {
        var previousCount = _owner.PageCount;
        var report = new PageEditReport();

        // The library reads and rewrites the pages before its first await, so the call starts on the pool.
        return Call(token => manager.InsertAsync(index, paths, report, token))
            .Select(_ => Inserted(report, index, previousCount));
    }

    /// <summary>Says what an insert could not keep, and selects the pages it added.</summary>
    /// <param name="report">The edit's report.</param>
    /// <param name="index">Where the pages went.</param>
    /// <param name="previousCount">The number of pages before the insert.</param>
    /// <returns>The result of the operation.</returns>
    private PageManagementOperationResult Inserted(PageEditReport report, int index, int previousCount)
    {
        ShowSeparationNotice(report);
        return new(true, () => CopyRange(index, Math.Max(0, _owner.PageCount - previousCount)));
    }

    /// <summary>Reads picture files on the pool and inserts each as a page.</summary>
    /// <param name="manager">The page manager.</param>
    /// <param name="paths">The picture files.</param>
    /// <returns>A cold signal giving the result of the operation.</returns>
    private IObservable<PageManagementOperationResult> InsertPictures(IPageManager manager, string[] paths)
    {
        var head = _owner.Services.Head;
        return Signal.Start(() => ReadImages(head, paths, _lifetime), TaskPoolSequencer.Default)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SwitchMap(images => images.Length == 0 ? NoPictures() : InsertReadPictures(manager, images, paths.Length));
    }

    /// <summary>Says none of the chosen files could be read as pictures.</summary>
    /// <returns>A signal giving an operation that changed nothing.</returns>
    private IObservable<PageManagementOperationResult> NoPictures()
    {
        _owner.SetNotice(LocalizedMessage.Get(UiText, "None of the chosen files could be read as pictures."));
        return Signal.Return(default(PageManagementOperationResult));
    }

    /// <summary>Inserts pictures that were read, one page each.</summary>
    /// <param name="manager">The page manager.</param>
    /// <param name="images">The pictures that were read.</param>
    /// <param name="chosen">How many files were chosen.</param>
    /// <returns>A cold signal giving the result of the operation.</returns>
    private IObservable<PageManagementOperationResult> InsertReadPictures(IPageManager manager, PageImageSource[] images, int chosen)
    {
        var index = SelectedPages.Count > 0 ? SelectedPages[^1] + 1 : Math.Clamp(_owner.CurrentPageIndex + 1, 0, _owner.PageCount);
        return Call(token => manager.InsertImagesAsync(index, images, token))
            .Select(_ => InsertedPictures(index, images.Length, chosen));
    }

    /// <summary>Says when some chosen files were skipped, and selects the pages the pictures added.</summary>
    /// <param name="index">Where the pages went.</param>
    /// <param name="inserted">How many pictures were inserted.</param>
    /// <param name="chosen">How many files were chosen.</param>
    /// <returns>The result of the operation.</returns>
    private PageManagementOperationResult InsertedPictures(int index, int inserted, int chosen)
    {
        if (inserted < chosen)
        {
            _owner.SetNotice(LocalizedMessage.Get(UiText, "Some of the chosen files could not be read as pictures and were skipped."));
        }

        return new(true, () => CopyRange(index, inserted));
    }

    /// <summary>Writes the selected pages as a new PDF.</summary>
    /// <param name="manager">The page manager.</param>
    /// <param name="destination">Where to save, or empty when the choice was given up.</param>
    /// <returns>A cold signal giving an operation that changed nothing in the open document.</returns>
    private IObservable<PageManagementOperationResult> ExtractTo(IPageManager manager, string? destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            return Signal.Return(default(PageManagementOperationResult));
        }

        if (SamePath(destination, _owner.FilePath))
        {
            _owner.SetNotice(LocalizedMessage.Get(UiText, "Choose a different file so the open document is not replaced."));
            return Signal.Return(default(PageManagementOperationResult));
        }

        var report = new PageEditReport();
        var pages = CopySelectedPages();
        return CopyFile.Write(destination, TemporarySuffix, (stream, token) => ExtractPages(manager, pages, stream, report, token), static written => written, _lifetime)
            .Select(_ => Extracted(report));
    }

    /// <summary>Says how many pages were extracted.</summary>
    /// <param name="report">The edit's report.</param>
    /// <returns>An operation that changed nothing in the open document.</returns>
    private PageManagementOperationResult Extracted(PageEditReport report)
    {
        _owner.SetNotice(
            CopyNotice(report) is { } separations
                ? LocalizedMessage.Format(UiText, "Extracted {0} pages. {1}", SelectedPages.Count, separations)
                : LocalizedMessage.Format(UiText, "Extracted {0} pages.", SelectedPages.Count));
        return default;
    }

    /// <summary>Calls the page manager on the pool and brings the answer back to the interface thread.</summary>
    /// <param name="call">The library call.</param>
    /// <returns>A cold signal that ends once the call is done.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> Call(Func<CancellationToken, ValueTask> call) =>
        // The page manager is the PDF library's async API, so its call is the one task here.
        Signal.FromAsync(token => Wait(call, token), _lifetime)
            .SubscribeOn(TaskPoolSequencer.Default)
            .ObserveOn(RxSchedulers.MainThreadScheduler);

    /// <summary>Calls the page manager on the pool and brings the answer back to the interface thread.</summary>
    /// <typeparam name="TResult">What the call gives.</typeparam>
    /// <param name="call">The library call.</param>
    /// <returns>A cold signal giving the answer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<TResult> Call<TResult>(Func<CancellationToken, ValueTask<TResult>> call) =>
        Signal.FromAsync(token => call(token).AsTask(), _lifetime)
            .SubscribeOn(TaskPoolSequencer.Default)
            .ObserveOn(RxSchedulers.MainThreadScheduler);

    /// <summary>Runs one backend operation and keeps the UI state on the interface thread.</summary>
    /// <param name="operation">The operation.</param>
    /// <param name="failureMessage">The notice shown for expected file or parser failures.</param>
    /// <param name="refreshDocument">Whether a successful operation changed the open document.</param>
    /// <returns>A cold signal that ends once the operation is done or failed.</returns>
    private IObservable<RxVoid> Run(Func<IPageManager, IObservable<PageManagementOperationResult>> operation, string failureMessage, bool refreshDocument)
    {
        if (IsBusy || GetManager() is not { } manager)
        {
            RefreshState();
            return Signal.Return(RxVoid.Default);
        }

        IsBusy = true;
        RefreshState();
        return operation(manager)
            .Select(result => Applied(result, refreshDocument))
            .Catch<RxVoid, Exception>(error => Failed(error, failureMessage))
            .Finally(EndBusy);
    }

    /// <summary>Selects the pages an operation asks for, once the document changed.</summary>
    /// <param name="result">The result of the operation.</param>
    /// <param name="refreshDocument">Whether a successful operation changed the open document.</param>
    /// <returns>The completion value.</returns>
    private RxVoid Applied(PageManagementOperationResult result, bool refreshDocument)
    {
        if (result.Changed && refreshDocument)
        {
            StructureVersion++;
            if (result.SelectionAfterRefresh is { } selection)
            {
                SelectPages(selection());
            }
        }

        return RxVoid.Default;
    }

    /// <summary>Says why the operation failed, or passes on a failure that is not expected.</summary>
    /// <param name="error">The failure.</param>
    /// <param name="failureMessage">The notice shown for expected file or parser failures.</param>
    /// <returns>A signal that ends for an expected failure, or fails with the same error.</returns>
    private IObservable<RxVoid> Failed(Exception error, string failureMessage)
    {
        if (error is OperationCanceledException && _lifetime.IsCancellationRequested)
        {
            return Signal.Return(RxVoid.Default);
        }

        if (!IsExpectedFailure(error))
        {
            return Signal.Throw<RxVoid>(error);
        }

        _owner.SetNotice(LocalizedMessage.Literal($"{failureMessage} {error.Message}"));
        return Signal.Return(RxVoid.Default);
    }

    /// <summary>Lets the view model take another operation, once one is over.</summary>
    private void EndBusy()
    {
        IsBusy = false;
        RefreshState();
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
