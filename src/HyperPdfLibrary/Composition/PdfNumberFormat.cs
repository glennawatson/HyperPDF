// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>How a page number or list number is written.</summary>
public enum PdfNumberFormat
{
    /// <summary>Ordinary digits, such as 1, 2 and 3.</summary>
    Decimal = 0,

    /// <summary>Lower case Roman numerals, such as i, ii and iii.</summary>
    LowerRoman = 1,

    /// <summary>Upper case Roman numerals, such as I, II and III.</summary>
    UpperRoman = 2,

    /// <summary>Lower case letters: a to z, then aa to zz.</summary>
    LowerAlpha = 3,

    /// <summary>Upper case letters: A to Z, then AA to ZZ.</summary>
    UpperAlpha = 4,
}
