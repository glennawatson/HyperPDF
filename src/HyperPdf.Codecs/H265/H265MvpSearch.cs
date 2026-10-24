// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H265;

/// <summary>Searches the neighbours of a prediction block for spatial motion vector predictors (8.5.3.2.7).</summary>
internal readonly ref struct H265MvpSearch
{
    /// <summary>The reference lists of a slice.</summary>
    private const int ListCount = 2;

    /// <summary>The slice context.</summary>
    private readonly H265SliceContext _context;

    /// <summary>The prediction block.</summary>
    private readonly H265PuBlock _block;

    /// <summary>The reference list the vector is for.</summary>
    private readonly int _list;

    /// <summary>The picture the vector is for.</summary>
    private readonly H265Picture _target;

    /// <summary>Initializes a new instance of the <see cref="H265MvpSearch"/> struct.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The prediction block.</param>
    /// <param name="list">The reference list.</param>
    /// <param name="refIdx">The reference index in that list.</param>
    internal H265MvpSearch(H265SliceContext context, in H265PuBlock block, int list, int refIdx)
    {
        _context = context;
        _block = block;
        _list = list;
        _target = (list == 0 ? context.RefList0 : context.RefList1)[refIdx];
        var area = block.Area;
        IsScaled = H265PuAvailability.IsAvailable(context, block, area.X - 1, area.Y + area.Height)
            || H265PuAvailability.IsAvailable(context, block, area.X - 1, area.Y + area.Height - 1);
    }

    /// <summary>Gets a value indicating whether a left neighbour is available, which decides whether the above candidates may use scaling.</summary>
    internal bool IsScaled { get; }

    /// <summary>Finds the left candidate: first a neighbour referring to the same picture, then one whose vector can be scaled.</summary>
    /// <param name="vector">Receives the candidate.</param>
    /// <returns><see langword="true"/> when a candidate was found.</returns>
    internal bool FindLeft(out MotionVector vector)
    {
        var area = _block.Area;
        var x = area.X - 1;
        var below = area.Y + area.Height;
        return TryExact(x, below, out vector)
            || TryExact(x, below - 1, out vector)
            || TryScaled(x, below, out vector)
            || TryScaled(x, below - 1, out vector);
    }

    /// <summary>Finds the above candidate among neighbours that refer to the same picture.</summary>
    /// <param name="vector">Receives the candidate.</param>
    /// <returns><see langword="true"/> when a candidate was found.</returns>
    internal bool FindAbove(out MotionVector vector)
    {
        var area = _block.Area;
        var y = area.Y - 1;
        return TryExact(area.X + area.Width, y, out vector)
            || TryExact(area.X + area.Width - 1, y, out vector)
            || TryExact(area.X - 1, y, out vector);
    }

    /// <summary>Finds the above candidate allowing its vector to be scaled.</summary>
    /// <param name="vector">Receives the candidate.</param>
    /// <returns><see langword="true"/> when a candidate was found.</returns>
    internal bool FindAboveScaled(out MotionVector vector)
    {
        var area = _block.Area;
        var y = area.Y - 1;
        return TryScaled(area.X + area.Width, y, out vector)
            || TryScaled(area.X + area.Width - 1, y, out vector)
            || TryScaled(area.X - 1, y, out vector);
    }

    /// <summary>Tells whether a motion uses a list.</summary>
    /// <param name="motion">The motion.</param>
    /// <param name="list">The list.</param>
    /// <returns><see langword="true"/> when the list is used.</returns>
    private static bool Uses(H265PuMotion motion, int list) => list == 0 ? motion.UsesList0 : motion.UsesList1;

    /// <summary>Gets the reference index of a motion for a list.</summary>
    /// <param name="motion">The motion.</param>
    /// <param name="list">The list.</param>
    /// <returns>The index.</returns>
    private static int RefIdxOf(H265PuMotion motion, int list) => list == 0 ? motion.RefIdx0 : motion.RefIdx1;

    /// <summary>Gets the vector of a motion for a list.</summary>
    /// <param name="motion">The motion.</param>
    /// <param name="list">The list.</param>
    /// <returns>The vector.</returns>
    private static MotionVector VectorOf(H265PuMotion motion, int list) => list == 0 ? motion.Vector0 : motion.Vector1;

    /// <summary>Tries a neighbour whose motion refers to the same picture as the target.</summary>
    /// <param name="x">The neighbour's column.</param>
    /// <param name="y">The neighbour's row.</param>
    /// <param name="vector">Receives the vector.</param>
    /// <returns><see langword="true"/> when the neighbour qualifies.</returns>
    private bool TryExact(int x, int y, out MotionVector vector)
    {
        vector = default;
        if (!H265PuAvailability.IsAvailable(_context, _block, x, y))
        {
            return false;
        }

        var motion = _context.Picture.Motion[_context.Picture.GridIndex(x, y)];
        var other = 1 - _list;
        if (Uses(motion, _list) && RefPicture(_list, RefIdxOf(motion, _list)).Poc == _target.Poc)
        {
            vector = VectorOf(motion, _list);
            return true;
        }

        if (Uses(motion, other) && RefPicture(other, RefIdxOf(motion, other)).Poc == _target.Poc)
        {
            vector = VectorOf(motion, other);
            return true;
        }

        return false;
    }

    /// <summary>Tries a neighbour whose motion refers to a picture of the same kind (short or long term) as the target, scaling the vector by distance when the pictures differ.</summary>
    /// <param name="x">The neighbour's column.</param>
    /// <param name="y">The neighbour's row.</param>
    /// <param name="vector">Receives the vector.</param>
    /// <returns><see langword="true"/> when the neighbour qualifies.</returns>
    private bool TryScaled(int x, int y, out MotionVector vector)
    {
        vector = default;
        if (!H265PuAvailability.IsAvailable(_context, _block, x, y))
        {
            return false;
        }

        var motion = _context.Picture.Motion[_context.Picture.GridIndex(x, y)];
        var targetLong = _target.IsLongTerm;
        for (var step = 0; step < ListCount; step++)
        {
            var list = step == 0 ? _list : 1 - _list;
            if (!Uses(motion, list))
            {
                continue;
            }

            var neighbour = RefPicture(list, RefIdxOf(motion, list));
            if (neighbour.IsLongTerm != targetLong)
            {
                continue;
            }

            vector = VectorOf(motion, list);
            if (neighbour.Poc != _target.Poc && !targetLong)
            {
                var current = _context.Picture.Poc;
                vector = H265MotionScaling.Scale(vector, current - neighbour.Poc, current - _target.Poc);
            }

            return true;
        }

        return false;
    }

    /// <summary>Gets a reference picture of the current slice.</summary>
    /// <param name="list">The list.</param>
    /// <param name="refIdx">The index.</param>
    /// <returns>The picture.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private H265Picture RefPicture(int list, int refIdx) => (list == 0 ? _context.RefList0 : _context.RefList1)[refIdx];
}
