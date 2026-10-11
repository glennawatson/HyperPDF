// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// Owns a bounded set of reusable decoder arrays for one document or an explicitly shared drawing service. A document
/// closes its own pool without releasing arrays kept by another document or a shared service.
/// </summary>
[DebuggerDisplay("Scratch pools: {RetainedBytes} of {Budget} bytes")]
public sealed class ScratchPools
{
    /// <summary>The default retention limit: 48 MiB, about one decoded 600 dpi page.</summary>
    private const long DefaultBytes = 48L * 1024 * 1024;

    /// <summary>Guards pool registration, the budget and retained-byte count.</summary>
    private readonly Lock _gate = new();

    /// <summary>The lazily created pools by element type.</summary>
    private readonly Dictionary<Type, object> _pools = [];

    /// <summary>The trim action of each pool created by this owner.</summary>
    private readonly List<Action> _trimmers = [];

    /// <summary>The byte pool used most often by image decoders.</summary>
    private ScratchPool<byte>? _bytes;

    /// <summary>The integer pool used by image decoders.</summary>
    private ScratchPool<int>? _ints;

    /// <summary>The short pool used by image decoders.</summary>
    private ScratchPool<short>? _shorts;

    /// <summary>The Boolean pool used by image decoders.</summary>
    private ScratchPool<bool>? _bools;

    /// <summary>The most bytes this owner may retain.</summary>
    private long _budget = DefaultBytes;

    /// <summary>The bytes this owner retains.</summary>
    private long _retained;

    /// <summary>Whether document close has ended this owner's retention.</summary>
    private bool _closed;

    /// <summary>Gets the default retention limit.</summary>
    public static long DefaultBudget => DefaultBytes;

    /// <summary>Gets the bytes this owner currently keeps for reuse.</summary>
    public long RetainedBytes
    {
        get
        {
            lock (_gate)
            {
                return _retained;
            }
        }
    }

    /// <summary>Gets or sets the most bytes this owner keeps for reuse; lowering it trims its pools at once.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public long Budget
    {
        get
        {
            lock (_gate)
            {
                return _budget;
            }
        }

        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            bool trim;
            lock (_gate)
            {
                _budget = value;
                trim = _retained > value;
            }

            if (trim)
            {
                Trim();
            }
        }
    }

    /// <summary>Releases every buffer this owner keeps. Arrays in use are unaffected.</summary>
    public void Trim()
    {
        Action[] trimmers;
        lock (_gate)
        {
            trimmers = [.. _trimmers];
        }

        foreach (var trim in trimmers)
        {
            trim();
        }
    }

    /// <summary>Gets this owner's byte array pool.</summary>
    internal ScratchPool<byte> Bytes => Volatile.Read(ref _bytes) ?? Get<byte>();

    /// <summary>Gets this owner's integer array pool.</summary>
    internal ScratchPool<int> Ints => Volatile.Read(ref _ints) ?? Get<int>();

    /// <summary>Gets this owner's pool for one element type.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>The owned pool.</returns>
    internal ScratchPool<T> Get<T>()
    {
        if (GetCommon<T>() is { } common)
        {
            return common;
        }

        lock (_gate)
        {
            if (_pools.TryGetValue(typeof(T), out var existing))
            {
                return (ScratchPool<T>)existing;
            }

            var created = new ScratchPool<T>(this);
            _pools.Add(typeof(T), created);
            _trimmers.Add(created.Trim);
            PublishCommon(created);
            return created;
        }
    }

    /// <summary>Ends retention and releases idle arrays. Later returns are dropped.</summary>
    internal void Close()
    {
        lock (_gate)
        {
            _closed = true;
        }

        Trim();
    }

    /// <summary>Reserves room for one returned large array.</summary>
    /// <param name="bytes">The array's size.</param>
    /// <returns>Whether it fits this owner's budget.</returns>
    internal bool TryReserve(long bytes)
    {
        lock (_gate)
        {
            if (_closed || bytes > _budget - _retained)
            {
                return false;
            }

            _retained += bytes;
            return true;
        }
    }

    /// <summary>Releases room when a retained array is rented or dropped.</summary>
    /// <param name="bytes">The array's size.</param>
    internal void Release(long bytes)
    {
        lock (_gate)
        {
            _retained -= bytes;
        }
    }

    /// <summary>Reads a common element pool without taking the registration lock.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>The pool when it has been created.</returns>
    private ScratchPool<T>? GetCommon<T>()
    {
        if (typeof(T) == typeof(byte) && Volatile.Read(ref _bytes) is { } bytes)
        {
            return (ScratchPool<T>)(object)bytes;
        }

        if (typeof(T) == typeof(int) && Volatile.Read(ref _ints) is { } ints)
        {
            return (ScratchPool<T>)(object)ints;
        }

        if (typeof(T) == typeof(short) && Volatile.Read(ref _shorts) is { } shorts)
        {
            return (ScratchPool<T>)(object)shorts;
        }

        return typeof(T) == typeof(bool) && Volatile.Read(ref _bools) is { } bools
            ? (ScratchPool<T>)(object)bools
            : null;
    }

    /// <summary>Makes a new common element pool available for later reads.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="created">The pool registered under the lock.</param>
    private void PublishCommon<T>(ScratchPool<T> created)
    {
        if (typeof(T) == typeof(byte))
        {
            Volatile.Write(ref _bytes, (ScratchPool<byte>)(object)created);
        }
        else if (typeof(T) == typeof(int))
        {
            Volatile.Write(ref _ints, (ScratchPool<int>)(object)created);
        }
        else if (typeof(T) == typeof(short))
        {
            Volatile.Write(ref _shorts, (ScratchPool<short>)(object)created);
        }
        else if (typeof(T) == typeof(bool))
        {
            Volatile.Write(ref _bools, (ScratchPool<bool>)(object)created);
        }
    }
}
