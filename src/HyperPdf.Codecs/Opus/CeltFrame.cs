// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>What one CELT frame decides about the sharing of its bits, kept so each stage of decoding can read what the stages before it found.</summary>
internal sealed class CeltFrame
{
    /// <summary>The most entries of the normalised bands: two channels of the longest frame, which has eight short blocks of 100 entries.</summary>
    internal const int MaxNormEntries = 1600;

    /// <summary>The most entries of the widest band of the longest frame.</summary>
    internal const int MaxScratchEntries = 176;

    /// <summary>Gets or sets the first band that is coded.</summary>
    internal int Start { get; set; }

    /// <summary>Gets or sets one past the last band that is coded.</summary>
    internal int End { get; set; }

    /// <summary>Gets or sets the channels coded in the frame.</summary>
    internal int Channels { get; set; }

    /// <summary>Gets or sets the log2 of the frame size in units of 2.5 ms.</summary>
    internal int FrameSize { get; set; }

    /// <summary>Gets or sets the allocation trim, 0 to 10.</summary>
    internal int Trim { get; set; }

    /// <summary>Gets or sets the bits available for the allocation, in eighths of a bit.</summary>
    internal int TotalBits { get; set; }

    /// <summary>Gets or sets the first band that is coded as intensity stereo.</summary>
    internal int Intensity { get; set; }

    /// <summary>Gets or sets a value indicating whether the two channels are coded apart.</summary>
    internal bool DualStereo { get; set; }

    /// <summary>Gets or sets the bits left over after the allocation, which the shapes may spend.</summary>
    internal int Balance { get; set; }

    /// <summary>Gets or sets the number of bands the allocation coded.</summary>
    internal int CodedBands { get; set; }

    /// <summary>Gets the normalised entries of the bands read so far, which later bands fold from, for both channels.</summary>
    internal float[] Norm { get; } = new float[CeltFrame.MaxNormEntries];

    /// <summary>Gets the space a band reshapes the entries it folds from in.</summary>
    internal float[] Scratch { get; } = new float[CeltFrame.MaxScratchEntries];

    /// <summary>Gets the bits the frame asked to add to or take from each band.</summary>
    internal int[] Offsets { get; } = new int[CeltEnergy.Bands];

    /// <summary>Gets the most bits each band can use.</summary>
    internal int[] Caps { get; } = new int[CeltEnergy.Bands];

    /// <summary>Gets the bits each band spends on its shape, in eighths.</summary>
    internal int[] Pulses { get; } = new int[CeltEnergy.Bands];

    /// <summary>Gets the fine energy bits of each band.</summary>
    internal int[] FineBits { get; } = new int[CeltEnergy.Bands];

    /// <summary>Gets a flag for each band that says whether its final fine energy bit is read in the second round.</summary>
    internal int[] FinePriority { get; } = new int[CeltEnergy.Bands];

    /// <summary>Gets the time-frequency change of each band.</summary>
    internal int[] TimeFrequency { get; } = new int[CeltEnergy.Bands];

    /// <summary>Gets the allocation of each band at the lower table row, then the difference to the upper row; scratch for the allocation.</summary>
    internal int[] LowerBits { get; } = new int[CeltEnergy.Bands];

    /// <summary>Gets the difference between the allocation at the upper table row and the lower; scratch for the allocation.</summary>
    internal int[] UpperBits { get; } = new int[CeltEnergy.Bands];

    /// <summary>Gets the bits below which a band is given no pulses; scratch for the allocation.</summary>
    internal int[] Thresholds { get; } = new int[CeltEnergy.Bands];

    /// <summary>Gets how far the trim tilts each band's allocation; scratch for the allocation.</summary>
    internal int[] TrimOffsets { get; } = new int[CeltEnergy.Bands];
}
