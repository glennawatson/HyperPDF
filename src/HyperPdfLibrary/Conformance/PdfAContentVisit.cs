// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// One reading of a form, appearance, pattern or glyph procedure: the same stream read again with the same page resources
/// and starting state adds nothing, so the walk reads it once.
/// </summary>
/// <param name="Stream">The stream dictionary.</param>
/// <param name="PageResources">The page resources names fall back to.</param>
/// <param name="Graphics">The font and rendering mode the stream starts with.</param>
internal readonly record struct PdfAContentVisit(PdfDictionary Stream, PdfDictionary? PageResources, PdfATextGraphics Graphics);
