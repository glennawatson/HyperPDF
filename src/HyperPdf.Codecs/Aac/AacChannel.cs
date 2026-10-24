// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.Aac;

/// <summary>
/// One coded channel: what the frame being decoded says about it, the spectrum, and the overlap kept for the next frame. The frame
/// arrays are overwritten by every frame; only the overlap and the window shape live on between frames.
/// </summary>
[DebuggerDisplay("AacChannel: sequence {WindowSequence}, {MaxSfb} bands")]
internal sealed class AacChannel
{
    /// <summary>The coefficients of a long window.</summary>
    internal const int LongSize = 1024;

    /// <summary>The coefficients of a short window.</summary>
    internal const int ShortSize = 128;

    /// <summary>The short windows in a frame.</summary>
    internal const int ShortCount = 8;

    /// <summary>The largest number of bands a window has, rounded up to a power of two for indexing.</summary>
    internal const int BandStride = 64;

    /// <summary>The window sequence of eight short windows.</summary>
    internal const int EightShortSequence = 2;

    /// <summary>The most TNS filters a window has.</summary>
    internal const int TnsFilters = 3;

    /// <summary>The most coefficients of a TNS filter.</summary>
    internal const int TnsOrder = 32;

    /// <summary>The most pulses.</summary>
    internal const int PulseSlots = 4;

    /// <summary>Gets the quantised spectrum in the order the stream codes it.</summary>
    internal int[] Quantised { get; } = new int[LongSize];

    /// <summary>Gets the spectrum: one window of 1024 coefficients, or eight of 128 one after another.</summary>
    internal float[] Spectrum { get; } = new float[LongSize];

    /// <summary>Gets the second half of the last window, which the next frame adds to its first half.</summary>
    internal float[] Overlap { get; } = new float[LongSize];

    /// <summary>Gets the scalefactor of each band, by group then band.</summary>
    internal int[] ScaleFactors { get; } = new int[ShortCount * BandStride];

    /// <summary>Gets the codebook of each band, by group then band.</summary>
    internal byte[] Codebooks { get; } = new byte[ShortCount * BandStride];

    /// <summary>Gets the number of windows in each group.</summary>
    internal byte[] GroupLength { get; } = new byte[ShortCount];

    /// <summary>Gets the pulse positions, as offsets from the one before.</summary>
    internal int[] PulseOffset { get; } = new int[PulseSlots];

    /// <summary>Gets the pulse sizes.</summary>
    internal int[] PulseAmplitude { get; } = new int[PulseSlots];

    /// <summary>Gets the number of TNS filters of each window.</summary>
    internal byte[] TnsFilterCount { get; } = new byte[ShortCount];

    /// <summary>Gets the number of bands each TNS filter covers, by window then filter.</summary>
    internal byte[] TnsLength { get; } = new byte[ShortCount * TnsFilters];

    /// <summary>Gets the order of each TNS filter, by window then filter.</summary>
    internal byte[] TnsFilterOrder { get; } = new byte[ShortCount * TnsFilters];

    /// <summary>Gets whether each TNS filter runs from the top of its range down, by window then filter.</summary>
    internal bool[] TnsDownward { get; } = new bool[ShortCount * TnsFilters];

    /// <summary>Gets the quantised coefficients of each TNS filter, by window, filter and coefficient.</summary>
    internal sbyte[] TnsCoefficients { get; } = new sbyte[ShortCount * TnsFilters * TnsOrder];

    /// <summary>Gets the bits of the TNS coefficients before any compression, by window.</summary>
    internal byte[] TnsResolution { get; } = new byte[ShortCount];

    /// <summary>Gets or sets the window sequence: 0 long, 1 long start, 2 eight short, 3 long stop.</summary>
    internal int WindowSequence { get; set; }

    /// <summary>Gets or sets the shape of this frame's window: 0 sine, 1 Kaiser-Bessel derived.</summary>
    internal int WindowShape { get; set; }

    /// <summary>Gets or sets the shape of the previous frame's window.</summary>
    internal int PreviousShape { get; set; }

    /// <summary>Gets or sets the number of bands coded in each window.</summary>
    internal int MaxSfb { get; set; }

    /// <summary>Gets or sets the number of groups of windows.</summary>
    internal int GroupCount { get; set; }

    /// <summary>Gets or sets the global gain.</summary>
    internal int GlobalGain { get; set; }

    /// <summary>Gets or sets the number of pulses.</summary>
    internal int PulseCount { get; set; }

    /// <summary>Gets or sets the band the first pulse's offset counts from.</summary>
    internal int PulseStartBand { get; set; }

    /// <summary>Gets or sets a value indicating whether the frame has TNS.</summary>
    internal bool HasTns { get; set; }

    /// <summary>Gets a value indicating whether the frame is eight short windows.</summary>
    internal bool IsShort => WindowSequence == EightShortSequence;

    /// <summary>Gets the number of windows in the frame.</summary>
    internal int WindowCount => IsShort ? ShortCount : 1;

    /// <summary>Copies what the first channel of a pair says about the windows, for a pair that shares it.</summary>
    /// <param name="other">The channel to copy from.</param>
    internal void CopyWindowInfo(AacChannel other)
    {
        WindowSequence = other.WindowSequence;
        WindowShape = other.WindowShape;
        MaxSfb = other.MaxSfb;
        GroupCount = other.GroupCount;
        other.GroupLength.CopyTo(GroupLength, 0);
    }

    /// <summary>Forgets the sound kept from earlier frames.</summary>
    internal void Reset()
    {
        Array.Clear(Overlap);
        PreviousShape = 0;
    }
}
