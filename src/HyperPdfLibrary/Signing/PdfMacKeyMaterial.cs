// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography;

namespace HyperPdfLibrary.Signing;

/// <summary>The keys that make a PDF MAC token.</summary>
/// <param name="FileKey">The file encryption key.</param>
/// <param name="Salt">The 32-byte /KDFSalt of the encryption dictionary.</param>
[DebuggerDisplay("PdfMacKeyMaterial")]
internal sealed record PdfMacKeyMaterial(byte[] FileKey, byte[] Salt)
{
    /// <summary>Zeroes the keys, once the last token that needs them is made.</summary>
    internal void Wipe()
    {
        CryptographicOperations.ZeroMemory(FileKey);
        CryptographicOperations.ZeroMemory(Salt);
    }
}
