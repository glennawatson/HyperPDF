// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Security;

/// <summary>
/// The password algorithms of the standard security handler that readers and writers share (ISO 32000-2 7.6.4): padding and
/// encoding passwords, the file key of revisions 2 to 4 (algorithm 2) and the hash of revisions 5 to 7 (algorithm 2.B).
/// </summary>
internal static class PdfPasswordAlgorithms
{
    /// <summary>The length of a SHA-256 hash and an AES-256 key.</summary>
    internal const int Sha256Length = 32;

    /// <summary>The length of the padded password.</summary>
    internal const int PaddedLength = 32;

    /// <summary>The number of extra MD5 rounds from revision 3.</summary>
    internal const int Md5Rounds = 50;

    /// <summary>The number of RC4 rounds over the user key from revision 3.</summary>
    internal const int Rc4Rounds = 20;

    /// <summary>The longest password byte count used from revision 5.</summary>
    internal const int MaxUtf8Password = 127;

    /// <summary>The length of a salt.</summary>
    internal const int SaltLength = 8;

    /// <summary>The length of /U that is mixed into owner hashes from revision 5.</summary>
    internal const int UserDataLength = 48;

    /// <summary>Revision 2.</summary>
    private const int Revision2 = 2;

    /// <summary>Revision 4, the last RC4/AES-128 revision.</summary>
    private const int Revision4 = 4;

    /// <summary>Revision 6, the first that runs the iterated hash.</summary>
    private const int Revision6 = 6;

    /// <summary>The longest input of the revision 5 and 6 hash: password, salt and user data.</summary>
    private const int MaxHashInput = MaxUtf8Password + SaltLength + UserDataLength;

    /// <summary>The longest hash the revision 6 hash produces (SHA-512).</summary>
    private const int MaxHashLength = 64;

    /// <summary>The longest input of algorithm 2 hashed from the stack: the padded password, /O, /P, a file id of 64 bytes and the metadata flag.</summary>
    private const int MaxStackInput = 160;

    /// <summary>The number of rounds the revision 6 hash always runs.</summary>
    private const int HashMinRounds = 64;

    /// <summary>The offset subtracted from the round count in the revision 6 hash's exit test.</summary>
    private const int HashExitOffset = 32;

    /// <summary>The number of repetitions of the revision 6 hash input.</summary>
    private const int HashRepeats = 64;

    /// <summary>The modulus choosing the revision 6 hash function.</summary>
    private const int HashChoices = 3;

    /// <summary>The revision 6 hash choice that means SHA-384.</summary>
    private const int Sha384Choice = 1;

    /// <summary>Gets the padding mixed into passwords before revision 5.</summary>
    internal static ReadOnlySpan<byte> Padding =>
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    /// <summary>Pads or truncates a password to 32 bytes.</summary>
    /// <param name="password">The password.</param>
    /// <param name="destination">The 32-byte buffer.</param>
    internal static void PadPassword(ReadOnlySpan<byte> password, Span<byte> destination)
    {
        var length = Math.Min(password.Length, destination.Length);
        password[..length].CopyTo(destination);
        Padding[..(destination.Length - length)].CopyTo(destination[length..]);
    }

    /// <summary>Encodes a revision 2 to 4 password as PDFDocEncoding, or as Latin-1 when a character has no PDFDocEncoding byte.</summary>
    /// <param name="password">The password.</param>
    /// <returns>The password bytes.</returns>
    internal static byte[] EncodeLegacyPassword(string? password)
    {
        password ??= string.Empty;
        var bytes = new byte[password.Length];
        for (var i = 0; i < password.Length; i++)
        {
            if (!TryEncodeDocChar(password[i], out bytes[i]))
            {
                return Encoding.Latin1.GetBytes(password);
            }
        }

        return bytes;
    }

    /// <summary>Applies the NFKC form of SASLprep to a revision 5 or 6 password; text that cannot be normalised is used as it is.</summary>
    /// <param name="password">The password.</param>
    /// <returns>The normalised password.</returns>
    internal static string NormalizePassword(string? password)
    {
        password ??= string.Empty;
        try
        {
            return password.Normalize(NormalizationForm.FormKC);
        }
        catch (ArgumentException)
        {
            return password;
        }
    }

    /// <summary>Computes a file key from a user password (algorithm 2).</summary>
    /// <param name="password">The password bytes.</param>
    /// <param name="parameters">The values from the /Encrypt dictionary.</param>
    /// <returns>The key.</returns>
    internal static byte[] ComputeKey(ReadOnlySpan<byte> password, in KeyParameters parameters)
    {
        var key = new byte[parameters.KeyLength];
        ComputeKey(password, parameters, key);
        return key;
    }

