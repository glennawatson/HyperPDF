// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>One symbol placed by a text region.</summary>
/// <param name="Rank">The symbol's ID: its position among the symbols the region refers to.</param>
/// <param name="Left">The column of the placed bitmap's left edge.</param>
/// <param name="Bottom">The row of the placed bitmap's bottom edge.</param>
/// <param name="Width">The width of the placed bitmap: the symbol's, or the refined bitmap's.</param>
/// <param name="Component">The component whose bitmap refines the symbol, or -1 when the symbol is placed as it is.</param>
internal readonly record struct Jbig2TextInstance(int Rank, int Left, int Bottom, int Width, int Component);
