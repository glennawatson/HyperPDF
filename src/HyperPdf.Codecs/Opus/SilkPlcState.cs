// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>What the SILK layer remembers of its last good frame so a lost frame can be made up from it.</summary>
internal sealed class SilkPlcState
{
    /// <summary>Gets or sets the pitch lag to continue with, in Q8.</summary>
    internal int PitchLagQ8 { get; set; }

    /// <summary>Gets the long-term prediction filter to continue with, in Q14.</summary>
    internal short[] LtpCoefficients { get; } = new short[SilkConstants.LtpOrder];

    /// <summary>Gets the short-term prediction filter of the last good frame, in Q12.</summary>
    internal short[] PreviousLpc { get; } = new short[SilkConstants.MaxLpcOrder];

    /// <summary>Gets or sets a value indicating whether the frame before was lost.</summary>
    internal bool LastFrameLost { get; set; }

    /// <summary>Gets or sets the seed of the noise that stands in for the excitation.</summary>
    internal int RandSeed { get; set; }

    /// <summary>Gets or sets the scale of the noise, in Q14.</summary>
    internal int RandScaleQ14 { get; set; }

    /// <summary>Gets or sets the energy of the concealed signal.</summary>
    internal int ConcealedEnergy { get; set; }

    /// <summary>Gets or sets the shift the concealed energy was reduced by.</summary>
    internal int ConcealedEnergyShift { get; set; }

    /// <summary>Gets or sets the long-term prediction scale of the last good frame, in Q14.</summary>
    internal int PreviousLtpScaleQ14 { get; set; }

    /// <summary>Gets the gains of the last two subframes of the last good frame, in Q16.</summary>
    internal int[] PreviousGains { get; } = new int[2];

    /// <summary>Gets or sets the sampling rate the state is for, in kHz.</summary>
    internal int SampleRateKhz { get; set; }

    /// <summary>Gets or sets the subframes in the last good frame.</summary>
    internal int SubframeCount { get; set; }

    /// <summary>Gets or sets the samples in a subframe of the last good frame.</summary>
    internal int SubframeLength { get; set; }
}
