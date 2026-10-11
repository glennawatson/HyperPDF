// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The partition of one tile into components, resolutions, sub-bands, precincts and code-blocks (ISO 15444-1 annex B),
/// with the per-block decoding state. The arrays are pooled and reused from tile to tile.
/// </summary>
[DebuggerDisplay("JpxTile: {Blocks.Count} code-blocks")]
internal sealed class JpxTile : IDisposable
{
    /// <summary>The most code-blocks in one tile.</summary>
    private const int MaxBlocks = 1 << 22;

    /// <summary>The most precincts in one tile, over all sub-bands.</summary>
    private const int MaxPrecincts = 1 << 22;

    /// <summary>The sub-bands of a resolution above the lowest.</summary>
    private const int DetailBands = 3;

    /// <summary>The orientation bit of sub-bands that are high-pass horizontally.</summary>
    private const int HorizontalHigh = 1;

    /// <summary>The orientation bit of sub-bands that are high-pass vertically.</summary>
    private const int VerticalHigh = 2;

    /// <summary>The value of the mantissa's implicit leading one.</summary>
    private const double MantissaScale = 1 << JpxQuantization.MantissaBits;

    /// <summary>The factor that halves the step, because the block decoder works at twice the scale.</summary>
    private const float HalfStep = 0.5F;

    /// <summary>Initializes a new instance of the <see cref="JpxTile"/> class.</summary>
    /// <param name="components">The number of image components.</param>
    internal JpxTile(int components)
        : this(components, new ScratchPools())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="JpxTile"/> class with an explicit cache.</summary>
    /// <param name="components">The number of image components.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    internal JpxTile(int components, ScratchPools scratchPools)
    {
        Components = new JpxTileComponent[components];
        Resolutions = new(scratchPools);
        Bands = new(scratchPools);
        Precincts = new(scratchPools);
        Blocks = new(scratchPools);
        Segments = new(scratchPools);
        Chunks = new(scratchPools);
        Nodes = new(scratchPools);
    }

    /// <summary>Gets the tile-components.</summary>
    internal JpxTileComponent[] Components { get; }

    /// <summary>Gets the resolution levels of every component.</summary>
    internal JpxList<JpxResolutionLayout> Resolutions { get; }

    /// <summary>Gets the sub-bands.</summary>
    internal JpxList<JpxBandLayout> Bands { get; }

    /// <summary>Gets the precincts, per sub-band.</summary>
    internal JpxList<JpxPrecinctLayout> Precincts { get; }

    /// <summary>Gets the code-blocks.</summary>
    internal JpxList<JpxCodeBlock> Blocks { get; }

    /// <summary>Gets the codeword segments.</summary>
    internal JpxList<JpxSegment> Segments { get; }

    /// <summary>Gets the data chunks.</summary>
    internal JpxList<JpxChunk> Chunks { get; }

    /// <summary>Gets the tag tree nodes.</summary>
    internal JpxList<JpxTagNode> Nodes { get; }

    /// <summary>Gets the tile's area on the reference grid.</summary>
    internal JpxRectangle Area { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Resolutions.Dispose();
        Bands.Dispose();
        Precincts.Dispose();
        Blocks.Dispose();
        Segments.Dispose();
        Chunks.Dispose();
        Nodes.Dispose();
    }

