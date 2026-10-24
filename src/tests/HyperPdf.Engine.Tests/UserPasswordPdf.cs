// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Builds a one page PDF protected by the AES-256 standard security handler, revision 6 (ISO 32000-2 7.6.4.3.3 and
/// 7.6.4.4), with a user password. The file holds no strings or streams, so nothing besides the keys needs encrypting.
/// </summary>
internal static class UserPasswordPdf
{
    /// <summary>The standard security handler entries before the keys: AES-256 for strings and streams.</summary>
    private const string HandlerEntries =
        "/Filter /Standard /V 5 /R 6 /Length 256 /CF << /StdCF << /AuthEvent /DocOpen /CFM /AESV3 /Length 32 >> >> /StmF /StdCF /StrF /StdCF";

    /// <summary>The permissions: every operation allowed.</summary>
    private const int Permissions = -4;

    /// <summary>The bytes in the file encryption key.</summary>
    private const int KeyLength = 32;

    /// <summary>The bytes in a validation or key salt.</summary>
    private const int SaltLength = 8;

    /// <summary>The bytes in an AES block.</summary>
    private const int BlockLength = 16;

    /// <summary>The bytes of AES-128 key taken from the round hash.</summary>
    private const int RoundKeyLength = 16;

    /// <summary>The times the round input is repeated (algorithm 2.B step a).</summary>
    private const int RoundRepeats = 64;

    /// <summary>The fewest hash rounds (algorithm 2.B step e).</summary>
    private const int MinimumRounds = 64;

    /// <summary>The amount the last byte is compared above the round number (algorithm 2.B step e).</summary>
    private const int RoundMargin = 32;

    /// <summary>The number of SHA-2 variants the round chooses between.</summary>
    private const int HashChoices = 3;

    /// <summary>The bytes in the file identifier.</summary>
    private const int IdLength = 16;

    /// <summary>The place of the first marker byte in the decrypted <c>/Perms</c> block.</summary>
    private const int PermsMarker = 8;

    /// <summary>Builds the file.</summary>
    /// <param name="userPassword">The password that opens the file for reading.</param>
    /// <param name="ownerPassword">The password that opens the file with full permissions.</param>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] Create(string userPassword, string ownerPassword)
    {
        var fileKey = Sequence(KeyLength, 0x11);
        var userKey = PasswordKey(Encoding.UTF8.GetBytes(userPassword), Sequence(SaltLength, 0x21), Sequence(SaltLength, 0x31), [], fileKey, out var userEncryptedKey);
        var ownerKey = PasswordKey(Encoding.UTF8.GetBytes(ownerPassword), Sequence(SaltLength, 0x41), Sequence(SaltLength, 0x51), userKey, fileKey, out var ownerEncryptedKey);
        var keys = string.Create(
            CultureInfo.InvariantCulture,
            $"/O <{Convert.ToHexString(ownerKey)}> /U <{Convert.ToHexString(userKey)}> /OE <{Convert.ToHexString(ownerEncryptedKey)}> /UE <{Convert.ToHexString(userEncryptedKey)}>");
        var encrypt = string.Create(CultureInfo.InvariantCulture, $"<< {HandlerEntries} {keys} /Perms <{Convert.ToHexString(Perms(fileKey))}> /P {Permissions} >>");
        var id = Convert.ToHexString(Sequence(IdLength, 0x61));
        return MiniPdf.BuildWithTrailer(
            $"/Encrypt 4 0 R /ID [<{id}> <{id}>]",
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> >>",
            encrypt);
    }

    /// <summary>Works out a <c>/U</c> or <c>/O</c> value and its encrypted copy of the file key (algorithms 8 and 9).</summary>
    /// <param name="password">The password bytes.</param>
    /// <param name="validationSalt">The validation salt.</param>
    /// <param name="keySalt">The key salt.</param>
    /// <param name="userKey">The 48-byte <c>/U</c> value for the owner password; empty for the user password.</param>
    /// <param name="fileKey">The file encryption key.</param>
    /// <param name="encryptedKey">The <c>/UE</c> or <c>/OE</c> value.</param>
    /// <returns>The 48-byte <c>/U</c> or <c>/O</c> value.</returns>
    private static byte[] PasswordKey(byte[] password, byte[] validationSalt, byte[] keySalt, byte[] userKey, byte[] fileKey, out byte[] encryptedKey)
    {
        using var aes = Aes.Create();
        aes.Key = Hash(password, keySalt, userKey);
        encryptedKey = aes.EncryptCbc(fileKey, new byte[BlockLength], PaddingMode.None);
        return [.. Hash(password, validationSalt, userKey), .. validationSalt, .. keySalt];
    }

    /// <summary>Encrypts the permissions block with the file key (algorithm 10).</summary>
    /// <param name="fileKey">The file encryption key.</param>
    /// <returns>The 16-byte <c>/Perms</c> value.</returns>
    private static byte[] Perms(byte[] fileKey)
    {
        var block = new byte[BlockLength];
        BinaryPrimitives.WriteInt32LittleEndian(block, Permissions);
        BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(sizeof(int)), -1);
        "Tadb"u8.CopyTo(block.AsSpan(PermsMarker));
        using var aes = Aes.Create();
        aes.Key = fileKey;
        return aes.EncryptEcb(block, PaddingMode.None);
    }

    /// <summary>The revision 6 password hash (algorithm 2.B).</summary>
    /// <param name="password">The password bytes.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="userKey">The 48-byte <c>/U</c> value for owner hashes; empty for user hashes.</param>
    /// <returns>The 32-byte hash.</returns>
    private static byte[] Hash(byte[] password, byte[] salt, byte[] userKey)
    {
        var key = SHA256.HashData([.. password, .. salt, .. userKey]);
        using var aes = Aes.Create();
        var round = 0;
        var last = 0;
        while (round < MinimumRounds || round < last + RoundMargin)
        {
            byte[] once = [.. password, .. key, .. userKey];
            var repeated = new byte[once.Length * RoundRepeats];
            for (var i = 0; i < RoundRepeats; i++)
            {
                once.CopyTo(repeated, i * once.Length);
            }

            aes.Key = key.AsSpan(0, RoundKeyLength).ToArray();
            var encrypted = aes.EncryptCbc(repeated, key.AsSpan(RoundKeyLength, BlockLength), PaddingMode.None);
            key = Remainder(encrypted) switch
            {
                0 => SHA256.HashData(encrypted),
                1 => SHA384.HashData(encrypted),
                _ => SHA512.HashData(encrypted),
            };
            last = encrypted[^1];
            round++;
        }

        return key.AsSpan(0, KeyLength).ToArray();
    }

    /// <summary>Takes the first 16 bytes as a big-endian number modulo 3, which equals their byte sum modulo 3.</summary>
    /// <param name="encrypted">The round's encrypted bytes.</param>
    /// <returns>0, 1 or 2.</returns>
    private static int Remainder(byte[] encrypted)
    {
        var sum = 0;
        for (var i = 0; i < BlockLength; i++)
        {
            sum += encrypted[i];
        }

        return sum % HashChoices;
    }

    /// <summary>Makes a fixed byte sequence so the file is the same on every run.</summary>
    /// <param name="length">The length.</param>
    /// <param name="seed">The first byte.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Sequence(int length, byte seed)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)(seed + i);
        }

        return bytes;
    }
}
