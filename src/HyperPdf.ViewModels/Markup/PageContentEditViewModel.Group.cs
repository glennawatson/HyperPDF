// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdfLibrary;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>The part of Edit mode that works on several picked objects at once: picking more, moving, removing, lining up and spreading them.</summary>
public sealed partial class PageContentEditViewModel
{
    /// <summary>The fewest objects that can be spread evenly.</summary>
    private const int SpreadMinimum = 3;

    /// <summary>Gets the objects picked besides <see cref="Selected"/>, all on the same page as it.</summary>
    [Reactive]
    public partial PageObjectPick[] Group { get; private set; } = [];

    /// <summary>Gets how many objects are picked.</summary>
    public int PickedCount => Selected is null ? 0 : Group.Length + 1;

    /// <summary>Picks an object or takes it out of the pick, as a press with Shift or Control does.</summary>
    /// <param name="page">The page pressed.</param>
    /// <param name="point">The point pressed in page space.</param>
    /// <returns>A task completing once the page has been read and the pick changed.</returns>
    public async Task ExtendPickAsync(int page, PagePoint point)
    {
        if (!IsEditing || page < 0)
        {
            return;
        }

        CommitText();
        await LoadAsync(page).ConfigureAwait(true);
        if (Selected is not { } primary || primary.Page != page || Pick(page, point) is not { } hit)
        {
            return;
        }

        Toggle(primary, hit);
        _ = LoadStyleAsync();
        Status = UiText.Format("Picked {0} objects.", PickedCount);
        MarksVersion++;
    }

    /// <summary>Picks every object on a page.</summary>
    /// <param name="page">The page.</param>
    /// <returns>A task completing once the page has been read and the objects picked.</returns>
    public async Task PickEveryObjectAsync(int page)
    {
        if (!IsEditing || page < 0)
        {
            return;
        }

        CommitText();
        await LoadAsync(page).ConfigureAwait(true);
        if (!_pages.TryGetValue(page, out var objects) || objects.Length == 0)
        {
            return;
        }

        var all = new PageObjectPick[objects.Length];
        for (var i = 0; i < objects.Length; i++)
        {
            all[i] = new(page, objects[i]);
        }

        Selected = all[0];
        Group = all.Length > 1 ? all[1..] : [];
        _ = LoadStyleAsync();
        Status = UiText.Format("Picked {0} objects.", PickedCount);
        MarksVersion++;
    }

    /// <summary>Gets the box around every picked object.</summary>
    /// <returns>The box in page space, or an empty box when nothing is picked.</returns>
    public PageRect GroupBounds()
    {
        if (Selected is not { } primary)
        {
            return default;
        }

        var boxes = new PageRect[Group.Length + 1];
        boxes[0] = primary.Object.Bounds;
        for (var i = 0; i < Group.Length; i++)
        {
            boxes[i + 1] = Group[i].Object.Bounds;
        }

        return PageObjectLayout.Union(boxes);
    }

