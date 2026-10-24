// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The arrays the synthesis of a SILK frame works in, kept so no frame has to allocate them.</summary>
internal sealed class SilkSynthesisScratch
{
    /// <summary>Gets the whitened past output, which the long-term prediction reads.</summary>
    internal short[] Whitened { get; } = new short[SilkConstants.MaxFrameLength];

    /// <summary>Gets the whitened past output and the excitation of this frame, scaled to Q15.</summary>
    internal int[] LongTermState { get; } = new int[SilkConstants.Pair * SilkConstants.MaxFrameLength];

    /// <summary>Gets the residual of a subframe, in Q14.</summary>
    internal int[] Residual { get; } = new int[SilkConstants.MaxSubframeLength];

    /// <summary>Gets the state of the short-term prediction filter followed by a subframe of its output, in Q14.</summary>
    internal int[] ShortTermState { get; } = new int[SilkConstants.MaxSubframeLength + SilkConstants.MaxLpcOrder];

    /// <summary>Gets the pulses of a frame, rounded up to a whole shell block.</summary>
    internal int[] Pulses { get; } = new int[SilkConstants.MaxFrameLength + SilkConstants.ShellBlockLength];
}
