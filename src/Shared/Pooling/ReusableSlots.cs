// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// A few slots that keep idle, reusable objects between uses. Taking and offering never allocate and never block: each
/// slot is claimed with one atomic exchange, so any number of threads can share the slots. When every slot is empty a
/// caller creates a new object; when every slot is full an offered object is dropped.
/// </summary>
/// <typeparam name="T">The kind of object kept.</typeparam>
[DebuggerDisplay("ReusableSlots: {_slots.Length} slots")]
internal sealed class ReusableSlots<T>
    where T : class
{
    /// <summary>The fewest slots, so a small machine still shares between a render thread and a helper.</summary>
    private const int MinSlots = 4;

    /// <summary>The most slots, which bounds the memory idle objects hold.</summary>
    private const int MaxSlots = 32;

    /// <summary>The kept objects; null marks an empty slot.</summary>
    private readonly T?[] _slots;

    /// <summary>Initializes a new instance of the <see cref="ReusableSlots{T}"/> class with a slot for each processor, within limits.</summary>
    internal ReusableSlots()
        : this(Math.Clamp(Environment.ProcessorCount, MinSlots, MaxSlots))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ReusableSlots{T}"/> class.</summary>
    /// <param name="count">The number of idle objects kept, for objects that are costly to hold.</param>
    internal ReusableSlots(int count) => _slots = new T?[count];

    /// <summary>Takes an idle object out of the slots.</summary>
    /// <returns>The object, now owned by the caller, or <see langword="null"/> when none is idle.</returns>
    internal T? Take()
    {
        var slots = _slots;
        for (var i = 0; i < slots.Length; i++)
        {
            var item = Volatile.Read(ref slots[i]);
            if (item is not null && ReferenceEquals(Interlocked.CompareExchange(ref slots[i], null, item), item))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Takes an idle object that matches an operation's requirements.</summary>
    /// <typeparam name="TState">The requirement's state.</typeparam>
    /// <param name="state">The state passed to the predicate without a closure.</param>
    /// <param name="matches">Tests whether an idle object can serve this operation.</param>
    /// <returns>The matching object, or null when none is idle.</returns>
    internal T? Take<TState>(TState state, Func<T, TState, bool> matches)
    {
        var slots = _slots;
        for (var i = 0; i < slots.Length; i++)
        {
            var item = Volatile.Read(ref slots[i]);
            if (item is not null && matches(item, state) && ReferenceEquals(Interlocked.CompareExchange(ref slots[i], null, item), item))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Keeps an object the caller no longer uses.</summary>
    /// <param name="item">The object, which the caller must not use again.</param>
    /// <returns><see langword="true"/> when a slot kept it; <see langword="false"/> when every slot is full.</returns>
    internal bool Offer(T item)
    {
        var slots = _slots;
        for (var i = 0; i < slots.Length; i++)
        {
            if (Volatile.Read(ref slots[i]) is null && Interlocked.CompareExchange(ref slots[i], item, null) is null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Drops idle objects without affecting objects held by active callers.</summary>
    internal void Clear()
    {
        for (var index = 0; index < _slots.Length; index++)
        {
            _ = Interlocked.Exchange(ref _slots[index], null);
        }
    }
}