    /// <summary>Lays out a tile.</summary>
    /// <param name="geometry">The image geometry.</param>
    /// <param name="tile">The tile index.</param>
    /// <param name="parameters">The tile's coding parameters.</param>
    /// <returns><see langword="false"/> when the tile is too finely divided to decode.</returns>
    internal bool Build(JpxGeometry geometry, int tile, JpxTileParameters parameters)
    {
        Resolutions.Clear();
        Bands.Clear();
        Precincts.Clear();
        Blocks.Clear();
        Segments.Clear();
        Chunks.Clear();
        Nodes.Clear();
        Area = geometry.GetTile(tile);
        for (var c = 0; c < Components.Length; c++)
        {
            if (!AddComponent(geometry, c, parameters))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Divides a value by a power of two, rounding up, for values that may be negative.</summary>
    /// <param name="value">The value.</param>
    /// <param name="shift">The power of two.</param>
    /// <returns>The rounded quotient.</returns>
    private static long CeilShift(long value, int shift) => (value + (1L << shift) - 1) >> shift;

    /// <summary>Limits a coordinate to the int range.</summary>
    /// <param name="value">The coordinate.</param>
    /// <returns>The coordinate, at most <see cref="int.MaxValue"/>.</returns>
    private static int Clip(long value) => (int)Math.Min(value, int.MaxValue);

    /// <summary>Computes a detail sub-band's area from the tile-component area (equation B-15).</summary>
    /// <param name="area">The tile-component area.</param>
    /// <param name="level">The decomposition level of the resolution.</param>
    /// <param name="orientation">The sub-band orientation.</param>
    /// <returns>The sub-band area.</returns>
    private static JpxRectangle BandArea(in JpxRectangle area, int level, int orientation)
    {
        var across = (long)(orientation & HorizontalHigh) << level;
        var down = (long)(orientation >> 1) << level;
        return new(
            (int)CeilShift(area.X0 - across, level + 1),
            (int)CeilShift(area.Y0 - down, level + 1),
            (int)CeilShift(area.X1 - across, level + 1),
            (int)CeilShift(area.Y1 - down, level + 1));
    }

    /// <summary>Sets a sub-band's magnitude bits and step size from its quantization.</summary>
    /// <param name="band">The sub-band.</param>
    /// <param name="context">The resolution's shared values.</param>
    /// <param name="index">The sub-band index in quantization order.</param>
    /// <returns>The sub-band with its quantization.</returns>
    private static JpxBandLayout WithQuantization(in JpxBandLayout band, in BandContext context, int index)
    {
        var step = context.Quantization.GetStep(index);
        var exponent = step >> JpxQuantization.MantissaBits;

        // The irreversible path uses no sub-band gain: the inverse 9/7 transform scales high-pass samples by 2/K instead,
        // to match PDFium's output.
        var size = context.Style.Reversible ? 1F : HalfStep * (float)((1.0 + ((step & JpxQuantization.MantissaMask) / MantissaScale)) * Math.ScaleB(1.0, context.Precision - exponent));
        return band with { Magnitude = exponent + context.Quantization.GuardBits - 1, StepSize = size };
    }

    /// <summary>Lays out one tile-component.</summary>
    /// <param name="geometry">The image geometry.</param>
    /// <param name="component">The component index.</param>
    /// <param name="parameters">The tile's coding parameters.</param>
    /// <returns><see langword="false"/> when a limit is exceeded.</returns>
    private bool AddComponent(JpxGeometry geometry, int component, JpxTileParameters parameters)
    {
        var style = parameters.Styles[component];
        var area = geometry.ToComponent(Area, component);
        Components[component] = new(area, Resolutions.Count, style.Levels, style.Reversible, parameters.RoiShifts[component], style.BlockStyle);
        for (var r = 0; r <= style.Levels; r++)
        {
            if (!AddResolution(geometry, component, r, parameters))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Lays out one resolution level and its sub-bands.</summary>
    /// <param name="geometry">The image geometry.</param>
    /// <param name="component">The component index.</param>
    /// <param name="resolution">The resolution index.</param>
    /// <param name="parameters">The tile's coding parameters.</param>
    /// <returns><see langword="false"/> when a limit is exceeded.</returns>
    private bool AddResolution(JpxGeometry geometry, int component, int resolution, JpxTileParameters parameters)
    {
        var style = parameters.Styles[component];
        var tileComponent = Components[component];
        var area = tileComponent.Area.Reduce(style.Levels - resolution);
        var widthExponent = style.PrecinctWidthExponent(resolution);
        var heightExponent = style.PrecinctHeightExponent(resolution);
        var wide = area.X0 == area.X1 ? 0 : (int)(CeilShift(area.X1, widthExponent) - (area.X0 >> widthExponent));
        var high = area.Y0 == area.Y1 ? 0 : (int)(CeilShift(area.Y1, heightExponent) - (area.Y0 >> heightExponent));
        var layout = new JpxResolutionLayout(area, widthExponent, heightExponent, wide, high, Bands.Count, resolution == 0 ? 1 : DetailBands);
        if (((long)wide * high * layout.BandCount) + Precincts.Count > MaxPrecincts)
        {
            return false;
        }

        _ = Resolutions.Add(layout);
        var band = new BandContext(geometry.Components[component].Precision, parameters.Quantizations[component], style, tileComponent, resolution, layout);
        for (var b = 0; b < layout.BandCount; b++)
        {
            if (!AddBand(band, component, resolution == 0 ? 0 : b + 1))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Lays out one sub-band and its precincts.</summary>
    /// <param name="context">The resolution's shared values.</param>
    /// <param name="component">The component index.</param>
    /// <param name="orientation">The sub-band orientation.</param>
    /// <returns><see langword="false"/> when a limit is exceeded.</returns>
    private bool AddBand(in BandContext context, int component, int orientation)
    {
        var resolution = context.Resolution;
        var level = context.Style.Levels - resolution;
        var area = resolution == 0 ? context.Layout.Area : BandArea(context.TileComponent.Area, level, orientation);
        var previous = resolution == 0 ? default : context.TileComponent.Area.Reduce(level + 1);
        var groupWidth = context.Layout.PrecinctWidthExponent - (resolution == 0 ? 0 : 1);
        var groupHeight = context.Layout.PrecinctHeightExponent - (resolution == 0 ? 0 : 1);
        var band = new JpxBandLayout(
            area,
            orientation,
            component,
            (orientation & HorizontalHigh) != 0 ? previous.Width : 0,
            (orientation & VerticalHigh) != 0 ? previous.Height : 0,
            Math.Min(context.Style.BlockWidthExponent, groupWidth),
            Math.Min(context.Style.BlockHeightExponent, groupHeight),
            0,
            0,
            Precincts.Count);
        band = WithQuantization(band, context, resolution == 0 ? 0 : ((resolution - 1) * DetailBands) + orientation);
        var index = Bands.Add(band);
        return AddPrecincts(index, context, groupWidth, groupHeight);
    }

    /// <summary>Lays out the precincts of a sub-band and their code-blocks.</summary>
    /// <param name="bandIndex">The sub-band index.</param>
    /// <param name="context">The resolution's shared values.</param>
    /// <param name="groupWidth">The precinct width in the sub-band, as a power of two.</param>
    /// <param name="groupHeight">The precinct height in the sub-band, as a power of two.</param>
    /// <returns><see langword="false"/> when a limit is exceeded.</returns>
    private bool AddPrecincts(int bandIndex, in BandContext context, int groupWidth, int groupHeight)
    {
        var layout = context.Layout;
        var band = Bands[bandIndex];
        var startX = (long)(layout.Area.X0 >> layout.PrecinctWidthExponent) << layout.PrecinctWidthExponent;
        var startY = (long)(layout.Area.Y0 >> layout.PrecinctHeightExponent) << layout.PrecinctHeightExponent;
        if (context.Resolution > 0)
        {
            startX = CeilShift(startX, 1);
            startY = CeilShift(startY, 1);
        }

        for (var p = 0; p < layout.PrecinctCount; p++)
        {
            var x0 = startX + ((long)(p % layout.PrecinctsWide) << groupWidth);
            var y0 = startY + ((long)(p / layout.PrecinctsWide) << groupHeight);
            var group = new JpxRectangle(Clip(x0), Clip(y0), Clip(x0 + (1L << groupWidth)), Clip(y0 + (1L << groupHeight)));
            if (!AddPrecinct(bandIndex, band, group.Intersect(band.Area)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Lays out one precinct's code-blocks and tag trees.</summary>
    /// <param name="bandIndex">The sub-band index.</param>
    /// <param name="band">The sub-band.</param>
    /// <param name="area">The precinct's area in the sub-band.</param>
    /// <returns><see langword="false"/> when a limit is exceeded.</returns>
    private bool AddPrecinct(int bandIndex, in JpxBandLayout band, in JpxRectangle area)
    {
        if (area.IsEmpty)
        {
            _ = Precincts.Add(new(0, 0, Blocks.Count, 0, 0));
            return true;
        }

        var widthExponent = band.BlockWidthExponent;
        var heightExponent = band.BlockHeightExponent;
        var startX = (area.X0 >> widthExponent) << widthExponent;
        var startY = (area.Y0 >> heightExponent) << heightExponent;
        var wide = (int)(CeilShift(area.X1, widthExponent) - (area.X0 >> widthExponent));
        var high = (int)(CeilShift(area.Y1, heightExponent) - (area.Y0 >> heightExponent));
        if (((long)wide * high) + Blocks.Count > MaxBlocks)
        {
            return false;
        }

        var first = Blocks.Count;
        for (var j = 0; j < high; j++)
        {
            for (var i = 0; i < wide; i++)
            {
                var x0 = startX + (i << widthExponent);
                var y0 = startY + (j << heightExponent);
                var block = new JpxRectangle(x0, y0, x0 + (1 << widthExponent), y0 + (1 << heightExponent)).Intersect(area);
                _ = Blocks.Add(new() { Area = block, Band = bandIndex, FirstSegment = -1, LastSegment = -1, PacketSegment = -1, FirstChunk = -1, LastChunk = -1 });
            }
        }

        _ = Precincts.Add(new(wide, high, first, JpxTagTree.Add(Nodes, wide, high), JpxTagTree.Add(Nodes, wide, high)));
        return true;
    }

    /// <summary>The values every sub-band of one resolution shares.</summary>
    /// <param name="Precision">The component's sample precision.</param>
    /// <param name="Quantization">The component's quantization.</param>
    /// <param name="Style">The component's coding style.</param>
    /// <param name="TileComponent">The tile-component.</param>
    /// <param name="Resolution">The resolution index.</param>
    /// <param name="Layout">The resolution layout.</param>
    private readonly record struct BandContext(int Precision, JpxQuantization Quantization, JpxCodingStyle Style, JpxTileComponent TileComponent, int Resolution, JpxResolutionLayout Layout);
}
