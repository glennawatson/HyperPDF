// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signatures;

/// <summary>Whether a signature's cryptographic container could be checked.</summary>
public enum PdfCmsStatus
{
    /// <summary>The container was decoded and checked; see the digest and signature results.</summary>
    Checked = 0,

    /// <summary>The byte range is not valid, so the signed bytes are unknown.</summary>
    ByteRangeInvalid = 1,

    /// <summary>The field is not signed.</summary>
    Unsigned = 2,

    /// <summary>The /Contents could not be decoded as the format requires.</summary>
    Malformed = 3,

    /// <summary>The container holds no signer, or the signer's certificate cannot be found.</summary>
    MissingSigner = 4,

    /// <summary>
    /// The format, the digest or the signature algorithm is not supported, or this computer's cryptography library
    /// cannot compute the digest (SHA-3 and SHAKE256 need platform support); the signature is neither valid nor invalid.
    /// </summary>
    Unsupported = 5,

    /// <summary>
    /// The digest is one PDF does not allow: SHAKE128, or SHAKE256 with an output other than 512 bits or with
    /// parameters RFC 8702 and RFC 8419 forbid (ISO/TS 32001:2022, 5.1.4). The signature is not valid.
    /// </summary>
    DigestNotAllowed = 6,

    /// <summary>
    /// The signer's elliptic curve and digest are not a pair ISO/TS 32002:2022 permits (Tables 3 and 4), or an EdDSA
    /// signature does not use the digest its curve requires. The signature is not valid.
    /// </summary>
    CurveDigestNotAllowed = 7,

    /// <summary>
    /// The signer's certificate gives its elliptic curve as explicit or implicit parameters instead of a named curve
    /// (ISO/TS 32002:2022, 5.1.3). The signature is not valid.
    /// </summary>
    UnnamedCurve = 8,

    /// <summary>
    /// The signature algorithm hashes the signed attributes with a different digest from the content's (ISO/TS
    /// 32002:2022, 5.1.4). The signature is not valid.
    /// </summary>
    DigestMismatch = 9,
}
