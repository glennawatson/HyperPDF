// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>How a glyph is attached to another by GPOS.</summary>
internal enum AttachKind
{
    /// <summary>It is not attached.</summary>
    None = 0,

    /// <summary>A mark sits on a base, ligature or other mark (GPOS lookup types 4, 5 and 6).</summary>
    Mark = 1,

    /// <summary>A glyph joins the one before it at cursive anchors (GPOS lookup type 3).</summary>
    Cursive = 2,
}
