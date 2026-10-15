// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>How the labels of an inferred list are made; the values of the List <c>/ListNumbering</c> attribute (ISO 32000-2 14.8.5.5).</summary>
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
}
