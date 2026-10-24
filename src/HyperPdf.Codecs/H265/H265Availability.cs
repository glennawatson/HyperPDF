// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H265;

/// <summary>The availability derivation process for a block in z-scan order (6.4.1).</summary>
internal static class H265Availability
{
    /// <summary>Tells whether a neighbouring luma location may be used from the current location.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="currentX">The current column in luma samples.</param>
    /// <param name="currentY">The current row in luma samples.</param>
    /// <param name="neighbourX">The neighbour's column.</param>
    /// <param name="neighbourY">The neighbour's row.</param>
    /// <returns><see langword="true"/> when the neighbour is in the picture, comes first in z-scan order, and is in the same slice and tile.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsAvailable(H265SliceContext context, int currentX, int currentY, int neighbourX, int neighbourY)
    {
        var sps = context.Sps;
        if ((uint)neighbourX >= (uint)sps.Width || (uint)neighbourY >= (uint)sps.Height)
        {
            return false;
        }

        var layout = context.Layout;
        var shift = layout.MinTbLog2;
        var neighbourOrder = layout.MinTbAddrZs[((neighbourY >> shift) * layout.WidthInMinTbs) + (neighbourX >> shift)];
        var currentOrder = layout.MinTbAddrZs[((currentY >> shift) * layout.WidthInMinTbs) + (currentX >> shift)];
        if (neighbourOrder > currentOrder)
        {
            return false;
        }

        var ctbNb = ((neighbourY >> layout.CtbLog2) * layout.WidthInCtbs) + (neighbourX >> layout.CtbLog2);
        var ctbCurr = ((currentY >> layout.CtbLog2) * layout.WidthInCtbs) + (currentX >> layout.CtbLog2);
        return context.Picture.SliceOfCtb[ctbNb] == context.SliceAddrRs && layout.TileIdByRs[ctbNb] == layout.TileIdByRs[ctbCurr];
    }
}
