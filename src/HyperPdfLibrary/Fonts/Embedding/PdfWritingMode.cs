// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>The direction text advances in (ISO 32000-2, 9.7.4.2).</summary>
public enum PdfWritingMode
{
    /// <summary>Glyphs advance sideways; the font is written with <c>Identity-H</c>.</summary>
    Horizontal = 0,

    /// <summary>Glyphs advance downward; the font is written with <c>Identity-V</c> and vertical metrics.</summary>
    Vertical = 1,
}
