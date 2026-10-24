// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H265;

/// <summary>Reads the prediction units of an inter coding unit (7.3.8.6), derives their motion and predicts them.</summary>
internal static class H265InterUnits
{
    /// <summary>The prediction block width plus height that allows only uni-directional prediction.</summary>
    private const int NoBiSize = 12;

    /// <summary>The inter_pred_idc value that selects both lists.</summary>
    private const int PredBi = 2;

    /// <summary>The context increment of the second bin of inter_pred_idc.</summary>
    private const int InterPredSecondBin = 4;

    /// <summary>The prediction blocks of a unit divided into four.</summary>
    private const int QuarterParts = 4;

    /// <summary>The prediction blocks of a unit divided into two.</summary>
    private const int HalfParts = 2;

    /// <summary>The bins of merge_idx that the context coded bin and the unary end take off the bypass coded length.</summary>
    private const int MergeIdxBypassReduction = 2;

    /// <summary>The contexts of the leading bins of ref_idx.</summary>
    private const int RefIdxContexts = 2;

    /// <summary>The Exp-Golomb order of the motion vector difference magnitude.</summary>
    private const int MvdOrder = 1;

    /// <summary>The smallest magnitude that the extra bins of a motion vector difference code.</summary>
    private const int MvdExtraBase = 2;

    /// <summary>Reads and predicts the prediction units of an inter coding unit, and the rqt_root_cbf that follows.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    /// <returns><see langword="true"/> when a transform tree follows.</returns>
    internal static bool Decode(ref CabacEngine engine, H265SliceContext context, in H265CodingUnit unit)
    {
        var count = PartCount(unit.PartMode);
        var firstMerged = false;
        for (var index = 0; index < count; index++)
        {
            var block = new H265PuBlock(unit, PartArea(unit, index), index);
            var merged = DecodeBlock(ref engine, context, block, false);
            firstMerged = index == 0 ? merged : firstMerged;
        }

        return (unit.PartMode == H265PartMode.Part2Nx2N && firstMerged) || engine.DecodeDecision(H265CabacContexts.RqtRootCbf) == 1;
    }

    /// <summary>Reads and predicts a skipped coding unit.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void DecodeSkip(ref CabacEngine engine, H265SliceContext context, in H265CodingUnit unit) =>
        DecodeBlock(ref engine, context, new(unit, PartArea(unit, 0), 0), true);

    /// <summary>Gets the number of prediction blocks of a partitioning.</summary>
    /// <param name="partMode">The partitioning.</param>
    /// <returns>1, 2 or 4.</returns>
    private static int PartCount(int partMode) => partMode switch
    {
        H265PartMode.Part2Nx2N => 1,
        H265PartMode.NxN => QuarterParts,
        _ => HalfParts,
    };

    /// <summary>Gets the area of a prediction block of a coding unit.</summary>
    /// <param name="unit">The coding unit.</param>
    /// <param name="index">The block's index.</param>
    /// <returns>The block's position and size in luma samples.</returns>
    private static BlockRect PartArea(H265CodingUnit unit, int index)
    {
        var size = 1 << unit.Log2Size;
        var half = size >> 1;
        var quarter = size >> 2;
        var x = unit.X;
        var y = unit.Y;
        return unit.PartMode switch
        {
            H265PartMode.Part2NxN => new(x, y + (index * half), size, half),
            H265PartMode.PartNx2N => new(x + (index * half), y, half, size),
            H265PartMode.NxN => new(x + ((index & 1) * half), y + ((index >> 1) * half), half, half),
            H265PartMode.Part2NxnU => index == 0 ? new(x, y, size, quarter) : new(x, y + quarter, size, size - quarter),
            H265PartMode.Part2NxnD => index == 0 ? new(x, y, size, size - quarter) : new(x, y + size - quarter, size, quarter),
            H265PartMode.PartnLx2N => index == 0 ? new(x, y, quarter, size) : new(x + quarter, y, size - quarter, size),
            H265PartMode.PartnRx2N => index == 0 ? new(x, y, size - quarter, size) : new(x + size - quarter, y, quarter, size),
            _ => new(x, y, size, size),
        };
    }

    /// <summary>Reads one prediction unit, stores its motion and predicts its samples.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The prediction block.</param>
    /// <param name="skipped">Whether the coding unit is skipped, which implies merge mode.</param>
    /// <returns>Whether the block was coded in merge mode.</returns>
    private static bool DecodeBlock(ref CabacEngine engine, H265SliceContext context, in H265PuBlock block, bool skipped)
    {
        var merged = skipped || engine.DecodeDecision(H265CabacContexts.MergeFlag) == 1;
        H265PuMotion motion;
        if (merged)
        {
            var maxCandidates = context.Header.Refs.MaxMergeCand;
            var mergeIdx = maxCandidates > 1 ? ReadMergeIdx(ref engine, maxCandidates) : 0;
            motion = H265MergeCandidates.Derive(context, block, mergeIdx);
        }
        else
        {
            motion = ReadExplicit(ref engine, context, block);
        }

        Store(context, block.Area, motion);
        H265InterPredictor.Predict(context, block.Area, motion);
        return merged;
    }

    /// <summary>Reads merge_idx: a truncated unary code whose first bin has a context.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="maxCandidates">The most merging candidates.</param>
    /// <returns>The index.</returns>
    private static int ReadMergeIdx(ref CabacEngine engine, int maxCandidates) =>
        engine.DecodeDecision(H265CabacContexts.MergeIdx) == 0
            ? 0
            : 1 + H265Bins.ReadTruncatedUnaryBypass(ref engine, maxCandidates - MergeIdxBypassReduction);

