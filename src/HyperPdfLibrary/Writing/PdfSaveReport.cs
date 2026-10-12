// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Conformance;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Receives what a save found that the user should know. Pass one to a writer's save overload, then read it after the
/// save; one report is for one save.
/// </summary>
[DebuggerDisplay("PdfSaveReport: claim {Claim}")]
public sealed class PdfSaveReport
{
    /// <summary>
    /// Gets the check of the document's PDF/A claim, or <see langword="null"/> when the saved file claims no PDF/A level.
    /// When <see cref="PdfAClaimCheck.Removed"/> is set, the written file no longer met its level, so the claim was
    /// removed before writing; tell the user which rules it breaks.
    /// </summary>
    public PdfAClaimCheck? Claim { get; internal set; }
}
