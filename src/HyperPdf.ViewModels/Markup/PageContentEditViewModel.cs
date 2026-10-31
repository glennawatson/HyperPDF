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

    /// <summary>The version text of a PDF 2.0 file.</summary>
    private const string Pdf20 = "2.0";

    /// <summary>The tab holding the document.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Cancelled when the tab closes.</summary>
    private readonly CancellationToken _lifetime;

    /// <summary>Owns the lifetime source and the subscriptions.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>The objects read for each page, until the document's pages change.</summary>
    private readonly Dictionary<int, PageObjectInfo[]> _pages = [];

    /// <summary>The pages being read now, so a page is read once while the pointer moves over it.</summary>
    private readonly Dictionary<int, Task> _loading = [];

    /// <summary>The page a drag began on, or -1 when none is in progress.</summary>
    private int _dragPage = -1;

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
        _subscriptions.Add(owner.WhenChanged(static tab => tab.LayoutVersion).Skip(1).SubscribeSafe(_ => Forget(), ErrorTrace.Write));
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
            _ = LoadAsync(page);
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
    /// <returns>A task completing once the page has been read and the pick made.</returns>
    public async Task PressAsync(int page, PagePoint point, bool doubleClick)
    {
        if (!IsEditing || page < 0)
        {
            return;
        }

        CommitText();
        await LoadAsync(page).ConfigureAwait(true);
        Select(Pick(page, point));
        if (doubleClick)
        {
            _ = BeginTextEdit();
        }
    }

    /// <summary>Starts dragging the picked object, when the press is on it.</summary>
    /// <param name="page">The page pressed.</param>
    /// <param name="point">The press in page space.</param>
    /// <returns><see langword="true"/> when a drag began.</returns>
    public bool BeginDrag(int page, PagePoint point)
    {
        if (!IsEditing || TextEdit is not null || Selected is not { } picked || picked.Page != page || !picked.Object.Bounds.Contains(point))
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
            _ = EditAsync(edit, _ => Editor!.ReplaceTextAsync(edit.Page, edit.Object.Index, text, _lifetime).AsTask(), edit.Object.Bounds, UiText.Get("Changed the text."));
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
        if (!IsEditing || TextEdit is not null || Selected is not { } picked)
        {
            return false;
        }

        var bounds = picked.Object.Bounds with { Left = picked.Object.Bounds.Left + deltaX, Top = picked.Object.Bounds.Top + deltaY };
        _ = EditAsync(picked, _ => Editor!.MoveAsync(picked.Page, picked.Object.Index, deltaX, deltaY, _lifetime).AsTask(), bounds, UiText.Get("Moved."));
        return true;
    }

    /// <summary>Removes the picked object.</summary>
    /// <returns><see langword="true"/> when an object is picked, so the key that asked is used.</returns>
    public bool DeleteSelected()
    {
        if (!IsEditing || TextEdit is not null || Selected is not { } picked)
        {
            return false;
        }

        _ = EditAsync(picked, _ => Editor!.DeleteAsync(picked.Page, picked.Object.Index, _lifetime).AsTask(), default, UiText.Get("Removed."));
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
            _ = LoadAsync(page);
            return null;
        }

        var found = PageObjectHits.Find(objects, point);
        return found < 0 ? null : new(page, objects[found]);
    }

    /// <summary>Reads what a page is made of, unless that was done already.</summary>
    /// <param name="page">The page.</param>
    /// <returns>A task completing when the page is read.</returns>
    private Task LoadAsync(int page)
    {
        if (_pages.ContainsKey(page) || Editor is not { } editor)
        {
            return Task.CompletedTask;
        }

        // A press right after the pointer moved onto the page waits for the read the move started.
        if (_loading.TryGetValue(page, out var running))
        {
            return running;
        }

        var read = ReadPageAsync(page, editor);
        _loading[page] = read;
        return read;
    }

    /// <summary>Reads a page's objects and keeps them until the document's pages change.</summary>
    /// <param name="page">The page.</param>
    /// <param name="editor">The page editor.</param>
    /// <returns>A task completing when the page is read.</returns>
    private async Task ReadPageAsync(int page, IPageContentEditor editor)
    {
        try
        {
            _pages[page] = await Task.Run(() => editor.ReadObjectsAsync(page, _lifetime).AsTask(), _lifetime).ConfigureAwait(true);
            MarksVersion++;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is PdfException or InvalidDataException or IOException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            Status = UiText.Format("Could not read the page for editing: {0}", ex.Message);
        }
        finally
        {
            _ = _loading.Remove(page);
        }
    }

    /// <summary>Picks an object, or nothing, and says what it is.</summary>
    /// <param name="pick">The object.</param>
    private void Select(PageObjectPick? pick)
    {
        Selected = pick;
        Status = pick is null ? UiText.Get("Nothing picked.") : UiText.Format("Picked {0}.", Describe(pick.Object));
        MarksVersion++;
    }

    /// <summary>Runs one edit, then reads the page again and picks the object where it should now be.</summary>
    /// <param name="picked">The object being changed.</param>
    /// <param name="edit">The change, given where the object should end up.</param>
    /// <param name="expected">Where the object's box should be afterwards.</param>
    /// <param name="done">What to say once the edit is made.</param>
    /// <returns>A task completing when the page is read again.</returns>
    private async Task EditAsync(PageObjectPick picked, Func<PageRect, Task> edit, PageRect expected, string done)
    {
        if (Editor is null)
        {
            return;
        }

        try
        {
            await Task.Run(() => edit(expected), _lifetime).ConfigureAwait(true);
            _ = _pages.Remove(picked.Page);
            await LoadAsync(picked.Page).ConfigureAwait(true);
            var again = expected.Width > 0 && _pages.TryGetValue(picked.Page, out var objects) ? PageObjectHits.FindAgain(objects, picked.Object, expected) : -1;
            Selected = again < 0 ? null : new(picked.Page, _pages[picked.Page][again]);
            Hovered = null;
            _owner.ReportPageChange(picked.Page);
            Status = done;
            MarksVersion++;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is PdfException or InvalidDataException or IOException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            _owner.SetNotice(LocalizedMessage.Literal($"{UiText.Get("Could not edit the page.")} {ex.Message}"));
        }
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
        Hovered = null;
        MarksVersion++;
    }
}
