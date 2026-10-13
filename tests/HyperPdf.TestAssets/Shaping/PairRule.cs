// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets.Shaping;

/// <summary>A pair adjustment for a second glyph after a first.</summary>
/// <param name="Second">The second glyph.</param>
/// <param name="First">The adjustment of the first glyph.</param>
/// <param name="Next">The adjustment of the second glyph.</param>
public readonly record struct PairRule(int Second, ValueAdjustment First, ValueAdjustment Next);
