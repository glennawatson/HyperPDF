// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>Makes the signature value for a signature whose private key is outside the app, such as a hardware token or a remote signing service.</summary>
public interface ISignatureProvider
{
    /// <summary>Gets the signer's certificates and the kind of signature its key makes.</summary>
    SignerIdentity Identity { get; }

    /// <summary>Signs some bytes: the provider hashes them with the digest and signs the hash with the private key.</summary>
    /// <param name="data">The DER-encoded signed attributes (the bytes RFC 5652, 5.4 says to sign).</param>
    /// <param name="digest">The digest to hash with.</param>
    /// <param name="cancellationToken">Cancels the signing, for example while a remote service answers.</param>
    /// <returns>The signature value: PKCS #1 or PSS bytes for RSA, and the ASN.1 DER <c>Ecdsa-Sig-Value</c> for ECDSA.</returns>
    ValueTask<byte[]> SignAsync(ReadOnlyMemory<byte> data, SigningDigest digest, CancellationToken cancellationToken);
}
