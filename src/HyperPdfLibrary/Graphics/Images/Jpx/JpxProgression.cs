// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Walks a tile's packets in its progression order (ISO 15444-1 B.12), or in the volumes of its POC markers, and hands
/// each to the packet decoder. The position orders step over the reference grid as PDFium does. A packet seen in an
/// earlier volume is skipped.
/// </summary>
[DebuggerDisplay("JpxProgression: {_layers} layers")]
internal sealed class JpxProgression
{
    /// <summary>The most packets tracked for de-duplication.</summary>
    private const long MaxTracked = 1L << 26;

    /// <summary>The widest shift used on reference grid coordinates.</summary>
    private const int MaxShift = 48;

    /// <summary>The result of <see cref="Locate"/> for a position that starts no precinct.</summary>
    private const int NoPrecinct = -1;

    /// <summary>The result of <see cref="Locate"/> for a precinct outside the grid, which ends the volume.</summary>
    private const int Outside = -2;

    /// <summary>The image geometry.</summary>
    private readonly JpxGeometry _geometry;

    /// <summary>The packet decoder.</summary>
    private readonly JpxPacketDecoder _packets;

    /// <summary>The cache this progression walker uses for packet marks.</summary>
    private readonly ScratchPools _scratchPools;

    /// <summary>The tile.</summary>
    private JpxTile _tile = null!;

    /// <summary>Which packets were read, or <see langword="null"/> when too many to track.</summary>
    private bool[]? _seen;

    /// <summary>The number of quality layers.</summary>
    private int _layers;

    /// <summary>The most resolutions of any component.</summary>
    private int _resolutions;

    /// <summary>The most precincts of any resolution.</summary>
    private int _precincts;

    /// <summary>Initializes a new instance of the <see cref="JpxProgression"/> class.</summary>
    /// <param name="geometry">The image geometry.</param>
    /// <param name="packets">The packet decoder.</param>
    internal JpxProgression(JpxGeometry geometry, JpxPacketDecoder packets)
        : this(geometry, packets, new ScratchPools())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="JpxProgression"/> class with an explicit cache.</summary>
    /// <param name="geometry">The image geometry.</param>
    /// <param name="packets">The packet decoder.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    internal JpxProgression(JpxGeometry geometry, JpxPacketDecoder packets, ScratchPools scratchPools)
    {
        _geometry = geometry;
        _packets = packets;
        _scratchPools = scratchPools;
    }

    /// <summary>Reads every packet of a tile.</summary>
    /// <param name="tile">The tile layout.</param>
    /// <param name="parameters">The tile's coding parameters.</param>
    internal void Run(JpxTile tile, JpxTileParameters parameters)
    {
        _tile = tile;
        _layers = parameters.Coding.Layers;
        MeasureTile();
        var count = (long)_layers * _resolutions * tile.Components.Length * _precincts;
        _seen = count <= MaxTracked ? _scratchPools.Get<bool>().Rent((int)count) : null;
        _seen?.AsSpan(0, (int)count).Clear();
        try
        {
            RunVolumes(parameters);
        }
        finally
        {
            if (_seen is not null)
            {
                _scratchPools.Get<bool>().Return(_seen);
            }

            _seen = null;
        }
    }

    /// <summary>Determines whether a grid coordinate starts a precinct: it is on the precinct grid, or it is the tile edge and the precinct grid does not reach it.</summary>
    /// <param name="coordinate">The reference grid coordinate.</param>
    /// <param name="tileStart">The tile's first coordinate.</param>
    /// <param name="spacing">The precinct spacing on the reference grid.</param>
    /// <param name="misalignment">How far the resolution's start is off the precinct grid; zero when aligned.</param>
    /// <returns><see langword="true"/> when a precinct starts there.</returns>
    private static bool StartsPrecinct(long coordinate, long tileStart, long spacing, long misalignment) =>
        coordinate % spacing == 0 || (coordinate == tileStart && misalignment != 0);

