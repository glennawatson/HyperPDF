// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.ViewModels.Viewing;
using HyperPdfLibrary;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>
/// Edit mode: the text, pictures and drawings a page is made of are pointed at, picked, moved, retyped and removed on the
/// page itself, with each edit written to the document straight away and undone with the document's undo.
/// </summary>
[DebuggerDisplay("PageContentEditViewModel: editing {IsEditing}, picked {Selected}")]
public sealed partial class PageContentEditViewModel : ReactiveObject, IDisposable
{
    /// <summary>How far the pointer must go, in points, for a press on the picked object to count as a drag and not a click.</summary>
    private const float DragThreshold = 3;

    /// <summary>How many times a page is read after an edit before the read is given up on.</summary>
    private const int ReadAttempts = 3;

    /// <summary>The version text of a PDF 2.0 file.</summary>
    private const string Pdf20 = "2.0";

    /// <summary>The percent that means fully solid.</summary>
    private const int WholePercent = 100;

    /// <summary>The tab holding the document.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Cancelled when the tab closes.</summary>
    private readonly CancellationToken _lifetime;

    /// <summary>Owns the lifetime source and the subscriptions.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>The objects read for each page, until the document's pages change.</summary>
    private readonly Dictionary<int, PageObjectInfo[]> _pages = [];

    /// <summary>The pages being read now, so a page is read once while the pointer moves over it. Each ends when its read does.</summary>
    private readonly Dictionary<int, Signal<RxVoid>> _loading = [];

    /// <summary>The page a drag began on, or -1 when none is in progress.</summary>
    private int _dragPage = -1;

    /// <summary>The dashes chosen for the lines of picked objects, kept so a change of the ends or corners keeps them.</summary>
    private PageLineDash _lineDash;

    /// <summary>The ends chosen for the lines of picked objects.</summary>
    private PageLineCap _lineCap;

    /// <summary>The corners chosen for the lines of picked objects.</summary>
    private PageLineJoin _lineJoin;

    /// <summary>Where the drag began, in page space.</summary>
    private PagePoint _dragStart;

    /// <summary>Initializes a new instance of the <see cref="PageContentEditViewModel"/> class.</summary>
    /// <param name="owner">The tab holding the document.</param>
    public PageContentEditViewModel(DocumentTabViewModel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        var lifetime = new CancellationDisposable();
        _lifetime = lifetime.Token;
        _subscriptions.Add(lifetime);

        // Reordering, deleting or undoing pages, or reopening the file, can leave what was read and picked pointing at the wrong thing.
        // WhenChanged repeats a value when the tab raises a change for something else, which must not drop a pick that is still right.
        _subscriptions.Add(owner.WhenChanged(static tab => tab.LayoutVersion).DistinctUntilChanged().Skip(1).SubscribeSafe(_ => Forget(), ErrorTrace.Write));
    }

    /// <summary>Gets a value indicating whether Edit mode is on.</summary>
    [Reactive]
    public partial bool IsEditing { get; private set; }

    /// <summary>Gets the object under the pointer, or <see langword="null"/>.</summary>
    [Reactive]
    public partial PageObjectPick? Hovered { get; private set; }

    /// <summary>Gets the object picked to move, retype or remove, or <see langword="null"/>.</summary>
    [Reactive]
    public partial PageObjectPick? Selected { get; private set; }

    /// <summary>Gets the object whose text is being retyped on the page, or <see langword="null"/>.</summary>
    [Reactive]
    public partial PageObjectPick? TextEdit { get; private set; }

    /// <summary>Gets or sets the text typed in the page editor.</summary>
    [Reactive]
    public partial string EditingText { get; set; } = string.Empty;

    /// <summary>Gets a number that changes when the outlines drawn over the pages change, so they are drawn again.</summary>
    [Reactive]
    public partial int MarksVersion { get; private set; }

    /// <summary>Gets how far the picked object has been dragged so far, in page space, so its outline follows the pointer.</summary>
    [Reactive]
    public partial PagePoint DragDelta { get; private set; }

    /// <summary>Gets the page a drag is on, or -1.</summary>
    public int DragPage => _dragPage;

