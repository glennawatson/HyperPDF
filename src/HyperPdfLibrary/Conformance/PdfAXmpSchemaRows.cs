// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The predefined XMP schema table, split into property lines and structure field lines.</summary>
/// <param name="Properties">The property lines.</param>
/// <param name="Fields">The structure field lines.</param>
internal sealed record PdfAXmpSchemaRows(PdfAXmpSchemaRow[] Properties, PdfAXmpSchemaRow[] Fields);
