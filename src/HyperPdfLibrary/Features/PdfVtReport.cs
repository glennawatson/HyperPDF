// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>A read-only summary of a document's PDF/VT claim and its document part (record) structure.</summary>
/// <param name="Claim">The raw <c>GTS_PDFVTVersion</c> text, such as <c>PDF/VT-1</c>, or <see langword="null"/> when the file makes no claim.</param>
/// <param name="Flavor">The recognised flavor of the claim.</param>
/// <param name="RecordLevel">The hierarchy level at which records are held; zero when there is no document part root.</param>
/// <param name="NodeNames">The names of the hierarchy levels, outermost first.</param>
/// <param name="Records">The nodes at the record level, in order, each with its page range and metadata.</param>
/// <param name="Findings">Plain-words problems with the structure; empty when none were found.</param>
[DebuggerDisplay("PdfVtReport: {Flavor} {Records.Length} records, {Findings.Length} findings")]
public sealed record PdfVtReport(string? Claim, PdfVtFlavor Flavor, int RecordLevel, string[] NodeNames, PdfVtRecord[] Records, string[] Findings)
{
    /// <summary>Gets a value indicating whether the file claims PDF/VT.</summary>
    public bool IsClaimed => Claim is not null;

    /// <summary>Gets a value indicating whether no structural problem was found.</summary>
    public bool IsStructureSound => Findings.Length == 0;
}
