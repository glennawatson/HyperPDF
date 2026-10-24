// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.Mp3;

/// <summary>The side information for one channel of one granule: how its coefficients were coded, and what scales them.</summary>
[DebuggerDisplay("Mp3GranuleChannel: block {BlockType}, {BigValues} big values, gain {GlobalGain}")]
internal sealed class Mp3GranuleChannel
{
    /// <summary>The block type of short windows.</summary>
    internal const int ShortBlockType = 2;

    /// <summary>The number of Huffman regions.</summary>
    private const int RegionCount = 3;

    /// <summary>Gets or sets the bits of this granule's scale factors and Huffman data.</summary>
    internal int Part23Length { get; set; }

    /// <summary>Gets or sets the number of pairs of values coded with the big value tables.</summary>
    internal int BigValues { get; set; }

    /// <summary>Gets or sets the global gain, which scales every coefficient.</summary>
    internal int GlobalGain { get; set; }

    /// <summary>Gets or sets the code that says how many bits each scale factor takes.</summary>
    internal int ScalefacCompress { get; set; }

    /// <summary>Gets or sets a value indicating whether the granule uses a block type other than the normal long window.</summary>
    internal bool WindowSwitching { get; set; }

    /// <summary>Gets or sets the block type: 0 normal, 1 start, 2 short windows, 3 stop.</summary>
    internal int BlockType { get; set; }

    /// <summary>Gets or sets a value indicating whether the lowest two subbands use long windows while the rest use short ones.</summary>
    internal bool MixedBlock { get; set; }

    /// <summary>Gets the Huffman table of each region.</summary>
    internal int[] TableSelect { get; } = new int[RegionCount];

    /// <summary>Gets the extra gain of each short window, in steps of the gain.</summary>
    internal int[] SubblockGain { get; } = new int[RegionCount];

    /// <summary>Gets or sets the number of scale factor bands, less one, in the first region.</summary>
    internal int Region0Count { get; set; }

    /// <summary>Gets or sets the number of scale factor bands, less one, in the second region.</summary>
    internal int Region1Count { get; set; }

    /// <summary>Gets or sets a value indicating whether the scale factors of the upper bands are raised by a fixed table.</summary>
    internal bool Preflag { get; set; }

    /// <summary>Gets or sets the scale factor step: 0 for half steps, 1 for whole steps.</summary>
    internal int ScalefacScale { get; set; }

    /// <summary>Gets or sets the table of the small value quadruples: 0 or 1.</summary>
    internal int Count1Table { get; set; }

    /// <summary>Gets or sets the four bits that say which groups of scale factors repeat those of the first granule; bit 0 is the first group.</summary>
    internal int Scfsi { get; set; }

    /// <summary>Gets a value indicating whether the granule uses three short windows.</summary>
    internal bool IsShort => WindowSwitching && BlockType == ShortBlockType;
}
