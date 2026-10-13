// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using HyperPdfLibrary.Objects;
using static HyperPdfLibrary.Security.PdfPasswordAlgorithms;

namespace HyperPdfLibrary.Security;

/// <content>Working out the file key from a password.</content>
public sealed partial class PdfSecurityHandler
{
    /// <summary>The default key length in bytes for revision 2.</summary>
    private const int Revision2KeyLength = 5;

    /// <summary>The bits of a 40-bit key.</summary>
    private const int DefaultKeyBits = 40;

    /// <summary>The position of the "adb" marker in the decrypted /Perms block.</summary>
    private const int PermsMarkerOffset = 9;

    /// <summary>The longest file id kept on the stack.</summary>
    private const int MaxStackId = 64;

    /// <summary>The offset of the validation salt in /U and /O from revision 5.</summary>
    private const int ValidationSaltOffset = 32;

    /// <summary>The offset of the key salt in /U and /O from revision 5.</summary>
    private const int KeySaltOffset = 40;

    /// <summary>Gets the marker at bytes 9 to 11 of the decrypted /Perms block.</summary>
    private static ReadOnlySpan<byte> PermsMarker => "adb"u8;

    /// <summary>Finds the file key for a password, whichever revision the file uses.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="shape">The revision, version and key length.</param>
    /// <param name="firstId">The first file id.</param>
    /// <param name="password">The password.</param>
    /// <param name="role">Who the password makes the reader.</param>
    /// <returns>The file key, or <see langword="null"/> when the password is wrong.</returns>
    private static byte[]? Authenticate(PdfDictionary encrypt, in EncryptShape shape, ReadOnlySpan<byte> firstId, string? password, out PdfSecurityRole role)
    {
        bool isOwner;
        var key = shape.Revision >= Revision5
            ? Authenticate256(encrypt, shape.Revision, password, out isOwner)
            : Authenticate128(encrypt, shape.Revision, shape.Version, shape.KeyLength, firstId, password, out isOwner);
        role = isOwner ? PdfSecurityRole.Owner : PdfSecurityRole.User;
        return key;
    }

    /// <summary>Authenticates a revision 2 to 4 file, trying the user and then the owner password.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="version">The /V value.</param>
    /// <param name="keyLength">The validated key length in bytes.</param>
    /// <param name="firstId">The first file id.</param>
    /// <param name="password">The password.</param>
    /// <param name="isOwner">Whether the password is the owner password, which a password that is both user and owner password is.</param>
    /// <returns>The file key, or <see langword="null"/> when the password is wrong.</returns>
    private static byte[]? Authenticate128(PdfDictionary encrypt, int revision, int version, int keyLength, ReadOnlySpan<byte> firstId, string? password, out bool isOwner)
    {
        var owner = encrypt.GetStringBytes(KnownName.O);
        var user = encrypt.GetStringBytes(KnownName.U);
        var encryptMetadata = encrypt.GetBoolean(KnownName.EncryptMetadata, true) || version < Revision4;
        var passwordBytes = EncodeLegacyPassword(password);
        var parameters = new KeyParameters(owner, ReadPermissions(encrypt), firstId, revision, keyLength, encryptMetadata);

        // Both candidate keys are computed on the stack; only the key that matches is copied out.
        Span<byte> key = stackalloc byte[Md5.HashLength];
        Span<byte> userPassword = stackalloc byte[PaddedLength];
        Span<byte> ownerKey = stackalloc byte[Md5.HashLength];
        try
        {
            ComputeKey(passwordBytes, parameters, key);
            var userKey = key[..keyLength];
            var isUser = CheckUserKey(userKey, user, firstId, revision);

            // As the owner password, it decrypts /O into the user password.
            var passwordLength = RecoverUserPassword(passwordBytes, owner, revision, keyLength, userPassword);
            ComputeKey(userPassword[..passwordLength], parameters, ownerKey);
            var candidate = ownerKey[..keyLength];
            isOwner = CheckUserKey(candidate, user, firstId, revision);
            if (isOwner)
            {
                return candidate.ToArray();
            }

            return isUser ? userKey.ToArray() : null;
        }
        finally
        {
            // The password, the user password recovered from /O and both candidate keys are secrets; only the matching key leaves.
            CryptographicOperations.ZeroMemory(passwordBytes);
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(userPassword);
            CryptographicOperations.ZeroMemory(ownerKey);
        }
    }

