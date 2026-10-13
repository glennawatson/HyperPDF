// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>A structure element as PDFium reads it from a page's structure tree.</summary>
/// <param name="Type">The structure type after role mapping.</param>
/// <param name="AlternateText">The <c>/Alt</c> text, or <see langword="null"/>.</param>
/// <param name="ActualText">The <c>/ActualText</c>, or <see langword="null"/>.</param>
/// <param name="Title">The <c>/T</c> title, or <see langword="null"/>.</param>
/// <param name="Language">The element's own <c>/Lang</c>, or <see langword="null"/>.</param>
/// <param name="MarkedContentId">The MCID when <c>/K</c> is one, otherwise -1.</param>
/// <param name="Children">The child elements; marked content children are not listed.</param>
internal sealed record PdfiumStructElement(
    string Type,
    string? AlternateText,
    string? ActualText,
    string? Title,
    string? Language,
    int MarkedContentId,
    PdfiumStructElement[] Children);
