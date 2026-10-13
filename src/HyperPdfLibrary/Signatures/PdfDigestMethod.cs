// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signatures;

/// <summary>
/// A digest algorithm as a PDF /DigestMethod name gives it, in seed values (ISO 32000-2, Table 237) and signature
/// references (Table 256), with the names ISO/TS 32001:2022 adds; also the digest a signature's CMS container uses.
/// </summary>
public enum PdfDigestMethod
{
    /// <summary>A name or algorithm not listed here.</summary>
    Unknown = 0,

    /// <summary>The MD5 digest, deprecated in PDF 2.0.</summary>
    Md5 = 1,

    /// <summary>The SHA-1 digest, deprecated in PDF 2.0.</summary>
    Sha1 = 2,

    /// <summary>The SHA-256 digest.</summary>
    Sha256 = 3,

    /// <summary>The SHA-384 digest.</summary>
    Sha384 = 4,

    /// <summary>The SHA-512 digest.</summary>
    Sha512 = 5,

    /// <summary>The RIPEMD-160 digest.</summary>
    Ripemd160 = 6,

    /// <summary>The SHA3-256 digest (ISO/TS 32001).</summary>
    Sha3_256 = 7,

    /// <summary>The SHA3-384 digest (ISO/TS 32001).</summary>
    Sha3_384 = 8,

    /// <summary>The SHA3-512 digest (ISO/TS 32001).</summary>
    Sha3_512 = 9,

    /// <summary>The SHAKE256 digest with its 512-bit output (ISO/TS 32001).</summary>
    Shake256 = 10,
}
