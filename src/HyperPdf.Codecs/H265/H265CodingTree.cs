// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Reads the coding quadtree and the coding units in it (7.3.8.4 and 7.3.8.5).</summary>
internal static class H265CodingTree
{
    /// <summary>Reads one level of the coding quadtree, and the levels below it.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="x0">The column of the block's top-left luma sample.</param>
    /// <param name="y0">The row of the block's top-left luma sample.</param>
    /// <param name="log2CbSize">The base 2 logarithm of the block side.</param>
    /// <param name="depth">The quadtree depth.</param>
    internal static void Decode(ref CabacEngine engine, H265SliceContext context, int x0, int y0, int log2CbSize, int depth)
    {
        var sps = context.Sps;
        var size = 1 << log2CbSize;
        var split = log2CbSize > sps.Blocks.MinCbLog2;
        if (x0 + size <= sps.Width && y0 + size <= sps.Height && log2CbSize > sps.Blocks.MinCbLog2)
        {
            split = ReadSplitFlag(ref engine, context, x0, y0, depth);
        }

        H265QuantGroup.Begin(context, x0, y0, log2CbSize);
        if (!split)
        {
            H265CodingUnits.Decode(ref engine, context, x0, y0, log2CbSize, depth);
            return;
        }

        var half = size >> 1;
        var x1 = x0 + half;
        var y1 = y0 + half;
        Decode(ref engine, context, x0, y0, log2CbSize - 1, depth + 1);
        if (x1 < sps.Width)
        {
            Decode(ref engine, context, x1, y0, log2CbSize - 1, depth + 1);
        }

        if (y1 < sps.Height)
        {
            Decode(ref engine, context, x0, y1, log2CbSize - 1, depth + 1);
        }

        if (x1 < sps.Width && y1 < sps.Height)
        {
            Decode(ref engine, context, x1, y1, log2CbSize - 1, depth + 1);
        }
    }

    /// <summary>Reads split_cu_flag, whose context depends on how deeply the left and upper blocks are split (Table 9-49).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="x0">The column of the block.</param>
    /// <param name="y0">The row of the block.</param>
    /// <param name="depth">The quadtree depth of the block.</param>
    /// <returns>The flag.</returns>
    private static bool ReadSplitFlag(ref CabacEngine engine, H265SliceContext context, int x0, int y0, int depth)
    {
        var picture = context.Picture;
        var increment = 0;
        if (H265Availability.IsAvailable(context, x0, y0, x0 - 1, y0) && picture.CtDepth[picture.GridIndex(x0 - 1, y0)] > depth)
        {
            increment++;
        }

        if (H265Availability.IsAvailable(context, x0, y0, x0, y0 - 1) && picture.CtDepth[picture.GridIndex(x0, y0 - 1)] > depth)
        {
            increment++;
        }

        return engine.DecodeDecision(H265CabacContexts.SplitCu + increment) == 1;
    }
}
