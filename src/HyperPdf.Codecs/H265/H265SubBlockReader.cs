// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Reads one 4x4 sub-block of a residual block: whether it is coded, which of its coefficients are significant, and then their levels.</summary>
internal static class H265SubBlockReader
{
    /// <summary>The base 2 logarithm of the side of a sub-block.</summary>
    private const int SubBlockLog2 = 2;

    /// <summary>The coefficients in a sub-block.</summary>
    private const int SubBlockCoefficients = 16;

    /// <summary>The context offset of chroma coded_sub_block_flag.</summary>
    private const int ChromaCodedOffset = 2;

    /// <summary>The luma significance contexts that precede the chroma ones.</summary>
    private const int LumaSigContexts = 27;

    /// <summary>The significance context of a skipped luma block.</summary>
    private const int SkipLumaContext = 42;

    /// <summary>The significance context of a skipped chroma block, before the chroma offset is added.</summary>
    private const int SkipChromaContext = 16;

    /// <summary>The context offset added to the first sub-block's contexts when it is not the DC sub-block of a luma block.</summary>
    private const int OuterSubBlockOffset = 3;

    /// <summary>The offset of the 8x8 luma contexts for the diagonal scan.</summary>
    private const int Luma8x8Diagonal = 9;

    /// <summary>The offset of the 8x8 luma contexts for the other scans.</summary>
    private const int Luma8x8Other = 15;

    /// <summary>The offset of the luma contexts of larger blocks.</summary>
    private const int LumaLarge = 21;

    /// <summary>The offset of the 8x8 chroma contexts.</summary>
    private const int Chroma8x8 = 9;

    /// <summary>The offset of the chroma contexts of larger blocks.</summary>
    private const int ChromaLarge = 12;

    /// <summary>The block size that has its own offsets.</summary>
    private const int Log2Eight = 3;

    /// <summary>The mask of a position inside a sub-block.</summary>
    private const int SubBlockMask = (1 << SubBlockLog2) - 1;

    /// <summary>Gets the significance context for each position of a 4x4 block (Table 9-50), with the unused last position repeated.</summary>
    private static ReadOnlySpan<byte> ContextMap4x4 => [0x00, 0x01, 0x04, 0x05, 0x02, 0x03, 0x04, 0x05, 0x06, 0x06, 0x08, 0x08, 0x07, 0x07, 0x08, 0x08];

    /// <summary>Gets the context of a coefficient from its place in the sub-block, for each combination of coded neighbouring sub-blocks: none, right, below, both (9.3.4.2.5).</summary>
    private static ReadOnlySpan<byte> NeighbourContexts =>
    [
        0x02, 0x01, 0x01, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x02, 0x02, 0x02, 0x02, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x02, 0x01, 0x00, 0x00, 0x02, 0x01, 0x00, 0x00, 0x02, 0x01, 0x00, 0x00, 0x02, 0x01, 0x00, 0x00,
        0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02,
    ];

    /// <summary>Reads a sub-block.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="state">The residual state.</param>
    /// <param name="index">The sub-block's index in scan order.</param>
    internal static void Read(ref CabacEngine engine, H265SliceContext context, H265ResidualState state, int index)
    {
        var subScan = H265ResidualReader.Scan(state.ScanIdx, state.Log2Size - SubBlockLog2);
        var subX = subScan[index].X;
        var subY = subScan[index].Y;
        var side = state.SubBlocksPerSide;
        var right = subX < side - 1 ? state.CodedSubBlocks[(subY * H265ResidualState.SubBlockSide) + subX + 1] : 0;
        var below = subY < side - 1 ? state.CodedSubBlocks[((subY + 1) * H265ResidualState.SubBlockSide) + subX] : 0;
        var inferDc = false;
        var coded = true;
        if (index < state.LastSubBlock && index > 0)
        {
            var ctxInc = Math.Min(right + below, 1) + (state.CIdx > 0 ? ChromaCodedOffset : 0);
            coded = engine.DecodeDecision(H265CabacContexts.CodedSubBlock + ctxInc) == 1;
            inferDc = true;
        }

        state.CodedSubBlocks[(subY * H265ResidualState.SubBlockSide) + subX] = (byte)(coded ? 1 : 0);
        if (!coded)
        {
            return;
        }

        var sub = new H265SubBlockPosition(subX, subY, index, right | (below << 1));
        var significant = ReadSignificance(ref engine, state, sub, inferDc);
        if (significant != 0)
        {
            H265CoeffLevels.Read(ref engine, context, state, sub, significant);
        }
    }

