// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>What the PDF/A glyph rules test about one character code of a font, as veraPDF derives it.</summary>
/// <param name="Name">The glyph name, or null when the font gives none.</param>
/// <param name="Present">Whether the embedded program has the glyph; null when there is no usable program.</param>
/// <param name="ProgramWidth">The width the program gives, in text units; null without a usable program.</param>
/// <param name="DictionaryWidth">The width the font dictionary gives; null without a usable program.</param>
internal readonly record struct PdfAGlyphFacts(string? Name, bool? Present, double? ProgramWidth, double? DictionaryWidth);
