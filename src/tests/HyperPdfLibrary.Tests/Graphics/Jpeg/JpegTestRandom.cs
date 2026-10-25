// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>A small deterministic random source (xorshift), so test data is the same on every run.</summary>
internal sealed class JpegTestRandom
{
    /// <summary>The left shift of the first xorshift step.</summary>
    private const int ShiftA = 13;

    /// <summary>The right shift of the second xorshift step.</summary>
    private const int ShiftB = 17;

    /// <summary>The left shift of the third xorshift step.</summary>
    private const int ShiftC = 5;

    /// <summary>The state.</summary>
    private uint _state;

    /// <summary>Initializes a new instance of the <see cref="JpegTestRandom"/> class.</summary>
    /// <param name="seed">A non-zero seed.</param>
    internal JpegTestRandom(uint seed) => _state = seed;

    /// <summary>Gets a number in a range.</summary>
    /// <param name="minimum">The smallest value.</param>
    /// <param name="maximum">One more than the largest value.</param>
    /// <returns>The number.</returns>
    internal int Next(int minimum, int maximum)
    {
        _state ^= _state << ShiftA;
        _state ^= _state >> ShiftB;
        _state ^= _state << ShiftC;
        return minimum + (int)(_state % (uint)(maximum - minimum));
    }
}
