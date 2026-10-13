// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// A thread-safe cache keyed by object identity or value equality. Values are made outside the lock, so a slow decode
/// does not stall other threads; if two threads race, the first value stored wins and both use it. A null result is
/// cached too, so a damaged object is not decoded again.
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
[DebuggerDisplay("ObjectCache: {Count} entries")]
internal sealed class ObjectCache<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    /// <summary>Guards the map.</summary>
    private readonly Lock _gate = new();

    /// <summary>The key of the first entry, which is held inline so a cache of one entry makes no table.</summary>
    private TKey? _firstKey;

    /// <summary>The value of the first entry; null records a failed load.</summary>
    private TValue? _firstValue;

    /// <summary>Whether the first entry is held.</summary>
    private bool _hasFirst;

    /// <summary>The later cached values, made when a second key is stored; null records a failed load.</summary>
    private Dictionary<TKey, TValue?>? _map;

    /// <summary>Counts the clears, so a value made across one is not kept.</summary>
    private int _generation;

    /// <summary>Gets the number of cached entries.</summary>
    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return (_hasFirst ? 1 : 0) + (_map?.Count ?? 0);
            }
        }
    }

    /// <summary>Gets a value, making it when it is not cached.</summary>
    /// <typeparam name="TState">The type of the state given to the factory.</typeparam>
    /// <param name="key">The key.</param>
    /// <param name="state">The state given to the factory.</param>
    /// <param name="factory">Makes the value; may return null.</param>
    /// <returns>The value, or <see langword="null"/> when it could not be made.</returns>
    internal TValue? GetOrCreate<TState>(TKey key, TState state, Func<TKey, TState, TValue?> factory)
    {
        int generation;
        lock (_gate)
        {
            if (TryGet(key, out var existing))
            {
                return existing;
            }

            generation = _generation;
        }

        var created = factory(key, state);
        lock (_gate)
        {
            if (TryGet(key, out var winner))
            {
                return winner;
            }

            // A value made before a clear was made from what the clear dropped, such as a font that has since changed:
            // the caller uses it once and it is not kept.
            if (generation == _generation)
            {
                Store(key, created);
            }

            return created;
        }
    }

    /// <summary>Removes every entry, and stops values being made at the time from being kept.</summary>
    internal void Clear()
    {
        lock (_gate)
        {
            _generation++;
            _hasFirst = false;
            _firstKey = default;
            _firstValue = null;
            _map?.Clear();
        }
    }

    /// <summary>Finds an entry. The caller holds the gate.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">Receives the cached value, which is null for a failed load.</param>
    /// <returns><see langword="true"/> when the key is cached.</returns>
    private bool TryGet(TKey key, out TValue? value)
    {
        if (_hasFirst && EqualityComparer<TKey>.Default.Equals(_firstKey!, key))
        {
            value = _firstValue;
            return true;
        }

        if (_map is { } map)
        {
            return map.TryGetValue(key, out value);
        }

        value = null;
        return false;
    }

    /// <summary>Adds an entry that is not cached yet. The caller holds the gate.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, which is null for a failed load.</param>
    private void Store(TKey key, TValue? value)
    {
        if (!_hasFirst)
        {
            _firstKey = key;
            _firstValue = value;
            _hasFirst = true;
            return;
        }

        (_map ??= [])[key] = value;
    }
}
