// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H265;

/// <summary>How a picture is cut into coding tree blocks and tiles, with the scan conversions of 6.5.1 and 6.5.2.</summary>
[DebuggerDisplay("H265PictureLayout: {WidthInCtbs}x{HeightInCtbs} blocks, {TileColumns}x{TileRows} tiles")]
internal sealed class H265PictureLayout
{
    /// <summary>Initializes a new instance of the <see cref="H265PictureLayout"/> class.</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="pps">The picture parameter set.</param>
    /// <exception cref="InvalidDataException">The tile layout does not fit the picture.</exception>
    internal H265PictureLayout(H265Sps sps, H265Pps pps)
    {
        CtbLog2 = sps.Blocks.CtbLog2;
        MinTbLog2 = sps.Blocks.MinTbLog2;
        WidthInCtbs = sps.WidthInCtbs;
        HeightInCtbs = sps.HeightInCtbs;
        TileColumns = pps.Tiles.Columns;
        TileRows = pps.Tiles.Rows;
        ColumnBoundaries = Boundaries(WidthInCtbs, TileColumns, pps.Tiles.Uniform, pps.Tiles.ColumnWidths);
        RowBoundaries = Boundaries(HeightInCtbs, TileRows, pps.Tiles.Uniform, pps.Tiles.RowHeights);
        var count = WidthInCtbs * HeightInCtbs;
        RsToTs = new int[count];
        TsToRs = new int[count];
        TileIdByTs = new int[count];
        TileIdByRs = new int[count];
        BuildScans();
        WidthInMinTbs = (sps.Width + (1 << MinTbLog2) - 1) >> MinTbLog2;
        HeightInMinTbs = (sps.Height + (1 << MinTbLog2) - 1) >> MinTbLog2;
        MinTbAddrZs = BuildZScan();
    }

    /// <summary>Gets the base 2 logarithm of the coding tree block size.</summary>
    internal int CtbLog2 { get; }

    /// <summary>Gets the base 2 logarithm of the smallest transform block size.</summary>
    internal int MinTbLog2 { get; }

    /// <summary>Gets the coding tree blocks in a row.</summary>
    internal int WidthInCtbs { get; }

    /// <summary>Gets the rows of coding tree blocks.</summary>
    internal int HeightInCtbs { get; }

    /// <summary>Gets the smallest transform blocks in a row.</summary>
    internal int WidthInMinTbs { get; }

    /// <summary>Gets the rows of smallest transform blocks.</summary>
    internal int HeightInMinTbs { get; }

    /// <summary>Gets the tile columns.</summary>
    internal int TileColumns { get; }

    /// <summary>Gets the tile rows.</summary>
    internal int TileRows { get; }

    /// <summary>Gets the first coding tree block column of each tile column, then the picture width: one more entry than there are columns.</summary>
    internal int[] ColumnBoundaries { get; }

    /// <summary>Gets the first coding tree block row of each tile row, then the picture height.</summary>
    internal int[] RowBoundaries { get; }

    /// <summary>Gets the tile scan address of each coding tree block, by raster scan address.</summary>
    internal int[] RsToTs { get; }

    /// <summary>Gets the raster scan address of each coding tree block, by tile scan address.</summary>
    internal int[] TsToRs { get; }

    /// <summary>Gets the tile of each coding tree block, by tile scan address.</summary>
    internal int[] TileIdByTs { get; }

    /// <summary>Gets the tile of each coding tree block, by raster scan address.</summary>
    internal int[] TileIdByRs { get; }

    /// <summary>Gets the z-scan order address of each smallest transform block, by raster position.</summary>
    internal int[] MinTbAddrZs { get; }

    /// <summary>Gets the picture area in coding tree blocks.</summary>
    internal int CtbCount => RsToTs.Length;

