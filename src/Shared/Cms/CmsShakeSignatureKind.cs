// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Cms;

/// <summary>The signature algorithms checked with a SHAKE256 digest.</summary>
internal enum CmsShakeSignatureKind
{
    /// <summary>An algorithm this code does not check, such as Ed448.</summary>
    Unsupported = 0,

    /// <summary>ECDSA over the 64-byte hash.</summary>
    Ecdsa = 1,

    /// <summary>RSASSA-PSS with SHAKE256 for the hash and the mask.</summary>
    RsaPss = 2,
}
