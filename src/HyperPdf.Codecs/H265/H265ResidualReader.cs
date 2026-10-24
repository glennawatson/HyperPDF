// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Reads the residual of a transform block, residual_coding() (7.3.8.11): the flags, the last position, then each coded sub-block.</summary>
internal static class H265ResidualReader
{
    /// <summary>The scan index of the horizontal scan.</summary>
    private const int HorizontalScan = 1;

    /// <summary>The scan index of the vertical scan.</summary>
    private const int VerticalScan = 2;

    /// <summary>The first intra mode that uses the vertical scan.</summary>
    private const int VerticalScanFirst = 6;

    /// <summary>The last intra mode that uses the vertical scan.</summary>
    private const int VerticalScanLast = 14;

    /// <summary>The first intra mode that uses the horizontal scan.</summary>
    private const int HorizontalScanFirst = 22;

    /// <summary>The last intra mode that uses the horizontal scan.</summary>
    private const int HorizontalScanLast = 30;

    /// <summary>The smallest block side of the transform blocks that may use the mode dependent scans.</summary>
    private const int ScanLog2Max = 3;

    /// <summary>The number of coefficients in a sub-block.</summary>
    private const int SubBlockCoefficients = 16;

    /// <summary>The base 2 logarithm of the side of a sub-block, which is also the smallest transform block.</summary>
    private const int SubBlockLog2 = 2;

    /// <summary>The mask of a position inside a sub-block.</summary>
    private const int SubBlockMask = (1 << SubBlockLog2) - 1;

    /// <summary>The last prefix value that has no suffix.</summary>
    private const int LastPrefixNoSuffix = 3;

    /// <summary>The first context offset of the last position prefix of chroma blocks.</summary>
    private const int ChromaLastOffset = 15;

    /// <summary>The contexts that each block size adds for the luma last position prefix.</summary>
    private const int LastContextStep = 3;

    /// <summary>The shift of the block size terms of the luma last position contexts.</summary>
    private const int LastLumaShift = 2;

    /// <summary>The weight of the suffix in a last position with a prefix of 4 or more.</summary>
    private const int SuffixBase = 2;

    /// <summary>The statistics offset of luma blocks.</summary>
    private const int LumaStatOffset = 2;

    /// <summary>Gets a scan table.</summary>
    /// <param name="scanIdx">0 diagonal, 1 horizontal, 2 vertical.</param>
    /// <param name="log2Side">The base 2 logarithm of the side of the area scanned.</param>
    /// <returns>The positions in scan order.</returns>
    internal static ReadOnlySpan<H265ScanPosition> Scan(int scanIdx, int log2Side) => scanIdx switch
    {
        HorizontalScan => H265ScanOrder.Horizontal(log2Side),
        VerticalScan => H265ScanOrder.Vertical(log2Side),
        _ => H265ScanOrder.Diagonal(log2Side),
    };

    /// <summary>Reads the residual of a block into the slice context's coefficient array.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context; its coefficient array must be zero on entry.</param>
    /// <param name="block">Describes the block.</param>
    /// <returns>What was found besides the coefficients.</returns>
    internal static H265ResidualResult Read(ref CabacEngine engine, H265SliceContext context, in H265ResidualBlock block)
    {
        var state = context.Residual;
        state.Reset();
        state.Log2Size = block.Log2Size;
        state.CIdx = block.CIdx;
        state.ScanIdx = ScanIndexOf(context, block);
        var chromaCtx = block.CIdx > 0 ? 1 : 0;
        var skip = ReadTransformSkip(ref engine, context, block, chromaCtx);
        var rdpcm = ReadExplicitRdpcm(ref engine, context, block, skip, chromaCtx);
        ReadLastPosition(ref engine, state, block.CIdx, out var lastX, out var lastY);
        FindLastScanPosition(state, lastX, lastY);
        state.SkipLike = skip || block.Bypass;
        state.TransformSkipContext = context.Sps.Range.TransformSkipContext && state.SkipLike;
        rdpcm = ImplicitRdpcm(context, block, skip, rdpcm);
        state.NoSignHiding = block.Bypass || rdpcm != H265RdpcmMode.None;
        state.StatType = (block.CIdx == 0 ? LumaStatOffset : 0) + (state.SkipLike ? 1 : 0);
        for (var i = state.LastSubBlock; i >= 0; i--)
        {
            H265SubBlockReader.Read(ref engine, context, state, i);
        }

        return new(new H265BlockExtent(state.MaxX, state.MaxY), skip, rdpcm);
    }

    /// <summary>Reads transform_skip_flag when the block may skip the transform.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="block">Describes the block.</param>
    /// <param name="chromaCtx">1 for chroma, 0 for luma.</param>
    /// <returns>Whether the block skips the transform.</returns>
    private static bool ReadTransformSkip(ref CabacEngine engine, H265SliceContext context, in H265ResidualBlock block, int chromaCtx) =>
        context.Pps.Flags.TransformSkip && !block.Bypass && block.Log2Size <= context.Pps.Range.Log2MaxTransformSkipSize
        && engine.DecodeDecision(H265CabacContexts.TransformSkip + chromaCtx) == 1;

    /// <summary>Picks the coefficient scan of a block (7.4.9.11).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">Describes the block.</param>
    /// <returns>0 for the diagonal scan, 1 for horizontal and 2 for vertical.</returns>
    private static int ScanIndexOf(H265SliceContext context, in H265ResidualBlock block)
    {
        var small = block.Log2Size == SubBlockLog2
            || (block.Log2Size == ScanLog2Max && (block.CIdx == 0 || context.Sps.ChromaArrayType == ChromaSampling.Yuv444));
        if (!block.Intra || !small)
        {
            return 0;
        }

        if (block.PredMode is >= VerticalScanFirst and <= VerticalScanLast)
        {
            return VerticalScan;
        }

        return block.PredMode is >= HorizontalScanFirst and <= HorizontalScanLast ? HorizontalScan : 0;
    }

