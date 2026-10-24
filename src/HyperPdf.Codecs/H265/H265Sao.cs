// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Applies sample adaptive offset to a deblocked picture (8.7.3).</summary>
internal static class H265Sao
{
    /// <summary>The bands of a band offset.</summary>
    private const int Bands = 32;

    /// <summary>The bit depth term of the band shift.</summary>
    private const int BandBits = 5;

    /// <summary>The blocks around a block in each direction, counting itself.</summary>
    private const int Around = 3;

    /// <summary>The coding tree blocks in the square around a block, counting itself.</summary>
    private const int Neighbourhood = Around * Around;

    /// <summary>The number of edge classes.</summary>
    private const int EdgeClasses = 4;

    /// <summary>The value added to the sum of the two comparison signs to index <see cref="Categories"/>.</summary>
    private const int SignBias = 2;

    /// <summary>Gets the edge category from the sum of the comparison signs plus the bias: valley, step, flat, step, peak (8-412). A category of 0 leaves the sample alone.</summary>
    private static ReadOnlySpan<byte> Categories => [0x01, 0x02, 0x00, 0x03, 0x04];

    /// <summary>Gets the horizontal offsets of the two neighbours of each edge class, first neighbours then second (Table 8-13).</summary>
    private static ReadOnlySpan<sbyte> HorizontalOffsets => [-1, 0, -1, 1, 1, 0, 1, -1];

    /// <summary>Gets the vertical offsets of the two neighbours of each edge class, first neighbours then second (Table 8-13).</summary>
    private static ReadOnlySpan<sbyte> VerticalOffsets => [0, -1, -1, -1, 0, 1, 1, 1];

    /// <summary>Applies the offsets to every coding tree block that asks for them.</summary>
    /// <param name="context">The slice context of the picture.</param>
    internal static void Apply(H265SliceContext context)
    {
        if (!context.Sps.Tools.SaoEnabled)
        {
            return;
        }

        var picture = context.Picture;
        var layout = context.Layout;
        var components = context.HasChroma ? H265SaoParams.Components : 1;
        for (var component = 0; component < components; component++)
        {
            var plane = picture.PlaneOf(component);
            var source = context.SaoSource;
            plane.CopyTo(source, 0);
            for (var ctb = 0; ctb < layout.CtbCount; ctb++)
            {
                var offset = (ctb * H265SaoParams.IntsPerCtb) + (component * H265SaoParams.IntsPerComponent);
                if (picture.Sao[offset] == H265SaoParams.None || picture.HeaderOfCtb[ctb] is null)
                {
                    continue;
                }

                var block = MakeBlock(context, component, ctb, source, plane);
                ApplyBlock(context, block, picture.Sao.AsSpan(offset, H265SaoParams.IntsPerComponent));
            }
        }
    }

    /// <summary>Describes a coding tree block of a colour component.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="component">The colour component.</param>
    /// <param name="ctb">The raster scan address of the block.</param>
    /// <param name="source">The deblocked samples of the plane.</param>
    /// <param name="target">The plane to write to.</param>
    /// <returns>The block.</returns>
    private static H265SaoBlock MakeBlock(H265SliceContext context, int component, int ctb, ushort[] source, ushort[] target)
    {
        var sps = context.Sps;
        var layout = context.Layout;
        var picture = context.Picture;
        var subW = component == 0 ? 1 : sps.SubWidthC;
        var subH = component == 0 ? 1 : sps.SubHeightC;
        var ctbWidth = (1 << layout.CtbLog2) / subW;
        var ctbHeight = (1 << layout.CtbLog2) / subH;
        var planeWidth = component == 0 ? picture.Width : picture.ChromaWidth;
        var planeHeight = component == 0 ? picture.Height : picture.ChromaHeight;
        var x0 = (ctb % layout.WidthInCtbs) * ctbWidth;
        var y0 = (ctb / layout.WidthInCtbs) * ctbHeight;
        var area = new BlockRect(x0, y0, Math.Min(ctbWidth, planeWidth - x0), Math.Min(ctbHeight, planeHeight - y0));
        return new(component, ctb, source, target, area, planeWidth, planeHeight, component == 0 ? sps.BitDepthLuma : sps.BitDepthChroma);
    }

