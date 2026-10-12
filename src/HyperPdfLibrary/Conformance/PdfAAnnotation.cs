// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>An annotation listed in a page's <c>/Annots</c> array, which is where PDF/A checks annotations.</summary>
/// <param name="Dictionary">The annotation dictionary.</param>
/// <param name="Page">The zero-based index of the page that lists it.</param>
/// <param name="Number">The annotation's object number, or 0 when it is a direct object.</param>
internal readonly record struct PdfAAnnotation(PdfDictionary Dictionary, int Page, int Number);
