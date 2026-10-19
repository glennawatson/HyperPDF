// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Content;

/// <summary>
/// Implemented by a content device that wants to know when the glyph procedure of a Type 3 font runs. A bitmap glyph draws the
/// same image mask each time its character is shown, so a device that knows which glyph it is drawing can keep what it makes
/// from the mask and reuse it.
/// </summary>
public interface IType3GlyphScope
{
    /// <summary>Called before a Type 3 glyph's procedure runs.</summary>
    /// <param name="font">The Type 3 font.</param>
    /// <param name="code">The character code.</param>
    void BeginType3Glyph(PdfFont font, int code);

    /// <summary>Called after the glyph's procedure has run, whether or not it finished.</summary>
    void EndType3Glyph();
}
