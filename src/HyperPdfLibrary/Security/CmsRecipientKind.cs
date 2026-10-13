// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>How a recipient's certificate protects the content key.</summary>
internal enum CmsRecipientKind
{
    /// <summary>The certificate cannot be used: its key is neither RSA nor elliptic curve, or its key usage rules the use out.</summary>
    Unsupported = 0,

    /// <summary>RSA key transport (RFC 5652 6.2.1): the content key is encrypted with the public key.</summary>
    Rsa = 1,

    /// <summary>Ephemeral-static ECDH key agreement (RFC 5753 3.1): the content key is wrapped with a key agreed with a fresh key pair.</summary>
    EllipticCurve = 2,
}
