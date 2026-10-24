// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>A small deterministic generator (xorshift) for the pixels of test bitmaps.</summary>
internal sealed class Generator
{
    /// <summary>The first shift of the step.</summary>
    private const int ShiftLeft = 13;

    /// <summary>The second shift of the step.</summary>
    private const int ShiftRight = 17;

    /// <summary>The third shift of the step.</summary>
    private const int ShiftLeftAgain = 5;

    /// <summary>The state.</summary>
    private uint _state;

    /// <summary>Initializes a new instance of the <see cref="Generator"/> class.</summary>
    /// <param name="seed">The seed, which must not be zero.</param>
    internal Generator(int seed) => _state = (uint)seed;

    /// <summary>Gets the next value below a bound.</summary>
    /// <param name="bound">The exclusive upper bound.</param>
    /// <returns>A value from 0 to bound minus one.</returns>
    internal int Next(int bound)
    {
        _state ^= _state << ShiftLeft;
        _state ^= _state >> ShiftRight;
        _state ^= _state << ShiftLeftAgain;
        return (int)(_state % (uint)bound);
    }
}
