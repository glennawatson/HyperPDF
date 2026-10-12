// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The resources a content stream reads during PDF/A validation. A name is looked up in the stream's own resources first
/// and then in the page's, as veraPDF does for forms, patterns, glyphs and appearance streams.
/// </summary>
/// <param name="Page">The page's resources, or null.</param>
/// <param name="Own">The stream's own resources, or null for page content and streams without resources.</param>
internal readonly record struct PdfAColourResourceScope(PdfDictionary? Page, PdfDictionary? Own);
