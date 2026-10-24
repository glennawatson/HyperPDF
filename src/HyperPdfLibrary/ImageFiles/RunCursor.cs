// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Where the next pixel of a run-length coded bitmap goes.</summary>
/// <param name="width">The width in pixels.</param>
/// <param name="height">The height in pixels.</param>
[DebuggerDisplay("RunCursor: ({X}, {Y})")]
internal sealed class RunCursor(int width, int height)
{
    /// <summary>The indexes written, rows in the order they are stored.</summary>
    private readonly byte[] _output = new byte[width * height];

    /// <summary>Gets the column.</summary>
    internal int X { get; private set; }

    /// <summary>Gets the row, counted in the order rows are stored.</summary>
    internal int Y { get; private set; }

    /// <summary>Gets a value indicating whether the cursor has gone past the last row.</summary>
    internal bool Done => Y >= height;

    /// <summary>Gets the indexes written so far.</summary>
    /// <returns>The indexes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte[] ToArray() => _output;

    /// <summary>Stores an index at the cursor and moves on one pixel; a pixel outside the bitmap is dropped.</summary>
    /// <param name="index">The palette index.</param>
    internal void Put(byte index)
    {
        if (X < width && Y < height)
        {
            _output[(Y * width) + X] = index;
        }

        X++;
    }

    /// <summary>Marks the bitmap as complete, so no more pixels are taken.</summary>
    internal void Finish() => Y = height;

    /// <summary>Starts the next row.</summary>
    internal void NextLine()
    {
        X = 0;
        Y++;
    }

    /// <summary>Moves the cursor right and down.</summary>
    /// <param name="dx">The columns to move.</param>
    /// <param name="dy">The rows to move.</param>
    internal void Move(int dx, int dy)
    {
        X += dx;
        Y += dy;
    }
}
