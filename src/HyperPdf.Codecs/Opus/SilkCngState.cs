// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The comfort noise state of the SILK layer: a smoothed estimate of the background, which is played when a packet is lost.</summary>
internal sealed class SilkCngState
{
    /// <summary>Gets the excitation samples the noise is made from, in Q14.</summary>
    internal int[] Excitation { get; } = new int[SilkConstants.MaxFrameLength];

    /// <summary>Gets the smoothed line spectral frequencies of the background, in Q15.</summary>
    internal short[] SmoothedFrequencies { get; } = new short[SilkConstants.MaxLpcOrder];

    /// <summary>Gets the state of the synthesis filter.</summary>
    internal int[] SynthesisState { get; } = new int[SilkConstants.MaxLpcOrder];

    /// <summary>Gets or sets the smoothed gain of the background, in Q16.</summary>
    internal int SmoothedGainQ16 { get; set; }

    /// <summary>Gets or sets the seed of the generator that picks excitation samples.</summary>
    internal int RandSeed { get; set; }

    /// <summary>Gets or sets the sampling rate the state is for, in kHz.</summary>
    internal int SampleRateKhz { get; set; }
}
