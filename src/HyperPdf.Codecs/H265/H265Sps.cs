// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H265;

/// <summary>A sequence parameter set: the picture size, sample format and coding tools that hold for a coded video sequence.</summary>
/// <param name="Id">The set's number.</param>
/// <param name="Profile">The profile and level.</param>
/// <param name="ChromaFormat">0 for monochrome, 1 for 4:2:0, 2 for 4:2:2 and 3 for 4:4:4.</param>
/// <param name="SeparatePlanes">Whether the three colour planes are coded as separate pictures.</param>
/// <param name="Width">The coded width in luma samples.</param>
/// <param name="Height">The coded height in luma samples.</param>
/// <param name="Crop">The part of the coded picture that is not shown.</param>
/// <param name="BitDepthLuma">The bits of a luma sample.</param>
/// <param name="BitDepthChroma">The bits of a chroma sample.</param>
/// <param name="Log2MaxPocLsb">The bits of the low part of the picture order count in a slice header.</param>
/// <param name="Ordering">How far decoding may run ahead of output, for the highest temporal sub-layer.</param>
/// <param name="Blocks">The sizes of the coding and transform blocks.</param>
/// <param name="Tools">The coding tools switched on.</param>
/// <param name="Pcm">The settings of pulse code modulation coding units.</param>
/// <param name="Scaling">The quantisation matrices, or <see langword="null"/> when scaling lists are off.</param>
/// <param name="ShortTermSets">The short-term reference picture sets a slice may pick.</param>
/// <param name="LongTerm">The long-term reference pictures a slice may pick.</param>
/// <param name="Range">The range extension tools.</param>
[DebuggerDisplay("H265Sps: {Width}x{Height} format {ChromaFormat}")]
internal sealed record H265Sps(
    int Id,
    H265ProfileTier Profile,
    int ChromaFormat,
    bool SeparatePlanes,
    int Width,
    int Height,
    VideoCrop Crop,
    int BitDepthLuma,
    int BitDepthChroma,
    int Log2MaxPocLsb,
    H265SubLayerOrdering Ordering,
    H265BlockSizes Blocks,
    H265SpsTools Tools,
    H265Pcm Pcm,
    H265ScalingLists? Scaling,
    List<H265ShortTermRps> ShortTermSets,
    H265LongTermSps LongTerm,
    H265RangeTools Range)
{
    /// <summary>Gets the format of the colour planes as the decoding process sees it: 0 when there is no chroma or the planes are separate.</summary>
    internal int ChromaArrayType => SeparatePlanes ? 0 : ChromaFormat;

    /// <summary>Gets the width of a coding tree block in luma samples.</summary>
    internal int CtbSize => 1 << Blocks.CtbLog2;

    /// <summary>Gets the coding tree blocks in a row of the picture.</summary>
    internal int WidthInCtbs => (Width + CtbSize - 1) >> Blocks.CtbLog2;

    /// <summary>Gets the rows of coding tree blocks in the picture.</summary>
    internal int HeightInCtbs => (Height + CtbSize - 1) >> Blocks.CtbLog2;

    /// <summary>Gets the horizontal subsampling of the chroma planes: 2 for 4:2:0 and 4:2:2, otherwise 1.</summary>
    internal int SubWidthC => ChromaSampling.SubWidth(ChromaFormat);

    /// <summary>Gets the vertical subsampling of the chroma planes: 2 for 4:2:0, otherwise 1.</summary>
    internal int SubHeightC => ChromaSampling.SubHeight(ChromaFormat);
}
