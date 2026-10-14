// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>A small repeatable pseudo-random generator (xorshift32) for test data. It is not for security, and it gives the same numbers for the same seed on every platform.</summary>
internal sealed class SampleRandom
{
    /// <summary>The first xorshift shift.</summary>
    private const int ShiftA = 13;

    /// <summary>The second xorshift shift.</summary>
    private const int ShiftB = 17;

    /// <summary>The third xorshift shift.</summary>
    private const int ShiftC = 5;

    /// <summary>The shift that moves the well-mixed high byte of the state down to the low byte.</summary>
    private const int HighByteShift = 24;

    /// <summary>The state, never zero.</summary>
    private uint _state;

    /// <summary>Initializes a new instance of the <see cref="SampleRandom"/> class.</summary>
    /// <param name="seed">The seed; zero is replaced by one.</param>
    internal SampleRandom(uint seed) => _state = seed == 0 ? 1 : seed;

    /// <summary>Gets the next number.</summary>
    /// <returns>A number from 0 to <see cref="uint.MaxValue"/>.</returns>
    internal uint Next()
    {
        _state ^= _state << ShiftA;
        _state ^= _state >> ShiftB;
        _state ^= _state << ShiftC;
        return _state;
    }

    /// <summary>Gets the next number below a limit.</summary>
    /// <param name="limit">The exclusive upper limit, above zero.</param>
    /// <returns>A number from 0 to <paramref name="limit"/> minus one.</returns>
    internal int Next(int limit) => (int)(Next() % (uint)limit);

    /// <summary>Gets the next number in a range.</summary>
    /// <param name="minimum">The smallest value.</param>
    /// <param name="limit">The exclusive upper limit.</param>
    /// <returns>A number from <paramref name="minimum"/> to <paramref name="limit"/> minus one.</returns>
    internal int Next(int minimum, int limit) => minimum + Next(limit - minimum);

    /// <summary>Makes random bytes.</summary>
    /// <param name="length">The number of bytes.</param>
    /// <returns>The bytes.</returns>
    internal byte[] NextBytes(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)(Next() >> HighByteShift);
        }

        return bytes;
    }
}
