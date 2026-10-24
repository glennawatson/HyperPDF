// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>How a picture parameter set divides the picture into tiles.</summary>
/// <param name="Enabled">Whether the picture has more than one tile.</param>
/// <param name="Columns">The number of tile columns.</param>
/// <param name="Rows">The number of tile rows.</param>
/// <param name="Uniform">Whether the tiles are as equal in size as the picture allows.</param>
/// <param name="ColumnWidths">The widths of all but the last column in coding tree blocks, when the spacing is not uniform.</param>
/// <param name="RowHeights">The heights of all but the last row in coding tree blocks, when the spacing is not uniform.</param>
/// <param name="LoopFilterAcrossTiles">Whether the loop filters may cross tile edges.</param>
internal sealed record H265TileSpec(bool Enabled, int Columns, int Rows, bool Uniform, int[] ColumnWidths, int[] RowHeights, bool LoopFilterAcrossTiles);
