// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The availability derivation process for a prediction block (6.4.2).</summary>
internal static class H265PuAvailability
{
    /// <summary>Tells whether the prediction block covering a neighbouring location can be used for motion prediction.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The current prediction block.</param>
    /// <param name="neighbourX">The neighbouring location's column.</param>
    /// <param name="neighbourY">The neighbouring location's row.</param>
    /// <returns><see langword="true"/> when the neighbour is available and inter predicted.</returns>
    internal static bool IsAvailable(H265SliceContext context, in H265PuBlock block, int neighbourX, int neighbourY)
    {
        var unit = block.Unit;
        var size = block.UnitSize;
        var sameUnit = unit.X <= neighbourX && unit.Y <= neighbourY && unit.X + size > neighbourX && unit.Y + size > neighbourY;
        var available = sameUnit
            ? !(block.Area.Width << 1 == size && block.Area.Height << 1 == size && block.PartIdx == 1
                && unit.Y + block.Area.Height <= neighbourY && unit.X + block.Area.Width > neighbourX)
            : H265Availability.IsAvailable(context, block.Area.X, block.Area.Y, neighbourX, neighbourY);
        return available && context.Picture.Kind[context.Picture.GridIndex(neighbourX, neighbourY)] == H265Picture.Inter;
    }
}
