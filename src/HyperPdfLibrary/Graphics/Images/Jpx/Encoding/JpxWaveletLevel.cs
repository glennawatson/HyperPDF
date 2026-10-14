// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>One decomposition level of a tile-component as the forward wavelet sees it: the size of the resolution it splits and the sizes of the low-pass part.</summary>
/// <param name="Width">The width of the resolution the level splits.</param>
/// <param name="Height">The height of the resolution the level splits.</param>
/// <param name="ColumnParity">1 when the resolution starts on an odd column, so its first column is high-pass.</param>
/// <param name="RowParity">1 when the resolution starts on an odd row, so its first row is high-pass.</param>
/// <param name="LowColumns">The columns of the low-pass part: the width of the next resolution down.</param>
/// <param name="LowRows">The rows of the low-pass part: the height of the next resolution down.</param>
internal readonly record struct JpxWaveletLevel(int Width, int Height, int ColumnParity, int RowParity, int LowColumns, int LowRows)
{
    /// <summary>Finds the level that splits a resolution into the one below it and the detail sub-bands.</summary>
    /// <param name="tile">The tile, which holds the resolution areas.</param>
    /// <param name="component">The tile-component.</param>
    /// <param name="resolution">The resolution being split, 1 or more.</param>
    /// <returns>The level.</returns>
    internal static JpxWaveletLevel At(JpxTile tile, in JpxTileComponent component, int resolution)
    {
        var area = tile.Resolutions[component.FirstResolution + resolution].Area;
        var low = tile.Resolutions[component.FirstResolution + resolution - 1].Area;
        return new(area.Width, area.Height, area.X0 & 1, area.Y0 & 1, low.Width, low.Height);
    }

    /// <summary>Reflects an index about the ends of a line: whole-sample symmetric extension (equation F-4).</summary>
    /// <param name="index">The index, at most one outside the line.</param>
    /// <param name="count">The samples in the line, at least two.</param>
    /// <returns>The index of the sample to read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Mirror(int index, int count)
    {
        if (index < 0)
        {
            return -index;
        }

        var last = count - 1;
        return index > last ? last - (index - last) : index;
    }
}
