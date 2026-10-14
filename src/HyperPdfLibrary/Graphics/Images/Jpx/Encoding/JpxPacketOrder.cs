// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// Lists the packets of a tile in a progression order (ISO 15444-1 B.12). Each packet gets a sort key that packs the order's
/// fields, most significant first; the position progressions take where the precinct sits on the reference grid from
/// B.12.1.3. The packets are then sorted by key.
/// </summary>
internal static class JpxPacketOrder
{
    /// <summary>The bits of the layer field.</summary>
    private const int LayerBits = 16;

    /// <summary>The bits of the resolution and component fields.</summary>
    private const int SmallBits = 6;

    /// <summary>The bits of a precinct index or a position.</summary>
    private const int WideBits = 32;

    /// <summary>The values in each pair of a value and its width.</summary>
    private const int Pair = 2;

    /// <summary>Lists and sorts the packets of a tile.</summary>
    /// <param name="tile">The tile layout.</param>
    /// <param name="geometry">The image geometry.</param>
    /// <param name="settings">The coding parameters.</param>
    /// <param name="keys">Receives the packets in order; emptied first.</param>
    internal static void Build(JpxTile tile, JpxGeometry geometry, JpxEncodeSettings settings, JpxList<JpxPacketKey> keys)
    {
        keys.Clear();
        for (var c = 0; c < tile.Components.Length; c++)
        {
            var component = tile.Components[c];
            for (var r = 0; r <= component.Levels; r++)
            {
                AddResolution(tile, geometry, settings, keys, c, r);
            }
        }

        keys.AsSpan().Sort();
    }

    /// <summary>Adds a key per layer for each precinct of one resolution.</summary>
    /// <param name="tile">The tile layout.</param>
    /// <param name="geometry">The image geometry.</param>
    /// <param name="settings">The coding parameters.</param>
    /// <param name="keys">The packets.</param>
    /// <param name="component">The component.</param>
    /// <param name="resolution">The resolution.</param>
    private static void AddResolution(JpxTile tile, JpxGeometry geometry, JpxEncodeSettings settings, JpxList<JpxPacketKey> keys, int component, int resolution)
    {
        var info = tile.Components[component];
        var layout = tile.Resolutions[info.FirstResolution + resolution];
        if (layout.Area.IsEmpty)
        {
            return;
        }

        var level = info.Levels - resolution;
        var step = geometry.Components[component];
        for (var p = 0; p < layout.PrecinctCount; p++)
        {
            var x = Position(layout.Area.X0, layout.PrecinctWidthExponent, p % layout.PrecinctsWide, level, step.Dx, tile.Area.X0);
            var y = Position(layout.Area.Y0, layout.PrecinctHeightExponent, p / layout.PrecinctsWide, level, step.Dy, tile.Area.Y0);
            for (var l = 0; l < settings.Layers; l++)
            {
                _ = keys.Add(new(l, resolution, component, p, Pack(settings.Order, new(l, resolution, component, p, x, y))));
            }
        }
    }

    /// <summary>Gets where on the reference grid the position progressions meet a precinct (B.12.1.3).</summary>
    /// <param name="start">The resolution's first coordinate.</param>
    /// <param name="exponent">The precinct size exponent.</param>
    /// <param name="index">The precinct's column or row.</param>
    /// <param name="level">The resolution's decomposition level.</param>
    /// <param name="subsampling">The component's subsampling.</param>
    /// <param name="tileStart">The tile's first reference grid coordinate.</param>
    /// <returns>The reference grid coordinate.</returns>
    private static long Position(int start, int exponent, int index, int level, int subsampling, int tileStart)
    {
        var grid = (long)((start >> exponent) + index) << exponent;
        return index == 0 && start % (1 << exponent) != 0 ? tileStart : (grid << level) * subsampling;
    }

    /// <summary>Packs the fields of a packet into a sort key, the order's most significant field first.</summary>
    /// <param name="order">The progression order.</param>
    /// <param name="fields">The packet's fields.</param>
    /// <returns>The key.</returns>
    private static UInt128 Pack(JpxProgressionOrder order, in Fields fields) => order switch
    {
        JpxProgressionOrder.LayerResolutionComponentPosition => Join(fields.Layer, LayerBits, fields.Resolution, SmallBits, fields.Component, SmallBits, fields.Precinct, WideBits),
        JpxProgressionOrder.ResolutionLayerComponentPosition => Join(fields.Resolution, SmallBits, fields.Layer, LayerBits, fields.Component, SmallBits, fields.Precinct, WideBits),
        JpxProgressionOrder.ResolutionPositionComponentLayer => Join(fields.Resolution, SmallBits, fields.Y, WideBits, fields.X, WideBits, fields.Component, SmallBits, fields.Layer, LayerBits),
        JpxProgressionOrder.PositionComponentResolutionLayer => Join(fields.Y, WideBits, fields.X, WideBits, fields.Component, SmallBits, fields.Resolution, SmallBits, fields.Layer, LayerBits),
        _ => Join(fields.Component, SmallBits, fields.Y, WideBits, fields.X, WideBits, fields.Resolution, SmallBits, fields.Layer, LayerBits),
    };

    /// <summary>Concatenates fields into one key, the first most significant.</summary>
    /// <param name="parts">Pairs of a value and its width in bits.</param>
    /// <returns>The key.</returns>
    private static UInt128 Join(params ReadOnlySpan<long> parts)
    {
        UInt128 key = 0;
        for (var i = 0; i + 1 < parts.Length; i += Pair)
        {
            key = (key << (int)parts[i + 1]) | (ulong)parts[i];
        }

        return key;
    }

    /// <summary>The fields of one packet, with its position on the reference grid.</summary>
    /// <param name="Layer">The layer.</param>
    /// <param name="Resolution">The resolution.</param>
    /// <param name="Component">The component.</param>
    /// <param name="Precinct">The precinct.</param>
    /// <param name="X">The precinct's horizontal position on the reference grid.</param>
    /// <param name="Y">The precinct's vertical position on the reference grid.</param>
    private readonly record struct Fields(int Layer, int Resolution, int Component, int Precinct, long X, long Y);
}
