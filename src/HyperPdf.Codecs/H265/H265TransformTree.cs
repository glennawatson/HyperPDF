// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Reads the transform tree of a coding unit (7.3.8.8).</summary>
internal static class H265TransformTree
{
    /// <summary>The smallest transform block that carries its own chroma flags for formats other than 4:4:4.</summary>
    private const int SmallestChromaLog2 = 2;

    /// <summary>The transform block size at which a 4:2:2 node carries two chroma flags even when it splits.</summary>
    private const int TwoFlagsSplitLog2 = 3;

    /// <summary>The nodes a split node divides into.</summary>
    private const int ChildCount = 4;

    /// <summary>The value from which the base 2 logarithm of the node size is taken to give the context of split_transform_flag.</summary>
    private const int SplitContextBase = 5;

    /// <summary>Reads the transform tree of a coding unit.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    internal static void Decode(ref CabacEngine engine, H265SliceContext context, in H265CodingUnit unit)
    {
        var root = new H265TreeNode(unit.X, unit.Y, unit.X, unit.Y, unit.Log2Size, 0, 0);
        Decode(ref engine, context, unit, root, default);
    }

    /// <summary>Reads a node of the transform tree and the nodes below it.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    /// <param name="node">The node.</param>
    /// <param name="parent">The chroma flags of the parent node.</param>
    private static void Decode(ref CabacEngine engine, H265SliceContext context, in H265CodingUnit unit, H265TreeNode node, H265ChromaCbf parent)
    {
        var split = ReadSplit(ref engine, context, unit, node);
        var chroma = ReadChromaFlags(ref engine, context, node, split, parent);
        if (split)
        {
            var half = 1 << (node.Log2Size - 1);
            for (var i = 0; i < ChildCount; i++)
            {
                H265TreeNode child = new(node.X + ((i & 1) * half), node.Y + ((i >> 1) * half), node.X, node.Y, node.Log2Size - 1, node.Depth + 1, i);
                Decode(ref engine, context, unit, child, chroma);
            }

            return;
        }

        var chromaHere = context.Sps.ChromaArrayType == ChromaSampling.Yuv444 || node.Log2Size > SmallestChromaLog2 ? chroma : parent;
        var cbfLuma = true;
        if (unit.Intra || node.Depth != 0 || chromaHere.Any)
        {
            cbfLuma = engine.DecodeDecision(H265CabacContexts.CbfLuma + (node.Depth == 0 ? 1 : 0)) == 1;
        }

        H265TransformUnit.Decode(ref engine, context, unit, node, new(cbfLuma, chromaHere));
    }

    /// <summary>Reads split_transform_flag, or infers it (7.4.9.8).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    /// <param name="node">The node.</param>
    /// <returns>Whether the node divides into four.</returns>
    private static bool ReadSplit(ref CabacEngine engine, H265SliceContext context, in H265CodingUnit unit, H265TreeNode node)
    {
        var blocks = context.Sps.Blocks;
        if (IsSplitCoded(blocks, unit, node))
        {
            return engine.DecodeDecision(H265CabacContexts.SplitTransform + SplitContextBase - node.Log2Size) == 1;
        }

        var interSplit = blocks.MaxDepthInter == 0 && !unit.Intra && unit.PartMode != H265PartMode.Part2Nx2N && node.Depth == 0;
        return node.Log2Size > blocks.MaxTbLog2 || (unit.IntraSplit && node.Depth == 0) || interSplit;
    }

    /// <summary>Tells whether split_transform_flag is coded for a node rather than inferred.</summary>
    /// <param name="blocks">The block size limits of the sequence.</param>
    /// <param name="unit">The coding unit.</param>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> when the flag is coded.</returns>
    private static bool IsSplitCoded(H265BlockSizes blocks, in H265CodingUnit unit, H265TreeNode node) =>
        node.Log2Size <= blocks.MaxTbLog2 && node.Log2Size > blocks.MinTbLog2 && node.Depth < unit.MaxTrafoDepth && !(unit.IntraSplit && node.Depth == 0);

    /// <summary>Reads the chroma coded block flags of a node.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="node">The node.</param>
    /// <param name="split">Whether the node divides into four.</param>
    /// <param name="parent">The chroma flags of the parent node.</param>
    /// <returns>The flags; all false when the node has none of its own.</returns>
    private static H265ChromaCbf ReadChromaFlags(ref CabacEngine engine, H265SliceContext context, H265TreeNode node, bool split, H265ChromaCbf parent)
    {
        var chromaType = context.Sps.ChromaArrayType;
        if (chromaType == ChromaSampling.Monochrome || (node.Log2Size <= SmallestChromaLog2 && chromaType != ChromaSampling.Yuv444))
        {
            return default;
        }

        var second = chromaType == ChromaSampling.Yuv422 && (!split || node.Log2Size == TwoFlagsSplitLog2);
        ReadComponentFlags(ref engine, node.Depth, node.Depth == 0 || parent.Cb0, second, out var cb0, out var cb1);
        ReadComponentFlags(ref engine, node.Depth, node.Depth == 0 || parent.Cr0, second, out var cr0, out var cr1);
        return new(cb0, cb1, cr0, cr1);
    }

    /// <summary>Reads the coded block flags of one chroma component of a node.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="depth">The depth of the node.</param>
    /// <param name="read">Whether the flags are coded for this node.</param>
    /// <param name="second">Whether a second flag follows for the lower block of 4:2:2 video.</param>
    /// <param name="upper">Receives the flag of the first block.</param>
    /// <param name="lower">Receives the flag of the second block.</param>
    private static void ReadComponentFlags(ref CabacEngine engine, int depth, bool read, bool second, out bool upper, out bool lower)
    {
        upper = read && engine.DecodeDecision(H265CabacContexts.CbfChroma + depth) == 1;
        lower = read && second && engine.DecodeDecision(H265CabacContexts.CbfChroma + depth) == 1;
    }
}
