// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// Makes the signature value for a PDF signature. The library builds the CMS container and hands this provider the bytes to
/// sign, so an in-process certificate, a hardware token or a remote signing service can sit behind the same interface.
/// </summary>
public interface IPdfSignatureProvider
{
    /// <summary>Gets the DER-encoded certificates to embed: the signing certificate first, then the certificates that lead to a trusted root.</summary>
    IReadOnlyList<byte[]> Certificates { get; }

    /// <summary>Gets the kind of signature the key makes.</summary>
    PdfSignatureAlgorithm Algorithm { get; }

    /// <summary>Signs some bytes: the provider hashes them with the digest and signs the hash with the private key.</summary>
    /// <param name="data">The DER-encoded signed attributes (the bytes RFC 5652 5.4 says to sign).</param>
    /// <param name="digest">The digest to hash with.</param>
    /// <param name="cancellationToken">Cancels the signing, for example while a remote service answers.</param>
    /// <returns>The signature value: PKCS #1 or PSS bytes for RSA, and the ASN.1 DER <c>Ecdsa-Sig-Value</c> for ECDSA.</returns>
    ValueTask<byte[]> SignAsync(ReadOnlyMemory<byte> data, PdfDigestMethod digest, CancellationToken cancellationToken);
}
