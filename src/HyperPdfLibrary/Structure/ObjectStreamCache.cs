// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Structure;

/// <summary>
/// Keeps the most recently used decoded object streams within a count and byte budget. The newest stream always stays,
/// so a stream larger than the byte budget is still usable while it is being read; older streams are decoded again on demand.
/// </summary>
[DebuggerDisplay("ObjectStreamCache: {Count} streams, {Bytes} bytes")]
internal sealed class ObjectStreamCache
{
    /// <summary>The most decoded object streams kept by default.</summary>
    internal const int DefaultMaxStreams = 128;

    /// <summary>The most decoded bytes kept by default: 4 MiB.</summary>
    internal const long DefaultMaxBytes = 4L * 1024 * 1024;

    /// <summary>The slots made when the first stream is cached; the arrays grow as more are.</summary>
    private const int InitialSlots = 4;

    /// <summary>The factor by which the slot arrays grow.</summary>
    private const int GrowthFactor = 2;

    /// <summary>Guards every field below.</summary>
    private readonly Lock _gate = new();

    /// <summary>The object numbers of the cached streams, in slots <c>0.._count</c>.</summary>
    private int[] _containers = [];

    /// <summary>The cached streams, parallel to <see cref="_containers"/>.</summary>
    private ObjectStreamIndex?[] _items = [];

    /// <summary>When each slot was last used, parallel to <see cref="_containers"/>.</summary>
    private long[] _stamps = [];

    /// <summary>The most streams to keep.</summary>
    private int _maxStreams;

    /// <summary>The most decoded bytes to keep.</summary>
    private long _maxBytes;

    /// <summary>The number of cached streams.</summary>
    private int _count;

    /// <summary>The decoded bytes held.</summary>
    private long _bytes;

    /// <summary>The use counter that orders slots.</summary>
    private long _clock;

    /// <summary>Initializes a new instance of the <see cref="ObjectStreamCache"/> class with the default budget.</summary>
    internal ObjectStreamCache()
        : this(DefaultMaxStreams, DefaultMaxBytes)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ObjectStreamCache"/> class.</summary>
    /// <param name="maxStreams">The most streams to keep; at least one is always kept.</param>
    /// <param name="maxBytes">The most decoded bytes to keep, apart from the newest stream.</param>
    internal ObjectStreamCache(int maxStreams, long maxBytes)
    {
        _maxStreams = Math.Max(maxStreams, 1);
        _maxBytes = maxBytes;
    }

    /// <summary>Gets the number of streams held.</summary>
    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }

    /// <summary>Gets the decoded bytes held.</summary>
    internal long Bytes
    {
        get
        {
            lock (_gate)
            {
                return _bytes;
            }
        }
    }

    /// <summary>Finds a cached stream and marks it as just used.</summary>
    /// <param name="container">The object stream's number.</param>
    /// <param name="index">The stream.</param>
    /// <returns><see langword="true"/> when the stream is cached.</returns>
    internal bool TryGetValue(int container, out ObjectStreamIndex index)
    {
        lock (_gate)
        {
            var slot = Find(container);
            if (slot < 0)
            {
                index = null!;
                return false;
            }

            _clock++;
            _stamps[slot] = _clock;
            index = _items[slot]!;
            return true;
        }
    }

    /// <summary>Caches a stream as the newest, then drops the least recently used ones until the budget holds.</summary>
    /// <param name="container">The object stream's number.</param>
    /// <param name="index">The stream.</param>
    internal void Add(int container, ObjectStreamIndex index)
    {
        lock (_gate)
        {
            var slot = Find(container);
            if (slot >= 0)
            {
                _bytes -= _items[slot]!.ByteSize;
            }
            else
            {
                if (_count == _maxStreams)
                {
                    RemoveOldest();
                }
                else if (_count == _items.Length)
                {
                    Grow();
                }

                slot = _count;
                _count++;
            }

            _clock++;
            _containers[slot] = container;
            _items[slot] = index;
            _stamps[slot] = _clock;
            _bytes += index.ByteSize;
            TrimToBytes();
        }
    }

    /// <summary>Changes the budget, dropping streams until it holds.</summary>
    /// <param name="maxStreams">The most streams to keep; at least one is always kept.</param>
    /// <param name="maxBytes">The most decoded bytes to keep, apart from the newest stream.</param>
    internal void SetLimits(int maxStreams, long maxBytes)
    {
        lock (_gate)
        {
            _maxStreams = Math.Max(maxStreams, 1);
            while (_count > _maxStreams)
            {
                RemoveOldest();
            }

            if (_items.Length > _maxStreams)
            {
                Array.Resize(ref _containers, _maxStreams);
                Array.Resize(ref _items, _maxStreams);
                Array.Resize(ref _stamps, _maxStreams);
            }

            _maxBytes = maxBytes;
            TrimToBytes();
        }
    }

    /// <summary>Drops every stream.</summary>
    internal void Clear()
    {
        lock (_gate)
        {
            Array.Clear(_items, 0, _count);
            _count = 0;
            _bytes = 0;
        }
    }

    /// <summary>Finds the slot holding a stream; the caller holds the lock.</summary>
    /// <param name="container">The object stream's number.</param>
    /// <returns>The slot; -1 when not cached.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Find(int container) => _containers.AsSpan(0, _count).IndexOf(container);

    /// <summary>Makes room for more streams in the slot arrays, up to the most kept; the caller holds the lock.</summary>
    private void Grow()
    {
        var slots = Math.Min(Math.Max(InitialSlots, _items.Length * GrowthFactor), _maxStreams);
        Array.Resize(ref _containers, slots);
        Array.Resize(ref _items, slots);
        Array.Resize(ref _stamps, slots);
    }

    /// <summary>Drops the oldest streams until the byte budget holds, keeping at least one; the caller holds the lock.</summary>
    private void TrimToBytes()
    {
        while (_bytes > _maxBytes && _count > 1)
        {
            RemoveOldest();
        }
    }

    /// <summary>Drops the least recently used stream; the caller holds the lock.</summary>
    private void RemoveOldest()
    {
        var oldest = 0;
        for (var i = 1; i < _count; i++)
        {
            if (_stamps[i] < _stamps[oldest])
            {
                oldest = i;
            }
        }

        _bytes -= _items[oldest]!.ByteSize;
        var last = _count - 1;
        _containers[oldest] = _containers[last];
        _items[oldest] = _items[last];
        _stamps[oldest] = _stamps[last];
        _items[last] = null;
        _count = last;
    }
}
