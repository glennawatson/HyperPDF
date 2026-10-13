// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The three detail sub-bands of a resolution level.</summary>
/// <param name="HorizontalHigh">The HL band: high-pass along the rows, low-pass down the columns.</param>
/// <param name="VerticalHigh">The LH band: low-pass along the rows, high-pass down the columns.</param>
/// <param name="Diagonal">The HH band: high-pass both ways.</param>
internal readonly record struct JpxDetailBands(JpxRowSource HorizontalHigh, JpxRowSource VerticalHigh, JpxRowSource Diagonal);