    /// <summary>Reads the explicitly coded motion of a prediction unit and derives the vectors.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The prediction block.</param>
    /// <returns>The motion.</returns>
    private static H265PuMotion ReadExplicit(ref CabacEngine engine, H265SliceContext context, in H265PuBlock block)
    {
        var header = context.Header;
        var predIdc = header.SliceType == H265SliceType.B ? ReadInterPredIdc(ref engine, context, block) : 0;
        var vector0 = default(MotionVector);
        var vector1 = default(MotionVector);
        var refIdx0 = -1;
        var refIdx1 = -1;
        if (predIdc != 1)
        {
            refIdx0 = header.Refs.NumRefIdxL0 > 1 ? ReadRefIdx(ref engine, header.Refs.NumRefIdxL0) : 0;
            var mvd = ReadMvd(ref engine);
            var flag = engine.DecodeDecision(H265CabacContexts.MvpFlag);
            vector0 = Add(H265MvPredictor.Derive(context, block, new(0, refIdx0, flag)), mvd);
        }

        if (predIdc != 0)
        {
            refIdx1 = header.Refs.NumRefIdxL1 > 1 ? ReadRefIdx(ref engine, header.Refs.NumRefIdxL1) : 0;
            var mvd = header.Refs.MvdL1Zero && predIdc == PredBi ? default : ReadMvd(ref engine);
            var flag = engine.DecodeDecision(H265CabacContexts.MvpFlag);
            vector1 = Add(H265MvPredictor.Derive(context, block, new(1, refIdx1, flag)), mvd);
        }

        return new(vector0, vector1, (sbyte)refIdx0, (sbyte)refIdx1);
    }

    /// <summary>Adds a motion vector difference to a predictor with 16 bit wrap-around (8-94 to 8-97).</summary>
    /// <param name="predictor">The predictor.</param>
    /// <param name="difference">The difference.</param>
    /// <returns>The vector.</returns>
    private static MotionVector Add(MotionVector predictor, MotionVector difference) =>
        new((short)(predictor.X + difference.X), (short)(predictor.Y + difference.Y));

    /// <summary>Reads inter_pred_idc (Table 9-47).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The prediction block.</param>
    /// <returns>0 for list 0, 1 for list 1, 2 for both.</returns>
    private static int ReadInterPredIdc(ref CabacEngine engine, H265SliceContext context, in H265PuBlock block)
    {
        if (block.Area.Width + block.Area.Height != NoBiSize)
        {
            var depth = context.Picture.CtDepth[context.Picture.GridIndex(block.Unit.X, block.Unit.Y)];
            if (engine.DecodeDecision(H265CabacContexts.InterPredIdc + depth) == 1)
            {
                return PredBi;
            }
        }

        return engine.DecodeDecision(H265CabacContexts.InterPredIdc + InterPredSecondBin);
    }

    /// <summary>Reads ref_idx_l0 or ref_idx_l1: a truncated unary code whose first two bins have contexts.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="count">The pictures in the list.</param>
    /// <returns>The index.</returns>
    private static int ReadRefIdx(ref CabacEngine engine, int count)
    {
        var maximum = count - 1;
        var value = 0;
        while (value < maximum)
        {
            var bin = value < RefIdxContexts
                ? engine.DecodeDecision(H265CabacContexts.RefIdx + value)
                : engine.DecodeBypass();
            if (bin == 0)
            {
                break;
            }

            value++;
        }

        return value;
    }

    /// <summary>Reads a motion vector difference, mvd_coding() (7.3.8.9).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <returns>The difference.</returns>
    private static MotionVector ReadMvd(ref CabacEngine engine)
    {
        var greater0X = engine.DecodeDecision(H265CabacContexts.AbsMvdGreater0) == 1;
        var greater0Y = engine.DecodeDecision(H265CabacContexts.AbsMvdGreater0) == 1;
        var greater1X = greater0X && engine.DecodeDecision(H265CabacContexts.AbsMvdGreater1) == 1;
        var greater1Y = greater0Y && engine.DecodeDecision(H265CabacContexts.AbsMvdGreater1) == 1;
        var x = ReadMvdComponent(ref engine, greater0X, greater1X);
        var y = ReadMvdComponent(ref engine, greater0Y, greater1Y);
        return new(x, y);
    }

    /// <summary>Reads the magnitude and sign of one component of a motion vector difference.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="greater0">Whether the magnitude is above 0.</param>
    /// <param name="greater1">Whether the magnitude is above 1.</param>
    /// <returns>The component.</returns>
    private static int ReadMvdComponent(ref CabacEngine engine, bool greater0, bool greater1)
    {
        if (!greater0)
        {
            return 0;
        }

        var magnitude = greater1 ? MvdExtraBase + H265Bins.ReadExpGolombBypass(ref engine, MvdOrder) : 1;
        return engine.DecodeBypass() == 1 ? -magnitude : magnitude;
    }

    /// <summary>Stores the motion of a prediction block in the picture and marks its edges.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="area">The block.</param>
    /// <param name="motion">The motion.</param>
    private static void Store(H265SliceContext context, BlockRect area, H265PuMotion motion)
    {
        var picture = context.Picture;
        var columns = area.Width >> H265Picture.GridLog2;
        var rows = area.Height >> H265Picture.GridLog2;
        var start = picture.GridIndex(area.X, area.Y);
        for (var row = 0; row < rows; row++)
        {
            picture.Motion.AsSpan(start + (row * picture.GridWidth), columns).Fill(motion);
        }

        H265CodingGrid.MarkEdges(context, area.X, area.Y, area.Width, area.Height, H265Picture.PredictionEdgeLeft, H265Picture.PredictionEdgeTop);
    }
}
