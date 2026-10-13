// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>Everything decided before a signature is written: the field, the format, the digest, the certification and the values recorded.</summary>
/// <param name="Format">The signature format.</param>
/// <param name="Digest">The digest.</param>
/// <param name="Certification">The DocMDP permission, or <see cref="PdfMdpPermission.None"/> for an approval signature.</param>
/// <param name="FieldLock">The lock the signature carries as a FieldMDP reference, or <see langword="null"/>.</param>
/// <param name="Reason">The reason, or <see langword="null"/> to leave it out.</param>
/// <param name="Location">The location, or <see langword="null"/> to leave it out.</param>
/// <param name="ContactInfo">The contact information, or <see langword="null"/> to leave it out.</param>
/// <param name="SignerName">The signer's name.</param>
/// <param name="Time">The signing time.</param>
/// <param name="Field">The existing field to sign, or <see langword="null"/> to add a new one.</param>
/// <param name="FieldName">The fully qualified field name.</param>
/// <param name="Appearance">How the signature looks; its page and rectangle are the final ones.</param>
[DebuggerDisplay("PdfSigningPlan: {FieldName} {Format} {Digest}")]
internal sealed record PdfSigningPlan(
    PdfSignatureFormat Format,
    PdfDigestMethod Digest,
    PdfMdpPermission Certification,
    PdfFieldLock? FieldLock,
    string? Reason,
    string? Location,
    string? ContactInfo,
    string SignerName,
    DateTimeOffset Time,
    PdfSigningField? Field,
    string FieldName,
    PdfSignatureAppearance Appearance)
{
    /// <summary>Gets a value indicating whether the signature is a certification signature.</summary>
    internal bool IsCertification => Certification != PdfMdpPermission.None;

    /// <summary>Gets a value indicating whether the signature needs a FieldMDP reference.</summary>
    internal bool HasFieldLock => FieldLock is { Action: not PdfFieldLockAction.None };
}
