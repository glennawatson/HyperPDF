// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H265;

/// <summary>A slice segment header (7.3.6.1). A dependent segment keeps the settings of the slice before it.</summary>
/// <param name="Segment">Where the segment sits.</param>
/// <param name="SliceType">0 for B, 1 for P and 2 for I.</param>
/// <param name="PicOutput">Whether the picture is output.</param>
/// <param name="PocLsb">The low bits of the picture order count.</param>
/// <param name="ShortTerm">The short-term reference picture set.</param>
/// <param name="LongTerm">The long-term reference pictures.</param>
/// <param name="TemporalMvp">Whether temporal motion vector prediction may be used.</param>
/// <param name="SaoLuma">Whether luma sample adaptive offset is on.</param>
/// <param name="SaoChroma">Whether chroma sample adaptive offset is on.</param>
/// <param name="Refs">The reference list settings.</param>
/// <param name="Weights">The explicit prediction weights, or <see langword="null"/>.</param>
/// <param name="Quant">The quantiser settings.</param>
/// <param name="Deblocking">The loop filter settings.</param>
[DebuggerDisplay("H265SliceHeader: type {SliceType} at {Segment.Address}")]
internal sealed record H265SliceHeader(
    H265SliceSegment Segment,
    int SliceType,
    bool PicOutput,
    int PocLsb,
    H265ShortTermRps ShortTerm,
    H265LongTermRef[] LongTerm,
    bool TemporalMvp,
    bool SaoLuma,
    bool SaoChroma,
    H265RefSettings Refs,
    H265PredWeights? Weights,
    H265SliceQuant Quant,
    H265SliceDeblocking Deblocking)
{
    /// <summary>Gets the pictures the current picture refers to: those of the sets that are marked used.</summary>
    internal int NumPicTotalCurr
    {
        get
        {
            var count = ShortTerm.UsedCount;
            foreach (var entry in LongTerm)
            {
                if (entry.Used)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
