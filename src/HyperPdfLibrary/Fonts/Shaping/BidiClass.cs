// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>The bidirectional type of a character (Unicode Standard Annex #9, table 4).</summary>
internal enum BidiClass
{
    /// <summary>Left-to-right letter.</summary>
    L = 0,

    /// <summary>Right-to-left letter.</summary>
    R = 1,

    /// <summary>Arabic letter.</summary>
    AL = 2,

    /// <summary>European digit.</summary>
    EN = 3,

    /// <summary>European number separator.</summary>
    ES = 4,

    /// <summary>European number terminator.</summary>
    ET = 5,

    /// <summary>Arabic digit.</summary>
    AN = 6,

    /// <summary>Common number separator.</summary>
    CS = 7,

    /// <summary>Non-spacing mark.</summary>
    NSM = 8,

    /// <summary>Boundary neutral, such as a joiner.</summary>
    BN = 9,

    /// <summary>Paragraph separator.</summary>
    B = 10,

    /// <summary>Segment separator.</summary>
    S = 11,

    /// <summary>White space.</summary>
    WS = 12,

    /// <summary>Other neutral.</summary>
    ON = 13,
}
