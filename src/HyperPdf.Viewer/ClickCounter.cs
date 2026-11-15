// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer;

/// <summary>Counts presses that land close together in time and place, so a double click selects a word and a triple click a line.</summary>
/// <param name="X">The x of the last press in device pixels.</param>
/// <param name="Y">The y of the last press in device pixels.</param>
/// <param name="Timestamp">When the last press happened, as a <see cref="Stopwatch"/> timestamp.</param>
/// <param name="Count">The presses counted so far.</param>
[DebuggerDisplay("ClickCounter: {Count}")]
internal readonly record struct ClickCounter(int X, int Y, long Timestamp, int Count)
{
    /// <summary>The longest time between the presses of a multiple click, in milliseconds.</summary>
    private const double MultipleClickMilliseconds = 400;

    /// <summary>The furthest the pointer may move between the presses of a multiple click, in device pixels.</summary>
    private const int MultipleClickDistance = 4;

    /// <summary>The most presses counted before the count starts again.</summary>
    private const int MostClicks = 3;

    /// <summary>Counts a press.</summary>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <param name="timestamp">When it happened, as a <see cref="Stopwatch"/> timestamp.</param>
    /// <returns>The counter after the press.</returns>
    internal ClickCounter Press(int x, int y, long timestamp)
    {
        var near = Math.Abs(x - X) <= MultipleClickDistance && Math.Abs(y - Y) <= MultipleClickDistance;
        var soon = Stopwatch.GetElapsedTime(Timestamp, timestamp).TotalMilliseconds <= MultipleClickMilliseconds;
        var count = near && soon && Count is > 0 and < MostClicks ? Count + 1 : 1;
        return new(x, y, timestamp, count);
    }
}
