// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>How a font program is stored in the PDF (ISO 32000-2, 9.8.2 and 9.9).</summary>
internal enum EmbeddedProgramKind
{
    /// <summary>A TrueType font in <c>/FontFile2</c>, shown by a CIDFontType2 font.</summary>
    TrueType = 0,

    /// <summary>A bare CFF font in <c>/FontFile3</c> with subtype <c>/CIDFontType0C</c>, shown by a CIDFontType0 font.</summary>
    CompactFont = 1,

    /// <summary>An OpenType font with CFF outlines in <c>/FontFile3</c> with subtype <c>/OpenType</c>, shown by a CIDFontType0 font.</summary>
    OpenTypeCff = 2,
}