    /// <summary>Applies the offsets to one coding tree block of one colour component.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The block.</param>
    /// <param name="parameters">The parameters of the block's component.</param>
    private static void ApplyBlock(H265SliceContext context, in H265SaoBlock block, ReadOnlySpan<int> parameters)
    {
        var offsets = parameters.Slice(H265SaoParams.OffsetsOffset, H265SaoParams.OffsetCount);
        var protectedSamples = context.Pps.Flags.TransquantBypass || context.Sps.Pcm.LoopFilterDisabled;
        if (parameters[0] == H265SaoParams.Band)
        {
            ApplyBand(context, block, parameters[H265SaoParams.ClassOffset], offsets, protectedSamples);
        }
        else
        {
            ApplyEdge(context, block, parameters[H265SaoParams.ClassOffset], offsets, protectedSamples);
        }
    }

    /// <summary>Applies band offset to a block (8-414 and 8-415).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The block.</param>
    /// <param name="leftBand">The first of the four bands that get an offset.</param>
    /// <param name="offsets">The four offsets.</param>
    /// <param name="protectedSamples">Whether some samples may be exempt because they are lossless.</param>
    private static void ApplyBand(H265SliceContext context, in H265SaoBlock block, int leftBand, ReadOnlySpan<int> offsets, bool protectedSamples)
    {
        Span<int> table = stackalloc int[Bands];
        for (var k = 0; k < H265SaoParams.OffsetCount; k++)
        {
            table[(k + leftBand) & (Bands - 1)] = k + 1;
        }

        var shift = block.BitDepth - BandBits;
        var maximum = (1 << block.BitDepth) - 1;
        var area = block.Area;
        for (var y = area.Y; y < area.Y + area.Height; y++)
        {
            for (var x = area.X; x < area.X + area.Width; x++)
            {
                var index = (y * block.PlaneWidth) + x;
                var band = table[block.Source[index] >> shift];
                if (band != 0 && !(protectedSamples && IsProtected(context, block.Component, x, y)))
                {
                    block.Target[index] = (ushort)Math.Clamp(block.Source[index] + offsets[band - 1], 0, maximum);
                }
            }
        }
    }

    /// <summary>Applies edge offset to a block (8-409 to 8-413).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The block.</param>
    /// <param name="edgeClass">The edge class, 0 to 3.</param>
    /// <param name="offsets">The four offsets.</param>
    /// <param name="protectedSamples">Whether some samples may be exempt because they are lossless.</param>
    private static void ApplyEdge(H265SliceContext context, in H265SaoBlock block, int edgeClass, ReadOnlySpan<int> offsets, bool protectedSamples)
    {
        Span<bool> allowed = stackalloc bool[Neighbourhood];
        FillAllowed(context, block.Ctb, allowed);
        var horizontal0 = HorizontalOffsets[edgeClass];
        var vertical0 = VerticalOffsets[edgeClass];
        var horizontal1 = HorizontalOffsets[edgeClass + EdgeClasses];
        var vertical1 = VerticalOffsets[edgeClass + EdgeClasses];
        var maximum = (1 << block.BitDepth) - 1;
        var area = block.Area;
        for (var y = area.Y; y < area.Y + area.Height; y++)
        {
            for (var x = area.X; x < area.X + area.Width; x++)
            {
                if (!Neighbour(block, allowed, x + horizontal0, y + vertical0) || !Neighbour(block, allowed, x + horizontal1, y + vertical1))
                {
                    continue;
                }

                var index = (y * block.PlaneWidth) + x;
                int centre = block.Source[index];
                int first = block.Source[((y + vertical0) * block.PlaneWidth) + x + horizontal0];
                int second = block.Source[((y + vertical1) * block.PlaneWidth) + x + horizontal1];
                var category = Categories[SignBias + Math.Sign(centre - first) + Math.Sign(centre - second)];
                if (category != 0 && !(protectedSamples && IsProtected(context, block.Component, x, y)))
                {
                    block.Target[index] = (ushort)Math.Clamp(centre + offsets[category - 1], 0, maximum);
                }
            }
        }
    }

