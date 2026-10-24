// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Security;

/// <summary>The ciphers the public-key test documents use.</summary>
public enum PublicKeyCipher
{
    /// <summary>RC4 with a 128-bit key (<c>adbe.pkcs7.s4</c>).</summary>
    Rc4 = 0,

    /// <summary>AES-128 through a crypt filter (<c>adbe.pkcs7.s5</c>, AESV2).</summary>
    Aes128 = 1,

    /// <summary>AES-256 through a crypt filter (<c>adbe.pkcs7.s5</c>, AESV3).</summary>
    Aes256 = 2,
}
