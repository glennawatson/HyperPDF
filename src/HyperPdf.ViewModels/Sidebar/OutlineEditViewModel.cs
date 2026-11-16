// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Sidebar;

/// <summary>
/// Adds, renames, removes and rearranges the bookmarks of one open document. Every edit is one step in the document's undo
/// history, so the page edit Undo also takes it back. The result of each edit is written in <see cref="Status"/> for screen readers.
/// </summary>
[DebuggerDisplay("OutlineEditViewModel: {Status}")]
public sealed partial class OutlineEditViewModel : ReactiveObject, IDisposable, ILocalisationChildren
{
    /// <summary>The index that appends.</summary>
    private const int Append = -1;

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Cancelled when the tab closes, which stops an edit in flight; its source is owned by <see cref="_subscriptions"/>.</summary>
    private readonly CancellationToken _lifetime;

    /// <summary>Owns the selection subscriptions.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Whether a bookmark can be added.</summary>
    private readonly IObservable<bool> _canAdd;

    /// <summary>Whether the selected bookmark can be renamed or removed.</summary>
    private readonly IObservable<bool> _canChange;

    /// <summary>Whether the selected bookmark can move up.</summary>
    private readonly IObservable<bool> _canMoveUp;

    /// <summary>Whether the selected bookmark can move down.</summary>
    private readonly IObservable<bool> _canMoveDown;

    /// <summary>Whether the selected bookmark can become a child of the one above it.</summary>
    private readonly IObservable<bool> _canIndent;

    /// <summary>Whether the selected bookmark can move out of its parent.</summary>
    private readonly IObservable<bool> _canOutdent;

    /// <summary>Initializes a new instance of the <see cref="OutlineEditViewModel"/> class.</summary>
    /// <param name="owner">The tab holding the document.</param>
    public OutlineEditViewModel(DocumentTabViewModel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        _canAdd = this.WhenChanged(static vm => vm.CanAdd);
        _canChange = this.WhenChanged(static vm => vm.CanChange);
        _canMoveUp = this.WhenChanged(static vm => vm.CanMoveUp);
        _canMoveDown = this.WhenChanged(static vm => vm.CanMoveDown);
        _canIndent = this.WhenChanged(static vm => vm.CanIndent);
        _canOutdent = this.WhenChanged(static vm => vm.CanOutdent);
        var lifetime = new CancellationDisposable();
        _lifetime = lifetime.Token;
        _subscriptions.Add(lifetime);
        _subscriptions.Add(
            owner.WhenChanged(
                static tab => tab.SelectedOutlineItem,
                static tab => tab.Outline,
                static tab => tab.IsLoaded,
                static (
                    _,
                    _,
                    _) => RxVoid.Default).SubscribeSafe(_ => RefreshState(), ErrorTrace.Write));
    }

    /// <summary>Gets a number that changes each time an edit has changed the bookmark list, so a view can restore the keyboard focus the new list took away.</summary>
    [Reactive]
    public partial int EditVersion { get; private set; }

    /// <summary>Gets the interaction asking for a bookmark's title.</summary>
    public Interaction<TextPrompt, string?> PromptInteraction { get; } = new();

    /// <summary>Gets the command that undoes the latest edit of the document, which may be a bookmark edit.</summary>
    public ICommand UndoCommand => _owner.Pages.UndoCommand;

    /// <summary>Gets a value indicating whether the document's bookmarks can be edited.</summary>
    [Reactive]
    public partial bool CanAdd { get; private set; }

    /// <summary>Gets a value indicating whether a bookmark is selected and can be renamed or removed.</summary>
    [Reactive]
    public partial bool CanChange { get; private set; }

    /// <summary>Gets a value indicating whether the selected bookmark can move up.</summary>
    [Reactive]
    public partial bool CanMoveUp { get; private set; }

    /// <summary>Gets a value indicating whether the selected bookmark can move down.</summary>
    [Reactive]
    public partial bool CanMoveDown { get; private set; }

    /// <summary>Gets a value indicating whether the selected bookmark can become a child of the bookmark above it.</summary>
    [Reactive]
    public partial bool CanIndent { get; private set; }

    /// <summary>Gets a value indicating whether the selected bookmark can move out of its parent.</summary>
    [Reactive]
    public partial bool CanOutdent { get; private set; }

