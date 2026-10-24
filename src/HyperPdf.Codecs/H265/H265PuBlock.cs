// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>A prediction block of an inter coding unit.</summary>
/// <param name="Unit">The coding unit it belongs to.</param>
/// <param name="Area">The block's position and size in luma samples.</param>
/// <param name="PartIdx">The block's index among the prediction blocks of the unit.</param>
internal readonly record struct H265PuBlock(H265CodingUnit Unit, BlockRect Area, int PartIdx)
{
    /// <summary>Gets the width of the coding unit.</summary>
    internal int UnitSize => 1 << Unit.Log2Size;
}
