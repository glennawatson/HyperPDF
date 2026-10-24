// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Applies the deblocking filter to a decoded picture: every vertical edge first, then every horizontal edge (8.7.2).</summary>
internal static class H265Deblocking
{
    /// <summary>The grid blocks between two edges of the 8x8 filtering grid.</summary>
    private const int EdgeSpacing = 2;

    /// <summary>The samples in a grid block.</summary>
    private const int BlockSize = 4;

    /// <summary>The chroma samples between two edges that are filtered.</summary>
    private const int ChromaGrid = 8;

    /// <summary>Filters a picture.</summary>
    /// <param name="context">The slice context of the picture.</param>
    internal static void Apply(H265SliceContext context)
    {
        Pass(context, true);
        Pass(context, false);
    }

    /// <summary>Filters all the edges of one direction.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="vertical">Whether to filter vertical edges.</param>
    private static void Pass(H265SliceContext context, bool vertical)
    {
        var picture = context.Picture;
        for (var by = vertical ? 0 : EdgeSpacing; by < picture.GridHeight; by += vertical ? 1 : EdgeSpacing)
        {
            for (var bx = vertical ? EdgeSpacing : 0; bx < picture.GridWidth; bx += vertical ? EdgeSpacing : 1)
            {
                FilterEdge(context, bx, by, vertical);
            }
        }
    }

    /// <summary>Filters the segment of an edge that lies on the left or above a grid block, if it needs filtering.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="bx">The column of the Q block in the 4x4 grid.</param>
    /// <param name="by">The row of the Q block in the 4x4 grid.</param>
    /// <param name="vertical">Whether the edge is vertical.</param>
    private static void FilterEdge(H265SliceContext context, int bx, int by, bool vertical)
    {
        var picture = context.Picture;
        var indexQ = (by * picture.GridWidth) + bx;
        var indexP = vertical ? indexQ - 1 : indexQ - picture.GridWidth;
        var transformBit = vertical ? H265Picture.TransformEdgeLeft : H265Picture.TransformEdgeTop;
        var predictionBit = vertical ? H265Picture.PredictionEdgeLeft : H265Picture.PredictionEdgeTop;
        if ((picture.Flags[indexQ] & (transformBit | predictionBit)) == 0)
        {
            return;
        }

        var x = bx << H265Picture.GridLog2;
        var y = by << H265Picture.GridLog2;
        var ctbQ = CtbAt(context.Layout, x, y);
        var ctbP = vertical ? CtbAt(context.Layout, x - 1, y) : CtbAt(context.Layout, x, y - 1);
        var header = picture.HeaderOfCtb[ctbQ];
        if (header is null || header.Deblocking.Disabled || !CrossingAllowed(context, header, ctbP, ctbQ))
        {
            return;
        }

        var refsP = picture.SliceRefs[picture.RefsIndexOfCtb[ctbP]];
        var refsQ = picture.SliceRefs[picture.RefsIndexOfCtb[ctbQ]];
        var strength = H265BoundaryStrength.Of(picture, indexP, indexQ, refsP, refsQ, (picture.Flags[indexQ] & transformBit) != 0);
        if (strength == 0)
        {
            return;
        }

        var edge = new H265EdgeInfo(x, y, indexP, indexQ, vertical, strength);
        FilterLumaEdge(context, header, edge);
        FilterChromaEdges(context, header, edge);
    }

    /// <summary>Finds the coding tree block that holds a luma sample.</summary>
    /// <param name="layout">The picture layout.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The raster scan address.</returns>
    private static int CtbAt(H265PictureLayout layout, int x, int y) =>
        ((y >> layout.CtbLog2) * layout.WidthInCtbs) + (x >> layout.CtbLog2);

