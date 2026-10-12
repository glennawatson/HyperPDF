// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The parts of the graphics state the PDF/A content rules follow, saved by <c>q</c> and passed to nested content.</summary>
/// <param name="ProcessColor">Whether colour operators set colour; a Type 3 <c>d1</c> glyph and an uncoloured pattern ignore them.</param>
/// <param name="FillPattern">Whether the fill colour space is a pattern space, so <c>scn</c> names a pattern.</param>
/// <param name="StrokePattern">Whether the stroke colour space is a pattern space, so <c>SCN</c> names a pattern.</param>
/// <param name="Font">The current font dictionary, or <see langword="null"/>.</param>
internal readonly record struct PdfAGraphicsSnapshot(bool ProcessColor, bool FillPattern, bool StrokePattern, PdfDictionary? Font)
{
    /// <summary>Gets the state a page or appearance stream starts with.</summary>
    internal static PdfAGraphicsSnapshot Initial => new(true, false, false, null);
}
