// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Security;

/// <summary>The integrity check of an encrypted document by its PDF MAC tokens (ISO/TS 32004:2024), one result per revision.</summary>
/// <param name="Status">The worst result of any revision, in the order invalid, missing, unsupported, valid, not applicable.</param>
/// <param name="Revisions">The result of every revision, oldest first; empty when the document is not encrypted.</param>
[DebuggerDisplay("PdfMacReport: {Status}")]
public sealed record PdfMacReport(PdfMacStatus Status, IReadOnlyList<PdfMacRevisionResult> Revisions)
{
    /// <summary>Gets the report of a document that has no PDF MAC to check.</summary>
    public static PdfMacReport NotApplicable { get; } = new(PdfMacStatus.NotApplicable, []);

    /// <summary>Gets a value indicating whether every revision's PDF MAC was checked and matches, so the document is known to be intact.</summary>
    public bool IsIntact => Status == PdfMacStatus.Valid;

    /// <summary>Gets a value indicating whether the document has a PDF MAC, or requires one, so the viewer must report the result.</summary>
    public bool NeedsNotice => Status != PdfMacStatus.NotApplicable;

    /// <summary>Gets one sentence in plain words for the user, or an empty string when there is no PDF MAC to report.</summary>
    public string Summary => Status switch
    {
        PdfMacStatus.Valid => "The document's integrity protection (PDF MAC) was checked and the document is unchanged.",
        PdfMacStatus.Invalid => "The document's integrity protection (PDF MAC) does not match. The document was changed or damaged, or the protection is not valid. Do not trust its content.",
        PdfMacStatus.Missing => "The document requires integrity protection (PDF MAC), but a revision has none. The document may have been changed. It is not known to be intact.",
        PdfMacStatus.Unsupported => "The document has integrity protection (PDF MAC) that this computer cannot check. It is not known to be intact.",
        _ => string.Empty,
    };
}
