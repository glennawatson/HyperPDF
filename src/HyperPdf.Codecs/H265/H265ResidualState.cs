// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The state of reading the residual of one transform block: its scan, last position, coded sub-blocks and the context state that carries from one sub-block to the next.</summary>
internal sealed class H265ResidualState
{
    /// <summary>The sub-blocks along one side of the largest block.</summary>
    internal const int SubBlockSide = 8;

    /// <summary>The base 2 logarithm of the side of a sub-block.</summary>
    private const int SubBlockLog2 = 2;

    /// <summary>Gets the flags of the sub-blocks that hold coefficients, by row of eight.</summary>
    internal byte[] CodedSubBlocks { get; } = new byte[SubBlockSide * SubBlockSide];

    /// <summary>Gets or sets the base 2 logarithm of the block side.</summary>
    internal int Log2Size { get; set; }

    /// <summary>Gets or sets the colour component.</summary>
    internal int CIdx { get; set; }

    /// <summary>Gets or sets the scan: 0 diagonal, 1 horizontal, 2 vertical.</summary>
    internal int ScanIdx { get; set; }

    /// <summary>Gets or sets the index of the last sub-block, in scan order, that holds coefficients.</summary>
    internal int LastSubBlock { get; set; }

    /// <summary>Gets or sets the position in the last sub-block of the last coefficient, in scan order.</summary>
    internal int LastScanPos { get; set; }

    /// <summary>Gets or sets a value indicating whether the block skips the transform or bypasses quantisation, which changes several contexts.</summary>
    internal bool SkipLike { get; set; }

    /// <summary>Gets or sets a value indicating whether significance flags use the contexts of skipped blocks.</summary>
    internal bool TransformSkipContext { get; set; }

    /// <summary>Gets or sets a value indicating whether sign bit hiding is switched off for the block.</summary>
    internal bool NoSignHiding { get; set; }

    /// <summary>Gets or sets the Rice parameter state kind for persistent adaptation.</summary>
    internal int StatType { get; set; }

    /// <summary>Gets or sets the context state of the greater-than-1 flags that carries between sub-blocks.</summary>
    internal int Greater1Ctx { get; set; }

    /// <summary>Gets or sets a value indicating whether a sub-block with coefficients was read already.</summary>
    internal bool AnyCoded { get; set; }

    /// <summary>Gets or sets the last column holding a non-zero coefficient.</summary>
    internal int MaxX { get; set; }

    /// <summary>Gets or sets the last row holding a non-zero coefficient.</summary>
    internal int MaxY { get; set; }

    /// <summary>Gets or sets a value indicating whether the block uses the horizontal or vertical coefficient scan the contexts depend on.</summary>
    internal bool IsLuma => CIdx == 0;

    /// <summary>Gets the number of sub-blocks along one side of the block.</summary>
    internal int SubBlocksPerSide => 1 << (Log2Size - SubBlockLog2);

    /// <summary>Clears the state for a new block.</summary>
    internal void Reset()
    {
        CodedSubBlocks.AsSpan().Clear();
        Greater1Ctx = 1;
        AnyCoded = false;
        MaxX = 0;
        MaxY = 0;
    }
}
