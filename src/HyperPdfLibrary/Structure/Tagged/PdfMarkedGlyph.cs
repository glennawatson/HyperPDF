// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>One glyph a page draws, with the marked content it is drawn in.</summary>
/// <param name="Bounds">The glyph's box in viewer space, from its advance and the font's ascent and descent.</param>
/// <param name="Mcid">The marked content id of the innermost marked content with one; -1 when none.</param>
/// <param name="IsArtifact">Whether the glyph is inside <c>/Artifact</c> marked content: a running header, page number or decoration.</param>
/// <param name="TextStart">Where the glyph's Unicode text starts in <see cref="PdfMarkedContentPage.Text"/>.</param>
/// <param name="TextLength">The length of the glyph's Unicode text; zero when the font gives none.</param>
/// <param name="FontSize">The font size times the text matrix's vertical scale, in points.</param>
[DebuggerDisplay("PdfMarkedGlyph: mcid {Mcid} artifact={IsArtifact}")]
public readonly record struct PdfMarkedGlyph(PdfViewerRect Bounds, int Mcid, bool IsArtifact, int TextStart, int TextLength, float FontSize);
