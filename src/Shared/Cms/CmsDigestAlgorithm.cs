// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Cms;

/// <summary>A digest algorithm a CMS signature or timestamp names.</summary>
internal enum CmsDigestAlgorithm
{
    /// <summary>An algorithm this code does not know.</summary>
    Unknown = 0,

    /// <summary>The SHA-1 digest.</summary>
    Sha1 = 1,

    /// <summary>The SHA-256 digest.</summary>
    Sha256 = 2,

    /// <summary>The SHA-384 digest.</summary>
    Sha384 = 3,

    /// <summary>The SHA-512 digest.</summary>
    Sha512 = 4,

    /// <summary>The SHA3-256 digest that ISO/TS 32001 adds.</summary>
    Sha3_256 = 5,

    /// <summary>The SHA3-384 digest that ISO/TS 32001 adds.</summary>
    Sha3_384 = 6,

    /// <summary>The SHA3-512 digest that ISO/TS 32001 adds.</summary>
    Sha3_512 = 7,

    /// <summary>The SHAKE256 digest with a 512-bit output that ISO/TS 32001 adds (RFC 8702 and RFC 8419).</summary>
    Shake256 = 8,

    /// <summary>The SHAKE128 digest, which ISO/TS 32001 does not allow.</summary>
    Shake128 = 9,
}
