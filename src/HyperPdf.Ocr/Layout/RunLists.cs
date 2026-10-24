// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Layout;

/// <summary>The runs of ink found on a page, kept between pages so their lists are not made again.</summary>
[DebuggerDisplay("RunLists: {Starts.Count} runs")]
internal sealed class RunLists
{
    /// <summary>The factor the slots grow by when they are too few.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The slots of the last gathering of blobs.</summary>
    private int[] _slots = [];

    /// <summary>Gets the start of each run.</summary>
    internal List<int> Starts { get; } = [];

    /// <summary>Gets the end of each run, which is one past its last pixel.</summary>
    internal List<int> Ends { get; } = [];

    /// <summary>Gets the row of each run.</summary>
    internal List<int> Rows { get; } = [];

    /// <summary>Gets the union-find parent of each run.</summary>
    internal List<int> Parents { get; } = [];

    /// <summary>Forgets the runs of the last page.</summary>
    internal void Clear()
    {
        Starts.Clear();
        Ends.Clear();
        Rows.Clear();
        Parents.Clear();
    }

    /// <summary>Gets an array of slots, one for each run, all set to -1.</summary>
    /// <param name="length">The number of runs.</param>
    /// <returns>The slots, at least <paramref name="length"/> long.</returns>
    internal int[] EmptySlots(int length)
    {
        if (_slots.Length < length)
        {
            _slots = new int[Math.Max(length, _slots.Length * GrowthFactor)];
        }

        Array.Fill(_slots, -1, 0, length);
        return _slots;
    }
}
