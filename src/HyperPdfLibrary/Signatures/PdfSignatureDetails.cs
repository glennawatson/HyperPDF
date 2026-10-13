// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Signatures;

/// <summary>A signature field and the modification-detection data its signature dictionary holds, read as data.</summary>
/// <param name="Field">The stored signature, as <see cref="PdfDocumentAttachments.GetSignatures"/> lists it.</param>
/// <param name="FieldName">The field's fully qualified name.</param>
/// <param name="Format">The /Contents encoding, from /SubFilter.</param>
/// <param name="IsCertification">Whether the catalog's /Perms /DocMDP names this signature.</param>
/// <param name="DocMdp">The DocMDP /P of a DocMDP transform in /Reference (ISO 32000-2 12.8.2.2); None when absent or not 1 to 3.</param>
/// <param name="FieldMdp">A FieldMDP transform in /Reference, or <see langword="null"/>.</param>
/// <param name="Lock">The field's /Lock dictionary, or <see langword="null"/>.</param>
/// <param name="UsageRights">A UR3 transform in /Reference, or <see langword="null"/>.</param>
/// <param name="SignerName">The /Name entry, or an empty string.</param>
/// <param name="Location">The /Location entry, or an empty string.</param>
/// <param name="ContactInfo">The /ContactInfo entry, or an empty string.</param>
[DebuggerDisplay("PdfSignatureDetails: {FieldName} {Format}")]
public sealed record PdfSignatureDetails(
    PdfSignatureField Field,
    string FieldName,
    PdfSignatureFormat Format,
    bool IsCertification,
    PdfMdpPermission DocMdp,
    PdfFieldLock? FieldMdp,
    PdfFieldLock? Lock,
    PdfUsageRights? UsageRights,
    string SignerName,
    string Location,
    string ContactInfo)
{
    /// <summary>Gets the digests the field's seed value dictionary accepts, or <see langword="null"/> when it names none.</summary>
    public PdfSeedValueDigest? SeedDigest { get; init; }

    /// <summary>
    /// Gets the /DigestMethod of each signature reference dictionary in /Reference (ISO 32000-2, Table 256), in order;
    /// <see cref="PdfDigestMethod.Unknown"/> where a reference has none.
    /// </summary>
    public PdfDigestMethod[] ReferenceDigestMethods { get; init; } = [];

    /// <summary>Gets a value indicating whether this is a document timestamp rather than a person's signature.</summary>
    public bool IsDocumentTimestamp => Format == PdfSignatureFormat.Rfc3161;

    /// <summary>Gets a value indicating whether the field holds a signature.</summary>
    public bool IsSigned => Field.Contents.Length > 0;
}
