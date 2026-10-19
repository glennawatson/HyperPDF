// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace HyperPdf.Benchmarks.Generator;

/// <summary>
/// An array, never changed after it is built, that compares by content, so a pipeline model holding one is equal to the model of the previous
/// run when nothing changed, and the incremental engine skips the stages after it.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>
    where T : IEquatable<T>
{
    /// <summary>The hash seed, a prime.</summary>
    private const int Seed = 17;

    /// <summary>The hash multiplier, a prime.</summary>
    private const int Multiplier = 31;

    /// <summary>The elements; <see langword="default"/> when the array was never set.</summary>
    private readonly T[]? _array;

    /// <summary>Initializes a new instance of the <see cref="EquatableArray{T}"/> struct.</summary>
    /// <param name="array">The elements, which the caller no longer changes.</param>
    internal EquatableArray(T[] array) => _array = array;

    /// <summary>Gets the number of elements.</summary>
    internal int Length => _array?.Length ?? 0;

    /// <summary>Gets a value indicating whether there are no elements.</summary>
    internal bool IsEmpty => Length == 0;

    /// <summary>Gets the elements.</summary>
    internal IReadOnlyList<T> Items => _array ?? System.Array.Empty<T>();

    /// <inheritdoc/>
    public bool Equals(EquatableArray<T> other)
    {
        if (Length != other.Length)
        {
            return false;
        }

        for (var i = 0; i < Length; i++)
        {
            if (!_array![i].Equals(other._array![i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = Seed;
        for (var i = 0; i < Length; i++)
        {
            hash = unchecked((hash * Multiplier) + _array![i].GetHashCode());
        }

        return hash;
    }
}
