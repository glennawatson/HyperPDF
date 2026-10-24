// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The prediction and gain parameters of a SILK frame, worked out from its indices.</summary>
internal sealed class SilkFrameParameters
{
    /// <summary>Gets the pitch lag of each subframe.</summary>
    internal int[] PitchLags { get; } = new int[SilkConstants.MaxSubframes];

    /// <summary>Gets the gain of each subframe, in Q16.</summary>
    internal int[] Gains { get; } = new int[SilkConstants.MaxSubframes];

    /// <summary>Gets the short-term prediction filters, in Q12: one for the first half of the frame then one for the second.</summary>
    internal short[] Predictors { get; } = new short[SilkConstants.Pair * SilkConstants.MaxLpcOrder];

    /// <summary>Gets the long-term prediction filter of each subframe, in Q14.</summary>
    internal short[] LtpCoefficients { get; } = new short[SilkConstants.LtpOrder * SilkConstants.MaxSubframes];

    /// <summary>Gets or sets the scale of the long-term prediction, in Q14.</summary>
    internal int LtpScaleQ14 { get; set; }
}
