// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>The kind of outlines a font file holds.</summary>
public enum PdfFontOutlines
{
    /// <summary>TrueType quadratic outlines in a 'glyf' table; embedded as a CIDFontType2 font.</summary>
    TrueType = 0,

    /// <summary>Compact Font Format cubic outlines in a 'CFF ' table; embedded as a CIDFontType0 font.</summary>
    CompactFontFormat = 1,
}
