// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Markup;

/// <summary>Where the resize handles of a picked object lie, and what dragging one does to the box.</summary>
public static class PageObjectHandles
{
    /// <summary>The smallest a box may be dragged to, in points, so it cannot be turned inside out or lost.</summary>
    private const float MinimumSize = 2F;

    /// <summary>Half of a length.</summary>
    private const float Half = 0.5F;

    /// <summary>Gets the point of a handle on a box.</summary>
    /// <param name="bounds">The box.</param>
    /// <param name="handle">The handle.</param>
    /// <returns>The handle's centre in page space.</returns>
    public static PagePoint PositionOf(PageRect bounds, PageObjectHandle handle)
    {
        var middleX = bounds.Left + (bounds.Width * Half);
        var middleY = bounds.Top + (bounds.Height * Half);
        return handle switch
        {
            PageObjectHandle.TopLeft => new(bounds.Left, bounds.Top),
            PageObjectHandle.Top => new(middleX, bounds.Top),
            PageObjectHandle.TopRight => new(bounds.Right, bounds.Top),
            PageObjectHandle.Right => new(bounds.Right, middleY),
            PageObjectHandle.BottomRight => new(bounds.Right, bounds.Bottom),
            PageObjectHandle.Bottom => new(middleX, bounds.Bottom),
            PageObjectHandle.BottomLeft => new(bounds.Left, bounds.Bottom),
            PageObjectHandle.Left => new(bounds.Left, middleY),
            _ => new(middleX, middleY),
        };
    }

    /// <summary>Finds the handle under a point.</summary>
    /// <param name="bounds">The box of the picked object.</param>
    /// <param name="point">The point in page space.</param>
    /// <param name="reach">How far from a handle's centre still counts as on it, in points.</param>
    /// <returns>The handle, or <see cref="PageObjectHandle.None"/>.</returns>
    public static PageObjectHandle HitTest(PageRect bounds, PagePoint point, float reach)
    {
        if (point.X < bounds.Left - reach || point.X > bounds.Right + reach || point.Y < bounds.Top - reach || point.Y > bounds.Bottom + reach)
        {
            return PageObjectHandle.None;
        }

        for (var handle = PageObjectHandle.TopLeft; handle <= PageObjectHandle.Left; handle++)
        {
            var centre = PositionOf(bounds, handle);
            if (MathF.Abs(point.X - centre.X) <= reach && MathF.Abs(point.Y - centre.Y) <= reach)
            {
                return handle;
            }
        }

        return PageObjectHandle.None;
    }

    /// <summary>Gives the box after a handle was dragged to a point. The side opposite the handle stays where it is.</summary>
    /// <param name="original">The box when the drag began.</param>
    /// <param name="handle">The handle being dragged.</param>
    /// <param name="point">Where the pointer is, in page space.</param>
    /// <param name="keepShape">Whether the box keeps its proportions, as dragging a corner with Shift does.</param>
    /// <returns>The new box.</returns>
    public static PageRect Drag(PageRect original, PageObjectHandle handle, PagePoint point, bool keepShape)
    {
        var left = original.Left;
        var top = original.Top;
        var right = original.Right;
        var bottom = original.Bottom;
        if (OnLeft(handle))
        {
            left = MathF.Min(point.X, right - MinimumSize);
        }

        if (OnRight(handle))
        {
            right = MathF.Max(point.X, left + MinimumSize);
        }

        if (OnTop(handle))
        {
            top = MathF.Min(point.Y, bottom - MinimumSize);
        }

        if (OnBottom(handle))
        {
            bottom = MathF.Max(point.Y, top + MinimumSize);
        }

        var box = PageRect.FromEdges(left, top, right, bottom);
        return keepShape && IsCorner(handle) ? KeepShape(original, handle, box) : box;
    }

    /// <summary>Gets the change that carries one box onto another: the scale about the old corner, then the move.</summary>
    /// <param name="from">The old box.</param>
    /// <param name="to">The new box.</param>
    /// <returns>The change in page space.</returns>
    public static System.Numerics.Matrix3x2 Fit(PageRect from, PageRect to)
    {
        var scaleX = from.Width > 0 ? to.Width / from.Width : 1F;
        var scaleY = from.Height > 0 ? to.Height / from.Height : 1F;
        return System.Numerics.Matrix3x2.CreateTranslation(-from.Left, -from.Top)
            * System.Numerics.Matrix3x2.CreateScale(scaleX, scaleY)
            * System.Numerics.Matrix3x2.CreateTranslation(to.Left, to.Top);
    }

