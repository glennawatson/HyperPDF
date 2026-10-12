// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The resources a content stream's names resolve against, as veraPDF models them: the page's resources, and for a form,
/// pattern, glyph or appearance stream its own resources, which fall back to the page's. A name found only through that
/// fallback, or through resources a page inherits from the page tree, counts as an inherited resource.
/// </summary>
/// <param name="Page">The page's resources, or <see langword="null"/> when they are missing or not a dictionary.</param>
/// <param name="HasPage">Whether the page or its page tree has a <c>/Resources</c> entry at all.</param>
/// <param name="PageInherited">Whether the page has no <c>/Resources</c> key of its own.</param>
/// <param name="IsPage">Whether the content is the page's own content stream rather than a stream with its own resources.</param>
/// <param name="Local">The stream's own resources, or <see langword="null"/> when they are missing or not a dictionary.</param>
internal readonly record struct PdfAResourceScope(PdfDictionary? Page, bool HasPage, bool PageInherited, bool IsPage, PdfDictionary? Local)
{
    /// <summary>Gets the scope of a stream with its own resources, drawn on the same page.</summary>
    /// <param name="local">The stream's <c>/Resources</c> value, resolved.</param>
    /// <returns>The scope.</returns>
    internal PdfAResourceScope Extend(PdfValue local) => new(Page, HasPage, PageInherited, false, local.Kind == PdfKind.Dictionary ? local.AsDictionary() : null);
}
