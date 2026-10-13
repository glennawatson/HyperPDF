// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// A few idle working objects of one kind that a decoder keeps between decodes, so the next decode with the same owner
/// takes them instead of building them. The owner empties them when it is trimmed or closed.
/// </summary>
/// <typeparam name="T">The kind of object kept; disposing it releases everything it holds.</typeparam>
[DebuggerDisplay("ScratchObjects: {typeof(T).Name}")]
internal sealed class ScratchObjects<T> : ScratchPoolBase
    where T : class, IDisposable
{
    /// <summary>The idle objects kept at once, enough for the decodes that run together.</summary>
    private const int Slots = 4;

    /// <summary>The owner whose lifetime bounds the objects.</summary>
    private readonly ScratchPools _owner;

    /// <summary>The idle objects.</summary>
    private readonly ReusableSlots<T> _slots = new(Slots);

    /// <summary>Initializes a new instance of the <see cref="ScratchObjects{T}"/> class.</summary>
    /// <param name="owner">The owner whose lifetime bounds the objects.</param>
    internal ScratchObjects(ScratchPools owner) => _owner = owner;

    /// <summary>Takes an idle object.</summary>
    /// <returns>The object, now owned by the caller, or <see langword="null"/> when none is idle.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T? Take() => _slots.Take();

    /// <summary>Keeps an object the caller no longer uses, unless the owner has closed or every slot is full.</summary>
    /// <param name="item">The object, which the caller must not use again when it is kept.</param>
    /// <returns><see langword="true"/> when a slot kept the object; otherwise the caller disposes it.</returns>
    internal bool Offer(T item)
    {
        if (_owner.IsClosed || !_slots.Offer(item))
        {
            return false;
        }

        // The owner may have closed and emptied the slots between the check and the offer; empty them again so the item is not stranded.
        if (_owner.IsClosed)
        {
            Trim();
        }

        return true;
    }

    /// <summary>Disposes every idle object.</summary>
    internal override void Trim()
    {
        while (_slots.Take() is { } item)
        {
            item.Dispose();
        }
    }
}
