// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>An annotation flattening did not draw.</summary>
/// <param name="PageIndex">The zero based index of the page.</param>
/// <param name="AnnotationIndex">The annotation's index in the page's <c>/Annots</c> before flattening.</param>
/// <param name="Subtype">The annotation's subtype, such as <c>Widget</c> or <c>Highlight</c>.</param>
/// <param name="Reason">Why it was not drawn.</param>
/// <param name="Removed">Whether the annotation was removed anyway, as a form field that is no longer a field; otherwise it stays on the page.</param>
[DebuggerDisplay("PdfFlattenSkip: page {PageIndex} annotation {AnnotationIndex} {Subtype}: {Reason}")]
public readonly record struct PdfFlattenSkip(int PageIndex, int AnnotationIndex, string Subtype, PdfFlattenSkipReason Reason, bool Removed);
