// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// What signing would do, worked out without changing the document: the field, the page, the format and digest, the
/// certification and what later changes it allows, and every reason signing would be refused. A viewer shows it before the
/// user confirms.
/// </summary>
/// <param name="FieldName">The fully qualified name of the field that would hold the signature.</param>
/// <param name="IsNewField">Whether the field would be added; otherwise an existing empty field is signed.</param>
/// <param name="PageNumber">The one-based number of the page the signature is on.</param>
/// <param name="IsVisible">Whether the signature is drawn on the page.</param>
/// <param name="Format">The format.</param>
/// <param name="Digest">The digest.</param>
/// <param name="Reason">The reason that would be recorded, or an empty string.</param>
/// <param name="Location">The location that would be recorded, or an empty string.</param>
/// <param name="Certification">The DocMDP permission; <see cref="PdfMdpPermission.None"/> for an approval signature.</param>
/// <param name="LocksDocument">Whether signing locks every field.</param>
/// <param name="TimestampUrl">The timestamp authority the field's seed values name, or <see langword="null"/>.</param>
/// <param name="TimestampRequired">Whether the seed values require a signature timestamp.</param>
/// <param name="Problems">Every reason signing is refused, each a plain sentence; empty when signing can go ahead.</param>
/// <param name="Notes">What the user should know about, for example that the file will be raised to PDF 2.0.</param>
[DebuggerDisplay("PdfSigningPreview: {FieldName} page {PageNumber} {Format} {Digest}")]
public sealed record PdfSigningPreview(
    string FieldName,
    bool IsNewField,
    int PageNumber,
    bool IsVisible,
    PdfSignatureFormat Format,
    PdfDigestMethod Digest,
    string Reason,
    string Location,
    PdfMdpPermission Certification,
    bool LocksDocument,
    string? TimestampUrl,
    bool TimestampRequired,
    IReadOnlyList<string> Problems,
    IReadOnlyList<string> Notes)
{
    /// <summary>Gets a value indicating whether signing can go ahead.</summary>
    public bool CanSign => Problems.Count == 0;

    /// <summary>Gets a plain sentence saying what later changes the signature allows.</summary>
    public string LaterChanges => Certification switch
    {
        PdfMdpPermission.NoChanges => "No later change is allowed: any change to the document breaks the certification, except adding validation data or a document timestamp.",
        PdfMdpPermission.FormFillAndSign => "Later changes may fill in form fields and add signatures. Any other change breaks the certification.",
        PdfMdpPermission.AnnotateFormFillAndSign => "Later changes may fill in form fields, add signatures and add or change comments. Any other change breaks the certification.",
        _ => LocksDocument
            ? "No later change to a field is allowed after this signature."
            : "This approval signature does not limit later changes. A reader can show what was changed after it.",
    };
}
