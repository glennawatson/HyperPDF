// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Reads a transform unit (7.3.8.10) and reconstructs its blocks: prediction for intra units, then the residual.</summary>
internal static class H265TransformUnit
{
    /// <summary>The smallest luma transform block, whose chroma is coded with its parent.</summary>
    private const int SmallestLog2 = 2;

    /// <summary>The longest prefix of cu_qp_delta_abs before the Exp-Golomb suffix.</summary>
    private const int QpDeltaPrefixMax = 5;

    /// <summary>The index of the last block of a group of four, which carries the shared chroma.</summary>
    private const int LastBlock = 3;

    /// <summary>The chroma blocks of each component in 4:2:2 video, which has twice as many rows as 4:2:0.</summary>
    private const int Yuv422Blocks = 2;

    /// <summary>Reads and reconstructs a transform unit.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    /// <param name="node">The transform tree node.</param>
    /// <param name="flags">The coded block flags.</param>
    internal static void Decode(ref CabacEngine engine, H265SliceContext context, in H265CodingUnit unit, H265TreeNode node, H265TransformFlags flags)
    {
        if (flags.Luma || flags.Chroma.Any)
        {
            ReadDeltaQp(ref engine, context);
            if (flags.Chroma.Any && !unit.Bypass)
            {
                ReadChromaQpOffset(ref engine, context);
            }
        }

        var size = 1 << node.Log2Size;
        H265CodingGrid.MarkEdges(context, node.X, node.Y, size, size, H265Picture.TransformEdgeLeft, H265Picture.TransformEdgeTop);
        if (flags.Luma)
        {
            H265CodingGrid.MarkCoded(context, node.X, node.Y, size);
        }

        var picture = context.Picture;
        var lumaMode = picture.IntraMode[picture.GridIndex(node.X, node.Y)];
        ReconstructBlock(ref engine, context, unit, new(0, node.X, node.Y, node.Log2Size, lumaMode), flags.Luma);
        if (context.HasChroma)
        {
            DecodeChroma(ref engine, context, unit, node, flags.Chroma);
        }
    }

    /// <summary>Reads, predicts and reconstructs the chroma blocks that belong to a transform unit.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    /// <param name="node">The transform tree node.</param>
    /// <param name="chroma">The chroma coded block flags.</param>
    private static void DecodeChroma(ref CabacEngine engine, H265SliceContext context, in H265CodingUnit unit, H265TreeNode node, H265ChromaCbf chroma)
    {
        var sps = context.Sps;
        var chromaType = sps.ChromaArrayType;

        // A 4x4 luma block has no chroma of its own: the last of the four codes the chroma of the whole 8x8 area.
        var own = chromaType == ChromaSampling.Yuv444 || node.Log2Size > SmallestLog2;
        if (!own && node.BlkIdx != LastBlock)
        {
            return;
        }

        var x = own ? node.X : node.XBase;
        var y = own ? node.Y : node.YBase;
        var log2 = ChromaLog2(chromaType, node, own);
        var mode = ChromaModeFor(context, unit, x, y);
        var blocks = chromaType == ChromaSampling.Yuv422 ? Yuv422Blocks : 1;
        for (var component = H265Component.Cb; component <= H265Component.Cr; component++)
        {
            for (var index = 0; index < blocks; index++)
            {
                ReconstructBlock(ref engine, context, unit, new(component, x / sps.SubWidthC, (y / sps.SubHeightC) + (index << log2), log2, mode), chroma.Of(component, index));
            }
        }
    }

    /// <summary>Gets the base 2 logarithm of the side of a chroma block of a transform unit.</summary>
    /// <param name="chromaType">The chroma format.</param>
    /// <param name="node">The transform tree node.</param>
    /// <param name="own">Whether the node has chroma blocks of its own.</param>
    /// <returns>The logarithm.</returns>
    private static int ChromaLog2(int chromaType, H265TreeNode node, bool own)
    {
        if (!own)
        {
            return SmallestLog2;
        }

        return chromaType == ChromaSampling.Yuv444 ? node.Log2Size : node.Log2Size - 1;
    }

    /// <summary>Finds the chroma prediction mode that applies at a luma position of a coding unit.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    /// <param name="x">The column of the transform unit's luma origin.</param>
    /// <param name="y">The row of the transform unit's luma origin.</param>
    /// <returns>The mode.</returns>
    private static int ChromaModeFor(H265SliceContext context, in H265CodingUnit unit, int x, int y)
    {
        if (!unit.Intra)
        {
            return 0;
        }

        if (context.Sps.ChromaArrayType != ChromaSampling.Yuv444 || !unit.IntraSplit)
        {
            return context.ChromaModes[0];
        }

        var half = 1 << (unit.Log2Size - 1);
        return context.ChromaModes[((y - unit.Y >= half ? 1 : 0) << 1) + (x - unit.X >= half ? 1 : 0)];
    }

    /// <summary>Predicts a block when its unit is intra coded, then reads and adds its residual when it has one.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    /// <param name="block">The block.</param>
    /// <param name="coded">Whether the block has coefficients.</param>
    private static void ReconstructBlock(ref CabacEngine engine, H265SliceContext context, in H265CodingUnit unit, H265TuBlock block, bool coded)
    {
        if (unit.Intra)
        {
            H265IntraPredictor.Predict(context, new(block.CIdx, block.X, block.Y, block.Log2Size, block.Mode, unit.Bypass));
        }

        if (!coded)
        {
            return;
        }

        var residual = new H265ResidualBlock(block.Log2Size, block.CIdx, unit.Intra, unit.Bypass, block.Mode);
        var result = H265ResidualReader.Read(ref engine, context, residual);
        H265Reconstruction.Add(context, residual, result, block.X, block.Y);
    }

    /// <summary>Reads the quantiser change of a quantisation group (7.3.8.14).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    private static void ReadDeltaQp(ref CabacEngine engine, H265SliceContext context)
    {
        if (!context.Pps.Quant.CuQpDeltaEnabled || context.IsCuQpDeltaCoded)
        {
            return;
        }

        context.IsCuQpDeltaCoded = true;
        var value = 0;
        while (value < QpDeltaPrefixMax && engine.DecodeDecision(H265CabacContexts.CuQpDeltaAbs + (value == 0 ? 0 : 1)) == 1)
        {
            value++;
        }

        if (value == QpDeltaPrefixMax)
        {
            value += H265Bins.ReadExpGolombBypass(ref engine, 0);
        }

        if (value != 0 && engine.DecodeBypass() == 1)
        {
            value = -value;
        }

        context.CuQpDeltaVal = value;
        context.QpY = H265QuantGroup.Combine(context);
    }

    /// <summary>Reads the chroma quantiser offset of a group (7.3.8.15).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    private static void ReadChromaQpOffset(ref CabacEngine engine, H265SliceContext context)
    {
        if (!context.Header.Quant.CuChromaQpOffsetEnabled || context.IsCuChromaQpOffsetCoded)
        {
            return;
        }

        var flag = engine.DecodeDecision(H265CabacContexts.CuChromaQpOffsetFlag) == 1;
        var range = context.Pps.Range;
        var index = 0;
        if (flag && range.CbQpOffsetList.Length > 1)
        {
            while (index < range.CbQpOffsetList.Length - 1 && engine.DecodeDecision(H265CabacContexts.CuChromaQpOffsetIdx) == 1)
            {
                index++;
            }
        }

        context.IsCuChromaQpOffsetCoded = true;
        context.CuQpOffsetCb = flag ? range.CbQpOffsetList[index] : 0;
        context.CuQpOffsetCr = flag ? range.CrQpOffsetList[index] : 0;
    }
}
