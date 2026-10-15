// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>An underline or strike-through, drawn as a thin bar after the text.</summary>
/// <param name="Color">The colour as 0xRRGGBB, or <see cref="RichStyle.NoColor"/> for the field's colour.</param>
/// <param name="X">The bar's left edge.</param>
/// <param name="Y">The bar's lower edge.</param>
/// <param name="Width">The bar's width.</param>
/// <param name="Thickness">The bar's height.</param>
[DebuggerDisplay("RichRule: {X}, {Y}, {Width} x {Thickness}")]
internal readonly record struct RichRule(int Color, float X, float Y, float Width, float Thickness);
