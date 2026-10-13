// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Text;

/// <summary>Where a character of a text page came from.</summary>
public enum PdfTextCharKind
{
    /// <summary>A glyph shown by the content with a known Unicode value.</summary>
    Normal = 0,

    /// <summary>A space or line break the extractor inserted between glyphs.</summary>
    Generated = 1,

    /// <summary>A glyph whose font gives no Unicode value; the character code stands in for it and the page text leaves it out.</summary>
    NotUnicode = 2,

    /// <summary>A hyphen at the end of a line that splits a word; the page text leaves it out, so the word reads whole.</summary>
    Hyphen = 3,

    /// <summary>One character of a glyph's decomposed text, such as a ligature or an Arabic contextual form.</summary>
    Piece = 4,

    /// <summary>A character of a marked content span's /ActualText, which replaces the span's glyphs (ISO 32000-2, 14.9.4).</summary>
    ActualText = 5,
}
