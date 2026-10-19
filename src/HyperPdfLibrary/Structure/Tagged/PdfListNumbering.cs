// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>The label scheme of a tagged or inferred list (ISO 32000-2 14.8.5.5).</summary>
public enum PdfListNumbering
{
    /// <summary>The labels are arbitrary text, such as dashes.</summary>
    None = 0,

    /// <summary>Solid round bullets.</summary>
    Disc = 1,

    /// <summary>Open round bullets.</summary>
    Circle = 2,

    /// <summary>Solid square bullets.</summary>
    Square = 3,

    /// <summary>Decimal Arabic numerals.</summary>
    Decimal = 4,

    /// <summary>Upper case Roman numerals.</summary>
    UpperRoman = 5,

    /// <summary>Lower case Roman numerals.</summary>
    LowerRoman = 6,

    /// <summary>Upper case letters.</summary>
    UpperAlpha = 7,

    /// <summary>Lower case letters.</summary>
    LowerAlpha = 8,

    /// <summary>Unspecified bullets in a PDF 2.0 unordered list.</summary>
    Unordered = 9,

    /// <summary>Unspecified numbering in a PDF 2.0 ordered list.</summary>
    Ordered = 10,

    /// <summary>Terms followed by their definitions in a PDF 2.0 list.</summary>
    Description = 11,
}
