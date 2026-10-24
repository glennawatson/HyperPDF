// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The quantisation indices a SILK frame carries as side information.</summary>
internal sealed class SilkIndices
{
    /// <summary>The most subframes in a frame.</summary>
    internal const int MaxSubframes = 4;

    /// <summary>The most coefficients of a line spectral frequency vector, plus the index of its first stage.</summary>
    internal const int MaxNlsfIndices = 17;

    /// <summary>Gets the gain index of each subframe.</summary>
    internal sbyte[] Gains { get; } = new sbyte[MaxSubframes];

    /// <summary>Gets the long-term prediction filter index of each subframe.</summary>
    internal sbyte[] LtpFilters { get; } = new sbyte[MaxSubframes];

    /// <summary>Gets the line spectral frequency indices: the first stage, then the residual of each coefficient.</summary>
    internal sbyte[] Nlsf { get; } = new sbyte[MaxNlsfIndices];

    /// <summary>Gets or sets the index of the first pitch lag.</summary>
    internal int LagIndex { get; set; }

    /// <summary>Gets or sets the index of the pitch contour.</summary>
    internal int ContourIndex { get; set; }

    /// <summary>Gets or sets the signal type: 0 for no voice activity, 1 for unvoiced, 2 for voiced.</summary>
    internal int SignalType { get; set; }

    /// <summary>Gets or sets the quantisation offset type, 0 or 1.</summary>
    internal int QuantOffsetType { get; set; }

    /// <summary>Gets or sets the interpolation factor of the line spectral frequencies, in quarters; 4 means none.</summary>
    internal int NlsfInterpolation { get; set; }

    /// <summary>Gets or sets the periodicity index of the long-term prediction.</summary>
    internal int PeriodicityIndex { get; set; }

    /// <summary>Gets or sets the scaling index of the long-term prediction.</summary>
    internal int LtpScaleIndex { get; set; }

    /// <summary>Gets or sets the seed of the pseudo random signs of the excitation.</summary>
    internal int Seed { get; set; }
}
