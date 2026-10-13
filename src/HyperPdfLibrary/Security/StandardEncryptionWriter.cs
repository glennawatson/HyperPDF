// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Works out the entries of a new standard security handler dictionary: for revision 4 the owner and user strings of
/// ISO 32000-2 7.6.4.4.2 and 7.6.4.4.4 (algorithms 3 and 5) and the file key of 7.6.4.3.2 (algorithm 2); for revisions 6 and
/// 7 a random file key and the strings of 7.6.4.4.7 to 7.6.4.4.9 (algorithms 8, 9 and 10) with the hash of 7.6.4.3.4
/// (algorithm 2.B). Revisions 2, 3 and 5 are never written.
/// </summary>
internal static class StandardEncryptionWriter
{
    /// <summary>Revision 4, AES-128 with crypt filters.</summary>
    internal const int Revision4 = 4;

    /// <summary>Revision 6, AES-256 (ISO 32000-2).</summary>
    internal const int Revision6 = 6;

    /// <summary>Revision 7, AES-256-GCM (ISO/TS 32003).</summary>
    internal const int Revision7 = 7;

    /// <summary>The offset of the "adb" marker in the <c>/Perms</c> block.</summary>
    private const int MarkerOffset = 9;

    /// <summary>The file key length of revision 4, in bytes.</summary>
    private const int LegacyKeyLength = 16;

    /// <summary>The length of the <c>/U</c> hash and of the arbitrary padding that follows it in revision 4.</summary>
    private const int LegacyHalf = 16;

    /// <summary>The length of <c>/O</c> and <c>/U</c> in revision 4.</summary>
    private const int LegacyEntryLength = 32;

    /// <summary>The length of <c>/O</c> and <c>/U</c> in revisions 6 and 7.</summary>
    private const int ModernEntryLength = 48;

    /// <summary>The length of the hash at the start of <c>/O</c> and <c>/U</c> in revisions 6 and 7.</summary>
    private const int HashLength = 32;

    /// <summary>The length of the validation salt and of the key salt.</summary>
    private const int SaltLength = 8;

    /// <summary>The length of <c>/Perms</c>, one AES block.</summary>
    private const int PermsLength = 16;

    /// <summary>The offset of the metadata flag in the <c>/Perms</c> block.</summary>
    private const int MetadataFlagOffset = 8;

    /// <summary>The offset of the random bytes in the <c>/Perms</c> block.</summary>
    private const int PermsRandomOffset = 12;

    /// <summary>The offset of the high half of the permissions in the <c>/Perms</c> block.</summary>
    private const int PermsHighOffset = 4;

    /// <summary>The number of RC4 rounds after the first in algorithms 3 and 5.</summary>
    private const int ExtraRc4Rounds = 19;

    /// <summary>Gets the marker "adb" at bytes 9 to 11 of the <c>/Perms</c> block.</summary>
    private static ReadOnlySpan<byte> Marker => "adb"u8;

    /// <summary>Creates the entries.</summary>
    /// <param name="revision">The revision: 4, 6 or 7.</param>
    /// <param name="userPassword">The user password, or <see langword="null"/>.</param>
    /// <param name="ownerPassword">The owner password; when none, the user password is used (algorithm 3 step a).</param>
    /// <param name="permissions">The <c>/P</c> value.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <param name="firstId">The first part of the trailer <c>/ID</c>, which revision 4 mixes into the key and <c>/U</c>.</param>
    /// <param name="fill">The random source.</param>
    /// <returns>The entries.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="revision"/> is not 4, 6 or 7.</exception>
    internal static StandardEncryptionEntries Create(
        int revision,
        string? userPassword,
        string? ownerPassword,
        int permissions,
        bool encryptMetadata,
        ReadOnlySpan<byte> firstId,
        FillBytes fill)
    {
        var owner = string.IsNullOrEmpty(ownerPassword) ? userPassword : ownerPassword;
        return revision switch
        {
            Revision4 => CreateLegacy(userPassword, owner, permissions, encryptMetadata, firstId),
            Revision6 or Revision7 => CreateModern(revision, userPassword, owner, permissions, encryptMetadata, fill),
            _ => throw new ArgumentOutOfRangeException(nameof(revision), revision, "Only revisions 4, 6 and 7 are written."),
        };
    }

