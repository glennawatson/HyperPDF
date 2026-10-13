// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Signatures;

/// <summary>The result of checking a signature's cryptographic container.</summary>
/// <param name="Status">Whether the container could be checked.</param>
/// <param name="DigestValid">Whether the hash of the signed bytes matches the hash the signature holds.</param>
/// <param name="SignatureValid">Whether the signer's signature over that hash is good.</param>
/// <param name="Signer">The signer's (or a document timestamp's authority's) certificate, or <see langword="null"/>.</param>
/// <param name="DigestAlgorithm">The OID of the digest algorithm, or an empty string.</param>
/// <param name="SigningTime">The signing time from the signed attributes, or a document timestamp's time.</param>
/// <param name="Timestamp">The signature's timestamp, or <see langword="null"/>.</param>
/// <param name="Chain">The signer's chain, or <see langword="null"/> when there is no signer.</param>
/// <param name="Detail">Why the container could not be checked, or an empty string.</param>
[DebuggerDisplay("PdfCmsResult: {Status} digest {DigestValid} signature {SignatureValid}")]
public sealed record PdfCmsResult(
    PdfCmsStatus Status,
    bool DigestValid,
    bool SignatureValid,
    X509Certificate2? Signer,
    string DigestAlgorithm,
    DateTimeOffset? SigningTime,
    PdfTimestampResult? Timestamp,
    PdfChainResult? Chain,
    string Detail)
{
    /// <summary>Gets the digest algorithm as a /DigestMethod value, or <see cref="PdfDigestMethod.Unknown"/>.</summary>
    public PdfDigestMethod Digest => PdfDigestMethods.FromOid(DigestAlgorithm);

    /// <summary>Creates the result for a container that could not be checked.</summary>
    /// <param name="status">Why.</param>
    /// <param name="detail">A description.</param>
    /// <returns>The result.</returns>
    internal static PdfCmsResult Failed(PdfCmsStatus status, string detail) => new(status, false, false, null, string.Empty, null, null, null, detail);
}
