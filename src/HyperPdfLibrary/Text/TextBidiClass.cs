// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Text;

/// <summary>The Bidi_Class of a character (Unicode Standard Annex #9, table 4). The values index the generated tables.</summary>
internal enum TextBidiClass
{
    /// <summary>Left-to-right letter.</summary>
    L = 0,

    /// <summary>Right-to-left letter, such as Hebrew.</summary>
    R = 1,

    /// <summary>Arabic letter.</summary>
    AL = 2,

    /// <summary>European number.</summary>
    EN = 3,

    /// <summary>European number separator.</summary>
    ES = 4,

    /// <summary>European number terminator.</summary>
    ET = 5,

    /// <summary>Arabic number.</summary>
    AN = 6,

    /// <summary>Common number separator.</summary>
    CS = 7,

    /// <summary>Non-spacing mark.</summary>
    NSM = 8,

    /// <summary>Boundary neutral, such as a format control.</summary>
    BN = 9,

    /// <summary>Paragraph separator.</summary>
    B = 10,

    /// <summary>Segment separator, such as a tab.</summary>
    S = 11,

    /// <summary>White space.</summary>
    WS = 12,

    /// <summary>Other neutral, such as most punctuation.</summary>
    ON = 13,

    /// <summary>Left-to-right embedding.</summary>
    LRE = 14,

    /// <summary>Left-to-right override.</summary>
    LRO = 15,

    /// <summary>Right-to-left embedding.</summary>
    RLE = 16,

    /// <summary>Right-to-left override.</summary>
    RLO = 17,

    /// <summary>Pop directional format.</summary>
    PDF = 18,

    /// <summary>Left-to-right isolate.</summary>
    LRI = 19,

    /// <summary>Right-to-left isolate.</summary>
    RLI = 20,

    /// <summary>First strong isolate.</summary>
    FSI = 21,

    /// <summary>Pop directional isolate.</summary>
    PDI = 22,
}
