// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H265;

/// <summary>Reads the sample adaptive offset parameters of a coding tree block (7.3.8.3).</summary>
internal static class H265SaoReader
{
    /// <summary>The largest bit depth the offset range follows.</summary>
    private const int MaxOffsetBits = 10;

    /// <summary>The shift that finds the largest offset from the bit depth.</summary>
    private const int OffsetShift = 5;

    /// <summary>The bits of the band position.</summary>
    private const int BandPositionBits = 5;

    /// <summary>The bits of the edge class.</summary>
    private const int EdgeClassBits = 2;

    /// <summary>Reads the parameters for a coding tree block and stores them in the picture.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="ctbAddrRs">The raster scan address of the coding tree block.</param>
    internal static void Read(ref CabacEngine engine, H265SliceContext context, int ctbAddrRs)
    {
        var layout = context.Layout;
        var rx = ctbAddrRs % layout.WidthInCtbs;
        var ry = ctbAddrRs / layout.WidthInCtbs;
        var sao = context.Picture.Sao;
        var target = ctbAddrRs * H265SaoParams.IntsPerCtb;
        if (rx > 0 && CanMerge(context, ctbAddrRs, ctbAddrRs - 1) && engine.DecodeDecision(H265CabacContexts.SaoMerge) == 1)
        {
            sao.AsSpan((ctbAddrRs - 1) * H265SaoParams.IntsPerCtb, H265SaoParams.IntsPerCtb).CopyTo(sao.AsSpan(target, H265SaoParams.IntsPerCtb));
            return;
        }

        if (ry > 0 && CanMerge(context, ctbAddrRs, ctbAddrRs - layout.WidthInCtbs) && engine.DecodeDecision(H265CabacContexts.SaoMerge) == 1)
        {
            sao.AsSpan((ctbAddrRs - layout.WidthInCtbs) * H265SaoParams.IntsPerCtb, H265SaoParams.IntsPerCtb).CopyTo(sao.AsSpan(target, H265SaoParams.IntsPerCtb));
            return;
        }

        ReadComponents(ref engine, context, sao.AsSpan(target, H265SaoParams.IntsPerCtb));
    }

    /// <summary>Clears the parameters of a coding tree block, for a slice that has no sample adaptive offset.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="ctbAddrRs">The raster scan address of the coding tree block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear(H265SliceContext context, int ctbAddrRs) =>
        context.Picture.Sao.AsSpan(ctbAddrRs * H265SaoParams.IntsPerCtb, H265SaoParams.IntsPerCtb).Clear();

    /// <summary>Tells whether a neighbouring coding tree block is in the same slice and tile, so that the parameters may be copied from it.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="ctbAddrRs">The current coding tree block.</param>
    /// <param name="neighbour">The neighbouring coding tree block.</param>
    /// <returns><see langword="true"/> when merging is allowed.</returns>
    private static bool CanMerge(H265SliceContext context, int ctbAddrRs, int neighbour) =>
        context.Picture.SliceOfCtb[neighbour] == context.SliceAddrRs && context.Layout.TileIdByRs[neighbour] == context.Layout.TileIdByRs[ctbAddrRs];

    /// <summary>Reads the type, offsets and class of each colour component.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="target">Receives the parameters.</param>
    private static void ReadComponents(ref CabacEngine engine, H265SliceContext context, Span<int> target)
    {
        target.Clear();
        var components = context.HasChroma ? H265SaoParams.Components : 1;
        for (var component = 0; component < components; component++)
        {
            if (component == 0 ? context.Header.SaoLuma : context.Header.SaoChroma)
            {
                ReadComponent(ref engine, context, component, target);
            }
        }
    }

    /// <summary>Reads the type, offsets and class of one colour component.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="component">The colour component.</param>
    /// <param name="target">The parameters of all components.</param>
    private static void ReadComponent(ref CabacEngine engine, H265SliceContext context, int component, Span<int> target)
    {
        var part = target.Slice(component * H265SaoParams.IntsPerComponent, H265SaoParams.IntsPerComponent);
        var shared = target.Slice(H265SaoParams.IntsPerComponent, H265SaoParams.IntsPerComponent);
        var sharesChroma = component == H265Component.Cr;
        part[0] = sharesChroma ? shared[0] : ReadType(ref engine);
        if (part[0] == H265SaoParams.None)
        {
            return;
        }

        ReadOffsets(ref engine, context, component, part);
        if (part[0] == H265SaoParams.Edge)
        {
            part[H265SaoParams.ClassOffset] = sharesChroma ? shared[H265SaoParams.ClassOffset] : H265Bins.ReadBypass(ref engine, EdgeClassBits);
        }
    }

    /// <summary>Reads sao_type_idx: a context coded bin and, when it is set, one bypass bin that picks band or edge.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <returns>0 for none, 1 for band offset, 2 for edge offset.</returns>
    private static int ReadType(ref CabacEngine engine)
    {
        if (engine.DecodeDecision(H265CabacContexts.SaoType) == 0)
        {
            return H265SaoParams.None;
        }

        return engine.DecodeBypass() == 0 ? H265SaoParams.Band : H265SaoParams.Edge;
    }

    /// <summary>Reads the four offsets of a component and, for band offset, their signs and the band position.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="component">The colour component.</param>
    /// <param name="part">The component's values; the type is already set.</param>
    private static void ReadOffsets(ref CabacEngine engine, H265SliceContext context, int component, Span<int> part)
    {
        var bitDepth = component == 0 ? context.Sps.BitDepthLuma : context.Sps.BitDepthChroma;
        var maximum = (1 << (Math.Min(bitDepth, MaxOffsetBits) - OffsetShift)) - 1;
        var scale = component == 0 ? context.Pps.Range.Log2SaoOffsetScaleLuma : context.Pps.Range.Log2SaoOffsetScaleChroma;
        var offsets = part.Slice(H265SaoParams.OffsetsOffset, H265SaoParams.OffsetCount);
        for (var i = 0; i < H265SaoParams.OffsetCount; i++)
        {
            offsets[i] = H265Bins.ReadTruncatedUnaryBypass(ref engine, maximum);
        }

        if (part[0] == H265SaoParams.Band)
        {
            for (var i = 0; i < H265SaoParams.OffsetCount; i++)
            {
                if (offsets[i] != 0 && engine.DecodeBypass() == 1)
                {
                    offsets[i] = -offsets[i];
                }
            }

            part[H265SaoParams.ClassOffset] = H265Bins.ReadBypass(ref engine, BandPositionBits);
        }
        else
        {
            // Edge offsets are positive for the first two categories and negative for the last two.
            for (var i = H265SaoParams.FirstNegativeEdgeOffset; i < H265SaoParams.OffsetCount; i++)
            {
                offsets[i] = -offsets[i];
            }
        }

        for (var i = 0; i < H265SaoParams.OffsetCount; i++)
        {
            offsets[i] <<= scale;
        }
    }
}