    /// <summary>Gets the box a change carries a box to: the box around the four moved corners.</summary>
    /// <param name="box">The box.</param>
    /// <param name="change">The change in page space.</param>
    /// <returns>The box around the result.</returns>
    public static PageRect Carry(PageRect box, System.Numerics.Matrix3x2 change)
    {
        var a = System.Numerics.Vector2.Transform(new(box.Left, box.Top), change);
        var b = System.Numerics.Vector2.Transform(new(box.Right, box.Top), change);
        var c = System.Numerics.Vector2.Transform(new(box.Right, box.Bottom), change);
        var d = System.Numerics.Vector2.Transform(new(box.Left, box.Bottom), change);
        return PageRect.FromEdges(
            MathF.Min(MathF.Min(a.X, b.X), MathF.Min(c.X, d.X)),
            MathF.Min(MathF.Min(a.Y, b.Y), MathF.Min(c.Y, d.Y)),
            MathF.Max(MathF.Max(a.X, b.X), MathF.Max(c.X, d.X)),
            MathF.Max(MathF.Max(a.Y, b.Y), MathF.Max(c.Y, d.Y)));
    }

    /// <summary>Tells whether a handle is on the left side.</summary>
    /// <param name="handle">The handle.</param>
    /// <returns><see langword="true"/> for the left handles.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool OnLeft(PageObjectHandle handle) => handle is PageObjectHandle.TopLeft or PageObjectHandle.Left or PageObjectHandle.BottomLeft;

    /// <summary>Tells whether a handle is on the right side.</summary>
    /// <param name="handle">The handle.</param>
    /// <returns><see langword="true"/> for the right handles.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool OnRight(PageObjectHandle handle) => handle is PageObjectHandle.TopRight or PageObjectHandle.Right or PageObjectHandle.BottomRight;

    /// <summary>Tells whether a handle is on the top side.</summary>
    /// <param name="handle">The handle.</param>
    /// <returns><see langword="true"/> for the top handles.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool OnTop(PageObjectHandle handle) => handle is PageObjectHandle.TopLeft or PageObjectHandle.Top or PageObjectHandle.TopRight;

    /// <summary>Tells whether a handle is on the bottom side.</summary>
    /// <param name="handle">The handle.</param>
    /// <returns><see langword="true"/> for the bottom handles.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool OnBottom(PageObjectHandle handle) => handle is PageObjectHandle.BottomLeft or PageObjectHandle.Bottom or PageObjectHandle.BottomRight;

    /// <summary>Tells whether a handle is a corner.</summary>
    /// <param name="handle">The handle.</param>
    /// <returns><see langword="true"/> for a corner.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCorner(PageObjectHandle handle) =>
        handle is PageObjectHandle.TopLeft or PageObjectHandle.TopRight or PageObjectHandle.BottomLeft or PageObjectHandle.BottomRight;

    /// <summary>Makes a dragged box the shape of the original, keeping the corner opposite the handle where it is.</summary>
    /// <param name="original">The box when the drag began.</param>
    /// <param name="handle">The corner being dragged.</param>
    /// <param name="box">The box the drag gave.</param>
    /// <returns>The box with the original's proportions, as large as the drag asked for in the longer direction.</returns>
    private static PageRect KeepShape(PageRect original, PageObjectHandle handle, PageRect box)
    {
        var ratio = original.Height > 0 ? original.Width / original.Height : 1F;
        var width = MathF.Max(box.Width, box.Height * ratio);
        var height = ratio > 0 ? width / ratio : box.Height;
        var left = handle is PageObjectHandle.TopLeft or PageObjectHandle.BottomLeft ? original.Right - width : original.Left;
        var top = handle is PageObjectHandle.TopLeft or PageObjectHandle.TopRight ? original.Bottom - height : original.Top;
        return new(left, top, width, height);
    }
}
