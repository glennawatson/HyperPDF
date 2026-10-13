// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Cms;

/// <summary>An ECDSA curve ISO/TS 32002:2022, 5.1.3 lists (Table 3).</summary>
internal enum CmsEcCurve
{
    /// <summary>A named curve the specification does not list.</summary>
    Other = 0,

    /// <summary>NIST P-256 (secp256r1).</summary>
    P256 = 1,

    /// <summary>NIST P-384 (secp384r1).</summary>
    P384 = 2,

    /// <summary>NIST P-521 (secp521r1).</summary>
    P521 = 3,

    /// <summary>Brainpool brainpoolP256r1 (RFC 5639).</summary>
    BrainpoolP256r1 = 4,

    /// <summary>Brainpool brainpoolP384r1 (RFC 5639).</summary>
    BrainpoolP384r1 = 5,

    /// <summary>Brainpool brainpoolP512r1 (RFC 5639).</summary>
    BrainpoolP512r1 = 6,
}
