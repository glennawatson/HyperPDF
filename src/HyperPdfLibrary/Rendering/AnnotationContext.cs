// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>What drawing an appearance for an annotation needs.</summary>
/// <param name="Cache">The document's caches.</param>
/// <param name="Page">The page the annotation is on.</param>
/// <param name="Annotation">The annotation dictionary.</param>
[DebuggerDisplay("AnnotationContext: page {Page.Index}")]
internal readonly record struct AnnotationContext(PdfRenderCache Cache, PdfPage Page, PdfDictionary Annotation)
{
    /// <summary>Gets the document's objects.</summary>
    internal PdfObjectStore Store => Cache.Document.Objects;
}
