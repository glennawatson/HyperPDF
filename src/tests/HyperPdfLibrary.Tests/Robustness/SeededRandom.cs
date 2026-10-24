// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>
/// A small deterministic generator (SplitMix64) for the fuzzer. It gives the same sequence for the same seed on every
/// runtime, which <see cref="Random"/> does not promise, and keeps the CA5394 rule satisfied.
/// </summary>
internal sealed class SeededRandom
{
    /// <summary>The amount added to the state on each step.</summary>
    private const ulong Increment = 0x9E3779B97F4A7C15UL;

    /// <summary>The first mixing multiplier.</summary>
    private const ulong MixOne = 0xBF58476D1CE4E5B9UL;

    /// <summary>The second mixing multiplier.</summary>
    private const ulong MixTwo = 0x94D049BB133111EBUL;

    /// <summary>The first mixing shift.</summary>
    private const int ShiftOne = 30;

    /// <summary>The second mixing shift.</summary>
    private const int ShiftTwo = 27;

    /// <summary>The final mixing shift.</summary>
    private const int ShiftThree = 31;

    /// <summary>The generator state.</summary>
    private ulong _state;

    /// <summary>Initializes a new instance of the <see cref="SeededRandom"/> class.</summary>
    /// <param name="seed">The seed.</param>
    internal SeededRandom(int seed) => _state = (ulong)seed;

    /// <summary>Gets a number from 0 up to, but not including, a limit.</summary>
    /// <param name="limit">The exclusive upper bound; must be positive.</param>
    /// <returns>The number.</returns>
    internal int Next(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return (int)(NextUInt64() % (ulong)limit);
    }

    /// <summary>Gets a number from a minimum up to, but not including, a limit.</summary>
    /// <param name="minimum">The inclusive lower bound.</param>
    /// <param name="limit">The exclusive upper bound; must be above the minimum.</param>
    /// <returns>The number.</returns>
    internal int Next(int minimum, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(limit, minimum);
        return minimum + Next(limit - minimum);
    }

    /// <summary>Steps the generator.</summary>
    /// <returns>The next 64 bits.</returns>
    private ulong NextUInt64()
    {
        _state = unchecked(_state + Increment);
        var z = _state;
        z = unchecked((z ^ (z >> ShiftOne)) * MixOne);
        z = unchecked((z ^ (z >> ShiftTwo)) * MixTwo);
        return z ^ (z >> ShiftThree);
    }
}
