// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Reads a coding unit (7.3.8.5): its prediction mode, partitioning and prediction parameters, then its residual.</summary>
internal static class H265CodingUnits
{
    /// <summary>The smallest coding block that may be divided into four intra blocks of 4x4.</summary>
    private const int MinIntraSplitLog2 = 3;

    /// <summary>The context increment of the third bin of part_mode.</summary>
    private const int PartModeThirdBin = 2;

    /// <summary>The context increment of the bin of part_mode that tells whether an asymmetric partition follows.</summary>
    private const int PartModeAmpBin = 3;

    /// <summary>The luma prediction blocks of a coding unit divided into four.</summary>
    private const int NxNParts = 4;

    /// <summary>The largest value of the most probable mode index.</summary>
    private const int MpmIndexMax = 2;

    /// <summary>The bits of the remaining intra mode.</summary>
    private const int RemainingModeBits = 5;

    /// <summary>The bits of intra_chroma_pred_mode when it is not derived from luma.</summary>
    private const int ChromaModeBits = 2;

    /// <summary>Reads a coding unit.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="x0">The column of the unit's top-left luma sample.</param>
    /// <param name="y0">The row of the unit's top-left luma sample.</param>
    /// <param name="log2CbSize">The base 2 logarithm of the unit's side.</param>
    /// <param name="depth">The quadtree depth.</param>
    internal static void Decode(ref CabacEngine engine, H265SliceContext context, int x0, int y0, int log2CbSize, int depth)
    {
        var bypass = context.Pps.Flags.TransquantBypass && engine.DecodeDecision(H265CabacContexts.TransquantBypass) == 1;
        var skip = context.Header.SliceType != H265SliceType.I && ReadSkipFlag(ref engine, context, x0, y0);
        var intra = !skip && (context.Header.SliceType == H265SliceType.I || engine.DecodeDecision(H265CabacContexts.PredMode) == 1);
        var partMode = skip ? H265PartMode.Part2Nx2N : ReadPartMode(ref engine, context, intra, log2CbSize);
        context.QpY = H265QuantGroup.Combine(context);
        var unit = new H265CodingUnit(x0, y0, log2CbSize, intra, partMode, bypass, MaxTrafoDepth(context, intra, partMode));
        H265CodingGrid.Mark(context, unit, depth, skip);
        if (skip)
        {
            H265InterUnits.DecodeSkip(ref engine, context, unit);
        }
        else if (DecodePredictionAndResidualFlag(ref engine, context, unit))
        {
            H265TransformTree.Decode(ref engine, context, unit);
        }

        H265CodingGrid.Finish(context, unit);
    }

    /// <summary>Reads everything of a coding unit between its prediction mode and its transform tree.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    /// <returns><see langword="true"/> when a transform tree follows.</returns>
    private static bool DecodePredictionAndResidualFlag(ref CabacEngine engine, H265SliceContext context, H265CodingUnit unit) =>
        unit.Intra ? !ReadIntraParameters(ref engine, context, unit) : H265InterUnits.Decode(ref engine, context, unit);

    /// <summary>Works out how deep the transform tree of a coding unit may go.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="intra">Whether the unit is intra predicted.</param>
    /// <param name="partMode">The partitioning.</param>
    /// <returns>The depth.</returns>
    private static int MaxTrafoDepth(H265SliceContext context, bool intra, int partMode)
    {
        if (!intra)
        {
            return context.Sps.Blocks.MaxDepthInter;
        }

        return context.Sps.Blocks.MaxDepthIntra + (partMode == H265PartMode.NxN ? 1 : 0);
    }

    /// <summary>Reads cu_skip_flag, whose context depends on whether the left and upper units were skipped.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="x0">The column of the unit.</param>
    /// <param name="y0">The row of the unit.</param>
    /// <returns>The flag.</returns>
    private static bool ReadSkipFlag(ref CabacEngine engine, H265SliceContext context, int x0, int y0)
    {
        var picture = context.Picture;
        var increment = 0;
        if (H265Availability.IsAvailable(context, x0, y0, x0 - 1, y0) && (picture.Flags[picture.GridIndex(x0 - 1, y0)] & H265Picture.SkipFlag) != 0)
        {
            increment++;
        }

        if (H265Availability.IsAvailable(context, x0, y0, x0, y0 - 1) && (picture.Flags[picture.GridIndex(x0, y0 - 1)] & H265Picture.SkipFlag) != 0)
        {
            increment++;
        }

        return engine.DecodeDecision(H265CabacContexts.CuSkip + increment) == 1;
    }