    /// <summary>Computes a file key from a user password (algorithm 2) into a buffer.</summary>
    /// <param name="password">The password bytes.</param>
    /// <param name="parameters">The values from the /Encrypt dictionary.</param>
    /// <param name="destination">Receives the key; at least <see cref="KeyParameters.KeyLength"/> bytes.</param>
    internal static void ComputeKey(ReadOnlySpan<byte> password, in KeyParameters parameters, Span<byte> destination)
    {
        var length = PaddedLength + parameters.Owner.Length + sizeof(int) + parameters.FirstId.Length + sizeof(int);
        Span<byte> span = stackalloc byte[MaxStackInput];
        byte[]? rented = null;
        if (length > MaxStackInput)
        {
            rented = ArrayPool<byte>.Shared.Rent(length);
            span = rented;
        }

        try
        {
            HashKeyInput(password, parameters, span, destination);
        }
        finally
        {
            // The input holds the padded password.
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented, true);
            }
            else
            {
                CryptographicOperations.ZeroMemory(span);
            }
        }
    }

    /// <summary>Applies RC4 with the key XORed with each round number, counting from one round to another.</summary>
    /// <param name="key">The key.</param>
    /// <param name="data">The data.</param>
    /// <param name="from">The first round.</param>
    /// <param name="to">The last round.</param>
    internal static void ApplyRoundKeys(ReadOnlySpan<byte> key, Span<byte> data, int from, int to)
    {
        Span<byte> roundKey = stackalloc byte[Md5.HashLength];
        var step = from <= to ? 1 : -1;
        for (var round = from; round != to + step; round += step)
        {
            for (var k = 0; k < key.Length; k++)
            {
                roundKey[k] = (byte)(key[k] ^ round);
            }

            Rc4.Apply(roundKey[..key.Length], data);
        }
    }

    /// <summary>The password hash of revisions 5 and 6.</summary>
    /// <param name="password">The password bytes, at most 127.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="userData">The first 48 bytes of /U for owner checks, or empty.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="destination">The 32-byte hash.</param>
    internal static void Hash256(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> userData, int revision, Span<byte> destination)
    {
        Span<byte> input = stackalloc byte[MaxHashInput];
        password.CopyTo(input);
        salt.CopyTo(input[password.Length..]);
        userData.CopyTo(input[(password.Length + salt.Length)..]);
        _ = SHA256.HashData(input[..(password.Length + salt.Length + userData.Length)], destination);

        // The input holds the password.
        CryptographicOperations.ZeroMemory(input);
        if (revision >= Revision6)
        {
            HardenedHash(password, userData, destination);
        }
    }

    /// <summary>Finds the PDFDocEncoding byte of a character.</summary>
    /// <param name="value">The character.</param>
    /// <param name="encoded">The byte.</param>
    /// <returns><see langword="true"/> when the encoding has the character.</returns>
    private static bool TryEncodeDocChar(char value, out byte encoded)
    {
        encoded = 0;
        for (var candidate = 0; candidate <= byte.MaxValue && encoded == 0; candidate++)
        {
            encoded = PdfText.DecodeDocByte((byte)candidate) == value ? (byte)candidate : (byte)0;
        }

        // Byte zero only encodes the NUL character.
        return encoded != 0 || value == '\0';
    }

    /// <summary>The iterated hash that revision 6 adds (algorithm 2.B); one pooled buffer serves every round.</summary>
    /// <param name="password">The password bytes.</param>
    /// <param name="userData">The first 48 bytes of /U for owner checks, or empty.</param>
    /// <param name="digest">The hash so far, replaced by the result.</param>
    private static void HardenedHash(ReadOnlySpan<byte> password, ReadOnlySpan<byte> userData, Span<byte> digest)
    {
        Span<byte> key = stackalloc byte[MaxHashLength];
        digest.CopyTo(key);
        var keyLength = Sha256Length;
        var block = ArrayPool<byte>.Shared.Rent((MaxUtf8Password + MaxHashLength + UserDataLength) * HashRepeats);
        var encrypted = ArrayPool<byte>.Shared.Rent(block.Length);

        // The CPU's AES instructions need no cipher object per round; the .NET cipher is made once and rekeyed where they are missing.
        var fallback = AesCbcNoPadding.IsAccelerated ? null : Aes.Create();
        try
        {
            for (var round = 0;; round++)
            {
                var length = FillRepeats(password, key[..keyLength], userData, block);
                EncryptRound(fallback, key, block.AsSpan(0, length), encrypted);
                keyLength = NextHash(encrypted.AsSpan(0, length), key);
                if (round >= HashMinRounds - 1 && encrypted[length - 1] <= round + 1 - HashExitOffset)
                {
                    break;
                }
            }

            key[..Sha256Length].CopyTo(digest);
        }
        finally
        {
            fallback?.Dispose();

            // The blocks hold the password and hash, so they are wiped before the pool lends them again; so is the running hash,
            // whether or not the rounds finished.
            ArrayPool<byte>.Shared.Return(block, true);
            ArrayPool<byte>.Shared.Return(encrypted, true);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>Encrypts a round's block with AES-128 in CBC mode, the key and initialisation vector being the start of the running hash.</summary>
    /// <param name="fallback">The .NET cipher to use when the CPU has no AES instructions, or <see langword="null"/>.</param>
    /// <param name="key">The running hash: the AES key and then the initialisation vector.</param>
    /// <param name="plain">The block.</param>
    /// <param name="destination">Receives the cipher text.</param>
    private static void EncryptRound(Aes? fallback, ReadOnlySpan<byte> key, ReadOnlySpan<byte> plain, Span<byte> destination)
    {
        var aesKey = key[..PdfSecurityHandler.AesBlock];
        var iv = key.Slice(PdfSecurityHandler.AesBlock, PdfSecurityHandler.AesBlock);
        if (fallback is null)
        {
            _ = AesCbcNoPadding.TryEncrypt(aesKey, iv, plain, destination);
            return;
        }

        fallback.SetKey(aesKey);
        _ = fallback.EncryptCbc(plain, iv, destination, PaddingMode.None);
    }

    /// <summary>Builds the input of algorithm 2 in a buffer and hashes it into the key.</summary>
    /// <param name="password">The password bytes.</param>
    /// <param name="parameters">The values from the /Encrypt dictionary.</param>
    /// <param name="span">The buffer for the input, large enough for it.</param>
    /// <param name="destination">Receives the key.</param>
    private static void HashKeyInput(ReadOnlySpan<byte> password, in KeyParameters parameters, Span<byte> span, Span<byte> destination)
    {
        PadPassword(password, span[..PaddedLength]);
        var offset = PaddedLength;
        parameters.Owner.CopyTo(span[offset..]);
        offset += parameters.Owner.Length;
        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], parameters.Permissions);
        offset += sizeof(int);
        parameters.FirstId.CopyTo(span[offset..]);
        offset += parameters.FirstId.Length;
        if (parameters.Revision >= Revision4 && !parameters.EncryptMetadata)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[offset..], -1);
            offset += sizeof(int);
        }

        Span<byte> digest = stackalloc byte[Md5.HashLength];
        _ = Md5.HashData(span[..offset], digest);
        if (parameters.Revision > Revision2)
        {
            for (var i = 0; i < Md5Rounds; i++)
            {
                _ = Md5.HashData(digest[..parameters.KeyLength], digest);
            }
        }

        digest[..parameters.KeyLength].CopyTo(destination);
        CryptographicOperations.ZeroMemory(digest);
    }

    /// <summary>Writes password, key and user data 64 times over.</summary>
    /// <param name="password">The password bytes.</param>
    /// <param name="key">The current key.</param>
    /// <param name="userData">The user data, or empty.</param>
    /// <param name="block">The buffer.</param>
    /// <returns>The bytes written.</returns>
    private static int FillRepeats(ReadOnlySpan<byte> password, ReadOnlySpan<byte> key, ReadOnlySpan<byte> userData, Span<byte> block)
    {
        var unit = password.Length + key.Length + userData.Length;
        for (var i = 0; i < HashRepeats; i++)
        {
            var at = block[(i * unit)..];
            password.CopyTo(at);
            key.CopyTo(at[password.Length..]);
            userData.CopyTo(at[(password.Length + key.Length)..]);
        }

        return unit * HashRepeats;
    }

    /// <summary>Hashes a round's output with SHA-256, SHA-384 or SHA-512, chosen by its first 16 bytes.</summary>
    /// <param name="encrypted">The round's output.</param>
    /// <param name="key">The buffer receiving the hash.</param>
    /// <returns>The hash length.</returns>
    private static int NextHash(ReadOnlySpan<byte> encrypted, Span<byte> key)
    {
        var sum = 0;
        foreach (var b in encrypted[..PdfSecurityHandler.AesBlock])
        {
            sum += b;
        }

        return (sum % HashChoices) switch
        {
            0 => SHA256.HashData(encrypted, key),
            Sha384Choice => SHA384.HashData(encrypted, key),
            _ => SHA512.HashData(encrypted, key),
        };
    }

    /// <summary>The /Encrypt values that go into a revision 2 to 4 file key.</summary>
    /// <param name="owner">The /O value.</param>
    /// <param name="permissions">The /P value.</param>
    /// <param name="firstId">The first file id.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="keyLength">The key length in bytes.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    internal readonly ref struct KeyParameters(ReadOnlySpan<byte> owner, int permissions, ReadOnlySpan<byte> firstId, int revision, int keyLength, bool encryptMetadata)
    {
        /// <summary>Gets the /O value.</summary>
        public ReadOnlySpan<byte> Owner { get; } = owner;

        /// <summary>Gets the /P value.</summary>
        public int Permissions { get; } = permissions;

        /// <summary>Gets the first file id.</summary>
        public ReadOnlySpan<byte> FirstId { get; } = firstId;

        /// <summary>Gets the revision.</summary>
        public int Revision { get; } = revision;

        /// <summary>Gets the key length in bytes.</summary>
        public int KeyLength { get; } = keyLength;

        /// <summary>Gets a value indicating whether metadata is encrypted.</summary>
        public bool EncryptMetadata { get; } = encryptMetadata;
    }
}
