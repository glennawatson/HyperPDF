// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>How the page number in a page label is written (ISO 32000-2, 12.4.2).</summary>
public enum PdfPageLabelStyle
{
    /// <summary>No number: the label is only its prefix.</summary>
    None = 0,

    /// <summary>Decimal digits: 1, 2, 3.</summary>
    Decimal = 1,

    /// <summary>Upper-case Roman numerals: I, II, III.</summary>
    UpperRoman = 2,

    /// <summary>Lower-case Roman numerals: i, ii, iii.</summary>
    LowerRoman = 3,

    /// <summary>Upper-case letters: A, B, C, then AA, BB, CC.</summary>
    UpperLetters = 4,

    /// <summary>Lower-case letters: a, b, c, then aa, bb, cc.</summary>
    LowerLetters = 5,
}
