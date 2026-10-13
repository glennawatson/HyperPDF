// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// An array pool for decoder scratch space. Requests of up to 64 KiB come from the shared pool, which hands out powers of
/// two, so what it gives back is never above 64 KiB either. Larger arrays are kept in a few slots, and only while its
/// owner's budget has room: the shared pool would round them up into the large object heap, where its trimming drops
/// them again and again. Sizes round up to one eighth steps, so a returned buffer fits the next page of a similar size.
/// The owner empties its pools when it closes.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
[DebuggerDisplay("ScratchPool: {_count} large arrays")]
internal sealed class ScratchPool<T> : ScratchPoolBase
{
    /// <summary>The bytes up to which a request is served by the shared pool.</summary>
    private const long LargeBytes = 64L * 1024;

    /// <summary>The large arrays kept at once.</summary>
    private const int Slots = 12;

    /// <summary>The bits of mantissa kept when a size is rounded up: one eighth steps.</summary>
    private const int SizeClassBits = 3;

    /// <summary>The share of the wanted length a kept array may exceed it by, as a shift: one quarter.</summary>
    private const int WasteShift = 2;

    /// <summary>The bytes in one element.</summary>
    private static readonly int ElementBytes = Math.Max(Unsafe.SizeOf<T>(), 1);

    /// <summary>
    /// The most elements the shared pool serves: the largest power of two that fits <see cref="LargeBytes"/>. A request
    /// of that many or fewer gets an array of a power of two at most that long, and an array that long or shorter goes
    /// back to the shared pool, so a rented array always returns to the pool it came from.
    /// </summary>
    private static readonly int SharedElements = 1 << BitOperations.Log2((uint)(LargeBytes / ElementBytes));

    /// <summary>Whether the elements hold references, so arrays must be cleared before they are kept.</summary>
    private static readonly bool HoldsReferences = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    /// <summary>The budget and lifetime owner of this element pool.</summary>
    private readonly ScratchPools _owner;

    /// <summary>The kept large arrays; made when the first one is kept.</summary>
    private T[]?[]? _slots;

    /// <summary>The number of kept arrays.</summary>
    private int _count;

    /// <summary>Initializes a new instance of the <see cref="ScratchPool{T}"/> class.</summary>
    /// <param name="owner">The budget and lifetime owner.</param>
    internal ScratchPool(ScratchPools owner) => _owner = owner;

    /// <summary>Rents an array of at least the given length. Its contents are not defined.</summary>
    /// <param name="minimumLength">The least length.</param>
    /// <returns>The array; give it back with <see cref="Return(T[])"/>.</returns>
    internal T[] Rent(int minimumLength) => minimumLength <= SharedElements
        ? ArrayPool<T>.Shared.Rent(minimumLength)
        : Take(minimumLength) ?? Allocate(RoundUp(minimumLength));

    /// <summary>Returns a rented array.</summary>
    /// <param name="array">The array.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Return(T[] array) => Return(array, false);

    /// <summary>Returns a rented array.</summary>
    /// <param name="array">The array.</param>
    /// <param name="clearArray">Whether to clear the array first; arrays of references are always cleared.</param>
    internal void Return(T[] array, bool clearArray)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (array.Length <= SharedElements)
        {
            ArrayPool<T>.Shared.Return(array, clearArray);
            return;
        }

        if (clearArray || HoldsReferences)
        {
            Array.Clear(array);
        }

        Store(array);
    }

    /// <summary>Releases every kept array.</summary>
    internal override void Trim()
    {
        lock (_owner.Gate)
        {
            if (_slots is not { } slots)
            {
                return;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] is not { } array)
                {
                    continue;
                }

                _owner.Release((long)array.Length * ElementBytes);
                slots[i] = null;
            }

            _count = 0;
        }
    }

    /// <summary>Rounds a length up to the next one eighth step of its power of two.</summary>
    /// <param name="length">The wanted length.</param>
    /// <returns>The length to allocate.</returns>
    private static int RoundUp(int length)
    {
        var step = 1 << (BitOperations.Log2((uint)length) - SizeClassBits);
        var rounded = ((long)length + step - 1) & ~((long)step - 1);
        return (int)Math.Min(rounded, Array.MaxLength);
    }

    /// <summary>Allocates an array without clearing it when the elements allow.</summary>
    /// <param name="length">The length.</param>
    /// <returns>The array.</returns>
    private static T[] Allocate(int length) => HoldsReferences ? new T[length] : GC.AllocateUninitializedArray<T>(length);

    /// <summary>Takes the smallest kept array that fits without wasting more than a quarter.</summary>
    /// <param name="length">The wanted length.</param>
    /// <returns>The array, or <see langword="null"/> when none fits.</returns>
    private T[]? Take(int length)
    {
        var limit = length + (length >> WasteShift);
        lock (_owner.Gate)
        {
            if (_slots is not { } slots)
            {
                return null;
            }

            var best = -1;
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] is { } candidate && candidate.Length >= length && candidate.Length <= limit && (best < 0 || candidate.Length < slots[best]!.Length))
                {
                    best = i;
                }
            }

            if (best < 0)
            {
                return null;
            }

            var array = slots[best]!;
            slots[best] = null;
            _count--;
            _owner.Release((long)array.Length * ElementBytes);
            return array;
        }
    }

    /// <summary>Keeps an array when there is a free slot and the budget has room.</summary>
    /// <param name="array">The array.</param>
    private void Store(T[] array)
    {
        lock (_owner.Gate)
        {
            var slots = _slots ??= new T[]?[Slots];
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] is not null)
                {
                    continue;
                }

                if (_owner.TryReserve((long)array.Length * ElementBytes))
                {
                    slots[i] = array;
                    _count++;
                }

                return;
            }
        }
    }
}
