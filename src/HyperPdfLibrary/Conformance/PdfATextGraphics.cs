// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The part of the graphics state the PDF/A font rules follow: the selected font and the rendering mode.</summary>
/// <param name="Font">The selected font, or null when none is selected or the name does not resolve to a font.</param>
/// <param name="Mode">The text rendering mode, 0 to 7.</param>
internal readonly record struct PdfATextGraphics(PdfAFontUse? Font, int Mode);
