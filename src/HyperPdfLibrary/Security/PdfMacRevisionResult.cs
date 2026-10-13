// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Security;

/// <summary>The PDF MAC check of one revision.</summary>
/// <param name="RevisionIndex">The revision's position in <see cref="Signatures.PdfRevision.Index"/>, 0 for the original save.</param>
/// <param name="Status">The result.</param>
/// <param name="Location">Where the revision keeps its token.</param>
/// <param name="Message">A plain sentence that says why the result is what it is.</param>
[DebuggerDisplay("PdfMacRevisionResult: revision {RevisionIndex} {Status}")]
public sealed record PdfMacRevisionResult(int RevisionIndex, PdfMacStatus Status, PdfMacLocation Location, string Message);
