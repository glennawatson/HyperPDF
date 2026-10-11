// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// An array pool for decoder scratch space. Small arrays come from the shared pool. Arrays of 256 KiB or more are kept
/// in a few slots, and only while its owner's budget has room. Sizes round up to one eighth steps, so a returned buffer
/// fits the next page of a similar size. The owner empties its pools when it closes.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
[DebuggerDisplay("ScratchPool: {_count} large arrays")]
internal sealed class ScratchPool<T>
{
    /// <summary>The arrays at least this many bytes long use the bounded pool.</summary>
    private const long LargeBytes = 256L * 1024;

    /// <summary>The large arrays kept at once.</summary>
    private const int Slots = 12;

    /// <summary>The bits of mantissa kept when a size is rounded up: one eighth steps.</summary>
    private const int SizeClassBits = 3;

    /// <summary>The share of the wanted length a kept array may exceed it by, as a shift: one quarter.</summary>
    private const int WasteShift = 2;

    /// <summary>The bytes in one element.</summary>
    private static readonly int ElementBytes = Unsafe.SizeOf<T>();

    /// <summary>Whether the elements hold references, so arrays must be cleared before they are kept.</summary>
    private static readonly bool HoldsReferences = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    /// <summary>The budget and lifetime owner of this element pool.</summary>
    private readonly ScratchPools _owner;

    /// <summary>Guards the slots.</summary>
    private readonly Lock _gate = new();

    /// <summary>The kept large arrays.</summary>
    private readonly T[]?[] _slots = new T[]?[Slots];

    /// <summary>The number of kept arrays.</summary>
    private int _count;

    /// <summary>Initializes a new instance of the <see cref="ScratchPool{T}"/> class.</summary>
    /// <param name="owner">The budget and lifetime owner.</param>
    internal ScratchPool(ScratchPools owner) => _owner = owner;

    /// <summary>Rents an array of at least the given length. Its contents are not defined.</summary>
    /// <param name="minimumLength">The least length.</param>
    /// <returns>The array; give it back with <see cref="Return(T[])"/>.</returns>
    internal T[] Rent(int minimumLength) => minimumLength <= 0 || (long)minimumLength * ElementBytes < LargeBytes
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
        if ((long)array.Length * ElementBytes < LargeBytes)
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
    internal void Trim()
    {
        lock (_gate)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] is not { } array)
                {
                    continue;
                }

                _owner.Release((long)array.Length * ElementBytes);
                _slots[i] = null;
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
        lock (_gate)
        {
            var best = -1;
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] is { } candidate && candidate.Length >= length && candidate.Length <= limit && (best < 0 || candidate.Length < _slots[best]!.Length))
                {
                    best = i;
                }
            }

            if (best < 0)
            {
                return null;
            }

            var array = _slots[best]!;
            _slots[best] = null;
            _count--;
            _owner.Release((long)array.Length * ElementBytes);
            return array;
        }
    }

    /// <summary>Keeps an array when there is a free slot and the budget has room.</summary>
    /// <param name="array">The array.</param>
    private void Store(T[] array)
    {
        lock (_gate)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] is not null)
                {
                    continue;
                }

                if (_owner.TryReserve((long)array.Length * ElementBytes))
                {
                    _slots[i] = array;
                    _count++;
                }

                return;
            }
        }
    }
}