    /// <summary>Creates the entries of revision 4: RC4-keyed <c>/O</c> and <c>/U</c> (algorithms 3 and 5) and the MD5 file key (algorithm 2).</summary>
    /// <param name="userPassword">The user password.</param>
    /// <param name="ownerPassword">The owner password, already defaulted to the user password.</param>
    /// <param name="permissions">The <c>/P</c> value.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <param name="firstId">The first file id.</param>
    /// <returns>The entries.</returns>
    private static StandardEncryptionEntries CreateLegacy(string? userPassword, string? ownerPassword, int permissions, bool encryptMetadata, ReadOnlySpan<byte> firstId)
    {
        var user = PdfPasswordAlgorithms.EncodeLegacyPassword(userPassword);
        var owner = PdfPasswordAlgorithms.EncodeLegacyPassword(ownerPassword);
        try
        {
            var ownerEntry = ComputeLegacyOwner(owner, user);
            var key = PdfPasswordAlgorithms.ComputeKey(
                user,
                new(ownerEntry, permissions, firstId, Revision4, LegacyKeyLength, encryptMetadata));
            return new(Revision4, permissions, key, ownerEntry, ComputeLegacyUser(key, firstId), null, null, null);
        }
        finally
        {
            // The password bytes are secrets that nothing needs once the entries are made.
            CryptographicOperations.ZeroMemory(user);
            CryptographicOperations.ZeroMemory(owner);
        }
    }

    /// <summary>Computes <c>/O</c> (algorithm 3): the padded user password, RC4-encrypted twenty times under keys from the owner password.</summary>
    /// <param name="ownerPassword">The owner password bytes.</param>
    /// <param name="userPassword">The user password bytes.</param>
    /// <returns>The 32-byte value.</returns>
    private static byte[] ComputeLegacyOwner(ReadOnlySpan<byte> ownerPassword, ReadOnlySpan<byte> userPassword)
    {
        Span<byte> padded = stackalloc byte[PdfPasswordAlgorithms.PaddedLength];
        PdfPasswordAlgorithms.PadPassword(ownerPassword, padded);
        Span<byte> digest = stackalloc byte[Md5.HashLength];
        _ = Md5.HashData(padded, digest);
        for (var i = 0; i < PdfPasswordAlgorithms.Md5Rounds; i++)
        {
            _ = Md5.HashData(digest, digest);
        }

        var entry = new byte[LegacyEntryLength];
        PdfPasswordAlgorithms.PadPassword(userPassword, entry);
        Rc4.Apply(digest[..LegacyKeyLength], entry);
        PdfPasswordAlgorithms.ApplyRoundKeys(digest[..LegacyKeyLength], entry, 1, ExtraRc4Rounds);

        // The padded password and the key made from it are secrets.
        CryptographicOperations.ZeroMemory(padded);
        CryptographicOperations.ZeroMemory(digest);
        return entry;
    }

    /// <summary>Computes <c>/U</c> (algorithm 5): the MD5 of the padding and the file id, RC4-encrypted twenty times under the file key, then 16 padding bytes.</summary>
    /// <param name="key">The file key.</param>
    /// <param name="firstId">The first file id.</param>
    /// <returns>The 32-byte value.</returns>
    private static byte[] ComputeLegacyUser(ReadOnlySpan<byte> key, ReadOnlySpan<byte> firstId)
    {
        var input = new byte[PdfPasswordAlgorithms.PaddedLength + firstId.Length];
        PdfPasswordAlgorithms.Padding.CopyTo(input);
        firstId.CopyTo(input.AsSpan(PdfPasswordAlgorithms.PaddedLength));
        var entry = new byte[LegacyEntryLength];
        _ = Md5.HashData(input, entry.AsSpan(0, LegacyHalf));
        Rc4.Apply(key, entry.AsSpan(0, LegacyHalf));
        PdfPasswordAlgorithms.ApplyRoundKeys(key, entry.AsSpan(0, LegacyHalf), 1, ExtraRc4Rounds);

        // The last 16 bytes are arbitrary; readers compare only the first 16.
        return entry;
    }

    /// <summary>Creates the entries of revisions 6 and 7 (algorithms 8, 9 and 10) around a random 256-bit file key.</summary>
    /// <param name="revision">The revision.</param>
    /// <param name="userPassword">The user password.</param>
    /// <param name="ownerPassword">The owner password, already defaulted to the user password.</param>
    /// <param name="permissions">The <c>/P</c> value.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <param name="fill">The random source.</param>
    /// <returns>The entries.</returns>
    private static StandardEncryptionEntries CreateModern(int revision, string? userPassword, string? ownerPassword, int permissions, bool encryptMetadata, FillBytes fill)
    {
        var fileKey = new byte[PdfPasswordAlgorithms.Sha256Length];
        fill(fileKey);

        // The passwords are held in stack buffers and wiped as soon as the entries are made.
        Span<byte> user = stackalloc byte[PdfPasswordAlgorithms.MaxUtf8Password];
        Span<byte> owner = stackalloc byte[PdfPasswordAlgorithms.MaxUtf8Password];
        var userLength = EncodeModernPassword(userPassword, user);
        var ownerLength = EncodeModernPassword(ownerPassword, owner);
        var userEntry = new byte[ModernEntryLength];
        var userKey = ProtectKey(user[..userLength], [], fileKey, revision, userEntry, fill);
        var ownerEntry = new byte[ModernEntryLength];
        var ownerKey = ProtectKey(owner[..ownerLength], userEntry, fileKey, revision, ownerEntry, fill);
        CryptographicOperations.ZeroMemory(user);
        CryptographicOperations.ZeroMemory(owner);
        return new(revision, permissions, fileKey, ownerEntry, userEntry, ownerKey, userKey, ComputePerms(fileKey, permissions, encryptMetadata, fill));
    }

