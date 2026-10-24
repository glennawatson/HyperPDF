// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H264;

/// <summary>A decoded picture: its planes, and what the decoder keeps about each macroblock for the neighbours that follow it.</summary>
[DebuggerDisplay("H264Picture: {MbWidth}x{MbHeight} macroblocks")]
internal sealed class H264Picture
{
    /// <summary>The luma samples in a macroblock row.</summary>
    internal const int LumaSize = 16;

    /// <summary>The chroma samples in a macroblock row of 4:2:0 video.</summary>
    internal const int ChromaSize = 8;

    /// <summary>The coefficient counts kept per macroblock: 16 luma blocks and 4 for each chroma component.</summary>
    internal const int CountsPerMb = 24;

    /// <summary>The first of the chroma counts.</summary>
    internal const int ChromaCounts = 16;

    /// <summary>The 4x4 blocks in a macroblock, each with a motion vector.</summary>
    internal const int BlocksPerMb = 16;

    /// <summary>The 8x8 groups in a macroblock, each with a reference index.</summary>
    internal const int GroupsPerMb = 4;

    /// <summary>The reference index of a block that is intra coded or not decoded.</summary>
    internal const sbyte NoReference = -1;

    /// <summary>Initializes a new instance of the <see cref="H264Picture"/> class.</summary>
    /// <param name="columns">The width in macroblocks.</param>
    /// <param name="rows">The height in macroblocks.</param>
    internal H264Picture(int columns, int rows)
    {
        MbWidth = columns;
        MbHeight = rows;
        var count = columns * rows;
        Luma = new byte[count * LumaSize * LumaSize];
        Cb = new byte[count * ChromaSize * ChromaSize];
        Cr = new byte[count * ChromaSize * ChromaSize];
        SliceOfMb = new int[count];
        Kinds = new byte[count];
        IntraModes = new byte[count * LumaSize];
        Counts = new byte[count * CountsPerMb];
        Qp = new sbyte[count];
        Cbp = new byte[count];
        ChromaModes = new byte[count];
        DcFlags = new byte[count];
        Field0 = new(count);
        Field1 = new(count);
        Skipped = new bool[count];
        Transform8x8 = new bool[count];
        DirectOnly = new bool[count];
        DirectGroups = new byte[count];
    }

    /// <summary>Gets the width in macroblocks.</summary>
    internal int MbWidth { get; }

    /// <summary>Gets the height in macroblocks.</summary>
    internal int MbHeight { get; }

    /// <summary>Gets the bytes in a row of the luma plane.</summary>
    internal int LumaStride => MbWidth * LumaSize;

    /// <summary>Gets the bytes in a row of a chroma plane.</summary>
    internal int ChromaStride => MbWidth * ChromaSize;

    /// <summary>Gets the luma plane.</summary>
    internal byte[] Luma { get; }

    /// <summary>Gets the Cb plane.</summary>
    internal byte[] Cb { get; }

    /// <summary>Gets the Cr plane.</summary>
    internal byte[] Cr { get; }

    /// <summary>Gets the number of the slice each macroblock belongs to, or -1 for a macroblock not yet decoded.</summary>
    internal int[] SliceOfMb { get; }

    /// <summary>Gets the kind of each macroblock.</summary>
    internal byte[] Kinds { get; }

    /// <summary>Gets the Intra 4x4 prediction mode of each 4x4 block of each macroblock, by raster position in the macroblock.</summary>
    internal byte[] IntraModes { get; }

    /// <summary>Gets the number of non-zero coefficients of each block of each macroblock.</summary>
    internal byte[] Counts { get; }

    /// <summary>Gets the luma quantiser of each macroblock.</summary>
    internal sbyte[] Qp { get; }

    /// <summary>Gets the coded block pattern of each macroblock: the luma bits, then the chroma value shifted left by four.</summary>
    internal byte[] Cbp { get; }

    /// <summary>Gets the intra chroma prediction mode of each macroblock.</summary>
    internal byte[] ChromaModes { get; }

    /// <summary>Gets which DC blocks of each macroblock had coded_block_flag set: bit 0 luma, bit 1 Cb, bit 2 Cr.</summary>
    internal byte[] DcFlags { get; }

