// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Filters;

/// <summary>Cleared working objects retained only for their document or registered shared owner's lifetime.</summary>
/// <typeparam name="T">The working object type.</typeparam>
internal sealed class ScratchSlots<T> : ScratchPoolBase
    where T : class
{
    /// <summary>The idle objects retained for one owner.</summary>
    private const int Slots = 4;

    /// <summary>The owner that bounds retention.</summary>
    private readonly ScratchPools _owner;

    /// <summary>The idle objects.</summary>
    private readonly ReusableSlots<T> _slots = new(Slots);

    /// <summary>Initializes a new instance of the <see cref="ScratchSlots{T}"/> class.</summary>
    /// <param name="owner">The owner.</param>
    internal ScratchSlots(ScratchPools owner) => _owner = owner;

    /// <summary>Takes an exclusively owned working object.</summary>
    /// <returns>An idle object, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T? Take() => _slots.Take();

    /// <summary>Takes an idle object whose capacity or configuration matches the operation.</summary>
    /// <typeparam name="TState">The operation state type.</typeparam>
    /// <param name="state">The operation state.</param>
    /// <param name="matches">Checks an idle object.</param>
    /// <returns>A matching object, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T? Take<TState>(TState state, Func<T, TState, bool> matches) => _slots.Take(state, matches);

    /// <summary>Retains an already cleared object while the owner remains open.</summary>
    /// <param name="item">The cleared object.</param>
    /// <returns>Whether the owner retained it.</returns>
    internal bool Offer(T item)
    {
        if (_owner.IsClosed || !_slots.Offer(item))
        {
            return false;
        }

        if (_owner.IsClosed)
        {
            Trim();
        }

        return true;
    }

    /// <summary>Drops idle objects; active operations keep their own objects.</summary>
    internal override void Trim() => _slots.Clear();
}