    /// <summary>Reads explicit_rdpcm_flag and its direction for an inter block that skips the transform.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="block">Describes the block.</param>
    /// <param name="skip">Whether the block skips the transform.</param>
    /// <param name="chromaCtx">1 for chroma, 0 for luma.</param>
    /// <returns>The residual DPCM mode.</returns>
    private static H265RdpcmMode ReadExplicitRdpcm(ref CabacEngine engine, H265SliceContext context, in H265ResidualBlock block, bool skip, int chromaCtx)
    {
        if (block.Intra || !context.Sps.Range.ExplicitRdpcm || !(skip || block.Bypass))
        {
            return H265RdpcmMode.None;
        }

        if (engine.DecodeDecision(H265CabacContexts.ExplicitRdpcm + chromaCtx) == 0)
        {
            return H265RdpcmMode.None;
        }

        return engine.DecodeDecision(H265CabacContexts.ExplicitRdpcmDir + chromaCtx) == 1 ? H265RdpcmMode.Vertical : H265RdpcmMode.Horizontal;
    }

    /// <summary>Applies the implicit residual DPCM of intra blocks that skip the transform or bypass quantisation (8.4.4.1).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">Describes the block.</param>
    /// <param name="skip">Whether the block skips the transform.</param>
    /// <param name="explicitMode">The mode read from the explicit flags.</param>
    /// <returns>The mode that applies.</returns>
    private static H265RdpcmMode ImplicitRdpcm(H265SliceContext context, in H265ResidualBlock block, bool skip, H265RdpcmMode explicitMode)
    {
        var implicitApplies = block.Intra && context.Sps.Range.ImplicitRdpcm && (skip || block.Bypass);
        return !implicitApplies ? explicitMode : block.PredMode switch
        {
            H265IntraModes.Horizontal => H265RdpcmMode.Horizontal,
            H265IntraModes.Vertical => H265RdpcmMode.Vertical,
            _ => explicitMode,
        };
    }

    /// <summary>Reads the position of the last significant coefficient (7.3.8.11).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="state">The residual state.</param>
    /// <param name="component">The colour component.</param>
    /// <param name="lastX">Receives the column.</param>
    /// <param name="lastY">Receives the row.</param>
    private static void ReadLastPosition(ref CabacEngine engine, H265ResidualState state, int component, out int lastX, out int lastY)
    {
        var log2 = state.Log2Size;
        var prefixX = ReadLastPrefix(ref engine, H265CabacContexts.LastSigCoeffX, log2, component);
        var prefixY = ReadLastPrefix(ref engine, H265CabacContexts.LastSigCoeffY, log2, component);
        var first = LastFromPrefix(ref engine, prefixX);
        var second = LastFromPrefix(ref engine, prefixY);

        // The vertical scan codes the column and row the other way round.
        var swapped = state.ScanIdx == VerticalScan;
        lastX = swapped ? second : first;
        lastY = swapped ? first : second;
    }

    /// <summary>Reads last_sig_coeff_x_prefix or last_sig_coeff_y_prefix (9.3.4.2.3).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="baseContext">The first context of the element.</param>
    /// <param name="log2">The base 2 logarithm of the block side.</param>
    /// <param name="component">The colour component.</param>
    /// <returns>The prefix.</returns>
    private static int ReadLastPrefix(ref CabacEngine engine, int baseContext, int log2, int component)
    {
        var offset = component == 0 ? (LastContextStep * (log2 - SubBlockLog2)) + ((log2 - 1) >> LastLumaShift) : ChromaLastOffset;
        var shift = component == 0 ? (log2 + 1) >> LastLumaShift : log2 - SubBlockLog2;
        var maximum = (log2 << 1) - 1;
        var value = 0;
        while (value < maximum && engine.DecodeDecision(baseContext + offset + (value >> shift)) == 1)
        {
            value++;
        }

        return value;
    }

    /// <summary>Turns a prefix, and the suffix bins that follow when it is long enough, into a coefficient position (7-74 to 7-77).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The position.</returns>
    private static int LastFromPrefix(ref CabacEngine engine, int prefix)
    {
        if (prefix <= LastPrefixNoSuffix)
        {
            return prefix;
        }

        var bits = (prefix >> 1) - 1;
        var suffix = H265Bins.ReadBypass(ref engine, bits);
        return ((1 << bits) * (SuffixBase + (prefix & 1))) + suffix;
    }

    /// <summary>Finds the sub-block and the position inside it of the last coefficient.</summary>
    /// <param name="state">The residual state.</param>
    /// <param name="lastX">The column of the last coefficient.</param>
    /// <param name="lastY">The row of the last coefficient.</param>
    private static void FindLastScanPosition(H265ResidualState state, int lastX, int lastY)
    {
        state.LastSubBlock = IndexOf(Scan(state.ScanIdx, state.Log2Size - SubBlockLog2), lastX >> SubBlockLog2, lastY >> SubBlockLog2);
        state.LastScanPos = IndexOf(Scan(state.ScanIdx, SubBlockLog2), lastX & SubBlockMask, lastY & SubBlockMask);
    }

    /// <summary>Finds a position in a scan.</summary>
    /// <param name="scan">The scan.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The index in scan order, or 0 when the position is not in the scan.</returns>
    private static int IndexOf(ReadOnlySpan<H265ScanPosition> scan, int x, int y)
    {
        for (var i = 0; i < scan.Length; i++)
        {
            if (scan[i].X == x && scan[i].Y == y)
            {
                return i;
            }
        }

        return 0;
    }
}
