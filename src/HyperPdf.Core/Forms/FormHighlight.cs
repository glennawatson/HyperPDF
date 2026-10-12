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
    /// <summary>The colour PDFium draws by default.</summary>
    private const uint DefaultColor = 0xB4CCDCU;

    /// <summary>The opacity PDFium draws by default.</summary>
    private const byte DefaultAlpha = 72;

    /// <summary>Gets the default tint, the one PDFium draws.</summary>
    public static FormHighlight Default => new(DefaultColor, DefaultAlpha);

    /// <summary>Gets a value indicating whether the tint is drawn at all.</summary>
    public bool IsVisible => Alpha != 0;
}
