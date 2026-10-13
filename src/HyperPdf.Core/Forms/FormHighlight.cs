// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Forms;

/// <summary>The soft tint drawn over fillable form fields so people can see where to type.</summary>
/// <param name="Color">The colour as 0xRRGGBB.</param>
/// <param name="Alpha">The opacity, 0 (clear) to 255 (solid). 0 turns the tint off.</param>
[DebuggerDisplay("FormHighlight: #{Color:X6} alpha {Alpha}")]
public readonly record struct FormHighlight(uint Color, byte Alpha)
{
    /// <summary>A calm mid blue, which stays distinct for most colour-vision types and is not a warning colour.</summary>
    private const uint DefaultColor = 0x7FB2E5U;

    /// <summary>
    /// A quarter opacity. Over white the tint becomes about RGB 223, 236, 248, so black text keeps a contrast ratio near
    /// 17 to 1 (WCAG 2.2 asks for 4.5 to 1) while the field still stands out from the page.
    /// </summary>
    private const byte DefaultAlpha = 64;

    /// <summary>Gets the default tint: a light blue wash over each fillable field.</summary>
    public static FormHighlight Default => new(DefaultColor, DefaultAlpha);

    /// <summary>Gets a value indicating whether the tint is drawn at all.</summary>
    public bool IsVisible => Alpha != 0;
}