    /// <summary>Gets the motion of every macroblock for reference list 0.</summary>
    internal H264MotionField Field0 { get; }

    /// <summary>Gets the motion of every macroblock for reference list 1.</summary>
    internal H264MotionField Field1 { get; }

    /// <summary>Gets the horizontal motion vector of each 4x4 block of each macroblock for list 0, in quarter samples, by raster position in the macroblock.</summary>
    internal short[] MvX => Field0.MvX;

    /// <summary>Gets the vertical motion vector of each 4x4 block of each macroblock for list 0, in quarter samples.</summary>
    internal short[] MvY => Field0.MvY;

    /// <summary>Gets the list 0 reference index of each 8x8 group of each macroblock, or -1 when the group is intra coded.</summary>
    internal sbyte[] RefIdx => Field0.RefIdx;

    /// <summary>Gets the identity of the frame each 8x8 group of each macroblock refers to in list 0, or -1 when it is intra coded.</summary>
    internal int[] RefUid => Field0.RefUid;

    /// <summary>Gets the size of the horizontal list 0 motion vector difference of each 4x4 block of each macroblock, limited to 255.</summary>
    internal byte[] MvdX => Field0.MvdX;

    /// <summary>Gets the size of the vertical list 0 motion vector difference of each 4x4 block of each macroblock, limited to 255.</summary>
    internal byte[] MvdY => Field0.MvdY;

    /// <summary>Gets or sets the identity of this frame among the frames of the stream.</summary>
    internal int Uid { get; set; }

    /// <summary>Gets or sets the picture order count of the frame.</summary>
    internal int Poc { get; set; }

    /// <summary>Gets or sets the offset of the Cb quantiser from the luma quantiser, which the deblocking filter needs.</summary>
    internal int ChromaQpOffsetCb { get; set; }

    /// <summary>Gets or sets the offset of the Cr quantiser from the luma quantiser, which the deblocking filter needs.</summary>
    internal int ChromaQpOffsetCr { get; set; }

    /// <summary>Gets the deblocking filter settings of each slice, in the order the slices were decoded.</summary>
    internal List<H264SliceFilter> SliceFilters { get; } = [];

    /// <summary>Gets a value for each macroblock saying whether it was skipped.</summary>
    internal bool[] Skipped { get; }

    /// <summary>Gets a value for each macroblock saying whether its luma uses the 8x8 transform.</summary>
    internal bool[] Transform8x8 { get; }

    /// <summary>Gets a value for each macroblock saying whether it is a B_Skip or B_Direct_16x16 macroblock.</summary>
    internal bool[] DirectOnly { get; }

    /// <summary>Gets which 8x8 groups of each macroblock got their motion by direct prediction: bit 0 is the first group.</summary>
    internal byte[] DirectGroups { get; }

    /// <summary>Marks a macroblock as having no motion, as an intra macroblock does, and as not skipped.</summary>
    /// <param name="address">The macroblock's address.</param>
    internal void ClearMotion(int address)
    {
        Skipped[address] = false;
        Transform8x8[address] = false;
        DirectOnly[address] = false;
        DirectGroups[address] = 0;
        Field0.Clear(address);
        Field1.Clear(address);
    }

    /// <summary>Returns the picture to the state a new one has, so a picture nothing refers to any more can be decoded into again.</summary>
    internal void Recycle()
    {
        Array.Clear(Luma);
        Array.Clear(Cb);
        Array.Clear(Cr);
        Array.Clear(IntraModes);
        Array.Clear(Counts);
        Array.Clear(Qp);
        Array.Clear(Cbp);
        Array.Clear(ChromaModes);
        Array.Clear(DcFlags);
        Array.Clear(Skipped);
        Array.Clear(Transform8x8);
        Array.Clear(DirectOnly);
        Array.Clear(DirectGroups);
        Field0.Recycle();
        Field1.Recycle();
        SliceFilters.Clear();
        Uid = 0;
        Poc = 0;
        ChromaQpOffsetCb = 0;
        ChromaQpOffsetCr = 0;
        ResetMacroblocks();
    }

    /// <summary>Marks every macroblock as not decoded.</summary>
    internal void ResetMacroblocks()
    {
        Array.Fill(SliceOfMb, -1);
        Array.Clear(Kinds);
    }
}
