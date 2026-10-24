// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Predicts the samples of a prediction block from its reference pictures and writes them to the picture (8.5.3.3).</summary>
internal static class H265InterPredictor
{
    /// <summary>The bits of the fraction of a luma vector.</summary>
    private const int LumaFractionBits = 2;

    /// <summary>The mask of the fraction of a luma vector.</summary>
    private const int LumaFractionMask = (1 << LumaFractionBits) - 1;

    /// <summary>The bits of the fraction of a chroma vector.</summary>
    private const int ChromaFractionBits = 3;

    /// <summary>The mask of the fraction of a chroma vector.</summary>
    private const int ChromaFractionMask = (1 << ChromaFractionBits) - 1;

    /// <summary>Predicts all colour components of a block.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="area">The block in luma samples.</param>
    /// <param name="motion">The block's motion.</param>
    internal static void Predict(H265SliceContext context, BlockRect area, H265PuMotion motion)
    {
        PredictComponent(context, 0, area, motion);
        if (!context.HasChroma)
        {
            return;
        }

        var sps = context.Sps;
        var chromaArea = new BlockRect(area.X / sps.SubWidthC, area.Y / sps.SubHeightC, area.Width / sps.SubWidthC, area.Height / sps.SubHeightC);
        PredictComponent(context, H265Component.Cb, chromaArea, motion);
        PredictComponent(context, H265Component.Cr, chromaArea, motion);
    }

    /// <summary>Predicts one colour component of a block.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="component">The colour component.</param>
    /// <param name="area">The block in its colour plane.</param>
    /// <param name="motion">The block's motion.</param>
    private static void PredictComponent(H265SliceContext context, int component, BlockRect area, H265PuMotion motion)
    {
        var buffers = context.Prediction;
        var size = area.Width * area.Height;
        if (motion.UsesList0)
        {
            Interpolate(context, component, area, context.RefList0[motion.RefIdx0], motion.Vector0, buffers.List0.AsSpan(0, size));
        }

        if (motion.UsesList1)
        {
            Interpolate(context, component, area, context.RefList1[motion.RefIdx1], motion.Vector1, buffers.List1.AsSpan(0, size));
        }

        H265WeightedPrediction.Write(context, component, area, motion);
    }

    /// <summary>Interpolates the reference block one vector points to.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="component">The colour component.</param>
    /// <param name="area">The block in its colour plane.</param>
    /// <param name="reference">The reference picture.</param>
    /// <param name="vector">The luma motion vector.</param>
    /// <param name="destination">Receives the intermediate samples.</param>
    private static void Interpolate(H265SliceContext context, int component, BlockRect area, H265Picture reference, MotionVector vector, Span<short> destination)
    {
        var sps = context.Sps;
        var move = MoveOf(sps, component, vector);
        var planeHeight = component == 0 ? reference.Height : reference.ChromaHeight;
        var source = new BlockRect(area.X + move.WholeX, area.Y + move.WholeY, area.Width, area.Height);
        var bitDepth = component == 0 ? sps.BitDepthLuma : sps.BitDepthChroma;
        var job = new H265InterpolationJob(reference.PlaneOf(component), reference.StrideOf(component), planeHeight, source, move.FractionX, move.FractionY, bitDepth, component != 0);
        H265Interpolation.Run(job, context.Prediction, destination);
    }

    /// <summary>Splits a luma motion vector into whole samples and a fraction in the units of a colour component (8.5.3.3.3.1).</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="component">The colour component.</param>
    /// <param name="vector">The luma motion vector.</param>
    /// <returns>The move.</returns>
    private static H265FractionalMove MoveOf(H265Sps sps, int component, MotionVector vector)
    {
        if (component == 0)
        {
            return new(vector.X >> LumaFractionBits, vector.Y >> LumaFractionBits, vector.X & LumaFractionMask, vector.Y & LumaFractionMask);
        }

        var chromaX = (vector.X << 1) / sps.SubWidthC;
        var chromaY = (vector.Y << 1) / sps.SubHeightC;
        return new(chromaX >> ChromaFractionBits, chromaY >> ChromaFractionBits, chromaX & ChromaFractionMask, chromaY & ChromaFractionMask);
    }
}
