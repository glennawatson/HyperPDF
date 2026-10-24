// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>A short-term reference picture set: the pictures before and after the current one, by picture order count distance.</summary>
/// <param name="DeltaPocS0">The distances to the earlier pictures, each negative and further away than the one before.</param>
/// <param name="UsedS0">Whether the current picture refers to each earlier picture.</param>
/// <param name="DeltaPocS1">The distances to the later pictures, each positive and further away than the one before.</param>
/// <param name="UsedS1">Whether the current picture refers to each later picture.</param>
internal sealed record H265ShortTermRps(int[] DeltaPocS0, bool[] UsedS0, int[] DeltaPocS1, bool[] UsedS1)
{
    /// <summary>Gets the pictures in the set.</summary>
    internal int Count => DeltaPocS0.Length + DeltaPocS1.Length;

    /// <summary>Gets the pictures in the set that the current picture refers to.</summary>
    internal int UsedCount => CountUsed(UsedS0) + CountUsed(UsedS1);

    /// <summary>Counts the true flags.</summary>
    /// <param name="flags">The flags.</param>
    /// <returns>The count.</returns>
    private static int CountUsed(bool[] flags)
    {
        var count = 0;
        foreach (var flag in flags)
        {
            if (flag)
            {
                count++;
            }
        }

        return count;
    }
}
