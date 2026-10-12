// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Text.Layout;

/// <summary>A glyph placed in a text box.</summary>
/// <param name="Glyph">The glyph index in the font.</param>
/// <param name="X">The pen position, in points from the box's left edge.</param>
/// <param name="Y">The baseline, in points down from the box's top edge.</param>
/// <param name="Cluster">The index of the first character the glyph shows, in the whole text.</param>
[DebuggerDisplay("LaidGlyph: {Glyph} at ({X}, {Y})")]
public readonly record struct LaidGlyph(ushort Glyph, float X, float Y, int Cluster);
