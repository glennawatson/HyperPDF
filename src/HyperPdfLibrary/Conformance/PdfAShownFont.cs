// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The Type 3 font a content stream shows text with, as the transparency search follows it.</summary>
/// <param name="Procedures">The font's <c>/CharProcs</c>, or <see langword="null"/> when the font is not a Type 3 font.</param>
/// <param name="Encoding">The font's encoding, mapping codes to glyph procedure names.</param>
/// <param name="Resources">The resources the glyph procedures use.</param>
internal readonly record struct PdfAShownFont(PdfDictionary? Procedures, SimpleEncoding? Encoding, PdfDictionary? Resources);
