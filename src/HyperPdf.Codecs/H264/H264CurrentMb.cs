// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H264;

/// <summary>The macroblock being decoded: where it is, what it is, which neighbours can predict it, and its coefficients.</summary>
[DebuggerDisplay("H264CurrentMb: {Address} kind {Kind}")]
internal sealed class H264CurrentMb
{
    /// <summary>Gets the coefficients of the 16 luma blocks, 16 each, by raster position in the block.</summary>
    internal int[] Luma { get; } = new int[256];

    /// <summary>Gets the DC levels of an Intra 16x16 macroblock.</summary>
    internal int[] LumaDc { get; } = new int[16];

    /// <summary>Gets the DC levels of the two chroma components, 4 each.</summary>
    internal int[] ChromaDc { get; } = new int[8];

    /// <summary>Gets the coefficients of the chroma blocks, 4 blocks of 16 for each of the two components.</summary>
    internal int[] ChromaAc { get; } = new int[128];

    /// <summary>Gets the Intra 4x4 prediction modes of the macroblock's 16 blocks, in coding order.</summary>
    internal int[] Modes { get; } = new int[16];

    /// <summary>Gets or sets a value indicating whether the macroblock's luma uses 8x8 transforms (and, if intra, 8x8 prediction) rather than 4x4.</summary>
    internal bool Transform8x8 { get; set; }

    /// <summary>Gets or sets the address of the macroblock.</summary>
    internal int Address { get; set; }

    /// <summary>Gets or sets the macroblock's column.</summary>
    internal int X { get; set; }

    /// <summary>Gets or sets the macroblock's row.</summary>
    internal int Y { get; set; }

    /// <summary>Gets or sets the kind of macroblock.</summary>
    internal byte Kind { get; set; }

    /// <summary>Gets or sets the Intra 16x16 prediction mode.</summary>
    internal int Intra16x16Mode { get; set; }

    /// <summary>Gets or sets the chroma prediction mode.</summary>
    internal int ChromaMode { get; set; }

    /// <summary>Gets or sets the luma bits of the coded block pattern.</summary>
    internal int CbpLuma { get; set; }

    /// <summary>Gets or sets the chroma part of the coded block pattern, 0 to 2.</summary>
    internal int CbpChroma { get; set; }

    /// <summary>Gets or sets the neighbours of the macroblock that may predict it.</summary>
    internal H264Neighbours Neighbours { get; set; }

    /// <summary>Clears the coefficients before a macroblock is read.</summary>
    internal void ClearCoefficients()
    {
        Transform8x8 = false;
        Array.Clear(Luma);
        Array.Clear(LumaDc);
        Array.Clear(ChromaDc);
        Array.Clear(ChromaAc);
    }
}