    /// <summary>Runs the default volume, or each POC volume.</summary>
    /// <param name="parameters">The tile's coding parameters.</param>
    private void RunVolumes(JpxTileParameters parameters)
    {
        var components = _tile.Components.Length;
        if (parameters.Changes is not { Count: > 0 } changes)
        {
            _ = RunVolume(new(0, 0, _layers, _resolutions, components, parameters.Coding.Order));
            return;
        }

        foreach (var change in changes)
        {
            var volume = change with
            {
                LayerEnd = Math.Min(change.LayerEnd, _layers),
                ResolutionEnd = Math.Min(change.ResolutionEnd, _resolutions),
                ComponentEnd = Math.Min(change.ComponentEnd, components),
            };
            if (!RunVolume(volume))
            {
                return;
            }
        }
    }

    /// <summary>Finds the most resolutions and precincts of the tile's components.</summary>
    private void MeasureTile()
    {
        _resolutions = 0;
        _precincts = 0;
        foreach (var component in _tile.Components)
        {
            _resolutions = Math.Max(_resolutions, component.Levels + 1);
            for (var r = 0; r <= component.Levels; r++)
            {
                _precincts = Math.Max(_precincts, _tile.Resolutions[component.FirstResolution + r].PrecinctCount);
            }
        }
    }

    /// <summary>Runs one progression volume.</summary>
    /// <param name="volume">The volume.</param>
    /// <returns><see langword="false"/> when the data has run out.</returns>
    private bool RunVolume(in JpxProgressionChange volume) => volume.Order switch
    {
        JpxProgressionOrder.LayerResolutionComponentPosition => RunLayerFirst(volume),
        JpxProgressionOrder.ResolutionLayerComponentPosition => RunResolutionLayer(volume),
        JpxProgressionOrder.ResolutionPositionComponentLayer => RunResolutionPosition(volume),
        JpxProgressionOrder.PositionComponentResolutionLayer => RunPositionFirst(volume),
        _ => RunComponentFirst(volume),
    };

    /// <summary>Reads one packet unless an earlier volume already did.</summary>
    /// <param name="layer">The layer.</param>
    /// <param name="resolution">The resolution.</param>
    /// <param name="component">The component.</param>
    /// <param name="precinct">The precinct.</param>
    /// <returns><see langword="false"/> when the data has run out.</returns>
    private bool Visit(int layer, int resolution, int component, int precinct)
    {
        if (_seen is not null)
        {
            var index = (((((layer * _resolutions) + resolution) * _tile.Components.Length) + component) * _precincts) + precinct;
            if (_seen[index])
            {
                return true;
            }

            _seen[index] = true;
        }

        return _packets.Decode(layer, resolution, component, precinct);
    }

