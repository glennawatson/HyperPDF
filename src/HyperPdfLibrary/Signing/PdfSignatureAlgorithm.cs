// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signing;

/// <summary>The kind of signature a signing key makes, which decides the signature algorithm the CMS container names.</summary>
public enum PdfSignatureAlgorithm
{
    /// <summary>No algorithm is set.</summary>
    None = 0,

    /// <summary>An RSA signature with PKCS #1 v1.5 padding (rsaEncryption), which every PDF reader can check.</summary>
    RsaPkcs1 = 1,

    /// <summary>An RSA signature with PSS padding (RSASSA-PSS, RFC 4056), with the digest as the mask function's digest and a salt as long as the digest.</summary>
    RsaPss = 2,

    /// <summary>
    /// An ECDSA signature in the ASN.1 DER form (RFC 3279), with the algorithm named for the digest (RFC 5753, RFC 8702).
    /// ISO/TS 32002:2022 limits which digest each curve may use.
    /// </summary>
    Ecdsa = 3,
}
