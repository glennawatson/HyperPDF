// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>An annotation as PDFium reads it, with its form field when it is a widget.</summary>
/// <param name="Subtype">The <c>FPDF_ANNOT_*</c> value.</param>
/// <param name="SubtypeName">The PDF <c>/Subtype</c> name for <paramref name="Subtype"/>; empty when PDFium does not know it.</param>
/// <param name="FieldType">The <c>FPDF_FORMFIELD_*</c> value, or -1 when the annotation is not a field.</param>
/// <param name="FieldName">The full field name, or <see langword="null"/>.</param>
/// <param name="FieldValue">The field value, or <see langword="null"/>.</param>
internal sealed record PdfiumAnnotation(int Subtype, string SubtypeName, int FieldType, string? FieldName, string? FieldValue);
