// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Rendering.Cpu;

/// <summary>
/// Keeps the pixels a group of draws left in the frame, so a later frame that makes the very same draws puts the pixels back and
/// does not draw them again. A group is known by every value of every draw in it, in order, with the version of each picture
/// drawn, so a change of any of them draws the group afresh. Only pixels that the group alone decides are kept: each kept
/// rectangle starts with an opaque draw, and a draw that lets the frame under it show must lie inside one, or nothing is kept.
/// </summary>
[DebuggerDisplay("KeptDraws: {_keptCount} rectangles, kept {_kept}")]
internal sealed class KeptDraws
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The value the mix of a group's draws starts from.</summary>
    private const ulong Seed = 0xCBF29CE484222325;

    /// <summary>The multiplier that spreads each value through the mix.</summary>
    private const ulong Spread = 0x100000001B3;

    /// <summary>The fewest frames running a group is drawn the same way before its pixels are kept.</summary>
    private const int FewestSameFrames = 2;

    /// <summary>The most frames running a group that keeps changing must stay the same before its pixels are kept.</summary>
    private const int MostSameFrames = 64;

    /// <summary>The rectangles the group being drawn has covered with opaque draws.</summary>
    private readonly List<BlitRows> _covered = [];

    /// <summary>The rectangles whose pixels are kept.</summary>
    private BlitRows[] _keptRects = [];

    /// <summary>How many rectangles are kept.</summary>
    private int _keptCount;

    /// <summary>The kept pixels, one rectangle after another.</summary>
    private byte[] _pixels = [];

    /// <summary>The mix of the draws of the group being followed.</summary>
    private ulong _mix;

    /// <summary>How many draws the group being followed has made.</summary>
    private int _draws;

    /// <summary>Whether the group being followed can be kept.</summary>
    private bool _keepable;

    /// <summary>The mix of the draws whose pixels are kept.</summary>
    private ulong _keptMix;

    /// <summary>How many draws left the kept pixels.</summary>
    private int _keptDraws;

    /// <summary>The frame width the kept pixels were drawn at.</summary>
    private int _keptWidth;

    /// <summary>The frame height the kept pixels were drawn at.</summary>
    private int _keptHeight;

    /// <summary>Whether pixels are kept.</summary>
    private bool _kept;

    /// <summary>The mix of the draws of the group drawn in the frame before.</summary>
    private ulong _lastMix;

    /// <summary>How many draws the group drawn in the frame before made.</summary>
    private int _lastDraws;

    /// <summary>How many frames running the group has been drawn the same way.</summary>
    private int _sameFrames;

    /// <summary>How many frames running the group must be drawn the same way before its pixels are kept.</summary>
    private int _framesNeeded = FewestSameFrames;

    /// <summary>Whether the kept pixels have been put back at least once.</summary>
    private bool _used;

    /// <summary>Gets a value indicating whether pixels are kept, so a trial can find them.</summary>
    internal bool HasKept => _kept;

    /// <summary>Starts following a group of draws.</summary>
    internal void Begin()
    {
        _mix = Seed;
        _draws = 0;
        _keepable = true;
        _covered.Clear();
    }

    /// <summary>Forgets the kept pixels, which a group that has changed no longer matches.</summary>
    internal void Forget()
    {
        // Pixels that were kept and never put back were copied for nothing, as when the group settles for only a frame or two
        // between changes. The group must then stay the same for longer before it is kept again.
        _framesNeeded = _used ? FewestSameFrames : Math.Min(_framesNeeded + _framesNeeded, MostSameFrames);
        _kept = false;
    }

    /// <summary>Marks the group being followed as one that cannot be kept, because a draw in it depends on more than the group.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Spoil() => _keepable = false;

    /// <summary>Adds one draw to the group: what it is, where it lands and what it draws.</summary>
    /// <param name="kind">The kind of draw.</param>
    /// <param name="rows">Where it lands or what it clips to.</param>
    /// <param name="value">The colour, or the identity of the picture.</param>
    /// <param name="version">The version of the picture, or 0.</param>
    internal void Note(int kind, in BlitRows rows, uint value, int version)
    {
        var mix = _mix;
        mix = (mix ^ (uint)kind) * Spread;
        mix = (mix ^ (uint)rows.Left) * Spread;
        mix = (mix ^ (uint)rows.Top) * Spread;
        mix = (mix ^ (uint)rows.Right) * Spread;
        mix = (mix ^ (uint)rows.Bottom) * Spread;
        mix = (mix ^ value) * Spread;
        _mix = (mix ^ (uint)version) * Spread;
        _draws++;
    }

    /// <summary>Records where a draw that was really made landed, and whether its pixels are the group's alone.</summary>
    /// <param name="rows">Where it landed, already clipped to the frame.</param>
    /// <param name="opaque">Whether it replaced every pixel there.</param>
    internal void Cover(in BlitRows rows, bool opaque)
    {
        if (rows.IsEmpty || IsInside(rows))
        {
            return;
        }

        if (opaque)
        {
            _covered.Add(rows);
            return;
        }

        // The frame under this draw shows through it, and the frame there is not the group's.
        _keepable = false;
    }

    /// <summary>Tells whether the group just followed is the one whose pixels are kept.</summary>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <returns><see langword="true"/> when the kept pixels are what the group would draw.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Matches(int width, int height) =>
        _kept && _keepable && _mix == _keptMix && _draws == _keptDraws && width == _keptWidth && height == _keptHeight;

    /// <summary>Puts the kept pixels back into the frame.</summary>
    /// <param name="frame">The frame pixels.</param>
    /// <param name="stride">The bytes of one frame row.</param>
    internal void Restore(Span<byte> frame, int stride)
    {
        _used = true;
        var from = 0;
        for (var i = 0; i < _keptCount; i++)
        {
            var rect = _keptRects[i];
            var length = (rect.Right - rect.Left) * BytesPerPixel;
            for (var row = rect.Top; row < rect.Bottom; row++)
            {
                _pixels.AsSpan(from, length).CopyTo(frame.Slice((row * stride) + (rect.Left * BytesPerPixel), length));
                from += length;
            }
        }
    }

    /// <summary>Keeps the pixels the group just drawn left in the frame, or forgets what was kept when the group cannot be kept.</summary>
    /// <param name="frame">The frame pixels.</param>
    /// <param name="stride">The bytes of one frame row.</param>
    /// <param name="height">The frame height.</param>
    internal void Capture(ReadOnlySpan<byte> frame, int stride, int height)
    {
        // A group that changes from frame to frame, as while a page turns, is not worth keeping: its pixels are copied only once
        // it has been drawn the same way twice running.
        _sameFrames = _mix == _lastMix && _draws == _lastDraws ? _sameFrames + 1 : 1;
        _lastMix = _mix;
        _lastDraws = _draws;
        _kept = false;
        if (_sameFrames < _framesNeeded || !_keepable || _covered.Count == 0)
        {
            return;
        }

        _used = false;
        var bytes = 0;
        foreach (var rect in _covered)
        {
            bytes += (rect.Right - rect.Left) * (rect.Bottom - rect.Top) * BytesPerPixel;
        }

        MakeRoom(bytes);
        var to = 0;
        _keptCount = 0;
        foreach (var rect in _covered)
        {
            _keptRects[_keptCount] = rect;
            _keptCount++;
            var length = (rect.Right - rect.Left) * BytesPerPixel;
            for (var row = rect.Top; row < rect.Bottom; row++)
            {
                frame.Slice((row * stride) + (rect.Left * BytesPerPixel), length).CopyTo(_pixels.AsSpan(to, length));
                to += length;
            }
        }

        _keptMix = _mix;
        _keptDraws = _draws;
        _keptWidth = stride / BytesPerPixel;
        _keptHeight = height;
        _kept = true;
    }

    /// <summary>Makes room for the rectangles covered and their pixels.</summary>
    /// <param name="bytes">The bytes of the pixels to keep.</param>
    private void MakeRoom(int bytes)
    {
        if (_keptRects.Length < _covered.Count)
        {
            _keptRects = new BlitRows[_covered.Capacity];
        }

        // The room grows only when the group covers more than it ever has, so steady frames make nothing.
        if (_pixels.Length < bytes)
        {
            _pixels = GC.AllocateUninitializedArray<byte>(bytes);
        }
    }

    /// <summary>Tells whether a rectangle lies inside one the group has already covered.</summary>
    /// <param name="rows">The rectangle.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private bool IsInside(in BlitRows rows)
    {
        foreach (var rect in _covered)
        {
            if (rows.Left >= rect.Left && rows.Top >= rect.Top && rows.Right <= rect.Right && rows.Bottom <= rect.Bottom)
            {
                return true;
            }
        }

        return false;
    }
}
