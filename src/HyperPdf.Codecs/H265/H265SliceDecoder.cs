// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Decodes the coding tree units of one slice segment (7.3.8.1), handling tiles, wavefront rows and the CABAC state that carries between them (9.3.1 and 9.3.2).</summary>
internal static class H265SliceDecoder
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The distance back to the unit that, when it is in another tile, shows a unit is the second of its row in its tile.</summary>
    private const int TwoBack = 2;

    /// <summary>Decodes a slice segment into the picture of the context.</summary>
    /// <param name="rbsp">The payload of the slice segment NAL unit, without its header and emulation prevention bytes.</param>
    /// <param name="context">The slice context, with its header and reference lists set.</param>
    /// <exception cref="InvalidDataException">The slice data is damaged or cut short.</exception>
    internal static void Decode(ReadOnlySpan<byte> rbsp, H265SliceContext context)
    {
        var header = context.Header;
        var segment = header.Segment;
        var layout = context.Layout;
        var initType = H265CabacContexts.InitTypeOf(header.SliceType, header.Refs.CabacInit);
        BeginSegment(context);
        var engine = new CabacEngine(rbsp, (long)segment.DataOffset * ByteBits, context.Contexts);
        var ts = layout.RsToTs[segment.Address];
        var atSegmentStart = true;
        while (true)
        {
            var rs = layout.TsToRs[ts];
            context.Picture.SliceOfCtb[rs] = context.SliceAddrRs;
            context.Picture.HeaderOfCtb[rs] = header;
            context.Picture.RefsIndexOfCtb[rs] = context.SliceIndex;
            PrepareContexts(context, ts, rs, atSegmentStart, initType);
            atSegmentStart = false;
            DecodeCtu(ref engine, context, rs);
            if (engine.IsOverrun)
            {
                throw new InvalidDataException("An H.265 slice is cut short.");
            }

            StoreWavefront(context, ts, rs);
            var end = engine.DecodeTerminate() == 1;
            ts++;
            if (end || ts >= layout.CtbCount)
            {
                break;
            }

            if (!StartsSubset(context, ts, layout.TsToRs[ts]))
            {
                continue;
            }

            _ = engine.DecodeTerminate();
            engine.Restart((engine.BitPosition + ByteBits - 1) & ~(long)(ByteBits - 1));
        }

        StoreDependent(context);
    }

    /// <summary>Saves the context variables at the end of a slice segment for a dependent segment to continue from.</summary>
    /// <param name="context">The slice context.</param>
    private static void StoreDependent(H265SliceContext context)
    {
        if (!context.Pps.Flags.DependentSlices)
        {
            return;
        }

        context.Contexts.CopyTo(context.DependentContexts, 0);
        context.StatCoeff.CopyTo(context.DependentStatCoeff, 0);
    }

    /// <summary>Sets up the state a new slice segment starts from.</summary>
    /// <param name="context">The slice context.</param>
    private static void BeginSegment(H265SliceContext context)
    {
        var segment = context.Header.Segment;
        if (segment.Dependent)
        {
            return;
        }

        context.SliceAddrRs = segment.Address;
        context.CuQpOffsetCb = 0;
        context.CuQpOffsetCr = 0;
        context.QpYPred = context.Header.Quant.SliceQp;
        context.QpY = context.Header.Quant.SliceQp;
        context.CuQpDeltaVal = 0;
        context.LastQpY = context.Header.Quant.SliceQp;
    }

    /// <summary>Reads one coding tree unit: its sample adaptive offset parameters, then its coding quadtree.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="rs">The raster scan address of the unit.</param>
    private static void DecodeCtu(ref CabacEngine engine, H265SliceContext context, int rs)
    {
        var layout = context.Layout;
        var header = context.Header;
        if (header.SaoLuma || header.SaoChroma)
        {
            H265SaoReader.Read(ref engine, context, rs);
        }
        else
        {
            H265SaoReader.Clear(context, rs);
        }

        var x = (rs % layout.WidthInCtbs) << layout.CtbLog2;
        var y = (rs / layout.WidthInCtbs) << layout.CtbLog2;
        H265CodingTree.Decode(ref engine, context, x, y, layout.CtbLog2, 0);
    }

    /// <summary>Sets the context variables for a coding tree unit when it starts a segment, a tile or a wavefront row (9.3.2.1).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="ts">The tile scan address of the unit.</param>
    /// <param name="rs">The raster scan address of the unit.</param>
    /// <param name="atSegmentStart">Whether the unit is the first of its slice segment.</param>
    /// <param name="initType">The initialisation type of the slice.</param>
    private static void PrepareContexts(H265SliceContext context, int ts, int rs, bool atSegmentStart, int initType)
    {
        var layout = context.Layout;
        var firstInTile = ts == 0 || layout.TileIdByTs[ts] != layout.TileIdByTs[ts - 1];
        var rowStart = context.Pps.Flags.EntropyCodingSync && StartsRow(layout, rs);
        var sliceQp = context.Header.Quant.SliceQp;
        if (firstInTile)
        {
            Initialize(context, initType);
            context.LastQpY = sliceQp;
        }
        else if (rowStart)
        {
            SynchronizeRow(context, rs, initType);
            context.LastQpY = sliceQp;
        }
        else if (atSegmentStart && context.Header.Segment.Dependent)
        {
            context.DependentContexts.CopyTo(context.Contexts, 0);
            context.DependentStatCoeff.CopyTo(context.StatCoeff, 0);
        }
        else if (atSegmentStart)
        {
            Initialize(context, initType);
        }
    }

    /// <summary>Initialises the context variables and Rice states from the slice settings.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="initType">The initialisation type.</param>
    private static void Initialize(H265SliceContext context, int initType)
    {
        H265CabacContexts.Initialize(context.Contexts, initType, context.Header.Quant.SliceQp);
        Array.Clear(context.StatCoeff);
    }

    /// <summary>Takes the context variables saved after the second unit of the row above, when the unit above and to the right is available, otherwise initialises them.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="rs">The raster scan address of the unit.</param>
    /// <param name="initType">The initialisation type.</param>
    private static void SynchronizeRow(H265SliceContext context, int rs, int initType)
    {
        var layout = context.Layout;
        var ctbSize = 1 << layout.CtbLog2;
        var x = (rs % layout.WidthInCtbs) << layout.CtbLog2;
        var y = (rs / layout.WidthInCtbs) << layout.CtbLog2;
        if (H265Availability.IsAvailable(context, x, y, x + ctbSize, y - ctbSize))
        {
            context.WppContexts.CopyTo(context.Contexts, 0);
            context.WppStatCoeff.CopyTo(context.StatCoeff, 0);
        }
        else
        {
            Initialize(context, initType);
        }
    }

    /// <summary>Saves the context variables after the second unit of a tile row, for the row below to start from.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="ts">The tile scan address of the unit just read.</param>
    /// <param name="rs">The raster scan address of the unit just read.</param>
    private static void StoreWavefront(H265SliceContext context, int ts, int rs)
    {
        if (!context.Pps.Flags.EntropyCodingSync)
        {
            return;
        }

        var layout = context.Layout;
        var second = (rs % layout.WidthInCtbs) == 1 || (rs > 1 && layout.TileIdByTs[ts] != layout.TileIdByRs[rs - TwoBack]);
        if (!second)
        {
            return;
        }

        context.Contexts.CopyTo(context.WppContexts, 0);
        context.StatCoeff.CopyTo(context.WppStatCoeff, 0);
    }

    /// <summary>Tells whether a unit is the first of a row of units in its tile.</summary>
    /// <param name="layout">The picture layout.</param>
    /// <param name="rs">The raster scan address of the unit.</param>
    /// <returns><see langword="true"/> when the unit starts a row.</returns>
    private static bool StartsRow(H265PictureLayout layout, int rs) =>
        (rs % layout.WidthInCtbs) == 0 || layout.TileIdByRs[rs] != layout.TileIdByRs[rs - 1];

    /// <summary>Tells whether the unit about to be read starts a new subset of slice data, which a bit and byte alignment precede.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="ts">The tile scan address of the next unit.</param>
    /// <param name="rs">The raster scan address of the next unit.</param>
    /// <returns><see langword="true"/> when a new subset starts.</returns>
    private static bool StartsSubset(H265SliceContext context, int ts, int rs)
    {
        var layout = context.Layout;
        var newTile = context.Pps.Tiles.Enabled && layout.TileIdByTs[ts] != layout.TileIdByTs[ts - 1];
        var newRow = context.Pps.Flags.EntropyCodingSync && StartsRow(layout, rs);
        return newTile || newRow;
    }
}
