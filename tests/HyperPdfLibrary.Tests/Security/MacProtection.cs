// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>How a test document is encrypted: its /Encrypt dictionary, its file key and how it protects each string and stream.</summary>
/// <param name="EncryptText">The /Encrypt dictionary as PDF text, including /KDFSalt.</param>
/// <param name="FileKey">The file encryption key.</param>
/// <param name="Protect">Encrypts the bytes of a string or stream for an object number.</param>
internal sealed record MacProtection(string EncryptText, byte[] FileKey, Func<int, byte[], byte[]> Protect)
{
    /// <summary>The length of the /KDFSalt.</summary>
    internal const int SaltLength = 32;

    /// <summary>The permission flags with bit 13 zero: a PDF MAC is required (ISO/TS 32004 5.1.2).</summary>
    internal const int MacRequired = -4100;

    /// <summary>The permission flags with bit 13 set: no PDF MAC is required.</summary>
    internal const int MacNotRequired = -4;

    /// <summary>The length of an AES block and of a CBC IV.</summary>
    private const int BlockLength = 16;

    /// <summary>The seed of the /KDFSalt.</summary>
    private const byte SaltSeed = 0x70;

    /// <summary>The seed of the IV of each object.</summary>
    private const byte IvSeed = 0xA0;

    /// <summary>The length of the closing brackets of a dictionary, which the salt is written before.</summary>
    private const int DictionaryEndLength = 2;

    /// <summary>Gets the 32-byte /KDFSalt of every test document.</summary>
    internal static byte[] Salt { get; } = Sequence(SaltLength, SaltSeed);

    /// <summary>Creates the standard security handler with AES-256 in CBC mode (/V 5, /R 6) or AES-GCM (/V 6, /R 7).</summary>
    /// <param name="gcm">Whether to use AES-GCM.</param>
    /// <param name="permissions">The permission flags.</param>
    /// <returns>The protection.</returns>
    internal static MacProtection Standard(bool gcm, int permissions)
    {
        var fileKey = AesGcmPdf.FileKey;
        var text = AesGcmPdf.StandardEncryptDictionary(gcm, permissions, $"/KDFSalt <{Convert.ToHexString(Salt)}>");
        return new(text, fileKey, gcm ? AesGcmPdf.Protect : (number, plain) => Cbc(fileKey, number, plain));
    }

    /// <summary>Creates the public-key security handler (sub-filter adbe.pkcs7.s5, AES-256) for one certificate.</summary>
    /// <param name="recipient">The recipient's certificate.</param>
    /// <param name="permissions">The permissions in the recipient's list.</param>
    /// <returns>The protection.</returns>
    internal static MacProtection PublicKey(X509Certificate2 recipient, int permissions)
    {
        byte[][] envelopes = [PublicKeyPdf.Envelope(permissions, recipient)];
        var fileKey = PublicKeyPdf.FileKey(envelopes, PublicKeyCipher.Aes256, true);
        var text = PublicKeyPdf.EncryptDictionary(PublicKeyCipher.Aes256, envelopes, true);
        text = string.Create(CultureInfo.InvariantCulture, $"{text[..^DictionaryEndLength]} /KDFSalt <{Convert.ToHexString(Salt)}> >>");
        return new(text, fileKey, (number, plain) => Cbc(fileKey, number, plain));
    }

    /// <summary>Makes a fixed byte sequence so a file is the same on every run.</summary>
    /// <param name="length">The length.</param>
    /// <param name="seed">The first byte.</param>
    /// <returns>The bytes.</returns>
    internal static byte[] Sequence(int length, byte seed) => [.. Enumerable.Range(0, length).Select(i => (byte)(seed + i))];

    /// <summary>Encrypts with AES-256 in CBC mode: a 16-byte IV, then the PKCS#7 padded cipher text (ISO 32000-2 7.6.3).</summary>
    /// <param name="key">The file key.</param>
    /// <param name="number">The object number, which selects the IV.</param>
    /// <param name="plain">The plain bytes.</param>
    /// <returns>The protected bytes.</returns>
    private static byte[] Cbc(byte[] key, int number, byte[] plain)
    {
        var iv = Sequence(BlockLength, (byte)(IvSeed + number));
        using var aes = Aes.Create();
        aes.Key = key;
        return [.. iv, .. aes.EncryptCbc(plain, iv)];
    }
}