    /// <summary>Reads the significance flags of a sub-block.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="state">The residual state.</param>
    /// <param name="sub">Where the sub-block is.</param>
    /// <param name="inferDc">Whether the DC coefficient is inferred significant when no other coefficient is.</param>
    /// <returns>A mask with bit n set when the coefficient at scan position n is significant.</returns>
    private static int ReadSignificance(ref CabacEngine engine, H265ResidualState state, H265SubBlockPosition sub, bool inferDc)
    {
        var scan = H265ResidualReader.Scan(state.ScanIdx, SubBlockLog2);
        var mask = 0;
        var start = SubBlockCoefficients - 1;
        if (sub.Index == state.LastSubBlock)
        {
            mask = 1 << state.LastScanPos;
            start = state.LastScanPos - 1;
        }

        for (var n = start; n >= 0; n--)
        {
            if (n == 0 && inferDc)
            {
                mask |= 1;
                break;
            }

            var column = (sub.X << SubBlockLog2) + scan[n].X;
            var row = (sub.Y << SubBlockLog2) + scan[n].Y;
            if (engine.DecodeDecision(H265CabacContexts.SigCoeff + SignificanceContext(state, sub.PreviousCoded, column, row)) == 0)
            {
                continue;
            }

            mask |= 1 << n;
            inferDc = false;
        }

        return mask;
    }

    /// <summary>Works out the context increment of a significance flag (9.3.4.2.5).</summary>
    /// <param name="state">The residual state.</param>
    /// <param name="previousCoded">Bit 0 set when the sub-block to the right is coded, bit 1 when the one below is.</param>
    /// <param name="column">The column of the coefficient in the block.</param>
    /// <param name="row">The row of the coefficient in the block.</param>
    /// <returns>The increment.</returns>
    private static int SignificanceContext(H265ResidualState state, int previousCoded, int column, int row)
    {
        var luma = state.CIdx == 0;
        var sigCtx = BaseContext(state, previousCoded, column, row);
        return luma ? sigCtx : LumaSigContexts + sigCtx;
    }

    /// <summary>Works out the significance context before the chroma offset is added.</summary>
    /// <param name="state">The residual state.</param>
    /// <param name="previousCoded">Bit 0 set when the sub-block to the right is coded, bit 1 when the one below is.</param>
    /// <param name="column">The column of the coefficient in the block.</param>
    /// <param name="row">The row of the coefficient in the block.</param>
    /// <returns>The context.</returns>
    private static int BaseContext(H265ResidualState state, int previousCoded, int column, int row)
    {
        if (state.TransformSkipContext)
        {
            return state.CIdx == 0 ? SkipLumaContext : SkipChromaContext;
        }

        if (state.Log2Size == SubBlockLog2)
        {
            return ContextMap4x4[(row << SubBlockLog2) + column];
        }

        return column + row == 0 ? 0 : NeighbourContext(state, previousCoded, column, row);
    }

    /// <summary>Works out the context of a coefficient from the coded sub-blocks beside it and its place in its sub-block.</summary>
    /// <param name="state">The residual state.</param>
    /// <param name="previousCoded">Bit 0 set when the sub-block to the right is coded, bit 1 when the one below is.</param>
    /// <param name="column">The column of the coefficient in the block.</param>
    /// <param name="row">The row of the coefficient in the block.</param>
    /// <returns>The context before the chroma offset is added.</returns>
    private static int NeighbourContext(H265ResidualState state, int previousCoded, int column, int row)
    {
        var inner = ((row & SubBlockMask) << SubBlockLog2) + (column & SubBlockMask);
        var sigCtx = NeighbourContexts[(previousCoded * SubBlockCoefficients) + inner];
        if (state.CIdx != 0)
        {
            return sigCtx + (state.Log2Size == Log2Eight ? Chroma8x8 : ChromaLarge);
        }

        var outer = (column >> SubBlockLog2) + (row >> SubBlockLog2) > 0 ? OuterSubBlockOffset : 0;
        return sigCtx + outer + LumaSizeOffset(state);
    }

    /// <summary>Gets the offset of the luma significance contexts for the block size and scan.</summary>
    /// <param name="state">The residual state.</param>
    /// <returns>The offset.</returns>
    private static int LumaSizeOffset(H265ResidualState state)
    {
        if (state.Log2Size != Log2Eight)
        {
            return LumaLarge;
        }

        return state.ScanIdx == 0 ? Luma8x8Diagonal : Luma8x8Other;
    }
}