    /// <summary>Tells whether a point is on any of the picked objects.</summary>
    /// <param name="page">The page.</param>
    /// <param name="point">The point in page space.</param>
    /// <returns><see langword="true"/> when a picked object is under the point.</returns>
    public bool IsOverPicked(int page, PagePoint point)
    {
        if (Selected is not { } primary || primary.Page != page)
        {
            return false;
        }

        if (primary.Object.Bounds.Contains(point))
        {
            return true;
        }

        for (var i = 0; i < Group.Length; i++)
        {
            if (Group[i].Object.Bounds.Contains(point))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Lines the picked objects up.</summary>
    /// <param name="alignment">The edge or middle to line up on.</param>
    /// <returns><see langword="true"/> when more than one object is picked, so the change was asked for.</returns>
    public bool AlignPicked(PageObjectAlignment alignment)
    {
        if (!CanGroup(out var picks))
        {
            return false;
        }

        var boxes = BoxesOf(picks);
        var moves = new PagePoint[picks.Length];
        PageObjectLayout.Align(boxes, alignment, moves);
        MovePicks(picks, boxes, moves, UiText.Get("Lined up."));
        return true;
    }

    /// <summary>Spreads the picked objects so the gaps between them are equal.</summary>
    /// <param name="across">True to spread from left to right, false to spread from top to bottom.</param>
    /// <returns><see langword="true"/> when at least three objects are picked.</returns>
    public bool SpreadPicked(bool across)
    {
        if (!CanGroup(out var picks) || picks.Length < SpreadMinimum)
        {
            return false;
        }

        var boxes = BoxesOf(picks);
        var moves = new PagePoint[picks.Length];
        PageObjectLayout.Spread(boxes, across, moves);
        MovePicks(picks, boxes, moves, UiText.Get("Spread evenly."));
        return true;
    }

    /// <summary>Tells whether two picks are the same object on the same page.</summary>
    /// <param name="first">The first pick.</param>
    /// <param name="second">The second pick.</param>
    /// <returns><see langword="true"/> when both name the same object.</returns>
    private static bool IsSame(PageObjectPick first, PageObjectPick second) => first.Page == second.Page && first.Object.Index == second.Object.Index;

    /// <summary>Finds a pick among others.</summary>
    /// <param name="picks">The picks.</param>
    /// <param name="hit">The pick to find.</param>
    /// <returns>Its place, or -1.</returns>
    private static int IndexOf(PageObjectPick[] picks, PageObjectPick hit)
    {
        for (var i = 0; i < picks.Length; i++)
        {
            if (IsSame(picks[i], hit))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Makes a copy of the picks without one.</summary>
    /// <param name="picks">The picks.</param>
    /// <param name="at">The place of the pick to leave out.</param>
    /// <returns>The remaining picks.</returns>
    private static PageObjectPick[] Remove(PageObjectPick[] picks, int at)
    {
        var rest = new PageObjectPick[picks.Length - 1];
        Array.Copy(picks, 0, rest, 0, at);
        Array.Copy(picks, at + 1, rest, at, picks.Length - at - 1);
        return rest;
    }

    /// <summary>Gets the boxes of picks.</summary>
    /// <param name="picks">The picks.</param>
    /// <returns>Their boxes, in the same order.</returns>
    private static PageRect[] BoxesOf(PageObjectPick[] picks)
    {
        var boxes = new PageRect[picks.Length];
        for (var i = 0; i < picks.Length; i++)
        {
            boxes[i] = picks[i].Object.Bounds;
        }

        return boxes;
    }

    /// <summary>Lines the picked objects up on their left edges.</summary>
    [ReactiveCommand]
    private void AlignLeft() => _ = AlignPicked(PageObjectAlignment.Left);

    /// <summary>Lines the picked objects up on their middles, left to right.</summary>
    [ReactiveCommand]
    private void AlignCentre() => _ = AlignPicked(PageObjectAlignment.Centre);

    /// <summary>Lines the picked objects up on their right edges.</summary>
    [ReactiveCommand]
    private void AlignRight() => _ = AlignPicked(PageObjectAlignment.Right);

    /// <summary>Lines the picked objects up on their top edges.</summary>
    [ReactiveCommand]
    private void AlignTop() => _ = AlignPicked(PageObjectAlignment.Top);

    /// <summary>Lines the picked objects up on their middles, top to bottom.</summary>
    [ReactiveCommand]
    private void AlignMiddle() => _ = AlignPicked(PageObjectAlignment.Middle);

    /// <summary>Lines the picked objects up on their bottom edges.</summary>
    [ReactiveCommand]
    private void AlignBottom() => _ = AlignPicked(PageObjectAlignment.Bottom);

    /// <summary>Spreads the picked objects evenly from left to right.</summary>
    [ReactiveCommand]
    private void SpreadAcross() => _ = SpreadPicked(across: true);

    /// <summary>Spreads the picked objects evenly from top to bottom.</summary>
    [ReactiveCommand]
    private void SpreadDown() => _ = SpreadPicked(across: false);

    /// <summary>Picks every object of the page shown.</summary>
    [ReactiveCommand]
    private void PickEveryObject() => _ = PickEveryObjectAsync(_owner.CurrentPageIndex);

    /// <summary>Adds an object to the pick, or takes it out when it is picked already.</summary>
    /// <param name="primary">The main pick.</param>
    /// <param name="hit">The object pressed.</param>
    private void Toggle(PageObjectPick primary, PageObjectPick hit)
    {
        if (IsSame(hit, primary))
        {
            // Taking away the main pick hands it to the next one.
            Selected = Group.Length > 0 ? Group[0] : null;
            Group = Group.Length > 1 ? Group[1..] : [];
            return;
        }

        var at = IndexOf(Group, hit);
        Group = at >= 0 ? Remove(Group, at) : [.. Group, hit];
    }

    /// <summary>Moves several picked objects by the same distance, as one step.</summary>
    /// <param name="deltaX">How far to move right, in page space points.</param>
    /// <param name="deltaY">How far to move down, in page space points.</param>
    /// <returns><see langword="true"/> when several objects are picked.</returns>
    private bool MoveGroup(float deltaX, float deltaY)
    {
        if (!CanGroup(out var picks))
        {
            return false;
        }

        var boxes = BoxesOf(picks);
        var moves = new PagePoint[picks.Length];
        for (var i = 0; i < moves.Length; i++)
        {
            moves[i] = new(deltaX, deltaY);
        }

        MovePicks(picks, boxes, moves, UiText.Get("Moved."));
        return true;
    }

    /// <summary>Removes several picked objects as one step.</summary>
    /// <returns><see langword="true"/> when several objects are picked.</returns>
    private bool DeleteGroup()
    {
        if (!CanGroup(out var picks))
        {
            return false;
        }

        var indexes = new int[picks.Length];
        for (var i = 0; i < picks.Length; i++)
        {
            indexes[i] = picks[i].Object.Index;
        }

        var gone = new PageRect[picks.Length];
        _ = EditGroupAsync(picks, gone, () => Editor!.DeleteManyAsync(picks[0].Page, indexes, _lifetime).AsTask(), UiText.Get("Removed."));
        return true;
    }

    /// <summary>Tells whether several objects are picked and Edit mode lets them be changed.</summary>
    /// <param name="picks">Receives the picked objects, the main pick first.</param>
    /// <returns><see langword="true"/> when more than one object is picked.</returns>
    private bool CanGroup(out PageObjectPick[] picks)
    {
        if (!IsEditing || TextEdit is not null || Selected is not { } primary || Group.Length == 0)
        {
            picks = [];
            return false;
        }

        picks = [primary, .. Group];
        return true;
    }

    /// <summary>Writes a move of each picked object to the page.</summary>
    /// <param name="picks">The objects.</param>
    /// <param name="boxes">Their boxes now.</param>
    /// <param name="moves">How far each moves.</param>
    /// <param name="done">What to say once it is made.</param>
    private void MovePicks(PageObjectPick[] picks, PageRect[] boxes, PagePoint[] moves, string done)
    {
        var changes = new PageObjectChange[picks.Length];
        var expected = new PageRect[picks.Length];
        for (var i = 0; i < picks.Length; i++)
        {
            changes[i] = new(picks[i].Object.Index, Matrix3x2.CreateTranslation(moves[i].X, moves[i].Y));
            expected[i] = boxes[i] with { Left = boxes[i].Left + moves[i].X, Top = boxes[i].Top + moves[i].Y };
        }

        _ = EditGroupAsync(picks, expected, () => Editor!.TransformManyAsync(picks[0].Page, changes, _lifetime).AsTask(), done);
    }

    /// <summary>Writes the same change of shape to each picked object.</summary>
    /// <param name="picks">The objects.</param>
    /// <param name="change">The change in page space, the same for every object so the group keeps its arrangement.</param>
    /// <param name="done">What to say once it is made.</param>
    private void ShapePicks(PageObjectPick[] picks, Matrix3x2 change, string done)
    {
        var changes = new PageObjectChange[picks.Length];
        var expected = new PageRect[picks.Length];
        for (var i = 0; i < picks.Length; i++)
        {
            changes[i] = new(picks[i].Object.Index, change);
            expected[i] = PageObjectHandles.Carry(picks[i].Object.Bounds, change);
        }

        _ = EditGroupAsync(picks, expected, () => Editor!.TransformManyAsync(picks[0].Page, changes, _lifetime).AsTask(), done);
    }

    /// <summary>Writes a change of colour to each picked object.</summary>
    /// <param name="picks">The objects.</param>
    /// <param name="colors">What to change.</param>
    /// <param name="done">What to say once it is made.</param>
    private void PaintPicks(PageObjectPick[] picks, PageObjectColors colors, string done)
    {
        var indexes = new int[picks.Length];
        var expected = BoxesOf(picks);
        for (var i = 0; i < picks.Length; i++)
        {
            indexes[i] = picks[i].Object.Index;
        }

        _ = EditGroupAsync(picks, expected, () => Editor!.SetColorsManyAsync(picks[0].Page, indexes, colors, _lifetime).AsTask(), done);
    }

    /// <summary>Writes a change of how objects are drawn to each picked object.</summary>
    /// <param name="picks">The objects.</param>
    /// <param name="look">What to change.</param>
    /// <param name="done">What to say once it is made.</param>
    private void LookPicks(PageObjectPick[] picks, PageObjectLook look, string done)
    {
        var indexes = new int[picks.Length];
        var expected = BoxesOf(picks);
        for (var i = 0; i < picks.Length; i++)
        {
            indexes[i] = picks[i].Object.Index;
        }

        _ = EditGroupAsync(picks, expected, () => Editor!.SetLookManyAsync(picks[0].Page, indexes, look, _lifetime).AsTask(), done);
    }

    /// <summary>Finds objects again after an edit.</summary>
    /// <param name="page">The page.</param>
    /// <param name="picks">The objects that were changed.</param>
    /// <param name="expected">Where each object's box should be now, or an empty box for an object that is meant to be gone.</param>
    /// <returns>The objects found, in the order of <paramref name="picks"/>.</returns>
    private List<PageObjectPick> FindAgainAll(int page, PageObjectPick[] picks, PageRect[] expected)
    {
        var found = new List<PageObjectPick>(picks.Length);
        if (!_pages.TryGetValue(page, out var objects))
        {
            return found;
        }

        for (var i = 0; i < picks.Length; i++)
        {
            var at = expected[i].Width > 0 ? PageObjectHits.FindAgain(objects, picks[i].Object, expected[i]) : -1;
            if (at >= 0)
            {
                found.Add(new(page, objects[at]));
            }
        }

        return found;
    }

    /// <summary>Runs one edit of several objects, then reads the page again and picks them where they should now be.</summary>
    /// <param name="picks">The objects being changed.</param>
    /// <param name="expected">Where each object's box should be afterwards, or an empty box for an object that is meant to be gone.</param>
    /// <param name="edit">The change.</param>
    /// <param name="done">What to say once the edit is made.</param>
    /// <returns>A task completing when the page is read again.</returns>
    private async Task EditGroupAsync(PageObjectPick[] picks, PageRect[] expected, Func<Task> edit, string done)
    {
        if (Editor is null)
        {
            return;
        }

        try
        {
            await Task.Run(edit, _lifetime).ConfigureAwait(true);
            var page = picks[0].Page;
            _ = _pages.Remove(page);
            await LoadAsync(page).ConfigureAwait(true);
            var found = FindAgainAll(page, picks, expected);
            Selected = found.Count > 0 ? found[0] : null;
            Group = found.Count > 1 ? found.GetRange(1, found.Count - 1).ToArray() : [];
            _ = LoadStyleAsync();
            Hovered = null;
            _owner.ReportPageChange(page);
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
}