    /// <summary>Tells whether an edge between two coding tree blocks may be filtered given the slice and tile settings.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="header">The header of the slice that holds the Q block.</param>
    /// <param name="ctbP">The coding tree block holding the P side.</param>
    /// <param name="ctbQ">The coding tree block holding the Q side.</param>
    /// <returns><see langword="true"/> when the edge may be filtered.</returns>
    private static bool CrossingAllowed(H265SliceContext context, H265SliceHeader header, int ctbP, int ctbQ)
    {
        if (ctbP == ctbQ)
        {
            return true;
        }

        var picture = context.Picture;
        if (picture.SliceOfCtb[ctbP] < 0)
        {
            return false;
        }

        if (picture.SliceOfCtb[ctbP] != picture.SliceOfCtb[ctbQ] && !header.Deblocking.LoopFilterAcrossSlices)
        {
            return false;
        }

        return context.Layout.TileIdByRs[ctbP] == context.Layout.TileIdByRs[ctbQ] || context.Pps.Tiles.LoopFilterAcrossTiles;
    }

    /// <summary>Filters the luma samples along an edge segment.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="header">The header of the slice that holds the Q block.</param>
    /// <param name="edge">The edge segment.</param>
    private static void FilterLumaEdge(H265SliceContext context, H265SliceHeader header, H265EdgeInfo edge)
    {
        var picture = context.Picture;
        var average = (picture.QpY[edge.IndexP] + picture.QpY[edge.IndexQ] + 1) >> 1;
        var parameters = new H265EdgeParameters(
            edge.Strength,
            average,
            header.Deblocking.TcOffsetDiv2,
            header.Deblocking.BetaOffsetDiv2,
            context.Sps.BitDepthLuma,
            (picture.Flags[edge.IndexP] & H265Picture.BypassFlag) != 0,
            (picture.Flags[edge.IndexQ] & H265Picture.BypassFlag) != 0);
        var stride = picture.Width;
        var segment = new H265EdgeSegment(picture.Luma, (edge.Y * stride) + edge.X, edge.Vertical ? 1 : stride, edge.Vertical ? stride : 1);
        H265EdgeFilter.FilterLuma(segment, parameters);
    }

    /// <summary>Filters the chroma samples along an edge segment when the edge lies on the chroma filtering grid.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="header">The header of the slice that holds the Q block.</param>
    /// <param name="edge">The edge segment.</param>
    private static void FilterChromaEdges(H265SliceContext context, H265SliceHeader header, H265EdgeInfo edge)
    {
        if (!context.HasChroma || edge.Strength != H265BoundaryStrength.Intra)
        {
            return;
        }

        var sps = context.Sps;
        var chromaX = edge.X / sps.SubWidthC;
        var chromaY = edge.Y / sps.SubHeightC;
        if ((edge.Vertical ? chromaX : chromaY) % ChromaGrid != 0)
        {
            return;
        }

        var q0 = (chromaY * context.Picture.ChromaWidth) + chromaX;
        for (var component = H265Component.Cb; component <= H265Component.Cr; component++)
        {
            FilterChromaPlane(context, header, edge, component, q0);
        }
    }

    /// <summary>Filters the samples of one chroma component along an edge segment.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="header">The header of the slice that holds the Q block.</param>
    /// <param name="edge">The edge segment.</param>
    /// <param name="component">The chroma component, 1 or 2.</param>
    /// <param name="q0">The index of the first Q sample in the chroma plane.</param>
    private static void FilterChromaPlane(H265SliceContext context, H265SliceHeader header, H265EdgeInfo edge, int component, int q0)
    {
        var sps = context.Sps;
        var picture = context.Picture;
        var average = (picture.QpY[edge.IndexP] + picture.QpY[edge.IndexQ] + 1) >> 1;
        var offset = component == H265Component.Cb ? context.Pps.Quant.CbQpOffset : context.Pps.Quant.CrQpOffset;
        var parameters = new H265EdgeParameters(
            edge.Strength,
            H265Dequantizer.MapChroma(sps.ChromaArrayType, average + offset),
            header.Deblocking.TcOffsetDiv2,
            header.Deblocking.BetaOffsetDiv2,
            sps.BitDepthChroma,
            (picture.Flags[edge.IndexP] & H265Picture.BypassFlag) != 0,
            (picture.Flags[edge.IndexQ] & H265Picture.BypassFlag) != 0);
        var stride = picture.ChromaWidth;
        var segment = new H265EdgeSegment(picture.PlaneOf(component), q0, edge.Vertical ? 1 : stride, edge.Vertical ? stride : 1);
        H265EdgeFilter.FilterChroma(segment, BlockSize / (edge.Vertical ? sps.SubHeightC : sps.SubWidthC), parameters);
    }
}
