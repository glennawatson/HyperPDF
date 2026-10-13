// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>The style flags of a font face.</summary>
[Flags]
internal enum FaceStyleFlags
{
    /// <summary>No flags.</summary>
    None = 0,

    /// <summary>Every glyph has the same advance.</summary>
    FixedPitch = 1 << 0,

    /// <summary>The face's classification says it has serifs.</summary>
    Serif = 1 << 1,

    /// <summary>The face is italic or oblique.</summary>
    Italic = 1 << 2,

    /// <summary>The face is bold.</summary>
    Bold = 1 << 3,

    /// <summary>The face has a Unicode character map.</summary>
    UnicodeCmap = 1 << 4,
}
