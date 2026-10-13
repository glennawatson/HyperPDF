// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The shape of one resolution level's rebuild from the next lower resolution and three detail sub-bands.</summary>
/// <param name="Width">The resolution's width.</param>
/// <param name="Height">The resolution's height.</param>
/// <param name="LowWidth">The low-pass columns: the next lower resolution's width.</param>
/// <param name="LowHeight">The low-pass rows: the next lower resolution's height.</param>
/// <param name="ColumnParity">1 when the resolution starts on an odd column.</param>
/// <param name="RowParity">1 when the resolution starts on an odd row.</param>
internal readonly record struct JpxLevelShape(int Width, int Height, int LowWidth, int LowHeight, int ColumnParity, int RowParity)
{
    /// <summary>Gets the high-pass columns.</summary>
    internal int HighWidth => Width - LowWidth;

    /// <summary>Gets the high-pass rows.</summary>
    internal int HighHeight => Height - LowHeight;

    /// <summary>Makes the shape of a resolution from its area and the area of the next lower resolution.</summary>
    /// <param name="area">The resolution's area.</param>
    /// <param name="low">The next lower resolution's area.</param>
    /// <returns>The shape.</returns>
    internal static JpxLevelShape Of(in JpxRectangle area, in JpxRectangle low) =>
        new(area.Width, area.Height, low.Width, low.Height, area.X0 & 1, area.Y0 & 1);
}
