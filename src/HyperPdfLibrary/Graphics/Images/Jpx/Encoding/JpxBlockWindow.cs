// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>Where a code-block's coefficients are in a tile-component's buffer, and what the block coder needs to know about its sub-band.</summary>
/// <param name="X">The first column in the buffer.</param>
/// <param name="Y">The first row in the buffer.</param>
/// <param name="Width">The block width.</param>
/// <param name="Height">The block height.</param>
/// <param name="Orientation">The sub-band orientation: 0 for LL, 1 for HL, 2 for LH, 3 for HH.</param>
/// <param name="Style">The code-block mode switches.</param>
internal readonly record struct JpxBlockWindow(int X, int Y, int Width, int Height, int Orientation, JpxBlockStyle Style);
