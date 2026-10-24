// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H265;

/// <summary>Reads groups of bins that several syntax elements share: fixed length and unary codes of bypass bins, and Exp-Golomb suffixes.</summary>
internal static class H265Bins
{
    /// <summary>The largest Exp-Golomb order this reader follows, which keeps the value inside 32 bits.</summary>
    private const int MaxOrder = 31;

    /// <summary>Reads a fixed length code of bypass bins, most significant bit first (9.3.3.5).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="count">The number of bins.</param>
    /// <returns>The value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ReadBypass(ref CabacEngine engine, int count)
    {
        var value = 0;
        for (var i = 0; i < count; i++)
        {
            value = (value << 1) | engine.DecodeBypass();
        }

        return value;
    }

    /// <summary>Reads a truncated unary code of bypass bins (9.3.3.2 with no Rice parameter).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="maximum">The largest value, which has no terminating zero.</param>
    /// <returns>The value.</returns>
    internal static int ReadTruncatedUnaryBypass(ref CabacEngine engine, int maximum)
    {
        var value = 0;
        while (value < maximum && engine.DecodeBypass() == 1)
        {
            value++;
        }

        return value;
    }

    /// <summary>Reads a k-th order Exp-Golomb code of bypass bins (9.3.3.3).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="order">The order k.</param>
    /// <returns>The value.</returns>
    internal static int ReadExpGolombBypass(ref CabacEngine engine, int order)
    {
        var value = 0;
        var k = order;
        while (k < MaxOrder && engine.DecodeBypass() == 1)
        {
            value += 1 << k;
            k++;
        }

        return value + ReadBypass(ref engine, k);
    }
}
