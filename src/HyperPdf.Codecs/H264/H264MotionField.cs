// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H264;

/// <summary>The motion of every macroblock of a picture for one reference list: vectors per 4x4 block, references per 8x8 group.</summary>
[DebuggerDisplay("H264MotionField: {RefIdx.Length} groups")]
internal sealed class H264MotionField
{
    /// <summary>Initializes a new instance of the <see cref="H264MotionField"/> class.</summary>
    /// <param name="macroblocks">The macroblocks in the picture.</param>
    internal H264MotionField(int macroblocks)
    {
        MvX = new short[macroblocks * H264Picture.BlocksPerMb];
        MvY = new short[macroblocks * H264Picture.BlocksPerMb];
        MvdX = new byte[macroblocks * H264Picture.BlocksPerMb];
        MvdY = new byte[macroblocks * H264Picture.BlocksPerMb];
        RefIdx = new sbyte[macroblocks * H264Picture.GroupsPerMb];
        RefUid = new int[macroblocks * H264Picture.GroupsPerMb];
        Array.Fill(RefIdx, H264Picture.NoReference);
        Array.Fill(RefUid, H264Picture.NoReference);
    }

    /// <summary>Gets the horizontal motion vector of each 4x4 block of each macroblock, in quarter samples, by raster position in the macroblock.</summary>
    internal short[] MvX { get; }

    /// <summary>Gets the vertical motion vector of each 4x4 block of each macroblock, in quarter samples.</summary>
    internal short[] MvY { get; }

    /// <summary>Gets the size of the horizontal motion vector difference of each 4x4 block, limited to 255, which CABAC contexts need.</summary>
    internal byte[] MvdX { get; }

    /// <summary>Gets the size of the vertical motion vector difference of each 4x4 block, limited to 255.</summary>
    internal byte[] MvdY { get; }

    /// <summary>Gets the reference index of each 8x8 group of each macroblock, or -1 when the group does not use this list.</summary>
    internal sbyte[] RefIdx { get; }

    /// <summary>Gets the identity of the frame each 8x8 group refers to, or -1 when it does not use this list.</summary>
    internal int[] RefUid { get; }

    /// <summary>Returns the field to the state a new one has.</summary>
    internal void Recycle()
    {
        Array.Clear(MvX);
        Array.Clear(MvY);
        Array.Clear(MvdX);
        Array.Clear(MvdY);
        Array.Fill(RefIdx, H264Picture.NoReference);
        Array.Fill(RefUid, H264Picture.NoReference);
    }

    /// <summary>Marks a macroblock as using nothing from this list.</summary>
    /// <param name="address">The macroblock's address.</param>
    internal void Clear(int address)
    {
        Array.Fill(RefIdx, H264Picture.NoReference, address * H264Picture.GroupsPerMb, H264Picture.GroupsPerMb);
        Array.Fill(RefUid, H264Picture.NoReference, address * H264Picture.GroupsPerMb, H264Picture.GroupsPerMb);
        Array.Clear(MvX, address * H264Picture.BlocksPerMb, H264Picture.BlocksPerMb);
        Array.Clear(MvY, address * H264Picture.BlocksPerMb, H264Picture.BlocksPerMb);
        Array.Clear(MvdX, address * H264Picture.BlocksPerMb, H264Picture.BlocksPerMb);
        Array.Clear(MvdY, address * H264Picture.BlocksPerMb, H264Picture.BlocksPerMb);
    }
}