    /// <summary>Reads part_mode (Table 9-45).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="intra">Whether the unit is intra predicted.</param>
    /// <param name="log2CbSize">The base 2 logarithm of the unit's side.</param>
    /// <returns>The partitioning.</returns>
    private static int ReadPartMode(ref CabacEngine engine, H265SliceContext context, bool intra, int log2CbSize)
    {
        var minCb = context.Sps.Blocks.MinCbLog2;
        if (intra)
        {
            if (log2CbSize != minCb)
            {
                return H265PartMode.Part2Nx2N;
            }

            return engine.DecodeDecision(H265CabacContexts.PartMode) == 1 ? H265PartMode.Part2Nx2N : H265PartMode.NxN;
        }

        if (engine.DecodeDecision(H265CabacContexts.PartMode) == 1)
        {
            return H265PartMode.Part2Nx2N;
        }

        return log2CbSize == minCb ? ReadMinSizePartMode(ref engine, log2CbSize) : ReadLargePartMode(ref engine, context);
    }

    /// <summary>Reads the rest of part_mode of an inter unit of the smallest size.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="log2CbSize">The base 2 logarithm of the unit's side.</param>
    /// <returns>The partitioning.</returns>
    private static int ReadMinSizePartMode(ref CabacEngine engine, int log2CbSize)
    {
        if (engine.DecodeDecision(H265CabacContexts.PartMode + 1) == 1)
        {
            return H265PartMode.Part2NxN;
        }

        if (log2CbSize == MinIntraSplitLog2)
        {
            return H265PartMode.PartNx2N;
        }

        return engine.DecodeDecision(H265CabacContexts.PartMode + PartModeThirdBin) == 1 ? H265PartMode.PartNx2N : H265PartMode.NxN;
    }

    /// <summary>Reads the rest of part_mode of an inter unit larger than the smallest size, where asymmetric partitions may occur.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <returns>The partitioning.</returns>
    private static int ReadLargePartMode(ref CabacEngine engine, H265SliceContext context)
    {
        var horizontal = engine.DecodeDecision(H265CabacContexts.PartMode + 1) == 1;
        if (!context.Sps.Tools.AmpEnabled)
        {
            return horizontal ? H265PartMode.Part2NxN : H265PartMode.PartNx2N;
        }

        if (engine.DecodeDecision(H265CabacContexts.PartMode + PartModeAmpBin) == 1)
        {
            return horizontal ? H265PartMode.Part2NxN : H265PartMode.PartNx2N;
        }

        var second = engine.DecodeBypass() == 1;
        if (horizontal)
        {
            return second ? H265PartMode.Part2NxnD : H265PartMode.Part2NxnU;
        }

        return second ? H265PartMode.PartnRx2N : H265PartMode.PartnLx2N;
    }

    /// <summary>Reads the intra prediction modes of a unit, or its pulse code modulation samples.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    /// <returns><see langword="true"/> when the unit holds PCM samples, so that no transform tree follows.</returns>
    private static bool ReadIntraParameters(ref CabacEngine engine, H265SliceContext context, H265CodingUnit unit)
    {
        if (H265Pcm.IsAllowed(context, unit) && engine.DecodeTerminate() == 1)
        {
            H265Pcm.Read(ref engine, context, unit);
            return true;
        }

        var parts = unit.PartMode == H265PartMode.NxN ? NxNParts : 1;
        var size = 1 << unit.Log2Size;
        var step = parts == 1 ? size : size >> 1;
        Span<bool> mpmFlags = stackalloc bool[NxNParts];
        for (var i = 0; i < parts; i++)
        {
            mpmFlags[i] = engine.DecodeDecision(H265CabacContexts.PrevIntraLumaPred) == 1;
        }

        for (var i = 0; i < parts; i++)
        {
            var x = unit.X + ((i & 1) * step);
            var y = unit.Y + ((i >> 1) * step);
            var value = mpmFlags[i] ? H265Bins.ReadTruncatedUnaryBypass(ref engine, MpmIndexMax) : H265Bins.ReadBypass(ref engine, RemainingModeBits);
            var mode = H265IntraModes.DeriveLuma(context, x, y, mpmFlags[i], value);
            H265CodingGrid.SetIntraMode(context, x, y, step, mode);
        }

        ReadChromaModes(ref engine, context, unit, parts, step);
        return false;
    }

    /// <summary>Reads the intra chroma prediction modes and derives the chroma modes.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    /// <param name="parts">The number of luma prediction blocks.</param>
    /// <param name="step">The side of a luma prediction block.</param>
    private static void ReadChromaModes(ref CabacEngine engine, H265SliceContext context, H265CodingUnit unit, int parts, int step)
    {
        var chromaType = context.Sps.ChromaArrayType;
        if (chromaType == ChromaSampling.Monochrome)
        {
            return;
        }

        var count = chromaType == ChromaSampling.Yuv444 ? parts : 1;
        var picture = context.Picture;
        for (var i = 0; i < count; i++)
        {
            var syntax = H265IntraModes.DerivedFromLuma;
            if (engine.DecodeDecision(H265CabacContexts.IntraChromaPredMode) == 1)
            {
                syntax = H265Bins.ReadBypass(ref engine, ChromaModeBits);
            }

            var luma = picture.IntraMode[picture.GridIndex(unit.X + ((i & 1) * step), unit.Y + ((i >> 1) * step))];
            context.ChromaModes[i] = H265IntraModes.DeriveChroma(chromaType, syntax, luma);
        }
    }
}
