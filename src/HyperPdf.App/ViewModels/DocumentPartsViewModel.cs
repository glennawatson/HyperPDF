// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.App.Services;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// A tab's document parts and article threads, shown as a tree of parts with their pages and a list of articles that can be
/// read bead by bead in the order the file gives. The panel only appears for documents that have either. The outline, page
/// labels and thumbnails stay as they are, so the pages are never hidden by a damaged structure.
/// </summary>
[DebuggerDisplay("DocumentPartsViewModel: {Parts.Count} parts, {Threads.Count} articles")]
public sealed partial class DocumentPartsViewModel : ReactiveObject, IDisposable
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Follows the reader's choices for the view model's lifetime.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Whether a thread can move on to a later place.</summary>
    private readonly IObservable<bool> _canGoNext;

    /// <summary>Whether a thread can move back to an earlier place.</summary>
    private readonly IObservable<bool> _canGoPrevious;

    /// <summary>The place each article was last read at, by its position in <see cref="Threads"/>, so choosing it again carries on from there.</summary>
    private readonly Dictionary<int, int> _lastPlaces = [];

    /// <summary>Whether the lists are being filled, so the change of selection is not taken as the reader's choice.</summary>
    private bool _updating;

    /// <summary>Initializes a new instance of the <see cref="DocumentPartsViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public DocumentPartsViewModel(DocumentTabViewModel owner)
    {
        _owner = owner;
        _canGoNext = this.WhenChanged(static vm => vm.SelectedThread, static vm => vm.PlaceIndex, static (thread, place) => thread is not null && place + 1 < thread.Beads.Count);
        _canGoPrevious = this.WhenChanged(static vm => vm.SelectedThread, static vm => vm.PlaceIndex, static (thread, place) => thread is not null && place > 0);

        // The first value of each is the unselected state before the document is read. A choice made while the lists are filled
        // comes from the refill, not the reader; the filters read the view model's own state because a constructor cannot hand
        // 'this' to a state-passing operator.
        _subscriptions.Add(this.WhenChanged(static vm => vm.SelectedPart)
            .Skip(1)
            .KeepNotNull()
            .Where(_ => !_updating)
            .SubscribeSafe(GoToPart, ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static vm => vm.SelectedThread)
            .Skip(1)
            .KeepNotNull()
            .Where(_ => !_updating)
            .SubscribeSafe(StartArticle, ErrorTrace.Write));
    }

    /// <summary>Gets the top-level parts of the document.</summary>
    public ObservableCollection<PartItemViewModel> Parts { get; } = [];

    /// <summary>Gets the article threads of the document.</summary>
    public ObservableCollection<ThreadItemViewModel> Threads { get; } = [];

    /// <summary>Gets a value indicating whether the document has parts.</summary>
    [Reactive]
    public partial bool HasParts { get; private set; }

    /// <summary>Gets a value indicating whether the document has article threads.</summary>
    [Reactive]
    public partial bool HasThreads { get; private set; }

    /// <summary>Gets a value indicating whether the document has parts or articles, so the panel is offered.</summary>
    [Reactive]
    public partial bool HasStructure { get; private set; }

    /// <summary>Gets or sets the part chosen in the tree; choosing one goes to its first page.</summary>
    [Reactive]
    public partial PartItemViewModel? SelectedPart { get; set; }

    /// <summary>Gets or sets the article chosen in the list; choosing one goes to where it was last read, or to its start.</summary>
    [Reactive]
    public partial ThreadItemViewModel? SelectedThread { get; set; }

    /// <summary>Gets the zero based place the chosen article is read at; -1 when none is chosen.</summary>
    [Reactive]
    public partial int PlaceIndex { get; private set; }

    /// <summary>Gets where the chosen article is read, in words, such as "Place 2 of 3, page 4"; empty when none is chosen.</summary>
    [Reactive]
    public partial string PlaceText { get; private set; } = string.Empty;

    /// <summary>Lists the document's parts and articles; called when the document loads and when its pages change.</summary>
    public void Refresh()
    {
        var chosenThread = Threads.IndexOf(SelectedThread!);
        _updating = true;
        try
        {
            Fill(DocumentFeature<IDocumentPartSource>.Get(_owner.TryGetDocument()));
            SelectedPart = null;
            SelectedThread = (uint)chosenThread < (uint)Threads.Count ? Threads[chosenThread] : null;
            ShowPlace(SelectedThread, chosenThread >= 0 && SelectedThread is not null ? _lastPlaces.GetValueOrDefault(chosenThread) : -1);
        }
        finally
        {
            _updating = false;
        }

        if (!HasStructure && _owner.IsPartsMode)
        {
            _owner.SidebarMode = SidebarMode.Thumbnails;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Builds the entries of a level of the parts tree.</summary>
    /// <param name="parts">The document's parts.</param>
    /// <returns>The entries.</returns>
    private static List<PartItemViewModel> Build(IReadOnlyList<DocumentPart> parts)
    {
        var result = new List<PartItemViewModel>(parts.Count);
        foreach (var part in parts)
        {
            result.Add(new(part, Build(part.Children)));
        }

        return result;
    }

    /// <summary>Fills the lists from the document.</summary>
    /// <param name="source">The document's parts and threads, or <see langword="null"/> when it cannot give them.</param>
    private void Fill(IDocumentPartSource? source)
    {
        Parts.Clear();
        Threads.Clear();
        foreach (var part in Build(source?.GetParts() ?? []))
        {
            Parts.Add(part);
        }

        foreach (var thread in source?.GetThreads() ?? [])
        {
            Threads.Add(new(thread));
        }

        HasParts = Parts.Count > 0;
        HasThreads = Threads.Count > 0;
        HasStructure = HasParts || HasThreads;
    }

    /// <summary>Goes to the first page of a part the reader chose.</summary>
    /// <param name="part">The part.</param>
    private void GoToPart(PartItemViewModel part)
    {
        if (part.TargetPage is { } page)
        {
            _owner.GoToPage(page);
        }
    }

    /// <summary>Starts reading an article the reader chose, at the place it was last read or at its start.</summary>
    /// <param name="thread">The article.</param>
    private void StartArticle(ThreadItemViewModel thread)
    {
        var position = Threads.IndexOf(thread);
        ShowPlace(thread, Math.Clamp(_lastPlaces.GetValueOrDefault(position), 0, thread.Beads.Count - 1));
        GoToPlace(thread, PlaceIndex);
    }

    /// <summary>Moves to the next place of the chosen article.</summary>
    [ReactiveCommand(CanExecute = nameof(_canGoNext))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NextPlace() => MoveBy(1);

    /// <summary>Moves back to the previous place of the chosen article.</summary>
    [ReactiveCommand(CanExecute = nameof(_canGoPrevious))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PreviousPlace() => MoveBy(-1);

    /// <summary>Moves along the chosen article and shows the place.</summary>
    /// <param name="step">Plus or minus one.</param>
    private void MoveBy(int step)
    {
        if (SelectedThread is not { } thread)
        {
            return;
        }

        ShowPlace(thread, Math.Clamp(PlaceIndex + step, 0, thread.Beads.Count - 1));
        GoToPlace(thread, PlaceIndex);
    }

    /// <summary>Notes and describes where an article is read.</summary>
    /// <param name="thread">The article, or <see langword="null"/> when none is chosen.</param>
    /// <param name="place">The zero based place, or -1 for none.</param>
    private void ShowPlace(ThreadItemViewModel? thread, int place)
    {
        if (thread is null || place < 0 || place >= thread.Beads.Count)
        {
            PlaceIndex = -1;
            PlaceText = string.Empty;
            return;
        }

        PlaceIndex = place;
        _lastPlaces[Threads.IndexOf(thread)] = place;
        PlaceText = string.Create(CultureInfo.CurrentCulture, $"Place {place + 1} of {thread.Beads.Count}, page {thread.Beads[place].PageIndex + 1}");
    }

    /// <summary>Scrolls to a place of an article without adding it to Back history, so stepping through never fills it.</summary>
    /// <param name="thread">The article.</param>
    /// <param name="place">The zero based place.</param>
    private void GoToPlace(ThreadItemViewModel thread, int place)
    {
        var bead = thread.Beads[place];
        if (bead.Bounds is { } area)
        {
            _owner.ShowArea(bead.PageIndex, area);
            return;
        }

        _owner.ShowPage(bead.PageIndex);
    }
}
