// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Structure;

/// <summary>
/// The cross-reference sections still to read and those already read, held in pooled arrays for the length of one read.
/// A file links few sections, so the visited offsets are searched in order.
/// </summary>
internal ref struct XrefWalk
{
    /// <summary>The offsets waiting to be read; the last pushed is read first.</summary>
    private long[]? _pending;

    /// <summary>The number of offsets waiting.</summary>
    private int _pendingCount;

    /// <summary>The offsets already read.</summary>
    private long[]? _visited;

    /// <summary>Gets the number of sections visited.</summary>
    internal int Visited { get; private set; }

    /// <summary>Queues a section offset.</summary>
    /// <param name="offset">The offset.</param>
    internal void Push(long offset)
    {
        PooledArrays.EnsureRoom(ref _pending, _pendingCount, clear: false)[_pendingCount] = offset;
        _pendingCount++;
    }

    /// <summary>Takes the most recently queued offset.</summary>
    /// <param name="offset">The offset.</param>
    /// <returns><see langword="false"/> when none is waiting.</returns>
    internal bool TryPop(out long offset)
    {
        if (_pendingCount == 0)
        {
            offset = 0;
            return false;
        }

        _pendingCount--;
        offset = _pending![_pendingCount];
        return true;
    }

    /// <summary>Notes that a section is being read.</summary>
    /// <param name="offset">The section's offset.</param>
    /// <returns><see langword="false"/> when the section was read before.</returns>
    internal bool TryVisit(long offset)
    {
        if (_visited is not null && _visited.AsSpan(0, Visited).Contains(offset))
        {
            return false;
        }

        PooledArrays.EnsureRoom(ref _visited, Visited, clear: false)[Visited] = offset;
        Visited++;
        return true;
    }

    /// <summary>Gives the arrays back to the pool.</summary>
    internal void Release()
    {
        PooledArrays.Return(ref _pending, _pendingCount, clear: false);
        PooledArrays.Return(ref _visited, Visited, clear: false);
        _pendingCount = 0;
        Visited = 0;
    }
}
