// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The memory a band is read into and folded from.</summary>
internal readonly ref struct CeltBandSpans
{
    /// <summary>Initializes a new instance of the <see cref="CeltBandSpans"/> struct.</summary>
    /// <param name="shape">The band's entries, or its first channel when stereo.</param>
    /// <param name="second">The second channel's entries when stereo, or the second half of a split band, or empty.</param>
    /// <param name="lowband">The folded entries of the band below, or empty.</param>
    /// <param name="lowbandOut">Receives the entries later bands fold from, or empty.</param>
    /// <param name="scratch">Space for a reshaped copy of the folded entries.</param>
    internal CeltBandSpans(Span<float> shape, Span<float> second, Span<float> lowband, Span<float> lowbandOut, Span<float> scratch)
    {
        Shape = shape;
        Second = second;
        Lowband = lowband;
        LowbandOut = lowbandOut;
        Scratch = scratch;
    }

    /// <summary>Gets the band's entries, or its first channel when stereo.</summary>
    internal Span<float> Shape { get; }

    /// <summary>Gets the second channel's entries when stereo, or the second half of a split band, or empty.</summary>
    internal Span<float> Second { get; }

    /// <summary>Gets the folded entries of the band below, or empty.</summary>
    internal Span<float> Lowband { get; }

    /// <summary>Gets the memory that receives the entries later bands fold from, or empty.</summary>
    internal Span<float> LowbandOut { get; }

    /// <summary>Gets the space for a reshaped copy of the folded entries.</summary>
    internal Span<float> Scratch { get; }

    /// <summary>Gets a value indicating whether the band has a second channel.</summary>
    internal bool IsStereo => !Second.IsEmpty;
}