    /// <summary>Reads every layer of one precinct.</summary>
    /// <param name="layerEnd">The layer after the last.</param>
    /// <param name="resolution">The resolution.</param>
    /// <param name="component">The component.</param>
    /// <param name="precinct">The precinct.</param>
    /// <returns><see langword="false"/> when the data has run out.</returns>
    private bool VisitLayers(int layerEnd, int resolution, int component, int precinct)
    {
        for (var layer = 0; layer < layerEnd; layer++)
        {
            if (!Visit(layer, resolution, component, precinct))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads every precinct of one layer, resolution and component.</summary>
    /// <param name="layer">The layer.</param>
    /// <param name="resolution">The resolution.</param>
    /// <param name="component">The component.</param>
    /// <returns><see langword="false"/> when the data has run out.</returns>
    private bool VisitPrecincts(int layer, int resolution, int component)
    {
        var info = _tile.Components[component];
        if (resolution > info.Levels)
        {
            return true;
        }

        var count = _tile.Resolutions[info.FirstResolution + resolution].PrecinctCount;
        for (var p = 0; p < count; p++)
        {
            if (!Visit(layer, resolution, component, p))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Runs the layer-resolution-component-position order.</summary>
    /// <param name="volume">The volume.</param>
    /// <returns><see langword="false"/> when the data has run out.</returns>
    private bool RunLayerFirst(in JpxProgressionChange volume)
    {
        for (var layer = 0; layer < volume.LayerEnd; layer++)
        {
            for (var r = volume.ResolutionStart; r < volume.ResolutionEnd; r++)
            {
                for (var c = volume.ComponentStart; c < volume.ComponentEnd; c++)
                {
                    if (!VisitPrecincts(layer, r, c))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>Runs the resolution-layer-component-position order.</summary>
    /// <param name="volume">The volume.</param>
    /// <returns><see langword="false"/> when the data has run out.</returns>
    private bool RunResolutionLayer(in JpxProgressionChange volume)
    {
        for (var r = volume.ResolutionStart; r < volume.ResolutionEnd; r++)
        {
            for (var layer = 0; layer < volume.LayerEnd; layer++)
            {
                for (var c = volume.ComponentStart; c < volume.ComponentEnd; c++)
                {
                    if (!VisitPrecincts(layer, r, c))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>Runs the resolution-position-component-layer order.</summary>
    /// <param name="volume">The volume.</param>
    /// <returns><see langword="false"/> when the data has run out.</returns>
    private bool RunResolutionPosition(in JpxProgressionChange volume)
    {
        var step = FindStep(0, _tile.Components.Length);
        if (!step.IsUsable(_tile.Area))
        {
            return false;
        }

        for (var r = volume.ResolutionStart; r < volume.ResolutionEnd; r++)
        {
            for (long y = _tile.Area.Y0; y < _tile.Area.Y1; y += step.Y - (y % step.Y))
            {
                for (long x = _tile.Area.X0; x < _tile.Area.X1; x += step.X - (x % step.X))
                {
                    if (!VisitComponents(volume, r, x, y))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>Runs one grid position of the resolution-position-component-layer order.</summary>
    /// <param name="volume">The volume.</param>
    /// <param name="resolution">The resolution.</param>
    /// <param name="x">The reference grid column.</param>
    /// <param name="y">The reference grid row.</param>
    /// <returns><see langword="false"/> when the data has run out or the volume ends.</returns>
    private bool VisitComponents(in JpxProgressionChange volume, int resolution, long x, long y)
    {
        for (var c = volume.ComponentStart; c < volume.ComponentEnd; c++)
        {
            if (!VisitPosition(volume.LayerEnd, resolution, c, x, y))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Runs the position-component-resolution-layer order.</summary>
    /// <param name="volume">The volume.</param>
    /// <returns><see langword="false"/> when the data has run out.</returns>
    private bool RunPositionFirst(in JpxProgressionChange volume)
    {
        var step = FindStep(0, _tile.Components.Length);
        if (!step.IsUsable(_tile.Area))
        {
            return false;
        }

        for (long y = _tile.Area.Y0; y < _tile.Area.Y1; y += step.Y - (y % step.Y))
        {
            for (long x = _tile.Area.X0; x < _tile.Area.X1; x += step.X - (x % step.X))
            {
                for (var c = volume.ComponentStart; c < volume.ComponentEnd; c++)
                {
                    if (!VisitResolutions(volume, c, x, y))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>Runs the component-position-resolution-layer order.</summary>
    /// <param name="volume">The volume.</param>
    /// <returns><see langword="false"/> when the data has run out.</returns>
    private bool RunComponentFirst(in JpxProgressionChange volume)
    {
        for (var c = volume.ComponentStart; c < volume.ComponentEnd; c++)
        {
            var step = FindStep(c, c + 1);
            if (!step.IsUsable(_tile.Area))
            {
                return false;
            }

            for (long y = _tile.Area.Y0; y < _tile.Area.Y1; y += step.Y - (y % step.Y))
            {
                for (long x = _tile.Area.X0; x < _tile.Area.X1; x += step.X - (x % step.X))
                {
                    if (!VisitResolutions(volume, c, x, y))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>Runs the resolutions of one component at one grid position.</summary>
    /// <param name="volume">The volume.</param>
    /// <param name="component">The component.</param>
    /// <param name="x">The reference grid column.</param>
    /// <param name="y">The reference grid row.</param>
    /// <returns><see langword="false"/> when the data has run out or the volume ends.</returns>
    private bool VisitResolutions(in JpxProgressionChange volume, int component, long x, long y)
    {
        var end = Math.Min(volume.ResolutionEnd, _tile.Components[component].Levels + 1);
        for (var r = volume.ResolutionStart; r < end; r++)
        {
            if (!VisitPosition(volume.LayerEnd, r, component, x, y))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads every layer of the precinct that starts at a grid position, if one does.</summary>
    /// <param name="layerEnd">The layer after the last.</param>
    /// <param name="resolution">The resolution.</param>
    /// <param name="component">The component.</param>
    /// <param name="x">The reference grid column.</param>
    /// <param name="y">The reference grid row.</param>
    /// <returns><see langword="false"/> when the data has run out or the volume ends.</returns>
    private bool VisitPosition(int layerEnd, int resolution, int component, long x, long y)
    {
        if (resolution > _tile.Components[component].Levels)
        {
            return true;
        }

        var precinct = Locate(component, resolution, x, y);
        return precinct switch
        {
            Outside => false,
            NoPrecinct => true,
            _ => VisitLayers(layerEnd, resolution, component, precinct),
        };
    }

    /// <summary>Finds the precinct of a component and resolution that starts at a grid position (B.12.1.3).</summary>
    /// <param name="component">The component.</param>
    /// <param name="resolution">The resolution.</param>
    /// <param name="x">The reference grid column.</param>
    /// <param name="y">The reference grid row.</param>
    /// <returns>The precinct index, <see cref="NoPrecinct"/> or <see cref="Outside"/>.</returns>
    private int Locate(int component, int resolution, long x, long y)
    {
        var info = _geometry.Components[component];
        var tileComponent = _tile.Components[component];
        var level = tileComponent.Levels - resolution;
        var layout = _tile.Resolutions[tileComponent.FirstResolution + resolution];
        var shiftX = layout.PrecinctWidthExponent + level;
        var shiftY = layout.PrecinctHeightExponent + level;
        if (shiftX > MaxShift || shiftY > MaxShift || layout.PrecinctCount == 0 || layout.Area.IsEmpty)
        {
            return NoPrecinct;
        }

        if (!StartsPrecinct(y, _tile.Area.Y0, (long)info.Dy << shiftY, ((long)layout.Area.Y0 << level) % (1L << shiftY))
            || !StartsPrecinct(x, _tile.Area.X0, (long)info.Dx << shiftX, ((long)layout.Area.X0 << level) % (1L << shiftX)))
        {
            return NoPrecinct;
        }

        var column = (JpxRectangle.CeilDivide(x, (long)info.Dx << level) >> layout.PrecinctWidthExponent) - (layout.Area.X0 >> layout.PrecinctWidthExponent);
        var row = (JpxRectangle.CeilDivide(y, (long)info.Dy << level) >> layout.PrecinctHeightExponent) - (layout.Area.Y0 >> layout.PrecinctHeightExponent);
        var index = (long)column + ((long)row * layout.PrecinctsWide);
        return column < 0 || row < 0 || index >= layout.PrecinctCount ? Outside : (int)index;
    }

    /// <summary>Finds the grid step of the position orders: the smallest precinct spacing of the components given.</summary>
    /// <param name="first">The first component.</param>
    /// <param name="end">The component after the last.</param>
    /// <returns>The step.</returns>
    private GridStep FindStep(int first, int end)
    {
        var stepX = long.MaxValue;
        var stepY = long.MaxValue;
        for (var c = first; c < end; c++)
        {
            var info = _geometry.Components[c];
            var tileComponent = _tile.Components[c];
            for (var r = 0; r <= tileComponent.Levels; r++)
            {
                var layout = _tile.Resolutions[tileComponent.FirstResolution + r];
                var level = tileComponent.Levels - r;
                stepX = Math.Min(stepX, (long)info.Dx << Math.Min(layout.PrecinctWidthExponent + level, MaxShift));
                stepY = Math.Min(stepY, (long)info.Dy << Math.Min(layout.PrecinctHeightExponent + level, MaxShift));
            }
        }

        return new(stepX, stepY);
    }

    /// <summary>The spacing of the grid positions a position order steps over.</summary>
    /// <param name="X">The horizontal step.</param>
    /// <param name="Y">The vertical step.</param>
    [DebuggerDisplay("GridStep: {X} x {Y}")]
    private readonly record struct GridStep(long X, long Y)
    {
        /// <summary>The most grid positions a position order may step over.</summary>
        private const long MaxPositions = 1L << 24;

        /// <summary>Determines whether stepping a tile at this spacing stays within the position limit.</summary>
        /// <param name="area">The tile area.</param>
        /// <returns><see langword="true"/> when the step can be used.</returns>
        internal bool IsUsable(in JpxRectangle area) =>
            X > 0 && Y > 0 && ((area.Width / X) + 1) * ((area.Height / Y) + 1) <= MaxPositions;
    }
}
