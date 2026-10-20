// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Keeps the most recently used text pages of a document. Pages are built outside the lock, so a slow page does not stall
/// other threads; if two threads build the same page, the first one stored wins and both use it. Lookups allocate nothing.
/// </summary>
[DebuggerDisplay("PdfTextPageCache: {_count} of {_pages.Length}")]
internal sealed class PdfTextPageCache
{
    /// <summary>Guards the entries.</summary>
    private readonly Lock _gate = new();

    /// <summary>The page index of each entry.</summary>
    private readonly int[] _keys;

    /// <summary>The cached pages.</summary>
    private readonly PdfTextPage?[] _pages;

    /// <summary>When each entry was last used.</summary>
    private readonly long[] _used;

    /// <summary>The number of entries in use.</summary>
    private int _count;

    /// <summary>The use counter.</summary>
    private long _clock;

    /// <summary>Initializes a new instance of the <see cref="PdfTextPageCache"/> class.</summary>
    /// <param name="capacity">The most pages kept.</param>
    internal PdfTextPageCache(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _keys = new int[capacity];
        _pages = new PdfTextPage?[capacity];
        _used = new long[capacity];
    }

    /// <summary>Gets a page's text, building it when it is not cached.</summary>
    /// <typeparam name="TState">The type of the state given to the builder.</typeparam>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="state">The state given to the builder.</param>
    /// <param name="build">Builds the page.</param>
    /// <returns>The text page.</returns>
    internal PdfTextPage GetOrAdd<TState>(int pageIndex, TState state, Func<int, TState, PdfTextPage> build)
    {
        lock (_gate)
        {
            if (TryFind(pageIndex) is { } cached)
            {
                return cached;
            }
        }

        var created = build(pageIndex, state);
        lock (_gate)
        {
            if (TryFind(pageIndex) is { } winner)
            {
                return winner;
            }

            Store(pageIndex, created);
            return created;
        }
    }

    /// <summary>Gets a page's text when it is cached, marking it used.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <returns>The page, or <see langword="null"/> when it is not cached.</returns>
    internal PdfTextPage? TryGet(int pageIndex)
    {
        lock (_gate)
        {
            return TryFind(pageIndex);
        }
    }

    /// <summary>Removes every page.</summary>
    internal void Clear()
    {
        lock (_gate)
        {
            Array.Clear(_pages);
            _count = 0;
        }
    }

    /// <summary>Finds a cached page and marks it used; call under the lock.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <returns>The page, or <see langword="null"/>.</returns>
    private PdfTextPage? TryFind(int pageIndex)
    {
        var index = _keys.AsSpan(0, _count).IndexOf(pageIndex);
        if (index < 0)
        {
            return null;
        }

        _clock++;
        _used[index] = _clock;
        return _pages[index];
    }

    /// <summary>Stores a page, replacing the least recently used one when full; call under the lock.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    private void Store(int pageIndex, PdfTextPage page)
    {
        var slot = _count;
        if (_count < _keys.Length)
        {
            _count++;
        }
        else
        {
            slot = 0;
            for (var i = 1; i < _count; i++)
            {
                if (_used[i] < _used[slot])
                {
                    slot = i;
                }
            }
        }

        _clock++;
        _keys[slot] = pageIndex;
        _pages[slot] = page;
        _used[slot] = _clock;
    }
}
