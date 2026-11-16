// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>How the number in a page label is written.</summary>
public enum PageLabelStyle
{
    /// <summary>No number: the label is only its prefix.</summary>
    None = 0,

    /// <summary>Decimal digits: 1, 2, 3.</summary>
    Decimal = 1,

    /// <summary>Upper-case Roman numerals: I, II, III.</summary>
    UpperRoman = 2,

    /// <summary>Lower-case Roman numerals: i, ii, iii.</summary>
    LowerRoman = 3,

    /// <summary>Upper-case letters: A, B, C.</summary>
    UpperLetters = 4,

    /// <summary>Lower-case letters: a, b, c.</summary>
    LowerLetters = 5,
}