    /// <summary>
    /// Fills a 48-byte <c>/U</c> or <c>/O</c> (hash, validation salt, key salt) and returns the matching <c>/UE</c> or <c>/OE</c>
    /// (algorithms 8 and 9): the file key encrypted with AES-256, CBC, no padding and a zero IV, under the hash of the password and key salt.
    /// </summary>
    /// <param name="password">The password's UTF-8 bytes, at most 127.</param>
    /// <param name="userData">The 48-byte <c>/U</c> for the owner entry, or empty for the user entry.</param>
    /// <param name="fileKey">The file key.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="entry">Receives the 48-byte string.</param>
    /// <param name="fill">The random source.</param>
    /// <returns>The 32-byte encrypted file key.</returns>
    private static byte[] ProtectKey(ReadOnlySpan<byte> password, ReadOnlySpan<byte> userData, byte[] fileKey, int revision, Span<byte> entry, FillBytes fill)
    {
        var salts = entry[HashLength..];
        fill(salts);
        PdfPasswordAlgorithms.Hash256(password, salts[..SaltLength], userData, revision, entry[..HashLength]);
        Span<byte> intermediate = stackalloc byte[HashLength];
        PdfPasswordAlgorithms.Hash256(password, salts[SaltLength..], userData, revision, intermediate);
        var protectedKey = new byte[fileKey.Length];
        Span<byte> zeroIv = stackalloc byte[PdfSecurityHandler.AesBlock];
        zeroIv.Clear();
        if (!AesCbcNoPadding.TryEncrypt(intermediate, zeroIv, fileKey, protectedKey))
        {
            using var aes = Aes.Create();
            aes.SetKey(intermediate);
            _ = aes.EncryptCbc(fileKey, zeroIv, protectedKey, PaddingMode.None);
        }

        CryptographicOperations.ZeroMemory(intermediate);
        return protectedKey;
    }

    /// <summary>Computes <c>/Perms</c> (algorithm 10): the permissions as 64 bits, the metadata flag, "adb" and random bytes, encrypted with AES-256 in ECB mode under the file key.</summary>
    /// <param name="fileKey">The file key.</param>
    /// <param name="permissions">The <c>/P</c> value.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <param name="fill">The random source.</param>
    /// <returns>The 16-byte value.</returns>
    private static byte[] ComputePerms(byte[] fileKey, int permissions, bool encryptMetadata, FillBytes fill)
    {
        Span<byte> block = stackalloc byte[PermsLength];
        BinaryPrimitives.WriteInt32LittleEndian(block, permissions);
        BinaryPrimitives.WriteInt32LittleEndian(block[PermsHighOffset..], -1);
        block[MetadataFlagOffset] = encryptMetadata ? (byte)'T' : (byte)'F';
        Marker.CopyTo(block[MarkerOffset..]);
        fill(block[PermsRandomOffset..]);

        // One block with a zero initialisation vector in CBC mode is the same as that block in ECB mode.
        var perms = new byte[PermsLength];
        Span<byte> zeroIv = stackalloc byte[PdfSecurityHandler.AesBlock];
        zeroIv.Clear();
        if (!AesCbcNoPadding.TryEncrypt(fileKey, zeroIv, block, perms))
        {
            using var aes = Aes.Create();
            aes.SetKey(fileKey);
            _ = aes.EncryptEcb(block, perms, PaddingMode.None);
        }

        return perms;
    }

    /// <summary>Normalises a revision 6 or 7 password to UTF-8 and cuts it to 127 bytes (7.6.4.3.3 steps a and b).</summary>
    /// <param name="password">The password.</param>
    /// <param name="destination">Receives the bytes; 127 bytes long.</param>
    /// <returns>The number of bytes written.</returns>
    private static int EncodeModernPassword(string? password, Span<byte> destination)
    {
        var text = PdfPasswordAlgorithms.NormalizePassword(password);
        var count = Encoding.UTF8.GetByteCount(text);
        if (count <= destination.Length)
        {
            return Encoding.UTF8.GetBytes(text, destination);
        }

        // A longer password is cut at 127 bytes, wherever that falls.
        var whole = ArrayPool<byte>.Shared.Rent(count);
        try
        {
            _ = Encoding.UTF8.GetBytes(text, whole);
            whole.AsSpan(0, destination.Length).CopyTo(destination);
            return destination.Length;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(whole, true);
        }
    }
}