    /// <summary>Checks a file key against /U (algorithms 4 and 5), in constant time.</summary>
    /// <param name="key">The candidate key.</param>
    /// <param name="user">The /U value.</param>
    /// <param name="firstId">The first file id.</param>
    /// <param name="revision">The revision.</param>
    /// <returns><see langword="true"/> when the key is right.</returns>
    private static bool CheckUserKey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> user, ReadOnlySpan<byte> firstId, int revision)
    {
        if (revision == Revision2)
        {
            Span<byte> check = stackalloc byte[PaddedLength];
            Padding.CopyTo(check);
            Rc4.Apply(key, check);
            return user.Length >= PaddedLength && CryptographicOperations.FixedTimeEquals(user[..PaddedLength], check);
        }

        Span<byte> input = firstId.Length <= MaxStackId ? stackalloc byte[PaddedLength + MaxStackId] : new byte[PaddedLength + firstId.Length];
        Padding.CopyTo(input);
        firstId.CopyTo(input[PaddedLength..]);
        Span<byte> digest = stackalloc byte[Md5.HashLength];
        _ = Md5.HashData(input[..(PaddedLength + firstId.Length)], digest);
        Rc4.Apply(key, digest);
        ApplyRoundKeys(key, digest, 1, Rc4Rounds - 1);
        return user.Length >= Md5.HashLength && CryptographicOperations.FixedTimeEquals(user[..Md5.HashLength], digest);
    }

    /// <summary>Decrypts /O with the owner password, giving the padded user password (algorithm 7).</summary>
    /// <param name="ownerPassword">The owner password bytes.</param>
    /// <param name="owner">The /O value.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="keyLength">The key length.</param>
    /// <param name="destination">Receives the user password bytes; at least <see cref="PaddedLength"/> bytes.</param>
    /// <returns>The number of user password bytes written.</returns>
    private static int RecoverUserPassword(ReadOnlySpan<byte> ownerPassword, ReadOnlySpan<byte> owner, int revision, int keyLength, Span<byte> destination)
    {
        Span<byte> padded = stackalloc byte[PaddedLength];
        PadPassword(ownerPassword, padded);
        Span<byte> digest = stackalloc byte[Md5.HashLength];
        _ = Md5.HashData(padded, digest);
        if (revision > Revision2)
        {
            for (var i = 0; i < Md5Rounds; i++)
            {
                _ = Md5.HashData(digest, digest);
            }
        }

        var key = digest[..keyLength];
        var result = destination[..Math.Min(owner.Length, PaddedLength)];
        owner[..result.Length].CopyTo(result);
        if (revision == Revision2)
        {
            Rc4.Apply(key, result);
            return result.Length;
        }

        ApplyRoundKeys(key, result, Rc4Rounds - 1, 0);
        return result.Length;
    }

    /// <summary>Authenticates a revision 5 or 6 file (algorithms 2.A and 2.B).</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="password">The password.</param>
    /// <param name="isOwner">Whether the password is the owner password.</param>
    /// <returns>The file key, or <see langword="null"/> when the password is wrong.</returns>
    private static byte[]? Authenticate256(PdfDictionary encrypt, int revision, string? password, out bool isOwner)
    {
        isOwner = false;
        var owner = encrypt.GetStringBytes(KnownName.O);
        var user = encrypt.GetStringBytes(KnownName.U);
        if (owner.Length < UserDataLength || user.Length < UserDataLength)
        {
            return null;
        }

        var passwordBytes = Encoding.UTF8.GetBytes(NormalizePassword(password));
        var bytes = passwordBytes.AsSpan(0, Math.Min(passwordBytes.Length, MaxUtf8Password));
        Span<byte> digest = stackalloc byte[Sha256Length];
        try
        {
            Hash256(bytes, owner.Slice(ValidationSaltOffset, SaltLength), user[..UserDataLength], revision, digest);
            if (CryptographicOperations.FixedTimeEquals(digest, owner[..Sha256Length]))
            {
                isOwner = true;
                Hash256(bytes, owner.Slice(KeySaltOffset, SaltLength), user[..UserDataLength], revision, digest);
                return UnwrapKey(digest, encrypt.GetStringBytes(KnownName.OE));
            }

            Hash256(bytes, user.Slice(ValidationSaltOffset, SaltLength), [], revision, digest);
            if (!CryptographicOperations.FixedTimeEquals(digest, user[..Sha256Length]))
            {
                return null;
            }

            Hash256(bytes, user.Slice(KeySaltOffset, SaltLength), [], revision, digest);
            return UnwrapKey(digest, encrypt.GetStringBytes(KnownName.UE));
        }
        finally
        {
            // The password bytes and the hashes that unwrap the file key are secrets.
            CryptographicOperations.ZeroMemory(passwordBytes);
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    /// <summary>
    /// Checks /Perms with the file key (algorithm 13, ISO 32000-2 7.6.4.4.12): the decrypted block shall hold "adb" at bytes
    /// 9 to 11. The permission and metadata bytes only "should" match /P and /EncryptMetadata, so they are not checked.
    /// </summary>
    /// <param name="perms">The /Perms value.</param>
    /// <param name="fileKey">The file key.</param>
    /// <returns><see langword="true"/> when /Perms is 16 bytes that decrypt with the "adb" marker.</returns>
    private static bool IsPermsValid(ReadOnlySpan<byte> perms, ReadOnlySpan<byte> fileKey)
    {
        if (perms.Length != AesBlock)
        {
            return false;
        }

        using var aes = Aes.Create();
        aes.Key = fileKey.ToArray();
        Span<byte> block = stackalloc byte[AesBlock];
        _ = aes.DecryptEcb(perms, block, PaddingMode.None);
        return block.Slice(PermsMarkerOffset, PermsMarker.Length).SequenceEqual(PermsMarker);
    }

    /// <summary>Decrypts /OE or /UE with an intermediate key, giving the file key.</summary>
    /// <param name="intermediate">The intermediate key.</param>
    /// <param name="wrapped">The wrapped key.</param>
    /// <returns>The file key, or <see langword="null"/> when malformed.</returns>
    private static byte[]? UnwrapKey(ReadOnlySpan<byte> intermediate, ReadOnlySpan<byte> wrapped)
    {
        if (wrapped.Length < Sha256Length)
        {
            return null;
        }

        using var aes = Aes.Create();
        aes.Key = intermediate.ToArray();
        return aes.DecryptCbc(wrapped[..Sha256Length], stackalloc byte[AesBlock], PaddingMode.None);
    }

    /// <summary>The numbers of an /Encrypt dictionary that decide how its password is checked.</summary>
    /// <param name="Revision">The /R value.</param>
    /// <param name="Version">The /V value.</param>
    /// <param name="KeyLength">The validated key length in bytes.</param>
    private readonly record struct EncryptShape(int Revision, int Version, int KeyLength);
}
