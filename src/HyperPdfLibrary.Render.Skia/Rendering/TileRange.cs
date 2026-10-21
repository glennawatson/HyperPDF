// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>The lattice positions of a tiling period that reach an area.</summary>
/// <param name="FirstColumn">The first column.</param>
/// <param name="LastColumn">The last column.</param>
/// <param name="FirstRow">The first row.</param>
/// <param name="LastRow">The last row.</param>
internal readonly record struct TileRange(int FirstColumn, int LastColumn, int FirstRow, int LastRow)
{
    /// <summary>Gets the number of positions.</summary>
    internal long Count => (long)(LastColumn - FirstColumn + 1) * (LastRow - FirstRow + 1);

    /// <summary>Finds the positions of a period that reach an area.</summary>
    /// <param name="reach">The area in pattern space.</param>
    /// <param name="tile">One period in pattern space.</param>
    /// <returns>The positions, or null when the area is not finite.</returns>
    internal static TileRange? Create(SKRect reach, SKRect tile) =>
        !float.IsFinite(reach.Width) || !float.IsFinite(reach.Height)
            ? null
            : new(
                (int)MathF.Floor((reach.Left - tile.Left) / tile.Width),
                (int)MathF.Floor((reach.Right - tile.Left) / tile.Width),
                (int)MathF.Floor((reach.Top - tile.Top) / tile.Height),
                (int)MathF.Floor((reach.Bottom - tile.Top) / tile.Height));
}