    /// <summary>Tells whether a neighbouring sample may be used: it is inside the picture and, when it lies in another coding tree block, that block may be crossed.</summary>
    /// <param name="block">The block.</param>
    /// <param name="allowed">Whether each of the 3x3 coding tree blocks around the block may be used.</param>
    /// <param name="x">The neighbour's column.</param>
    /// <param name="y">The neighbour's row.</param>
    /// <returns><see langword="true"/> when the neighbour can be used.</returns>
    private static bool Neighbour(in H265SaoBlock block, ReadOnlySpan<bool> allowed, int x, int y)
    {
        if ((uint)x >= (uint)block.PlaneWidth || (uint)y >= (uint)block.PlaneHeight)
        {
            return false;
        }

        var area = block.Area;
        return allowed[(Side(y, area.Y, area.Height) * Around) + Side(x, area.X, area.Width)];
    }

    /// <summary>Tells on which side of a block a coordinate lies along one axis.</summary>
    /// <param name="value">The coordinate.</param>
    /// <param name="start">The first coordinate of the block.</param>
    /// <param name="size">The size of the block.</param>
    /// <returns>0 before the block, 1 inside it, 2 after it.</returns>
    private static int Side(int value, int start, int size)
    {
        if (value < start)
        {
            return 0;
        }

        return value >= start + size ? Around - 1 : 1;
    }

    /// <summary>Works out which of the coding tree blocks around a block may supply neighbours: those in the picture whose slice and tile settings allow the filter to cross into them.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="ctb">The block.</param>
    /// <param name="allowed">Receives a flag for each of the 3x3 blocks.</param>
    private static void FillAllowed(H265SliceContext context, int ctb, Span<bool> allowed)
    {
        var layout = context.Layout;
        var picture = context.Picture;
        var rx = ctb % layout.WidthInCtbs;
        var ry = ctb / layout.WidthInCtbs;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var nx = rx + dx;
                var ny = ry + dy;
                var slot = ((dy + 1) * Around) + dx + 1;
                if (nx < 0 || ny < 0 || nx >= layout.WidthInCtbs || ny >= layout.HeightInCtbs)
                {
                    allowed[slot] = false;
                    continue;
                }

                var neighbour = (ny * layout.WidthInCtbs) + nx;
                allowed[slot] = neighbour == ctb || CanCross(context, picture, ctb, neighbour);
            }
        }
    }

    /// <summary>Tells whether the filter may use samples of another coding tree block (8.7.3.2).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="picture">The picture.</param>
    /// <param name="ctb">The block being filtered.</param>
    /// <param name="neighbour">The block the neighbour lies in.</param>
    /// <returns><see langword="true"/> when the neighbour may be used.</returns>
    private static bool CanCross(H265SliceContext context, H265Picture picture, int ctb, int neighbour)
    {
        var layout = context.Layout;
        var header = picture.HeaderOfCtb[ctb];
        var other = picture.HeaderOfCtb[neighbour];
        if (header is null || other is null)
        {
            return false;
        }

        if (picture.SliceOfCtb[ctb] != picture.SliceOfCtb[neighbour])
        {
            var neighbourIsEarlier = layout.RsToTs[neighbour] < layout.RsToTs[ctb];
            if (!(neighbourIsEarlier ? header : other).Deblocking.LoopFilterAcrossSlices)
            {
                return false;
            }
        }

        return context.Pps.Tiles.LoopFilterAcrossTiles || layout.TileIdByRs[ctb] == layout.TileIdByRs[neighbour];
    }

    /// <summary>Tells whether a sample belongs to a coding unit that the in-loop filters leave alone.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="component">The colour component.</param>
    /// <param name="x">The column in the colour plane.</param>
    /// <param name="y">The row in the colour plane.</param>
    /// <returns><see langword="true"/> when the sample must not be changed.</returns>
    private static bool IsProtected(H265SliceContext context, int component, int x, int y)
    {
        var picture = context.Picture;
        var lumaX = component == 0 ? x : x * context.Sps.SubWidthC;
        var lumaY = component == 0 ? y : y * context.Sps.SubHeightC;
        return (picture.Flags[picture.GridIndex(lumaX, lumaY)] & H265Picture.BypassFlag) != 0;
    }
}
