// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>A line of the predefined XMP schema table: a property's type, or a structure field's type.</summary>
/// <param name="InPdfA1">Whether the line applies to PDF/A-1.</param>
/// <param name="InPdfA2">Whether the line applies to PDF/A-2 and PDF/A-3.</param>
/// <param name="Owner">The property's namespace, or the structure type's name.</param>
/// <param name="FieldNamespace">The structure field's namespace; empty for a property.</param>
/// <param name="Name">The property or field name.</param>
/// <param name="Type">The value type name.</param>
internal sealed record PdfAXmpSchemaRow(bool InPdfA1, bool InPdfA2, string Owner, string FieldNamespace, string Name, string Type)
{
    /// <summary>Determines whether the line applies to a part.</summary>
    /// <param name="pdfA1">Whether PDF/A-1, rather than PDF/A-2 or PDF/A-3, is checked.</param>
    /// <returns><see langword="true"/> when it applies.</returns>
    internal bool AppliesTo(bool pdfA1) => pdfA1 ? InPdfA1 : InPdfA2;
}
