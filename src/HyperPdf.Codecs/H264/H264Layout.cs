// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The sizes of the blocks a macroblock is made of, and the quantiser constants that go with them.</summary>
internal static class H264Layout
{
    /// <summary>The 4x4 luma blocks in a macroblock.</summary>
    internal const int LumaBlocks = 16;

    /// <summary>The 4x4 blocks along one side of a macroblock's luma.</summary>
    internal const int BlocksPerRow = 4;

    /// <summary>The samples in a 4x4 block.</summary>
    internal const int BlockSamples = 16;

    /// <summary>The samples along one side of a 4x4 block.</summary>
    internal const int BlockSide = 4;

    /// <summary>The 4x4 blocks in the chroma of one component of a macroblock of 4:2:0 video.</summary>
    internal const int ChromaBlocks = 4;

    /// <summary>The 4x4 blocks along one side of the chroma of a macroblock of 4:2:0 video.</summary>
    internal const int ChromaBlocksPerRow = 2;

    /// <summary>The chroma components.</summary>
    internal const int Components = 2;

    /// <summary>The samples along one side of a macroblock's luma.</summary>
    internal const int MbLuma = 16;

    /// <summary>The samples along one side of a macroblock's chroma in 4:2:0 video.</summary>
    internal const int MbChroma = 8;

    /// <summary>The coefficients of a block whose DC value is coded separately.</summary>
    internal const int AcCoefficients = 15;

    /// <summary>The steps of the quantiser that double the scale.</summary>
    internal const int QpPerOctave = 6;

    /// <summary>The values the quantiser can have.</summary>
    internal const int QpRange = 52;

    /// <summary>The bits of the remaining Intra 4x4 prediction mode.</summary>
    internal const int RemainingModeBits = 3;

    /// <summary>The index of the last block in a row or column of luma blocks.</summary>
    internal const int LastBlock = 3;

    /// <summary>The bits in a sample.</summary>
    internal const int SampleBits = 8;
}
