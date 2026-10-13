// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Rendering;

/// <summary>
/// A binary min-heap of <see cref="QueuedRequest"/> ordered by stamp. The array is made once with room for a typical
/// burst and doubles only when a burst outgrows it, so steady-state queuing does not allocate. It is not thread-safe;
/// the scheduler's gate guards it.
/// </summary>
[DebuggerDisplay("RequestQueue: {Count} queued")]
internal sealed class RequestQueue
{
    /// <summary>The number of requests the queue holds before it grows.</summary>
    private const int InitialCapacity = 8;

    /// <summary>The factor the array grows by when a burst fills it.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The shift that doubles a heap index; a node's children are at twice its index plus one and two.</summary>
    private const int ChildShift = 1;

    /// <summary>The heap, with the smallest stamp at index 0.</summary>
    private QueuedRequest[] _items = new QueuedRequest[InitialCapacity];

    /// <summary>Gets the number of queued requests.</summary>
    internal int Count { get; private set; }

    /// <summary>Adds a request.</summary>
    /// <param name="item">The request and its stamp.</param>
    internal void Enqueue(in QueuedRequest item)
    {
        if (Count == _items.Length)
        {
            Array.Resize(ref _items, _items.Length * GrowthFactor);
        }

        var index = Count;
        Count = index + 1;
        var items = _items;
        while (index > 0)
        {
            var parent = (index - 1) >> ChildShift;
            if (items[parent].Stamp <= item.Stamp)
            {
                break;
            }

            items[index] = items[parent];
            index = parent;
        }

        items[index] = item;
    }

    /// <summary>Removes the request with the smallest stamp.</summary>
    /// <param name="item">Receives the request.</param>
    /// <returns><see langword="true"/> when the queue was not empty.</returns>
    internal bool TryDequeue(out QueuedRequest item)
    {
        if (Count == 0)
        {
            item = default;
            return false;
        }

        var items = _items;
        item = items[0];
        var last = Count - 1;
        Count = last;
        var moved = items[last];

        // Release the vacated slot so the queue does not keep a finished request alive.
        items[last] = default;
        if (last > 0)
        {
            SiftDown(items, last, moved);
        }

        return true;
    }

    /// <summary>Places a request at the root and moves it down to its position.</summary>
    /// <param name="items">The heap.</param>
    /// <param name="count">The number of requests in the heap.</param>
    /// <param name="moved">The request to place.</param>
    private static void SiftDown(QueuedRequest[] items, int count, in QueuedRequest moved)
    {
        var index = 0;
        while (true)
        {
            var child = (index << ChildShift) + 1;
            if (child >= count)
            {
                break;
            }

            if (child + 1 < count && items[child + 1].Stamp < items[child].Stamp)
            {
                child++;
            }

            if (moved.Stamp <= items[child].Stamp)
            {
                break;
            }

            items[index] = items[child];
            index = child;
        }

        items[index] = moved;
    }
}
