// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>The kind of signature a signing key makes, which decides the signature algorithm the signature container names.</summary>
public enum SigningKeyKind
{
    /// <summary>No kind is set; the signature is made as <see cref="RsaPkcs1"/>.</summary>
    None = 0,

    /// <summary>An RSA signature with PKCS #1 v1.5 padding, which every PDF reader can check.</summary>
    RsaPkcs1 = 1,

    /// <summary>An RSA signature with PSS padding (RFC 4056).</summary>
    RsaPss = 2,

    /// <summary>An ECDSA signature in the ASN.1 DER form (RFC 3279).</summary>
    Ecdsa = 3,
}
