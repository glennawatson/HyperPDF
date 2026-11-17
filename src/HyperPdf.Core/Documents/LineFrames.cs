// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Documents;

/// <summary>Sees the lines of a page as they read, whichever way the page turns them.</summary>
internal static class LineFrames
{
    /// <summary>The least the dot product of the directions of two lines of one paragraph may be, which allows about eight degrees.</summary>
    private const float SameDirection = 0.99F;

    /// <summary>One half.</summary>
    private const float Half = 0.5F;

    /// <summary>Gets the box of a line as it reads: how far along its reading it goes, and where it is in the way the lines follow one another.</summary>
    /// <param name="item">The line.</param>
    /// <returns>The framed line, in which a line that follows another has a larger top.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static LineFrame FrameOf(in PageObjectInfo item)
    {
        var box = item.Bounds;
        var reading = item.Reading;
        if (reading.IsUpright)
        {
            return new(box.Left, box.Right, box.Top, box.Bottom);
        }

        // A line that is turned is its length by its height around the middle of its bounds, seen along its own direction and across it.
        var middleX = (box.Left + box.Right) * Half;
        var middleY = (box.Top + box.Bottom) * Half;
        var along = (middleX * reading.DirectionX) + (middleY * reading.DirectionY);
        var across = (middleY * reading.DirectionX) - (middleX * reading.DirectionY);
        var halfLength = reading.Length * Half;
        var halfHeight = reading.Height * Half;
        return new(along - halfLength, along + halfLength, across - halfHeight, across + halfHeight);
    }

    /// <summary>Tells whether two lines read the same way, to within a few degrees.</summary>
    /// <param name="first">The first line.</param>
    /// <param name="second">The second line.</param>
    /// <returns><see langword="true"/> when they run the same way.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool SameWay(in PageObjectInfo first, in PageObjectInfo second)
    {
        var a = first.Reading;
        var b = second.Reading;
        return a.IsUpright || b.IsUpright
            ? a.IsUpright && b.IsUpright
            : (a.DirectionX * b.DirectionX) + (a.DirectionY * b.DirectionY) >= SameDirection;
    }
}
