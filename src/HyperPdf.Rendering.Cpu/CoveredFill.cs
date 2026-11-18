// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Cpu;

/// <summary>
/// Holds back a large opaque fill, such as the paper of a page, and the opaque pictures drawn straight after it, such as the page's
/// tiles. A page's tiles cover nearly all of its paper, so filling the paper first writes those pixels twice. When the pictures
/// make up one rectangle with no gap and no overlap, only the fill outside that rectangle is painted; otherwise the whole fill is.
/// The pictures are drawn after it either way, so the frame comes out exactly as if nothing had been held back.
/// </summary>
[DebuggerDisplay("CoveredFill: waiting {IsWaiting}, {_count} pictures")]
internal sealed class CoveredFill
{
    /// <summary>The most pictures held back over one fill: more tiles than a page shows at once on a large display.</summary>
    private const int MaxBlits = 48;

    /// <summary>The pictures held back, in the order they were drawn.</summary>
    private readonly DeferredBlit[] _blits = new DeferredBlit[MaxBlits];

    /// <summary>The number of pictures held back.</summary>
    private int _count;

    /// <summary>The pixels of the fill held back.</summary>
    private BlitRows _area;

    /// <summary>The colour of the fill held back.</summary>
    private uint _color;

    /// <summary>Whether the fill held back is the clear that starts a frame.</summary>
    private bool _isClear;

    /// <summary>Gets a value indicating whether a fill is held back.</summary>
    internal bool IsWaiting { get; private set; }

    /// <summary>Holds a fill back. Any fill held before must have been painted.</summary>
    /// <param name="area">The pixels it fills, inside the frame and the clip.</param>
    /// <param name="color">Its opaque colour.</param>
    internal void Begin(in BlitRows area, uint color)
    {
        _area = area;
        _color = color;
        IsWaiting = true;
        _isClear = false;
    }

    /// <summary>Holds back the clear that starts a frame. Any fill held before must have been painted.</summary>
    /// <param name="area">The whole frame.</param>
    /// <param name="color">Its opaque colour.</param>
    internal void BeginClear(in BlitRows area, uint color)
    {
        Begin(area, color);
        _isClear = true;
    }

    /// <summary>
    /// Cuts a held clear down to the margin around an opaque fill that is about to cover the rest, so the pixels the fill
    /// covers are painted once. Nothing happens, and the clear stays held, when the fill does not fit that case.
    /// </summary>
    /// <param name="owner">The backend that paints.</param>
    /// <param name="rows">The pixels the opaque fill covers.</param>
    /// <returns><see langword="true"/> when the clear is now only the margin, painted, and nothing is held.</returns>
    internal bool TryNarrowClear(CpuRenderBackend owner, in BlitRows rows)
    {
        if (!IsWaiting || !_isClear || _count != 0)
        {
            return false;
        }

        var inside = Shared(_area, rows);
        if (inside.IsEmpty)
        {
            return false;
        }

        IsWaiting = false;
        _isClear = false;
        owner.Fill(new(_area.Left, _area.Top, _area.Right, inside.Top), _color);
        owner.Fill(new(_area.Left, inside.Bottom, _area.Right, _area.Bottom), _color);
        owner.Fill(new(_area.Left, inside.Top, inside.Left, inside.Bottom), _color);
        owner.Fill(new(inside.Right, inside.Top, _area.Right, inside.Bottom), _color);
        return true;
    }

    /// <summary>Holds a picture back when a fill is waiting and the picture is copied as it is, with nothing showing through.</summary>
    /// <param name="surface">The picture.</param>
    /// <param name="request">Where it goes and how.</param>
    /// <returns><see langword="true"/> when it was held back; <see langword="false"/> when the caller must paint what is waiting and draw it.</returns>
    internal bool TryDefer(CpuPixelSurface surface, in BlitRequest request)
    {
        if (!IsWaiting || _count == MaxBlits || !request.Opaque || request.Width != surface.Width || request.Height != surface.Height)
        {
            return false;
        }

        _blits[_count] = new(surface, request);
        _count++;
        return true;
    }

    /// <summary>Paints what is waiting: the fill where the pictures do not cover it, then the pictures.</summary>
    /// <param name="owner">The backend that owns the frame buffer.</param>
    internal void Flush(CpuRenderBackend owner)
    {
        if (!IsWaiting)
        {
            return;
        }

        IsWaiting = false;
        if (TryGetCover(owner.Grid, out var cover))
        {
            owner.Fill(new(_area.Left, _area.Top, _area.Right, cover.Top), _color);
            owner.Fill(new(_area.Left, cover.Bottom, _area.Right, _area.Bottom), _color);
            owner.Fill(new(_area.Left, cover.Top, cover.Left, cover.Bottom), _color);
            owner.Fill(new(cover.Right, cover.Top, _area.Right, cover.Bottom), _color);
        }
        else
        {
            owner.Fill(_area, _color);
        }

        for (var i = 0; i < _count; i++)
        {
            owner.Blit(_blits[i].Surface, _blits[i].Request);
            _blits[i] = default;
        }

        _count = 0;
    }

    /// <summary>Gets the pixels two rectangles share.</summary>
    /// <param name="first">One rectangle.</param>
    /// <param name="second">The other.</param>
    /// <returns>The shared pixels, empty when there are none.</returns>
    private static BlitRows Shared(in BlitRows first, in BlitRows second) =>
        new(Math.Max(first.Left, second.Left), Math.Max(first.Top, second.Top), Math.Min(first.Right, second.Right), Math.Min(first.Bottom, second.Bottom));

    /// <summary>Tells whether a rectangle shares a pixel with any of the rectangles before it.</summary>
    /// <param name="parts">The rectangles before it.</param>
    /// <param name="part">The rectangle.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool Overlaps(ReadOnlySpan<BlitRows> parts, in BlitRows part)
    {
        foreach (ref readonly var other in parts)
        {
            if (!Shared(other, part).IsEmpty)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds the rectangle of the fill the pictures cover, when they cover one rectangle exactly.</summary>
    /// <param name="frame">The frame's width, height and bytes per row.</param>
    /// <param name="cover">Receives the rectangle.</param>
    /// <returns><see langword="true"/> when the pictures inside the fill touch without a gap or an overlap and make up one rectangle.</returns>
    private bool TryGetCover(in PixelGrid frame, out BlitRows cover)
    {
        cover = default;
        Span<BlitRows> parts = stackalloc BlitRows[MaxBlits];
        var used = 0;
        long area = 0;
        for (var i = 0; i < _count; i++)
        {
            var part = Shared(BlitRows.Clip(_blits[i].Request, frame), _area);
            if (part.IsEmpty)
            {
                continue;
            }

            if (Overlaps(parts[..used], part))
            {
                return false;
            }

            cover = used == 0 ? part : new(Math.Min(cover.Left, part.Left), Math.Min(cover.Top, part.Top), Math.Max(cover.Right, part.Right), Math.Max(cover.Bottom, part.Bottom));
            parts[used] = part;
            used++;
            area += (long)(part.Right - part.Left) * (part.Bottom - part.Top);
        }

        // Parts that do not overlap and whose areas add up to the area of the box around them fill that box.
        return used > 0 && area == (long)(cover.Right - cover.Left) * (cover.Bottom - cover.Top);
    }
}
