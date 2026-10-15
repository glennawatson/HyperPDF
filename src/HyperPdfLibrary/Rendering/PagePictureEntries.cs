// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>Keeps the common single-page cache entry inline and allocates overflow storage only for additional entries.</summary>
internal record struct PagePictureEntries
{
    /// <summary>The initial overflow capacity.</summary>
    private const int InitialCapacity = 4;

    /// <summary>The entries kept outside overflow storage.</summary>
    private const int InlineEntries = 1;

    /// <summary>The oldest entry, stored without an array.</summary>
    private PagePictures? _first;

    /// <summary>The remaining entries, oldest first.</summary>
    private PagePictures[]? _overflow;

    /// <summary>Gets the number of entries in use.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets an entry by its position in the cache.</summary>
    /// <param name="index">Zero for the oldest entry.</param>
    /// <returns>The cached entry.</returns>
    /// <exception cref="InvalidOperationException">The cache has no first entry.</exception>
    internal readonly PagePictures this[int index] => index == 0
        ? _first ?? throw new InvalidOperationException("The picture cache is empty.")
        : (_overflow ?? []).AsSpan()[index - 1];

    /// <summary>Adds an entry as the most recently used.</summary>
    /// <param name="entry">The entry to retain.</param>
    internal void Add(PagePictures entry)
    {
        if (Count == 0)
        {
            _first = entry;
        }
        else
        {
            if (_overflow is null || Count > _overflow.Length)
            {
                Array.Resize(ref _overflow, _overflow is null ? InitialCapacity : _overflow.Length << 1);
            }

            _overflow[Count - 1] = entry;
        }

        Count++;
    }

    /// <summary>Removes one entry and closes the gap.</summary>
    /// <param name="index">The position to remove.</param>
    internal void RemoveAt(int index)
    {
        if (index == 0)
        {
            RemoveFirst(1);
            return;
        }

        var overflow = (_overflow ?? []).AsSpan();
        overflow.Slice(index, Count - index - 1).CopyTo(overflow[(index - 1)..]);
        overflow.Slice(Count - InlineEntries - 1, 1).Clear();
        Count--;
    }

    /// <summary>Removes the oldest entries in one move.</summary>
    /// <param name="count">The number to remove.</param>
    internal void RemoveFirst(int count)
    {
        if (count == 0)
        {
            return;
        }

        if (count == Count)
        {
            Clear();
            return;
        }

        _first = this[count];
        var overflow = (_overflow ?? []).AsSpan();
        var remaining = Count - count - 1;
        overflow.Slice(count, remaining).CopyTo(overflow);
        overflow.Slice(remaining, count).Clear();
        Count -= count;
    }

    /// <summary>Releases every cached reference while keeping overflow storage available.</summary>
    internal void Clear()
    {
        _first = null;
        if (_overflow is not null)
        {
            Array.Clear(_overflow);
        }

        Count = 0;
    }
}
