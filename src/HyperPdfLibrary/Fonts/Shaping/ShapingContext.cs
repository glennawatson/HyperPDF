// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>The font and its layout tables used to shape one run.</summary>
/// <param name="Source">The font.</param>
/// <param name="Layout">The font's layout tables.</param>
internal readonly record struct ShapingContext(PdfFontSource Source, OtFontLayout Layout);
