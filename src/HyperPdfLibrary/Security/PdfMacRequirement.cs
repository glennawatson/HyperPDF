// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Security.Cryptography;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Decides whether an Encrypt dictionary requires a PDF MAC in every revision: permission bit 13 is zero in <c>/P</c> or in
/// the permissions that <c>/Perms</c> protects (ISO/TS 32004:2024 5.1.2 and 5.1.3), and the handler is version 5 or later.
/// </summary>
internal static class PdfMacRequirement
{
    /// <summary>The permission flags with only bit 13 set; the TS numbers bits from 1.</summary>
    internal const int Bit13 = 1 << 12;

    /// <summary>The length of the <c>/Perms</c> value.</summary>
    private const int PermsLength = 16;

    /// <summary>The offset of the "adb" marker in the decrypted <c>/Perms</c> block (ISO 32000-2 7.6.4.4.11).</summary>
    private const int MarkerOffset = 9;

    /// <summary>Gets the marker bytes after the metadata flag.</summary>
    private static ReadOnlySpan<byte> Marker => "adb"u8;

    /// <summary>Determines whether the dictionary requires a PDF MAC.</summary>
    /// <param name="encrypt">The Encrypt dictionary.</param>
    /// <param name="security">The document's security handler, whose file key decrypts <c>/Perms</c>.</param>
    /// <returns><see langword="true"/> when permission bit 13 is zero in <c>/P</c> or in <c>/Perms</c>, and <c>/V</c> is at least 5.</returns>
    internal static bool IsRequired(PdfDictionary encrypt, PdfSecurityHandler security)
    {
        if (encrypt.GetInt32(KnownName.V) < PdfMacKeys.MinimumVersion)
        {
            return false;
        }

        // The public-key handler keeps the permissions in the recipient's data instead of /P (ISO 32000-2 7.6.5.2).
        var permissions = PdfSecurityHandler.IsPublicKey(encrypt) ? security.Permissions : unchecked((int)encrypt.GetInteger(KnownName.P));
        if ((permissions & Bit13) == 0)
        {
            return true;
        }

        // /Perms is encrypted with the file key, so an attacker who strips bit 13 from /P cannot change it to match.
        return TryReadPermsFlags(encrypt.GetStringBytes(KnownName.Perms), security.FileKey, out var flags) && (flags & Bit13) == 0;
    }

    /// <summary>Decrypts <c>/Perms</c> and reads its permission flags.</summary>
    /// <param name="perms">The <c>/Perms</c> value.</param>
    /// <param name="fileKey">The file key.</param>
    /// <param name="flags">Receives the flags when the block decrypts to a valid one.</param>
    /// <returns><see langword="true"/> when the block carries the "adb" marker.</returns>
    internal static bool TryReadPermsFlags(ReadOnlySpan<byte> perms, ReadOnlySpan<byte> fileKey, out int flags)
    {
        flags = 0;
        if (perms.Length != PermsLength || fileKey.Length != PdfMacKeys.KeyEncryptionKeyLength)
        {
            return false;
        }

        using var aes = Aes.Create();
        aes.SetKey(fileKey);
        Span<byte> block = stackalloc byte[PermsLength];
        _ = aes.DecryptEcb(perms, block, PaddingMode.None);
        if (!block.Slice(MarkerOffset, Marker.Length).SequenceEqual(Marker))
        {
            return false;
        }

        flags = BinaryPrimitives.ReadInt32LittleEndian(block);
        return true;
    }
}