    /// <summary>Gets what the latest edit did or why it was refused.</summary>
    [Reactive]
    public partial string Status { get; private set; } = string.Empty;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited) => LocalisationRefresh.RefreshCore(_owner, visited);

    /// <summary>Finds an entry's place among its siblings.</summary>
    /// <param name="items">The entries at one level.</param>
    /// <param name="parentId">The object number of the level's parent; 0 for the top level.</param>
    /// <param name="parentParentId">The object number of the parent's parent.</param>
    /// <param name="parentIndex">The parent's position among its siblings.</param>
    /// <param name="id">The entry's object number.</param>
    /// <returns>The place, or <see langword="null"/> when the entry is not in the tree.</returns>
    internal static OutlinePlace? Locate(IReadOnlyList<OutlineItemViewModel> items, int parentId, int parentParentId, int parentIndex, int id)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Id == id)
            {
                return new(parentId, parentParentId, parentIndex, i, items.Count, i > 0 ? items[i - 1].Id : 0);
            }

            if (Locate(items[i].Children, items[i].Id, parentId, i, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Adds a bookmark to the current page, below the selected bookmark or at the top level.</summary>
    /// <returns>A signal that ends once the bookmark is added or the question was given up.</returns>
    [ReactiveCommand(CanExecute = nameof(_canAdd))]
    private IObservable<RxVoid> Add()
    {
        var page = _owner.CurrentPageIndex;
        return Ask(
            new(
                LocalizedMessage.Get(UiText, "Add Bookmark"),
                LocalizedMessage.Get(UiText, "Title"),
                UiText.Format("Page {0}", _owner.GetPageDisplay(page)),
                LocalizedMessage.Get(UiText, "Add Bookmark"),
                false),
            title => Run(
                editor => editor.AddBookmark(_owner.SelectedOutlineItem?.Id ?? 0, Append, title, page),
                UiText.Format("Added bookmark {0}.", title),
                _owner.SelectedOutlineItem?.Id ?? 0));
    }

    /// <summary>Asks for a new title for the selected bookmark and renames it.</summary>
    /// <returns>A signal that ends once the bookmark is renamed or the question was given up.</returns>
    [ReactiveCommand(CanExecute = nameof(_canChange))]
    private IObservable<RxVoid> Rename() =>
        _owner.SelectedOutlineItem is not { } item
            ? Signal.Return(RxVoid.Default)
            : Ask(
                new(
                    LocalizedMessage.Get(UiText, "Rename Bookmark"),
                    LocalizedMessage.Get(UiText, "Title"),
                    item.Title,
                    LocalizedMessage.Get(UiText, "Rename"),
                    false),
                title => Run(editor => editor.RenameBookmark(item.Id, title), UiText.Format("Renamed bookmark to {0}.", title), item.Id));

    /// <summary>Removes the selected bookmark and the bookmarks below it. Undo puts them back.</summary>
    /// <returns>A signal that ends once the bookmark is removed or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canChange))]
    private IObservable<RxVoid> Remove() => _owner.SelectedOutlineItem is { } item
        ? Run(
            editor => editor.RemoveBookmark(item.Id),
            UiText.Format("Removed bookmark {0}. Undo puts it back.", item.Title),
            0)
        : Signal.Return(RxVoid.Default);

    /// <summary>Moves the selected bookmark above its previous sibling.</summary>
    /// <returns>A signal that ends once the bookmark is moved or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canMoveUp))]
    private IObservable<RxVoid> MoveUp() => Place() is { } place && _owner.SelectedOutlineItem is { } item
        ? Run(
            editor => editor.MoveBookmark(item.Id, place.ParentId, place.Index - 1),
            UiText.Format("Moved {0} up.", item.Title),
            item.Id)
        : Signal.Return(RxVoid.Default);

    /// <summary>Moves the selected bookmark below its next sibling.</summary>
    /// <returns>A signal that ends once the bookmark is moved or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canMoveDown))]
    private IObservable<RxVoid> MoveDown() => Place() is { } place && _owner.SelectedOutlineItem is { } item
        ? Run(
            editor => editor.MoveBookmark(item.Id, place.ParentId, place.Index + 1),
            UiText.Format("Moved {0} down.", item.Title),
            item.Id)
        : Signal.Return(RxVoid.Default);

    /// <summary>Makes the selected bookmark the last child of the bookmark above it.</summary>
    /// <returns>A signal that ends once the bookmark is moved or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canIndent))]
    private IObservable<RxVoid> Indent() => Place() is { } place && _owner.SelectedOutlineItem is { } item
        ? Run(
            editor => editor.MoveBookmark(item.Id, place.PreviousId, Append),
            UiText.Format("Moved {0} into the bookmark above.", item.Title),
            item.Id)
        : Signal.Return(RxVoid.Default);

    /// <summary>Moves the selected bookmark out of its parent, to just after the parent.</summary>
    /// <returns>A signal that ends once the bookmark is moved or the edit failed.</returns>
    [ReactiveCommand(CanExecute = nameof(_canOutdent))]
    private IObservable<RxVoid> Outdent() => Place() is { } place && _owner.SelectedOutlineItem is { } item
        ? Run(
            editor => editor.MoveBookmark(item.Id, place.GrandParentId, place.ParentIndex + 1),
            UiText.Format("Moved {0} out of its parent.", item.Title),
            item.Id)
        : Signal.Return(RxVoid.Default);

    /// <summary>Gets the selected bookmark's place in the tree.</summary>
    /// <returns>The place, or <see langword="null"/> when nothing is selected.</returns>
    private OutlinePlace? Place() => _owner.SelectedOutlineItem is { } item ? Locate(_owner.Outline, 0, 0, 0, item.Id) : null;

    /// <summary>Asks for a title and, when one is typed, runs the edit it asks for.</summary>
    /// <param name="prompt">The question.</param>
    /// <param name="edit">Makes the edit from the title typed.</param>
    /// <returns>A cold signal that ends once the edit is made or the question was given up.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> Ask(TextPrompt prompt, Func<string, IObservable<RxVoid>> edit) =>
        PromptInteraction.Handle(prompt).ToSignal()
            .SwitchMap(title => string.IsNullOrWhiteSpace(title) ? Signal.Return(RxVoid.Default) : edit(title));

    /// <summary>Runs one edit, then lists the bookmarks again and says what happened.</summary>
    /// <param name="edit">The edit.</param>
    /// <param name="done">What to say when it worked.</param>
    /// <param name="selectId">The bookmark to select afterwards, or 0 for none.</param>
    /// <returns>A cold signal that ends once the edit is shown.</returns>
    private IObservable<RxVoid> Run(Func<IOutlineEditor, IObservable<DocumentEditResult>> edit, string done, int selectId) =>
        DocumentFeature<IOutlineEditor>.Get(_owner.TryGetDocument()) is not { } editor
            ? Signal.Return(RxVoid.Default)

            : edit(editor)
                .ThrowWhenCancelled(_lifetime)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Select(result => Edited(result, done, selectId))
                .Catch<RxVoid, OperationCanceledException>(Stopped);

    /// <summary>Lists the bookmarks again after an edit and says what happened.</summary>
    /// <param name="result">What the library said.</param>
    /// <param name="done">What to say when it worked.</param>
    /// <param name="selectId">The bookmark to select afterwards, or 0 for none.</param>
    /// <returns>The completion value.</returns>
    private RxVoid Edited(DocumentEditResult result, string done, int selectId)
    {
        if (result.Succeeded)
        {
            ShowEdited(selectId);
            _owner.Pages.Refresh();
            EditVersion++;
        }

        Status = result.Succeeded ? done : result.Problem ?? UiText.Get("The bookmarks could not be changed.");
        return RxVoid.Default;
    }

    /// <summary>Gives up quietly when the tab closed, and fails otherwise.</summary>
    /// <param name="error">The cancellation.</param>
    /// <returns>A signal that ends at once, or fails with the same error.</returns>
    private IObservable<RxVoid> Stopped(OperationCanceledException error) =>
        _lifetime.IsCancellationRequested ? Signal.Return(RxVoid.Default) : Signal.Throw<RxVoid>(error);

    /// <summary>Lists the bookmarks again, keeping the same entries open and the entry with the given number selected.</summary>
    /// <param name="selectId">The object number of the entry to select, or 0 for none.</param>
    private void ShowEdited(int selectId)
    {
        var expanded = new HashSet<int>();
        OutlineItemViewModel.CollectExpanded(_owner.Outline, expanded);
        var items = _owner.TryGetDocument() is { } document ? OutlineItemViewModel.Create(document.GetOutline()) : [];
        OutlineItemViewModel.ApplyExpanded(items, expanded);
        _owner.ShowEditedOutline(items, selectId);
    }

    /// <summary>Works out what the selected bookmark can do.</summary>
    private void RefreshState()
    {
        var editable = _owner.IsLoaded && DocumentFeature<IOutlineEditor>.Get(_owner.TryGetDocument()) is not null;
        var place = editable ? Place() : null;
        CanAdd = editable;
        CanChange = place is not null && _owner.SelectedOutlineItem is { Id: > 0 };
        CanMoveUp = CanChange && place is { Index: > 0 };
        CanMoveDown = CanChange && place is { } below && below.Index < below.Count - 1;
        CanIndent = CanChange && place is { PreviousId: > 0 };
        CanOutdent = CanChange && place is { ParentId: > 0 };
    }

    /// <summary>Where a bookmark sits in the tree.</summary>
    /// <param name="ParentId">The parent's object number; 0 for the top level.</param>
    /// <param name="GrandParentId">The parent's parent's object number.</param>
    /// <param name="ParentIndex">The parent's position among its siblings.</param>
    /// <param name="Index">The bookmark's position among its siblings.</param>
    /// <param name="Count">The number of siblings, counting the bookmark.</param>
    /// <param name="PreviousId">The previous sibling's object number; 0 when first.</param>
    internal readonly record struct OutlinePlace(int ParentId, int GrandParentId, int ParentIndex, int Index, int Count, int PreviousId);
}
