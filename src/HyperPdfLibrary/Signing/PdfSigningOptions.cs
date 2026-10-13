// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>What a new signature records and how it is made.</summary>
[DebuggerDisplay("PdfSigningOptions: field {FieldName} {Certification}")]
public sealed record PdfSigningOptions
{
    /// <summary>The headroom added to the estimated size of the signature, in bytes.</summary>
    internal const int DefaultHeadroom = 512;

    /// <summary>Gets the default options: an invisible approval signature in a new field on the first page.</summary>
    public static PdfSigningOptions Default { get; } = new();

    /// <summary>
    /// Gets the signature format: <see cref="PdfSignatureFormat.Pkcs7Detached"/> (adbe.pkcs7.detached) or
    /// <see cref="PdfSignatureFormat.CadesDetached"/> (ETSI.CAdES.detached, a PAdES baseline signature), or <see langword="null"/>
    /// to take the first format the field's seed values allow, falling back to adbe.pkcs7.detached.
    /// </summary>
    public PdfSignatureFormat? Format { get; init; }

    /// <summary>
    /// Gets the digest of the signature, or <see langword="null"/> to take the first one the field's seed values allow that
    /// the key may use (SHA-256, or the one ISO/TS 32002:2022 pairs with an elliptic curve). The SHA-3 digests and SHAKE256
    /// are PDF 2.0 extensions (ISO/TS 32001:2022); using one raises the file to PDF 2.0 and declares the extension.
    /// </summary>
    public PdfDigestMethod? Digest { get; init; }

    /// <summary>Gets the fully qualified name of an existing empty signature field to sign, or <see langword="null"/> to add a new field.</summary>
    public string? FieldName { get; init; }

    /// <summary>Gets the name for a new field, or <see langword="null"/> to number it (<c>Signature1</c>, <c>Signature2</c> and so on).</summary>
    public string? NewFieldName { get; init; }

    /// <summary>Gets how the signature looks, or <see langword="null"/> for an invisible one in a new field and the field's own look in an existing field.</summary>
    public PdfSignatureAppearance? Appearance { get; init; }

    /// <summary>Gets why the document is signed, or <see langword="null"/> to leave the reason out.</summary>
    public string? Reason { get; init; }

    /// <summary>Gets where the document is signed, or <see langword="null"/> to leave it out.</summary>
    public string? Location { get; init; }

    /// <summary>Gets how a person can reach the signer, or <see langword="null"/> to leave it out.</summary>
    public string? ContactInfo { get; init; }

    /// <summary>Gets the name to record for the signer, or <see langword="null"/> to take it from the certificate.</summary>
    public string? SignerName { get; init; }

    /// <summary>Gets the signing time, or <see langword="null"/> to read the clock.</summary>
    public DateTimeOffset? SigningTime { get; init; }

    /// <summary>Gets the clock read when <see cref="SigningTime"/> is not set, or <see langword="null"/> to use the system clock.</summary>
    public TimeProvider? Clock { get; init; }

    /// <summary>
    /// Gets the DocMDP permission of a certification signature (ISO 32000-2, 12.8.2.2): <see cref="PdfMdpPermission.None"/> signs as an
    /// approval signature; the other values certify the document and say which later changes keep the certification valid.
    /// A document can have only one certification signature, and it must be the first signature.
    /// </summary>
    public PdfMdpPermission Certification { get; init; }

    /// <summary>
    /// Gets whether to lock the document after signing, so no later change to any field keeps the signature valid (a FieldMDP
    /// transform with action All), or <see langword="null"/> to follow the field's seed values (default: do not lock).
    /// </summary>
    public bool? LockDocument { get; init; }

    /// <summary>Gets the RFC 3161 client that stamps the signature (a PAdES-T signature), or <see langword="null"/> for no signature timestamp.</summary>
    public IPdfTimestampClient? Timestamp { get; init; }

    /// <summary>Gets the digest the timestamp request uses; SHA-256 by default, which every timestamp authority accepts.</summary>
    public PdfDigestMethod TimestampDigest { get; init; } = PdfDigestMethod.Sha256;

    /// <summary>
    /// Gets the extra bytes reserved for the signature beyond the estimate of its size. If the finished signature still does not
    /// fit, the file is built again with the room it needs (up to three times).
    /// </summary>
    public int Headroom { get; init; } = DefaultHeadroom;

    /// <summary>Gets a value indicating whether the finished file is read back to check the byte range, the digest and the PDF/A claim. The default is <see langword="true"/>.</summary>
    public bool VerifyOutput { get; init; } = true;
}
