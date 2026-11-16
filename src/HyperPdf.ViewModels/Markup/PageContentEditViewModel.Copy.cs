// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Geometry;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>The part of Edit mode that copies, cuts, pastes and duplicates the picked object and brings it to the front.</summary>
public sealed partial class PageContentEditViewModel
{
    /// <summary>How far down and right a copy placed on its own page lies from the original, in points, so it can be seen and picked.</summary>
    private const float CopyStep = 12F;

    /// <summary>Gets the object that was copied, or <see langword="null"/> when nothing was.</summary>
    [Reactive]
    public partial PageObjectPick? Clipboard { get; private set; }

    /// <summary>Remembers the picked object, to paste it later.</summary>
    /// <returns><see langword="true"/> when one object is picked.</returns>
    public bool CopyPicked()
    {
        if (!CanShape(out var picked))
        {
            return false;
        }

        Clipboard = picked;
        Status = UiText.Get("Copied.");
        return true;
    }

    /// <summary>Remembers the picked object and removes it from the page.</summary>
    /// <returns><see langword="true"/> when one object is picked.</returns>
    public bool CutPicked() => CopyPicked() && DeleteSelected();

    /// <summary>Paints another copy of the picked object, a little down and right of it.</summary>
    /// <returns><see langword="true"/> when one object is picked.</returns>
    public bool DuplicatePicked()
    {
        if (!CanShape(out var picked))
        {
            return false;
        }

        Track(CopyOnto(picked, picked.Page, new(CopyStep, CopyStep), UiText.Get("Duplicated.")));
        return true;
    }

    /// <summary>Paints the copied object on a page, where it was on its page; on the page it came from it is put a little down and right.</summary>
    /// <param name="page">The page to paste on.</param>
    /// <returns><see langword="true"/> when something was copied.</returns>
    public bool PasteCopied(int page)
    {
        if (!IsEditing || TextEdit is not null || Clipboard is not { } copied || page < 0)
        {
            return false;
        }

        var offset = page == copied.Page ? new PagePoint(CopyStep, CopyStep) : default;
        Track(CopyOnto(copied, page, offset, UiText.Get("Pasted.")));
        return true;
    }

    /// <summary>Moves the picked object in front of everything else on its page.</summary>
    /// <returns><see langword="true"/> when one object is picked.</returns>
    public bool BringPickedToFront()
    {
        if (!CanShape(out var picked))
        {
            return false;
        }

        // The object is written again at the end of the page, so it is found again by what it is and where it is.
        Track(Edit(picked, _ => Editor!.BringToFront(picked.Page, picked.Object.Index), picked.Object.Bounds, UiText.Get("Brought to the front.")));
        return true;
    }

    /// <summary>Moves the picked object one step forward or back.</summary>
    /// <param name="backward">True to move it one step back, false to move it one step forward.</param>
    /// <returns><see langword="true"/> when one object is picked.</returns>
    public bool StepPicked(bool backward)
    {
        if (!CanShape(out var picked))
        {
            return false;
        }

        // The object is written again at the end of the page, so it is found again by what it is and where it is.
        if (backward)
        {
            Track(Edit(picked, _ => Editor!.SendBackward(picked.Page, picked.Object.Index), picked.Object.Bounds, UiText.Get("Sent back one step.")));
        }
        else
        {
            Track(Edit(picked, _ => Editor!.BringForward(picked.Page, picked.Object.Index), picked.Object.Bounds, UiText.Get("Brought forward one step.")));
        }

        return true;
    }

    /// <summary>Moves the picked object behind everything else on its page.</summary>
    /// <returns><see langword="true"/> when one object is picked.</returns>
    public bool SendPickedToBack()
    {
        if (!CanShape(out var picked))
        {
            return false;
        }

        Track(Edit(picked, _ => Editor!.SendToBack(picked.Page, picked.Object.Index), picked.Object.Bounds, UiText.Get("Sent to the back.")));
        return true;
    }

    /// <summary>Copies the picked object to the clipboard, from a menu.</summary>
    [ReactiveCommand]
    private void CopyPickedObject() => _ = CopyPicked();

    /// <summary>Cuts the picked object to the clipboard, from a menu.</summary>
    [ReactiveCommand]
    private void CutPickedObject() => _ = CutPicked();

    /// <summary>Pastes the copied object on the page shown, from a menu.</summary>
    [ReactiveCommand]
    private void PasteObject() => _ = PasteCopied(_owner.CurrentPageIndex);

    /// <summary>Duplicates the picked object, from a menu.</summary>
    [ReactiveCommand]
    private void DuplicateObject() => _ = DuplicatePicked();

    /// <summary>Brings the picked object to the front, from a menu.</summary>
    [ReactiveCommand]
    private void BringToFront() => _ = BringPickedToFront();

    /// <summary>Brings the picked object forward one step, from a menu.</summary>
    [ReactiveCommand]
    private void BringForward() => _ = StepPicked(backward: false);

    /// <summary>Sends the picked object back one step, from a menu.</summary>
    [ReactiveCommand]
    private void SendBackward() => _ = StepPicked(backward: true);

    /// <summary>Sends the picked object to the back, from a menu.</summary>
    [ReactiveCommand]
    private void SendToBack() => _ = SendPickedToBack();

    /// <summary>Finds an object again on its page. It may have moved or the page may have been edited since it was picked or copied.</summary>
    /// <param name="source">The object as it was.</param>
    /// <returns>A cold signal giving the object's number now, or -1 when it is gone.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<int> FindSource(PageObjectPick source) =>
        Reread(source.Page).Select(_ => SourceIndex(source));

    /// <summary>Gets the number an object has on its page as it was read again.</summary>
    /// <param name="source">The object as it was.</param>
    /// <returns>The object's number now, or -1 when it is gone.</returns>
    private int SourceIndex(PageObjectPick source)
    {
        if (!_pages.TryGetValue(source.Page, out var objects))
        {
            return -1;
        }

        var at = PageObjectHits.FindAgain(objects, source.Object, source.Object.Bounds);
        return at < 0 ? -1 : objects[at].Index;
    }

    /// <summary>Paints a copy of an object on a page, then reads the page again and picks the newest object, which is the copy.</summary>
    /// <param name="source">The object to copy, as it was when it was picked or copied.</param>
    /// <param name="page">The page the copy goes on.</param>
    /// <param name="offset">How far from the original's place the copy goes, in page space.</param>
    /// <param name="done">What to say once the copy is made.</param>
    /// <returns>A cold signal that ends when the page is read again.</returns>
    private IObservable<RxVoid> CopyOnto(PageObjectPick source, int page, PagePoint offset, string done) =>
        Editor is not { } editor
            ? Signal.Return(RxVoid.Default)
            : FindSource(source)
                .SwitchMap(index => index < 0
                    ? Signal.Return(RxVoid.Default)
                    : Call(editor.Copy(source.Page, index, page, offset))
                        .SwitchMap(_ => Reread(page))
                        .Select(_ => Pasted(page, done)))
                .Catch<RxVoid, Exception>(EditFailed);

    /// <summary>Picks the newest object of a page, which is the copy just painted, and says what was done.</summary>
    /// <param name="page">The page the copy went on.</param>
    /// <param name="done">What to say.</param>
    /// <returns>The completion value.</returns>
    private RxVoid Pasted(int page, string done)
    {
        Select(_pages.TryGetValue(page, out var now) && now.Length > 0 ? new PageObjectPick(page, now[^1]) : null);
        Hovered = null;
        _owner.ReportPageChange(page);
        Status = done;
        return RxVoid.Default;
    }
}
