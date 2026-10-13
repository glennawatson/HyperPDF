// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signing;

/// <summary>The kind of public key a signing certificate holds.</summary>
internal enum PdfSignerKeyKind
{
    /// <summary>A key this library does not read from the certificate itself, such as an EdDSA or DSA key.</summary>
    Other = 0,

    /// <summary>An RSA key.</summary>
    Rsa = 1,

    /// <summary>An elliptic-curve key (id-ecPublicKey).</summary>
    EllipticCurve = 2,
}
