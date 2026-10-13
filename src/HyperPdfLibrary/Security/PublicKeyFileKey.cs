// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace HyperPdfLibrary.Security;

/// <summary>Works out the file encryption key of a public-key encrypted file from its seed (ISO 32000-2 7.6.5.3), for readers and writers alike.</summary>
internal static class PublicKeyFileKey
{
    /// <summary>The length of the seed at the start of the enveloped content.</summary>
    internal const int SeedLength = 20;

    /// <summary>The length of the permission flags after the seed.</summary>
    internal const int PermissionsLength = 4;

    /// <summary>The marker appended to the key input when metadata is not encrypted.</summary>
    private const uint MetadataNotEncrypted = 0xFFFFFFFF;

    /// <summary>
    /// Derives the file key: SHA-1 (SHA-256 for AES-256) over the seed, every recipient's bytes in order, and four 0xFF bytes
    /// when metadata is not encrypted, cut to the key length.
    /// </summary>
    /// <param name="seed">The 20-byte seed.</param>
    /// <param name="recipients">The recipients.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <param name="keyLength">The key length in bytes; 32 means AES-256.</param>
    /// <returns>The file key.</returns>
    internal static byte[] Derive(ReadOnlySpan<byte> seed, byte[][] recipients, bool encryptMetadata, int keyLength)
    {
        // The input is gathered in one pooled buffer and hashed in one call, which needs no hash object.
        var length = seed.Length + (encryptMetadata ? 0 : PermissionsLength);
        foreach (var recipient in recipients)
        {
            length += recipient.Length;
        }

        var input = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            seed.CopyTo(input);
            var at = seed.Length;
            foreach (var recipient in recipients)
            {
                recipient.CopyTo(input, at);
                at += recipient.Length;
            }

            if (!encryptMetadata)
            {
                BinaryPrimitives.WriteUInt32BigEndian(input.AsSpan(at), MetadataNotEncrypted);
            }

            Span<byte> digest = stackalloc byte[PdfPasswordAlgorithms.Sha256Length];
            var algorithm = keyLength == PdfPasswordAlgorithms.Sha256Length ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA1;
            var written = CryptographicOperations.HashData(algorithm, input.AsSpan(0, length), digest);
            return digest[..Math.Min(keyLength, written)].ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(input, true);
        }
    }
}
