// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>Sets the starting state of the CABAC contexts (clause 9.3.1.1 of ITU-T H.264).</summary>
internal static class H264CabacContexts
{
    /// <summary>The number of contexts.</summary>
    internal const int Count = 1024;

    /// <summary>The values kept for each context: m then n.</summary>
    private const int ValuesPerContext = 2;

    /// <summary>The largest quantiser the initialisation uses.</summary>
    private const int MaxQp = 51;

    /// <summary>The shift of the product of m and the quantiser.</summary>
    private const int ProductShift = 4;

    /// <summary>The smallest value of the pre-context state.</summary>
    private const int MinPreState = 1;

    /// <summary>The largest value of the pre-context state.</summary>
    private const int MaxPreState = 126;

    /// <summary>The largest probability state that has the most probable symbol 0.</summary>
    private const int LastZeroState = 63;

    /// <summary>The pre-context state that starts the states with the most probable symbol 1.</summary>
    private const int FirstOneState = 64;

    /// <summary>The shift of the probability state in a context byte.</summary>
    private const int StateShift = 1;

    /// <summary>Starts every context.</summary>
    /// <param name="contexts">Receives the states, one byte each: the probability state in the high bits and the most probable symbol in the low bit.</param>
    /// <param name="isIntra">Whether the slice is an I or SI slice, which have their own values.</param>
    /// <param name="cabacInitIdc">The slice's cabac_init_idc, 0 to 2, when it is not an intra slice.</param>
    /// <param name="sliceQp">The slice's luma quantiser.</param>
    internal static void Initialize(Span<byte> contexts, bool isIntra, int cabacInitIdc, int sliceQp)
    {
        var set = isIntra ? 0 : cabacInitIdc + 1;
        var qp = Math.Clamp(sliceQp, 0, MaxQp);
        var values = H264CabacData.InitialValues.Slice(set * Count * ValuesPerContext, Count * ValuesPerContext);
        for (var index = 0; index < Count; index++)
        {
            var m = (sbyte)values[index * ValuesPerContext];
            var n = (sbyte)values[(index * ValuesPerContext) + 1];
            var pre = Math.Clamp(((m * qp) >> ProductShift) + n, MinPreState, MaxPreState);
            contexts[index] = pre <= LastZeroState
                ? (byte)((LastZeroState - pre) << StateShift)
                : (byte)(((pre - FirstOneState) << StateShift) | 1);
        }
    }
}
