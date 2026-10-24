// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>A repeatable sequence of numbers, so a generated test case can be found again.</summary>
/// <param name="state">The starting state, which must not be zero.</param>
public sealed class NumberSequence(uint state)
{
    /// <summary>The first shift of the sequence.</summary>
    private const int ShiftLeft = 13;

    /// <summary>The second shift of the sequence.</summary>
    private const int ShiftRight = 17;

    /// <summary>The third shift of the sequence.</summary>
    private const int ShiftLeftAgain = 5;

    /// <summary>The state of the sequence.</summary>
    private uint _state = state;

    /// <summary>Gets the next number below a limit.</summary>
    /// <param name="limit">The limit.</param>
    /// <returns>A number from zero up to but not including the limit.</returns>
    public int Next(int limit)
    {
        _state ^= _state << ShiftLeft;
        _state ^= _state >> ShiftRight;
        _state ^= _state << ShiftLeftAgain;
        return (int)(_state % (uint)limit);
    }
}
