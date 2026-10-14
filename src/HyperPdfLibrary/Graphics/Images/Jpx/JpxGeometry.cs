// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The image, tile and component layout on the reference grid, read from the SIZ marker.</summary>
/// <param name="Image">The image area on the reference grid.</param>
/// <param name="TileWidth">The width of a tile.</param>
/// <param name="TileHeight">The height of a tile.</param>
/// <param name="TileX0">The left edge of the first tile.</param>
/// <param name="TileY0">The top edge of the first tile.</param>
/// <param name="Components">The components.</param>
[DebuggerDisplay("JpxGeometry: {Image.Width}x{Image.Height}, {Components.Length} components")]
internal sealed record JpxGeometry(JpxRectangle Image, int TileWidth, int TileHeight, int TileX0, int TileY0, JpxComponentInfo[] Components)
{
    /// <summary>The most components decoded.</summary>
    internal const int MaxComponents = 16_384;

    /// <summary>The most tiles decoded; the tile index is a 16-bit number.</summary>
    internal const int MaxTiles = 65_535;

    /// <summary>The widest sample precision decoded.</summary>
    internal const int MaxPrecision = 31;

    /// <summary>The most samples of all components together.</summary>
    internal const long MaxSamples = 1L << 27;

    /// <summary>The bytes of the fixed part of the SIZ segment.</summary>
    private const int FixedBytes = 36;

    /// <summary>The bytes of each component entry.</summary>
    private const int ComponentBytes = 3;

    /// <summary>The offset of the image width.</summary>
    private const int WidthOffset = 2;

    /// <summary>The offset of the image height.</summary>
    private const int HeightOffset = 6;

    /// <summary>The offset of the image left edge.</summary>
    private const int X0Offset = 10;

    /// <summary>The offset of the image top edge.</summary>
    private const int Y0Offset = 14;

    /// <summary>The offset of the tile width.</summary>
    private const int TileWidthOffset = 18;

    /// <summary>The offset of the tile height.</summary>
    private const int TileHeightOffset = 22;

    /// <summary>The offset of the tile grid left edge.</summary>
    private const int TileX0Offset = 26;

    /// <summary>The offset of the tile grid top edge.</summary>
    private const int TileY0Offset = 30;

    /// <summary>The offset of the component count.</summary>
    private const int CountOffset = 34;

    /// <summary>The bit of the precision byte that marks signed samples.</summary>
    private const int SignedBit = 0x80;

    /// <summary>The bits of the precision byte that hold the precision less one.</summary>
    private const int PrecisionMask = 0x7F;

    /// <summary>The offset of the vertical subsampling within a component entry.</summary>
    private const int DyOffset = 2;

    /// <summary>Gets the number of tiles across the image.</summary>
    internal int TilesAcross => JpxRectangle.CeilDivide(Image.X1 - TileX0, TileWidth);

    /// <summary>Gets the number of tiles down the image.</summary>
    internal int TilesDown => JpxRectangle.CeilDivide(Image.Y1 - TileY0, TileHeight);

