// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Text.Layout;

/// <summary>One glyph from shaping, sized for a font size of one point.</summary>
/// <param name="Glyph">The glyph index in the font.</param>
/// <param name="Cluster">The index of the first character the glyph shows, in the shaped text.</param>
/// <param name="Advance">How far the pen moves after the glyph, in ems, kerning included.</param>
/// <param name="OffsetX">How far the glyph is drawn right of the pen, in ems.</param>
/// <param name="OffsetY">How far the glyph is drawn above the baseline, in ems.</param>
[DebuggerDisplay("ShapedGlyph: {Glyph} at cluster {Cluster}")]
public readonly record struct ShapedGlyph(ushort Glyph, int Cluster, float Advance, float OffsetX, float OffsetY);