    /// <summary>Gets what Edit mode last did or found, in plain words, for the status line and screen readers.</summary>
    [Reactive]
    public partial string Status { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the open document can be edited this way.</summary>
    public bool IsAvailable => _owner.IsLoaded && Editor is not null;

    /// <summary>Gets the page editor of the open document.</summary>
    private IPageContentEditor? Editor => DocumentFeature<IPageContentEditor>.Get(_owner.TryGetDocument());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Points at the object under a point, reading the page first when it has not been read.</summary>
    /// <param name="page">The page, or -1 when the pointer is not over one.</param>
    /// <param name="point">The point in page space.</param>
    public void HoverAt(int page, PagePoint point)
    {
        if (!IsEditing || page < 0)
        {
            Unhover();
            return;
        }

        if (!_pages.TryGetValue(page, out var objects))
        {
            Track(Load(page));
            Unhover();
            return;
        }

        var found = PageObjectHits.Find(objects, point);
        if (found < 0)
        {
            Unhover();
            return;
        }

        // The pointer moves often, so the same object under it is left alone rather than picked again.
        if (Hovered is { } current && current.Page == page && current.Object.Index == objects[found].Index)
        {
            return;
        }

        Hovered = new(page, objects[found]);
        MarksVersion++;
    }

    /// <summary>Picks the object under a point.</summary>
    /// <param name="page">The page pressed.</param>
    /// <param name="point">The point pressed in page space.</param>
    /// <param name="doubleClick">Whether the press was a double-click, which also starts retyping a text object.</param>
    /// <returns>A cold signal that ends once the page has been read and the pick made.</returns>
    public IObservable<RxVoid> Press(int page, PagePoint point, bool doubleClick)
    {
        if (!IsEditing || page < 0)
        {
            return Signal.Return(RxVoid.Default);
        }

        CommitText();
        return Load(page).Select(_ => Pressed(page, point, doubleClick));
    }

    /// <summary>Starts work that has no one to wait for it, and ends it when the view model does.</summary>
    /// <param name="work">The work.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Track(IObservable<RxVoid> work) =>
        _subscriptions.Add(work.SubscribeSafe(static _ => { }, ErrorTrace.Write));

    /// <summary>Starts dragging the picked object, when the press is on it.</summary>
    /// <param name="page">The page pressed.</param>
    /// <param name="point">The press in page space.</param>
    /// <returns><see langword="true"/> when a drag began.</returns>
    public bool BeginDrag(int page, PagePoint point)
    {
        if (!IsEditing || TextEdit is not null || !IsOverPicked(page, point))
        {
            return false;
        }

        _dragPage = page;
        _dragStart = point;
        DragDelta = default;
        return true;
    }

    /// <summary>Moves the outline of the dragged object with the pointer.</summary>
    /// <param name="point">The pointer in page space, on the page the drag began on.</param>
    public void DragTo(PagePoint point)
    {
        if (_dragPage < 0)
        {
            return;
        }

        DragDelta = new(point.X - _dragStart.X, point.Y - _dragStart.Y);
        MarksVersion++;
    }

    /// <summary>Ends a drag, writing the move to the page when the pointer went far enough to mean it.</summary>
    /// <returns><see langword="true"/> when a drag was in progress.</returns>
    public bool EndDrag()
    {
        if (_dragPage < 0)
        {
            return false;
        }

        var delta = DragDelta;
        _dragPage = -1;
        DragDelta = default;
        MarksVersion++;
        if (MathF.Abs(delta.X) + MathF.Abs(delta.Y) > DragThreshold)
        {
            _ = MoveSelected(delta.X, delta.Y);
        }

        return true;
    }

    /// <summary>Starts retyping the picked text on the page.</summary>
    /// <returns><see langword="true"/> when a text object is picked.</returns>
    public bool BeginTextEdit()
    {
        if (Selected is not { Object.Kind: PageObjectKind.Text } picked)
        {
            return false;
        }

        EditingText = picked.Object.Text;
        TextEdit = picked;
        MarksVersion++;
        return true;
    }

    /// <summary>Writes the retyped text to the page, when it changed.</summary>
    public void CommitText()
    {
        if (TextEdit is not { } edit)
        {
            return;
        }

        var text = EditingText;
        TextEdit = null;
        MarksVersion++;
        if (text != edit.Object.Text)
        {
            Track(Edit(edit, _ => Editor!.ReplaceTextAsync(edit.Page, edit.Object.Index, text, _lifetime), edit.Object.Bounds, UiText.Get("Changed the text.")));
        }
    }

    /// <summary>Drops the retyped text without writing it.</summary>
    public void CancelText()
    {
        if (TextEdit is null)
        {
            return;
        }

        TextEdit = null;
        MarksVersion++;
    }

    /// <summary>Moves the picked object.</summary>
    /// <param name="deltaX">How far to move right, in page space points.</param>
    /// <param name="deltaY">How far to move down, in page space points.</param>
    /// <returns><see langword="true"/> when an object is picked, so the key that asked is used.</returns>
    public bool MoveSelected(float deltaX, float deltaY)
    {
        if (MoveGroup(deltaX, deltaY))
        {
            return true;
        }

        if (!IsEditing || TextEdit is not null || Selected is not { } picked)
        {
            return false;
        }

        var bounds =picked.Object.Bounds with { Left = picked.Object.Bounds.Left + deltaX, Top = picked.Object.Bounds.Top + deltaY };
        Track(Edit(picked, _ => Editor!.MoveAsync(picked.Page, picked.Object.Index, deltaX, deltaY, _lifetime), bounds, UiText.Get("Moved.")));
        return true;
    }

    /// <summary>Removes the picked object.</summary>
    /// <returns><see langword="true"/> when an object is picked, so the key that asked is used.</returns>
    public bool DeleteSelected()
    {
        if (DeleteGroup())
        {
            return true;
        }

        if (!IsEditing || TextEdit is not null || Selected is not { } picked)
        {
            return false;
        }

        Track(Edit(picked, _ => Editor!.DeleteAsync(picked.Page, picked.Object.Index, _lifetime), default, UiText.Get("Removed.")));
        return true;
    }

    /// <summary>Lets go of the picked object, or leaves Edit mode when nothing is picked.</summary>
    /// <returns><see langword="true"/> when the key that asked is used.</returns>
    public bool Escape()
    {
        if (!IsEditing)
        {
            return false;
        }

        if (TextEdit is not null)
        {
            CancelText();
        }
        else if (Selected is not null)
        {
            Select(null);
        }
        else
        {
            LeaveEditing();
        }

        MarksVersion++;
        return true;
    }

    /// <summary>Names an object for the status line: its kind, and its words when it is text.</summary>
    /// <param name="info">The object.</param>
    /// <returns>The description.</returns>
    private static string Describe(in PageObjectInfo info) => info.Kind switch
    {
        PageObjectKind.Text => UiText.Format("text “{0}”", info.Text),
        PageObjectKind.Image => UiText.Get("picture"),
        PageObjectKind.Drawing => UiText.Get("drawing"),
        _ => UiText.Get("object"),
    };

    /// <summary>Waits for a page read to end, whether it ends before or after the wait begins.</summary>
    /// <param name="finished">What the read ends.</param>
    /// <returns>A signal that gives one value when the read has ended.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<RxVoid> Waiting(Signal<RxVoid> finished) =>
        finished.Count().Select(static _ => RxVoid.Default);

    /// <summary>Tells whether a failure is one an edit reports to the reader rather than a bug.</summary>
    /// <param name="error">The failure.</param>
    /// <returns><see langword="true"/> for a file, parser or argument failure.</returns>
    private static bool IsEditFailure(Exception error) =>
        error is PdfException or InvalidDataException or IOException or InvalidOperationException or ArgumentOutOfRangeException;

    /// <summary>Waits for a library call.</summary>
    /// <param name="call">The library call.</param>
    /// <returns>A task giving the completion value.</returns>
    private static async Task<RxVoid> Wait(Func<ValueTask> call)
    {
        await call().ConfigureAwait(false);
        return RxVoid.Default;
    }

    /// <summary>Turns Edit mode on or off.</summary>
    [ReactiveCommand]
    private void ToggleEditing()
    {
        if (IsEditing)
        {
            CommitText();
            Forget();
            LeaveEditing();
            return;
        }

        if (!IsAvailable)
        {
            _owner.SetNotice(LocalizedMessage.Get(UiText, "This document cannot be edited on the page."));
            return;
        }

        IsEditing = true;
        Status = $"{UiText.Get("Edit mode is on. Click text or a picture on the page to pick it. Double-click text to retype it.")} {VersionNote()}";
        MarksVersion++;
    }

    /// <summary>Says which PDF version the file is and what saving the edits will write, so the PDF 2.0 default is never a surprise.</summary>
    /// <returns>The sentence.</returns>
    private string VersionNote() => _owner.Source.Metadata?.FormatVersion is { Length: > 0 } version && version != Pdf20
        ? UiText.Format("This file is PDF {0}. Saving your changes writes PDF 2.0 when the file allows it.", version)
        : UiText.Get("Saving your changes writes a PDF 2.0 file.");

    /// <summary>Starts retyping the picked text from a menu or the keyboard.</summary>
    [ReactiveCommand]
    private void EditPickedText() => _ = BeginTextEdit();

    /// <summary>Removes the picked object from a menu.</summary>
    [ReactiveCommand]
    private void RemovePicked() => _ = DeleteSelected();

    /// <summary>Clears the object under the pointer, when there is one.</summary>
    private void Unhover()
    {
        if (Hovered is null)
        {
            return;
        }

        Hovered = null;
        MarksVersion++;
    }

    /// <summary>Gets the object at a point from what was read of the page, and starts reading the page when it has not been read.</summary>
    /// <param name="page">The page.</param>
    /// <param name="point">The point in page space.</param>
    /// <returns>The object, or <see langword="null"/>.</returns>
    private PageObjectPick? Pick(int page, PagePoint point)
    {
        if (!_pages.TryGetValue(page, out var objects))
        {
            Track(Load(page));
            return null;
        }

        var found = PageObjectHits.Find(objects, point);
        return found < 0 ? null : new(page, objects[found]);
    }

    /// <summary>Reads what a page is made of, unless that was done already.</summary>
    /// <param name="page">The page.</param>
    /// <returns>A signal that ends when the page is read. A press right after the pointer moved onto the page waits for the read the move started.</returns>
    private IObservable<RxVoid> Load(int page)
    {
        if (_pages.ContainsKey(page) || Editor is not { } editor)
        {
            return Signal.Return(RxVoid.Default);
        }

        if (_loading.TryGetValue(page, out var running))
        {
            return Waiting(running);
        }

        var finished = new Signal<RxVoid>();
        _loading[page] = finished;
        Track(ReadPage(page, editor).Finally(() => FinishLoad(page, finished)));

        // A read that ended at once is already forgotten, and a finished read is never waited for.
        return _loading.ContainsKey(page) ? Waiting(finished) : Signal.Return(RxVoid.Default);
    }

    /// <summary>Tells those waiting for a page that it was read, and forgets the read.</summary>
    /// <param name="page">The page.</param>
    /// <param name="finished">What those waiting for the page listen to.</param>
    private void FinishLoad(int page, Signal<RxVoid> finished)
    {
        _ = _loading.Remove(page);
        finished.OnNext(RxVoid.Default);
        finished.OnCompleted();
    }

    /// <summary>Reads a page's objects on the pool and keeps them until the document's pages change.</summary>
    /// <param name="page">The page.</param>
    /// <param name="editor">The page editor.</param>
    /// <returns>A cold signal that ends when the page is read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> ReadPage(int page, IPageContentEditor editor) =>
        // The page editor is the PDF library's async API, so its call is the one task here.
        Signal.FromAsync(token => editor.ReadObjectsAsync(page, token).AsTask(), _lifetime)
            .SubscribeOn(TaskPoolSequencer.Default)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Select(objects => PageRead(page, objects))
            .Catch<RxVoid, Exception>(ReadFailed);

    /// <summary>Keeps the objects read from a page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="objects">What the page is made of.</param>
    /// <returns>The completion value.</returns>
    private RxVoid PageRead(int page, PageObjectInfo[] objects)
    {
        _pages[page] = objects;
        MarksVersion++;
        return RxVoid.Default;
    }

    /// <summary>Says why a page could not be read, or passes on a failure that is not expected.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A signal that ends for an expected failure, or fails with the same error.</returns>
    private IObservable<RxVoid> ReadFailed(Exception error)
    {
        if (error is OperationCanceledException && _lifetime.IsCancellationRequested)
        {
            return Signal.Return(RxVoid.Default);
        }

        if (!IsEditFailure(error))
        {
            return Signal.Throw<RxVoid>(error);
        }

        Status = UiText.Format("Could not read the page for editing: {0}", error.Message);
        return Signal.Return(RxVoid.Default);
    }

    /// <summary>Makes the pick a press asked for once its page is read.</summary>
    /// <param name="page">The page pressed.</param>
    /// <param name="point">The point pressed in page space.</param>
    /// <param name="doubleClick">Whether the press was a double-click.</param>
    /// <returns>The completion value.</returns>
    private RxVoid Pressed(int page, PagePoint point, bool doubleClick)
    {
        Select(Pick(page, point));
        if (doubleClick)
        {
            _ = BeginTextEdit();
        }

        return RxVoid.Default;
    }

    /// <summary>Picks an object, or nothing, and says what it is.</summary>
    /// <param name="pick">The object.</param>
    private void Select(PageObjectPick? pick)
    {
        Selected = pick;
        Group = [];
        Track(LoadStyle());
        Status = pick is null ? UiText.Get("Nothing picked.") : UiText.Format("Picked {0}.", Describe(pick.Object));
        MarksVersion++;
    }

    /// <summary>Runs one edit, then reads the page again and picks the object where it should now be.</summary>
    /// <param name="picked">The object being changed.</param>
    /// <param name="edit">The change, given where the object should end up.</param>
    /// <param name="expected">Where the object's box should be afterwards.</param>
    /// <param name="done">What to say once the edit is made.</param>
    /// <returns>A cold signal that ends when the page is read again.</returns>
    private IObservable<RxVoid> Edit(PageObjectPick picked, Func<PageRect, ValueTask> edit, PageRect expected, string done) =>
        Editor is null
            ? Signal.Return(RxVoid.Default)
            : Call(() => edit(expected))
                .SwitchMap(_ => ReadAgain(picked, expected))
                .Select(again => Edited(picked, again, done))
                .Catch<RxVoid, Exception>(EditFailed);

    /// <summary>Calls the page editor on the pool and brings the answer back to the interface thread.</summary>
    /// <param name="call">The library call.</param>
    /// <returns>A cold signal that ends once the call is done.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> Call(Func<ValueTask> call) =>
        // The page editor is the PDF library's async API, so its call is the one task here.
        Signal.FromAsync(_ => Wait(call), _lifetime)
            .SubscribeOn(TaskPoolSequencer.Default)
            .ObserveOn(RxSchedulers.MainThreadScheduler);

    /// <summary>Reads a page again from the library, forgetting what was read before.</summary>
    /// <param name="page">The page.</param>
    /// <returns>A cold signal that ends when the page is read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> Reread(int page) =>
        Signal.Defer(() =>
        {
            _ = _pages.Remove(page);
            return Load(page);
        });

    /// <summary>Picks the edited object where it should now be and says what was done.</summary>
    /// <param name="picked">The object that was changed.</param>
    /// <param name="again">The index of the object in the page's objects, or -1 when it is gone.</param>
    /// <param name="done">What to say.</param>
    /// <returns>The completion value.</returns>
    private RxVoid Edited(PageObjectPick picked, int again, string done)
    {
        Selected = again < 0 ? null : new(picked.Page, _pages[picked.Page][again]);
        Group = [];
        Track(LoadStyle());
        Hovered = null;
        _owner.ReportPageChange(picked.Page);
        Status = done;
        MarksVersion++;
        return RxVoid.Default;
    }

    /// <summary>Says why an edit failed, or passes on a failure that is not expected.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>A signal that ends for an expected failure, or fails with the same error.</returns>
    private IObservable<RxVoid> EditFailed(Exception error)
    {
        if (error is OperationCanceledException && _lifetime.IsCancellationRequested)
        {
            return Signal.Return(RxVoid.Default);
        }

        if (!IsEditFailure(error))
        {
            return Signal.Throw<RxVoid>(error);
        }

        _owner.SetNotice(LocalizedMessage.Literal($"{UiText.Get("Could not edit the page.")} {error.Message}"));
        return Signal.Return(RxVoid.Default);
    }

    /// <summary>
    /// Reads a page again after an edit and finds the edited object in it. A read that fails, or that does not list an object which should still be
    /// there, is tried again, since the page has been seen right on a later read.
    /// </summary>
    /// <param name="picked">The object that was edited.</param>
    /// <param name="expected">Where the object's box should be now, or an empty box when it is meant to be gone.</param>
    /// <returns>A cold signal giving the index of the object in the page's objects, or -1 when it is gone or the page could not be read.</returns>
    private IObservable<int> ReadAgain(PageObjectPick picked, PageRect expected)
    {
        var state = new RereadState();
        return LazySequence.Concat(Rereads(picked, expected, state))
            .TakeWhile(_ => !state.Finished)
            .Count()
            .Select(_ => Resolved(picked.Page, state));
    }

    /// <summary>Makes each attempt to read a page again, one at a time as the caller moves along.</summary>
    /// <param name="picked">The object that was edited.</param>
    /// <param name="expected">Where the object's box should be now, or an empty box when it is meant to be gone.</param>
    /// <param name="state">What the attempts found.</param>
    /// <returns>A cold signal per attempt.</returns>
    private IEnumerable<IObservable<RxVoid>> Rereads(PageObjectPick picked, PageRect expected, RereadState state)
    {
        for (var attempt = 0; attempt < ReadAttempts; attempt++)
        {
            yield return Reread(picked.Page).Select(_ => Attempted(picked, expected, state));
        }
    }

    /// <summary>Looks for the edited object in the page that was read again.</summary>
    /// <param name="picked">The object that was edited.</param>
    /// <param name="expected">Where the object's box should be now, or an empty box when it is meant to be gone.</param>
    /// <param name="state">What the attempts found.</param>
    /// <returns>The completion value.</returns>
    private RxVoid Attempted(PageObjectPick picked, PageRect expected, RereadState state)
    {
        if (!_pages.TryGetValue(picked.Page, out var objects))
        {
            return RxVoid.Default;
        }

        if (expected.Width <= 0)
        {
            // An object meant to be gone is not looked for.
            state.Gone = true;
            state.Finished = true;
            return RxVoid.Default;
        }

        state.Again = PageObjectHits.FindAgain(objects, picked.Object, expected);
        state.Finished = state.Again >= 0;
        return RxVoid.Default;
    }

    /// <summary>Says when the page could not be read again, and gives what the attempts found.</summary>
    /// <param name="page">The page.</param>
    /// <param name="state">What the attempts found.</param>
    /// <returns>The index of the object in the page's objects, or -1.</returns>
    private int Resolved(int page, RereadState state)
    {
        if (state.Gone)
        {
            return -1;
        }

        if (!_pages.ContainsKey(page))
        {
            // The failed read said why in Status; the edit's own "done" text would replace it, so it is shown as a notice too.
            _owner.SetNotice(LocalizedMessage.Literal(Status));
        }

        return state.Again;
    }

    /// <summary>Turns Edit mode off. The changes stay in the document; saving writes them as a PDF 2.0 file.</summary>
    private void LeaveEditing()
    {
        IsEditing = false;
        Status = UiText.Get("Edit mode is off.");
    }

    /// <summary>Forgets what was read and picked.</summary>
    private void Forget()
    {
        _pages.Clear();
        TextEdit = null;
        Selected = null;
        Group = [];
        Style = default;
        Hovered = null;
        MarksVersion++;
    }
}
