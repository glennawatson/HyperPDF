// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>How a character joins its neighbours in cursive scripts (Unicode Standard, "Arabic" chapter, table 9-3).</summary>
internal enum JoiningType
{
    /// <summary>It does not join (non-joining).</summary>
    None = 0,

    /// <summary>It joins the letter before it only, as alef does (right-joining).</summary>
    Right = 1,

    /// <summary>It joins the letter after it only (left-joining).</summary>
    Left = 2,

    /// <summary>It joins on both sides (dual-joining).</summary>
    Dual = 3,

    /// <summary>It makes its neighbours join, as tatweel and the joiner do (join-causing).</summary>
    Causing = 4,

    /// <summary>It is a mark that does not affect joining (transparent).</summary>
    Transparent = 5,
}
