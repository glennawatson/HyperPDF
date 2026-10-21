// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Rendering;

/// <summary>Identifies an outline in one page recording.</summary>
/// <param name="Font">The owning font.</param>
/// <param name="Code">The font's character code.</param>
internal readonly record struct GlyphOutlineKey(PdfFont Font, int Code);
