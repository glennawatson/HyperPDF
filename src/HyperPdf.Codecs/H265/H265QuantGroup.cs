// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Tracks the quantisation groups: where the quantiser prediction starts again, and how the quantiser of a coding unit follows from it (8.6.1).</summary>
internal static class H265QuantGroup
{
    /// <summary>The range of the quantiser before the bit depth offset is added.</summary>
    private const int QpRange = 52;

    /// <summary>The quantiser steps that each bit of bit depth above 8 adds.</summary>
    private const int QpBitDepthScale = 6;

    /// <summary>The bit depth that has no quantiser offset.</summary>
    private const int BaseBitDepth = 8;

    /// <summary>Opens a new quantisation group when a quadtree block is big enough to start one.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="x0">The column of the block.</param>
    /// <param name="y0">The row of the block.</param>
    /// <param name="log2CbSize">The base 2 logarithm of the block side.</param>
    internal static void Begin(H265SliceContext context, int x0, int y0, int log2CbSize)
    {
        var ctbLog2 = context.Sps.Blocks.CtbLog2;
        if (context.Pps.Quant.CuQpDeltaEnabled && log2CbSize >= ctbLog2 - context.Pps.Quant.DiffCuQpDeltaDepth)
        {
            context.IsCuQpDeltaCoded = false;
            context.CuQpDeltaVal = 0;
            context.QpYPred = Predict(context, x0, y0);
            context.QpY = Combine(context);
        }

        if (context.Header.Quant.CuChromaQpOffsetEnabled && log2CbSize >= ctbLog2 - context.Pps.Range.DiffCuChromaQpOffsetDepth)
        {
            context.IsCuChromaQpOffsetCoded = false;
        }
    }

    /// <summary>Works out the quantiser of the current coding unit from the prediction and the coded change (8-283).</summary>
    /// <param name="context">The slice context.</param>
    /// <returns>The luma quantiser.</returns>
    internal static int Combine(H265SliceContext context)
    {
        var offset = QpBitDepthScale * (context.Sps.BitDepthLuma - BaseBitDepth);
        return ((context.QpYPred + context.CuQpDeltaVal + QpRange + (offset << 1)) % (QpRange + offset)) - offset;
    }

    /// <summary>Predicts the quantiser of a quantisation group from the groups to its left and above in the same coding tree block (8-282).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="groupX">The column of the group.</param>
    /// <param name="groupY">The row of the group.</param>
    /// <returns>The predicted quantiser.</returns>
    private static int Predict(H265SliceContext context, int groupX, int groupY)
    {
        var picture = context.Picture;
        var mask = (1 << context.Sps.Blocks.CtbLog2) - 1;
        var previous = context.LastQpY;
        var left = (groupX & mask) != 0 ? picture.QpY[picture.GridIndex(groupX - 1, groupY)] : previous;
        var above = (groupY & mask) != 0 ? picture.QpY[picture.GridIndex(groupX, groupY - 1)] : previous;
        return (left + above + 1) >> 1;
    }
}
