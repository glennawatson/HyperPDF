// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The operands of a content operator that the PDF/A content rules read.</summary>
/// <param name="First">The first operand when it is a name, else none.</param>
/// <param name="Last">The last operand when it is a name, else none.</param>
internal readonly record struct PdfAOperands(PdfName First, PdfName Last);