    /// <summary>Reads a SIZ segment.</summary>
    /// <param name="segment">The segment after its length field.</param>
    /// <returns>The geometry, or <see langword="null"/> when it is invalid or too large.</returns>
    internal static JpxGeometry? Read(ReadOnlySpan<byte> segment)
    {
        if (segment.Length < FixedBytes)
        {
            return null;
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(segment[CountOffset..]);
        if (count is 0 or > MaxComponents || segment.Length < FixedBytes + (count * ComponentBytes))
        {
            return null;
        }

        var image = new JpxRectangle(ReadInt(segment, X0Offset), ReadInt(segment, Y0Offset), ReadInt(segment, WidthOffset), ReadInt(segment, HeightOffset));
        var components = ReadComponents(segment.Slice(FixedBytes, count * ComponentBytes), count);
        if (components is null)
        {
            return null;
        }

        var geometry = new JpxGeometry(image, ReadInt(segment, TileWidthOffset), ReadInt(segment, TileHeightOffset), ReadInt(segment, TileX0Offset), ReadInt(segment, TileY0Offset), components);
        return geometry.IsValid() ? geometry : null;
    }

    /// <summary>Gets a tile's area on the reference grid.</summary>
    /// <param name="tile">The tile index.</param>
    /// <returns>The tile area clipped to the image.</returns>
    internal JpxRectangle GetTile(int tile)
    {
        var column = tile % TilesAcross;
        var row = tile / TilesAcross;
        var x0 = TileX0 + ((long)column * TileWidth);
        var y0 = TileY0 + ((long)row * TileHeight);
        return new(
            (int)Math.Max(x0, Image.X0),
            (int)Math.Max(y0, Image.Y0),
            (int)Math.Min(x0 + TileWidth, Image.X1),
            (int)Math.Min(y0 + TileHeight, Image.Y1));
    }

    /// <summary>Gets an area in a component's sample grid.</summary>
    /// <param name="area">The area on the reference grid.</param>
    /// <param name="component">The component index.</param>
    /// <returns>The area in component samples.</returns>
    internal JpxRectangle ToComponent(in JpxRectangle area, int component)
    {
        var info = Components[component];
        return new(
            JpxRectangle.CeilDivide(area.X0, info.Dx),
            JpxRectangle.CeilDivide(area.Y0, info.Dy),
            JpxRectangle.CeilDivide(area.X1, info.Dx),
            JpxRectangle.CeilDivide(area.Y1, info.Dy));
    }

    /// <summary>Reads a big-endian 32-bit value that must fit a signed int.</summary>
    /// <param name="segment">The segment.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The value, or -1 when it does not fit.</returns>
    private static int ReadInt(ReadOnlySpan<byte> segment, int offset)
    {
        var value = BinaryPrimitives.ReadUInt32BigEndian(segment[offset..]);
        return value > int.MaxValue ? -1 : (int)value;
    }

    /// <summary>Reads the component entries.</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="count">The number of components.</param>
    /// <returns>The components, or <see langword="null"/> when one is invalid.</returns>
    private static JpxComponentInfo[]? ReadComponents(ReadOnlySpan<byte> entries, int count)
    {
        var components = new JpxComponentInfo[count];
        for (var i = 0; i < count; i++)
        {
            var entry = entries.Slice(i * ComponentBytes, ComponentBytes);
            var precision = (entry[0] & PrecisionMask) + 1;
            var dx = entry[1];
            var dy = entry[DyOffset];
            if (precision > MaxPrecision || dx == 0 || dy == 0)
            {
                return null;
            }

            components[i] = new(precision, (entry[0] & SignedBit) != 0, dx, dy);
        }

        return components;
    }

    /// <summary>Checks the sizes against each other and the decoding limits.</summary>
    /// <returns><see langword="true"/> when the geometry can be decoded.</returns>
    private bool IsValid() =>
        Image is { X0: >= 0, Y0: >= 0, IsEmpty: false }
        && TilesValid()
        && (long)TilesAcross * TilesDown <= MaxTiles
        && (long)Image.Width * Image.Height <= ImageHeader.MaxPixels
        && CountSamples() <= MaxSamples;

    /// <summary>Checks that the tile grid is positive and its first tile overlaps the image (equation B-3).</summary>
    /// <returns><see langword="true"/> when valid.</returns>
    private bool TilesValid() =>
        TileWidth > 0 && TileHeight > 0 && TileX0 >= 0 && TileY0 >= 0
        && TileX0 <= Image.X0 && TileY0 <= Image.Y0
        && (long)TileX0 + TileWidth > Image.X0 && (long)TileY0 + TileHeight > Image.Y0;

    /// <summary>Counts the samples of every component.</summary>
    /// <returns>The total.</returns>
    private long CountSamples()
    {
        var total = 0L;
        for (var i = 0; i < Components.Length; i++)
        {
            var area = ToComponent(Image, i);
            total += (long)area.Width * area.Height;
        }

        return total;
    }
}
