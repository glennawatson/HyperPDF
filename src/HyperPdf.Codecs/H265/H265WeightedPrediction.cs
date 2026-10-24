// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Combines the intermediate predictions of one or two reference lists into picture samples, with the default or the explicit weighting (8.5.3.3.4).</summary>
internal static class H265WeightedPrediction
{
    /// <summary>The intermediate bit depth.</summary>
    private const int IntermediateBits = 14;

    /// <summary>The bit depth of 8 bit video.</summary>
    private const int EightBits = 8;

    /// <summary>The smallest shift that brings an intermediate sample of one list to the sample precision.</summary>
    private const int MinSingleShift = 2;

    /// <summary>The smallest shift that brings the sum of two intermediate samples to the sample precision.</summary>
    private const int MinAverageShift = 3;

    /// <summary>The colour components that share the chroma weight arrays: the weights of the two chroma components of a reference sit side by side.</summary>
    private const int ChromaComponents = 2;

    /// <summary>Writes the prediction of one colour component of a block to the picture.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="component">The colour component.</param>
    /// <param name="area">The block in its colour plane.</param>
    /// <param name="motion">The block's motion.</param>
    internal static void Write(H265SliceContext context, int component, BlockRect area, H265PuMotion motion)
    {
        var picture = context.Picture;
        var stride = picture.StrideOf(component);
        var target = new H265PredictionTarget(picture.PlaneOf(component), stride, (area.Y * stride) + area.X);
        var weighted = context.Header.SliceType == H265SliceType.P ? context.Pps.Flags.WeightedPred : context.Pps.Flags.WeightedBipred;
        if (weighted && context.Header.Weights is { } weights)
        {
            WriteExplicit(context, weights, component, motion, area, target);
            return;
        }

        WriteDefault(context, component, motion, area, target);
    }

    /// <summary>Writes a prediction without weighting: the samples of one list, or the average of both.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="component">The colour component.</param>
    /// <param name="motion">The block's motion.</param>
    /// <param name="area">The block.</param>
    /// <param name="target">Where to write.</param>
    private static void WriteDefault(H265SliceContext context, int component, H265PuMotion motion, BlockRect area, H265PredictionTarget target)
    {
        var buffers = context.Prediction;
        var size = area.Width * area.Height;
        var bitDepth = component == 0 ? context.Sps.BitDepthLuma : context.Sps.BitDepthChroma;
        if (motion.UsesList0 && motion.UsesList1)
        {
            WriteAverage(buffers.List0.AsSpan(0, size), buffers.List1.AsSpan(0, size), area, target, bitDepth);
            return;
        }

        WriteSingle(motion.UsesList0 ? buffers.List0.AsSpan(0, size) : buffers.List1.AsSpan(0, size), area, target, bitDepth);
    }

    /// <summary>Writes a prediction from one list without weighting (8-262 and 8-263).</summary>
    /// <param name="source">The intermediate samples.</param>
    /// <param name="area">The block.</param>
    /// <param name="target">Where to write.</param>
    /// <param name="bitDepth">The bit depth.</param>
    private static void WriteSingle(ReadOnlySpan<short> source, BlockRect area, H265PredictionTarget target, int bitDepth)
    {
        var shift = Math.Max(MinSingleShift, IntermediateBits - bitDepth);
        var offset = 1 << (shift - 1);
        var maximum = (1 << bitDepth) - 1;
        for (var y = 0; y < area.Height; y++)
        {
            var row = target.Offset + (y * target.Stride);
            for (var x = 0; x < area.Width; x++)
            {
                target.Plane[row + x] = (ushort)Math.Clamp((source[(y * area.Width) + x] + offset) >> shift, 0, maximum);
            }
        }
    }

    /// <summary>Writes the average of two predictions (8-264).</summary>
    /// <param name="first">The list 0 samples.</param>
    /// <param name="second">The list 1 samples.</param>
    /// <param name="area">The block.</param>
    /// <param name="target">Where to write.</param>
    /// <param name="bitDepth">The bit depth.</param>
    private static void WriteAverage(ReadOnlySpan<short> first, ReadOnlySpan<short> second, BlockRect area, H265PredictionTarget target, int bitDepth)
    {
        var shift = Math.Max(MinAverageShift, IntermediateBits + 1 - bitDepth);
        var offset = 1 << (shift - 1);
        var maximum = (1 << bitDepth) - 1;
        for (var y = 0; y < area.Height; y++)
        {
            var row = target.Offset + (y * target.Stride);
            for (var x = 0; x < area.Width; x++)
            {
                var index = (y * area.Width) + x;
                target.Plane[row + x] = (ushort)Math.Clamp((first[index] + second[index] + offset) >> shift, 0, maximum);
            }
        }
    }

    /// <summary>Writes a prediction with explicit weights (8-275 to 8-277).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="weights">The slice's weights.</param>
    /// <param name="component">The colour component.</param>
    /// <param name="motion">The block's motion.</param>
    /// <param name="area">The block.</param>
    /// <param name="target">Where to write.</param>
    private static void WriteExplicit(H265SliceContext context, H265PredWeights weights, int component, H265PuMotion motion, BlockRect area, H265PredictionTarget target)
    {
        var bitDepth = component == 0 ? context.Sps.BitDepthLuma : context.Sps.BitDepthChroma;
        var shift1 = Math.Max(MinSingleShift, IntermediateBits - bitDepth);
        var offsetShift = context.Sps.Range.HighPrecisionOffsets ? 0 : bitDepth - EightBits;
        var first = motion.UsesList0 ? Pick(weights.List0, component, motion.RefIdx0, offsetShift) : default;
        var second = motion.UsesList1 && weights.List1 is { } list1 ? Pick(list1, component, motion.RefIdx1, offsetShift) : default;
        var log2Wd = (component == 0 ? weights.LumaLog2Denom : weights.ChromaLog2Denom) + shift1;
        var mode = new H265ExplicitWeighting(motion.UsesList0, motion.UsesList1, first, second, log2Wd);
        var buffers = context.Prediction;
        var maximum = (1 << bitDepth) - 1;
        for (var y = 0; y < area.Height; y++)
        {
            var row = target.Offset + (y * target.Stride);
            for (var x = 0; x < area.Width; x++)
            {
                var index = (y * area.Width) + x;
                target.Plane[row + x] = (ushort)Math.Clamp(mode.Combine(buffers.List0[index], buffers.List1[index]), 0, maximum);
            }
        }
    }

    /// <summary>Picks the weight and offset for a colour component of one reference picture.</summary>
    /// <param name="list">The weights of the list.</param>
    /// <param name="component">The colour component.</param>
    /// <param name="refIdx">The reference index.</param>
    /// <param name="offsetShift">The left shift of the offsets.</param>
    /// <returns>The weight and shifted offset.</returns>
    private static H265Weight Pick(H265WeightList list, int component, int refIdx, int offsetShift)
    {
        if (component == 0)
        {
            return new(list.LumaWeight[refIdx], list.LumaOffset[refIdx] << offsetShift);
        }

        var index = (refIdx * ChromaComponents) + component - 1;
        return new(list.ChromaWeight[index], list.ChromaOffset[index] << offsetShift);
    }
}