    /// <summary>Works out where each tile column or row starts (6-3 and 6-4).</summary>
    /// <param name="total">The coding tree blocks along the picture side.</param>
    /// <param name="count">The tiles along the side.</param>
    /// <param name="uniform">Whether the tiles are as equal as possible.</param>
    /// <param name="sizes">The sizes of all but the last tile when they are not uniform.</param>
    /// <returns>The start of each tile and the end of the last.</returns>
    /// <exception cref="InvalidDataException">The sizes add up to more than the side.</exception>
    private static int[] Boundaries(int total, int count, bool uniform, int[] sizes)
    {
        var boundaries = new int[count + 1];
        for (var i = 0; i < count; i++)
        {
            var size = uniform ? (((i + 1) * total) / count) - ((i * total) / count) : ExplicitSize(sizes, i, total - boundaries[i]);
            boundaries[i + 1] = boundaries[i] + size;
        }

        if (boundaries[count] != total)
        {
            throw new InvalidDataException("The H.265 tile layout does not fit the picture.");
        }

        return boundaries;
    }

    /// <summary>Gets the size of a tile from the sizes the picture parameter set lists, which leave out the last tile.</summary>
    /// <param name="sizes">The listed sizes.</param>
    /// <param name="index">The tile's index.</param>
    /// <param name="remaining">The size left for the last tile.</param>
    /// <returns>The size.</returns>
    private static int ExplicitSize(int[] sizes, int index, int remaining) => index < sizes.Length ? sizes[index] : remaining;

    /// <summary>Finds which tile a coding tree block column or row belongs to.</summary>
    /// <param name="boundaries">The start of each tile.</param>
    /// <param name="position">The column or row.</param>
    /// <returns>The tile's index.</returns>
    private static int TileIndex(int[] boundaries, int position)
    {
        var index = 0;
        while (position >= boundaries[index + 1])
        {
            index++;
        }

        return index;
    }

    /// <summary>Fills the raster and tile scan conversions and the tile numbers (6-5 to 6-8).</summary>
    private void BuildScans()
    {
        for (var rs = 0; rs < RsToTs.Length; rs++)
        {
            var blockX = rs % WidthInCtbs;
            var blockY = rs / WidthInCtbs;
            var tileX = TileIndex(ColumnBoundaries, blockX);
            var tileY = TileIndex(RowBoundaries, blockY);
            var address = 0;
            for (var i = 0; i < tileX; i++)
            {
                address += (RowBoundaries[tileY + 1] - RowBoundaries[tileY]) * (ColumnBoundaries[i + 1] - ColumnBoundaries[i]);
            }

            for (var j = 0; j < tileY; j++)
            {
                address += WidthInCtbs * (RowBoundaries[j + 1] - RowBoundaries[j]);
            }

            address += ((blockY - RowBoundaries[tileY]) * (ColumnBoundaries[tileX + 1] - ColumnBoundaries[tileX])) + blockX - ColumnBoundaries[tileX];
            RsToTs[rs] = address;
            TsToRs[address] = rs;
            TileIdByTs[address] = (tileY * TileColumns) + tileX;
            TileIdByRs[rs] = (tileY * TileColumns) + tileX;
        }
    }

    /// <summary>Fills the z-scan order address of each smallest transform block (6-10).</summary>
    /// <returns>The addresses.</returns>
    private int[] BuildZScan()
    {
        var zscan = new int[WidthInMinTbs * HeightInMinTbs];
        var shift = CtbLog2 - MinTbLog2;
        for (var y = 0; y < HeightInMinTbs; y++)
        {
            for (var x = 0; x < WidthInMinTbs; x++)
            {
                var address = RsToTs[(WidthInCtbs * ((y << MinTbLog2) >> CtbLog2)) + ((x << MinTbLog2) >> CtbLog2)] << (shift << 1);
                for (var i = 0; i < shift; i++)
                {
                    var m = 1 << i;
                    var weight = m * m;
                    address += ((m & x) != 0 ? weight : 0) + ((m & y) != 0 ? weight << 1 : 0);
                }

                zscan[(y * WidthInMinTbs) + x] = address;
            }
        }

        return zscan;
    }
}
