// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>
/// The digest a certificate signature is made with. The SHA-3 and SHAKE256 digests are PDF 2.0 extensions
/// (ISO/TS 32001:2022): a file signed with one records the ISO_ developer extension and PDF version 2.0 in its catalog.
/// With an elliptic-curve certificate the digest must be one ISO/TS 32002:2022 (Table 3) pairs with the certificate's curve:
/// SHA-256 or SHA3-256 for P-256, SHA-384 or SHA3-384 for P-384 and SHA-512 or SHA3-512 for P-521.
/// </summary>
public enum SigningDigest
{
    /// <summary>The SHA-256 digest, which every PDF reader can check.</summary>
    Sha256 = 0,

    /// <summary>The SHA3-256 digest; needs a system cryptography library that provides SHA-3.</summary>
    Sha3_256 = 1,

    /// <summary>The SHA3-384 digest; needs a system cryptography library that provides SHA-3.</summary>
    Sha3_384 = 2,

    /// <summary>The SHA3-512 digest; needs a system cryptography library that provides SHA-3.</summary>
    Sha3_512 = 3,

    /// <summary>
    /// The SHAKE256 digest with a 512-bit output (id-shake256); needs an elliptic-curve certificate, signed with
    /// id-ecdsa-with-shake256 (RFC 8702), and a system cryptography library that provides SHAKE256.
    /// </summary>
    Shake256 = 4,

    /// <summary>
    /// The SHA-384 digest; the digest ISO/TS 32002 pairs with the P-384 curve, and the strongest SHA-2 digest brainpoolP384r1
    /// and brainpoolP256r1 allow besides SHA-512.
    /// </summary>
    Sha384 = 5,

    /// <summary>The SHA-512 digest; the digest ISO/TS 32002 pairs with the P-521 and brainpoolP512r1 curves.</summary>
    Sha512 = 6,
}
