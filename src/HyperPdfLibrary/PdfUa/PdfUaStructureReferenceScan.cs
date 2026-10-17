// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.PdfUa;

/// <summary>Carries one reference scan's context and exclusively leased working collections.</summary>
/// <param name="Document">The document being checked or repaired.</param>
/// <param name="Tree">The structure snapshot being scanned.</param>
/// <param name="Stm">The stream-reference key in this document's name table.</param>
/// <param name="Buffers">The working collections owned by this scan until it finishes.</param>
/// <param name="CancellationToken">Stops the current operation.</param>
internal readonly record struct PdfUaStructureReferenceScan(
    PdfDocument Document,
    PdfStructureTree Tree,
    PdfName Stm,
    PdfUaStructureReferenceBuffers Buffers,
    CancellationToken CancellationToken);
