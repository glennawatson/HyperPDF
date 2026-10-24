// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.Aac;

/// <summary>Finds the band tables of a sample rate. The lookups are built once and never change.</summary>
internal static class AacBands
{
    /// <summary>The band edges of the long window, by sample rate index.</summary>
    private static readonly short[][] LongByRate = BuildLong();

    /// <summary>The band edges of the short window, by sample rate index.</summary>
    private static readonly short[][] ShortByRate = BuildShort();

    /// <summary>Gets the sample rates a sample rate index names, in Hz.</summary>
    internal static ReadOnlySpan<int> SampleRates => [0x17700, 0x15888, 0xFA00, 0xBB80, 0xAC44, 0x7D00, 0x5DC0, 0x5622, 0x3E80, 0x2EE0, 0x2B11, 0x1F40];

    /// <summary>Gets the most bands a TNS filter of the long window may cover, by sample rate index.</summary>
    internal static ReadOnlySpan<byte> TnsLongBands => [0x1F, 0x1F, 0x22, 0x28, 0x2A, 0x33, 0x2E, 0x2E, 0x2A, 0x2A, 0x2A, 0x27];

    /// <summary>Gets the most bands a TNS filter of the short window may cover, by sample rate index.</summary>
    internal static ReadOnlySpan<byte> TnsShortBands => [0x9, 0x9, 0xA, 0xE, 0xE, 0xE, 0xE, 0xE, 0xE, 0xE, 0xE, 0xE];

    /// <summary>Finds the sample rate index of a rate.</summary>
    /// <param name="rate">The rate in Hz.</param>
    /// <returns>The index, or -1 when the rate is not one of the twelve AAC names.</returns>
    internal static int IndexOf(int rate)
    {
        var rates = SampleRates;
        for (var index = 0; index < rates.Length; index++)
        {
            if (rates[index] == rate)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Gets the band edges of the long window.</summary>
    /// <param name="rateIndex">The sample rate index.</param>
    /// <returns>The edges, from 0 to 1024.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short[] Long(int rateIndex) => LongByRate[rateIndex];

    /// <summary>Gets the band edges of the short window.</summary>
    /// <param name="rateIndex">The sample rate index.</param>
    /// <returns>The edges, from 0 to 128.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short[] Short(int rateIndex) => ShortByRate[rateIndex];

    /// <summary>Builds the long window table for each sample rate index.</summary>
    /// <returns>The tables.</returns>
    private static short[][] BuildLong()
    {
        var l96 = AacBandData.Long96.ToArray();
        var l64 = AacBandData.Long64.ToArray();
        var l48 = AacBandData.Long48.ToArray();
        var l32 = AacBandData.Long32.ToArray();
        var l24 = AacBandData.Long24.ToArray();
        var l16 = AacBandData.Long16.ToArray();
        var l8 = AacBandData.Long8.ToArray();
        return [l96, l96, l64, l48, l48, l32, l24, l24, l16, l16, l16, l8];
    }

    /// <summary>Builds the short window table for each sample rate index.</summary>
    /// <returns>The tables.</returns>
    private static short[][] BuildShort()
    {
        var s64 = AacBandData.Short64.ToArray();
        var s48 = AacBandData.Short48.ToArray();
        var s24 = AacBandData.Short24.ToArray();
        var s16 = AacBandData.Short16.ToArray();
        var s8 = AacBandData.Short8.ToArray();
        return [s64, s64, s64, s48, s48, s48, s24, s24, s16, s16, s16, s8];
    }
}
