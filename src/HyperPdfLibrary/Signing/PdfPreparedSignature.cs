// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signing;

/// <summary>A prepared file with the plan and the state a signature needs to be finished.</summary>
/// <param name="File">The file with the byte range patched in.</param>
/// <param name="Plan">The plan.</param>
/// <param name="Mac">The keys of the PDF MAC the signature carries, or <see langword="null"/> when the document needs none.</param>
/// <param name="Options">The options.</param>
/// <param name="Notes">What the user should know.</param>
[DebuggerDisplay("PdfPreparedSignature: {Plan.FieldName}")]
internal sealed record PdfPreparedSignature(PdfSigningFile File, PdfSigningPlan Plan, PdfMacKeyMaterial? Mac, PdfSigningOptions Options, IReadOnlyList<string> Notes);
