// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Charts;

/// <summary>Where one legend entry goes.</summary>
/// <param name="Index">The entry's place in the legend.</param>
/// <param name="X">The distance from the legend's left edge to the entry's swatch.</param>
/// <param name="Row">The row, counted from the top.</param>
internal readonly record struct LegendSpot(int Index, float X, int Row);
