// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Cms;

/// <summary>The kind of signature algorithm a CMS signer names.</summary>
internal enum CmsSignatureKind
{
    /// <summary>Any algorithm that is not ECDSA or EdDSA, such as RSA.</summary>
    Other = 0,

    /// <summary>ECDSA, named by id-ecPublicKey or by an ecdsa-with-digest identifier.</summary>
    Ecdsa = 1,

    /// <summary>The Ed25519 EdDSA algorithm, named by id-Ed25519 in RFC 8410.</summary>
    Ed25519 = 2,

    /// <summary>The Ed448 EdDSA algorithm, named by id-Ed448 in RFC 8410.</summary>
    Ed448 = 3,
}
