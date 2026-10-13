// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace HyperPdfLibrary.Security;

/// <summary>
/// The key derivation of a PDF MAC token (ISO/TS 32004:2024 6.4): the key-encryption key that wraps the MAC key is
/// HKDF (RFC 5869) with SHA-256, the document's 32-byte <c>/KDFSalt</c>, and the info string <c>PDFMAC</c>, over the
/// file encryption key.
/// </summary>
internal static class PdfMacKeys
{
    /// <summary>The length of <c>/KDFSalt</c> (ISO/TS 32004 5.1.1, Table 2).</summary>
    internal const int SaltLength = 32;

    /// <summary>The length of the key-encryption key: AES-256 key wrap (ISO/TS 32004 6.3.3, Table 7).</summary>
    internal const int KeyEncryptionKeyLength = 32;

    /// <summary>The length of the MAC key: HMAC with SHA-256 (ISO/TS 32004 6.3.5, Table 9).</summary>
    internal const int MacKeyLength = 32;

    /// <summary>The length of the wrapped MAC key: the key and the 8-byte check value of RFC 3394.</summary>
    internal const int WrappedKeyLength = MacKeyLength + 8;

    /// <summary>The lowest <c>/V</c> that allows a PDF MAC (ISO/TS 32004 5.2.1, Table 5).</summary>
    internal const int MinimumVersion = 5;

    /// <summary>Gets the HKDF info parameter, the six characters PDFMAC in UTF-8.</summary>
    private static ReadOnlySpan<byte> Info => "PDFMAC"u8;

    /// <summary>Derives the key-encryption key.</summary>
    /// <param name="fileKey">The file encryption key the security handler uses for the document.</param>
    /// <param name="salt">The 32-byte <c>/KDFSalt</c>.</param>
    /// <param name="destination">Receives the 32-byte key-encryption key.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void DeriveKeyEncryptionKey(ReadOnlySpan<byte> fileKey, ReadOnlySpan<byte> salt, Span<byte> destination) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, fileKey, destination, salt, Info);
}
