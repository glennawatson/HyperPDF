// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>How strings or streams are encrypted.</summary>
internal enum CryptMethod
{
    /// <summary>Not encrypted.</summary>
    None = 0,

    /// <summary>RC4 with a per-object key.</summary>
    Rc4 = 1,

    /// <summary>AES-128 in CBC mode with a per-object key.</summary>
    Aes128 = 2,

    /// <summary>AES-256 in CBC mode with the file key.</summary>
    Aes256 = 3,

    /// <summary>AES-256 in GCM mode with the file key: 12-byte IV, cipher text, then a 16-byte tag (ISO/TS 32003).</summary>
    AesGcm = 4,
}
