// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>
/// The cipher a new encrypted file uses. RC4 and the older revisions 2 and 3 are deprecated in PDF 2.0 and are never
/// written; revision 5 was a proprietary Adobe extension and is never written either.
/// </summary>
public enum PdfEncryptionAlgorithm
{
    /// <summary>AES-256 (crypt filter AESV3, revision 6, <c>/V 5</c>, ISO 32000-2). The default.</summary>
    Aes256 = 0,

    /// <summary>AES-128 (crypt filter AESV2, standard security handler revision 4, <c>/V 4</c>). Opens in the widest range of programs.</summary>
    Aes128 = 1,

    /// <summary>
    /// AES-256 in Galois/Counter Mode (crypt filter AESV4, revision 7, <c>/V 6</c>, ISO/TS 32003). It also detects changed
    /// data, but fewer programs open it, and it needs a PDF 2.0 file. Only the password handler offers it.
    /// </summary>
    Aes256Gcm = 2,
}
