// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>
/// Builds one-page documents encrypted with the public-key security handler, working out the file key and encrypting the
/// page's content stream independently of the library.
/// </summary>
internal static class PublicKeyPdf
{
    /// <summary>The content stream's object number.</summary>
    private const int ContentObject = 4;

    /// <summary>The length of the seed.</summary>
    private const int SeedLength = 20;

    /// <summary>The length of the permissions after the seed.</summary>
    private const int PermissionsLength = 4;

    /// <summary>The length of an AES-128 or RC4-128 key and of an AES block.</summary>
    private const int ShortKeyLength = 16;

    /// <summary>The bytes of an object number in an object key.</summary>
    private const int NumberBytes = 3;

    /// <summary>The bytes of a generation in an object key.</summary>
    private const int GenerationBytes = 2;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The seed's filler byte.</summary>
    private const byte SeedByte = 0x5A;

    /// <summary>Gets the page content: a filled blue square, which renders without fonts.</summary>
    internal static string Content { get; } = "0 0 1 rg 20 20 100 100 re f";

    /// <summary>Gets the AES-128 object key suffix.</summary>
    private static ReadOnlySpan<byte> AesSalt => "sAlT"u8;

    /// <summary>Builds the plaintext twin.</summary>
    /// <returns>The file.</returns>
    internal static byte[] Plain() => Build(Encoding.ASCII.GetBytes(Content), string.Empty);

    /// <summary>Builds an encrypted document.</summary>
    /// <param name="cipher">The cipher.</param>
    /// <param name="permissions">The permissions stored in the envelope.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted (crypt filter ciphers only).</param>
    /// <param name="recipients">The certificates to encrypt for.</param>
    /// <returns>The file.</returns>
    internal static byte[] Encrypted(PublicKeyCipher cipher, int permissions, bool encryptMetadata, params X509Certificate2[] recipients)
    {
        var envelopes = new byte[recipients.Length][];
        for (var i = 0; i < recipients.Length; i++)
        {
            envelopes[i] = Envelope(permissions, recipients[i]);
        }

        return FromEnvelopes(cipher, encryptMetadata, envelopes);
    }

    /// <summary>Builds an encrypted document from ready recipient lists, each holding the 0x5A seed.</summary>
    /// <param name="cipher">The cipher.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted (crypt filter ciphers only).</param>
    /// <param name="envelopes">The CMS recipient lists, in order.</param>
    /// <returns>The file.</returns>
    internal static byte[] FromEnvelopes(PublicKeyCipher cipher, bool encryptMetadata, params byte[][] envelopes)
    {
        var key = FileKey(envelopes, cipher, encryptMetadata);
        var data = EncryptContent(cipher, key, Encoding.ASCII.GetBytes(Content));
        return Build(data, EncryptDictionary(cipher, envelopes, encryptMetadata));
    }

    /// <summary>Builds a document whose /Encrypt dictionary is given as text.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <returns>The file.</returns>
    internal static byte[] WithDictionary(string encrypt) => Build(Encoding.ASCII.GetBytes(Content), encrypt);

    /// <summary>Writes the /Encrypt dictionary for a cipher.</summary>
    /// <param name="cipher">The cipher.</param>
    /// <param name="envelopes">The recipients.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <returns>The dictionary.</returns>
    internal static string EncryptDictionary(PublicKeyCipher cipher, byte[][] envelopes, bool encryptMetadata)
    {
        var list = new StringBuilder();
        foreach (var envelope in envelopes)
        {
            _ = list.Append('<').Append(Convert.ToHexString(envelope)).Append("> ");
        }

        var metadata = encryptMetadata ? string.Empty : " /EncryptMetadata false";
        const string Filters = "/StmF /DefaultCryptFilter /StrF /DefaultCryptFilter";
        return cipher switch
        {
            PublicKeyCipher.Rc4 => $"<< /Filter /Adobe.PubSec /SubFilter /adbe.pkcs7.s4 /V 2 /Length 128 /Recipients [{list}] >>",
            PublicKeyCipher.Aes128 =>
                $"<< /Filter /Adobe.PubSec /SubFilter /adbe.pkcs7.s5 /V 4 /CF << /DefaultCryptFilter << /CFM /AESV2 /Length 128 /Recipients [{list}]{metadata} >> >> {Filters} >>",
            _ => $"<< /Filter /Adobe.PubSec /SubFilter /adbe.pkcs7.s5 /V 5 /CF << /DefaultCryptFilter << /CFM /AESV3 /Length 256 /Recipients [{list}]{metadata} >> >> {Filters} >>",
        };
    }

    /// <summary>Makes a recipient list with <see cref="EnvelopedCms"/>: the seed and permissions enveloped for an RSA certificate.</summary>
    /// <param name="permissions">The permissions.</param>
    /// <param name="certificate">The recipient.</param>
    /// <returns>The EnvelopedData DER.</returns>
    internal static byte[] Envelope(int permissions, X509Certificate2 certificate)
    {
        var content = new byte[SeedLength + PermissionsLength];
        content.AsSpan(0, SeedLength).Fill(SeedByte);
        BinaryPrimitives.WriteInt32BigEndian(content.AsSpan(SeedLength), permissions);
        var envelope = new EnvelopedCms(new ContentInfo(content));
        envelope.Encrypt(new CmsRecipient(certificate));
        return envelope.Encode();
    }

    /// <summary>Works out the file key as ISO 32000-2 7.6.5.3 describes.</summary>
    /// <param name="envelopes">The recipients.</param>
    /// <param name="cipher">The cipher.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <returns>The key.</returns>
    internal static byte[] FileKey(byte[][] envelopes, PublicKeyCipher cipher, bool encryptMetadata)
    {
        var seed = new byte[SeedLength];
        seed.AsSpan().Fill(SeedByte);
        var input = new List<byte>(seed);
        foreach (var envelope in envelopes)
        {
            input.AddRange(envelope);
        }

        if (!encryptMetadata)
        {
            input.AddRange(BitConverter.GetBytes(uint.MaxValue));
        }

        return cipher == PublicKeyCipher.Aes256
            ? SHA256.HashData(input.ToArray())
            : CryptographicOperations.HashData(HashAlgorithmName.SHA1, input.ToArray())[..ShortKeyLength];
    }

    /// <summary>Encrypts the content stream.</summary>
    /// <param name="cipher">The cipher.</param>
    /// <param name="key">The file key.</param>
    /// <param name="plain">The plain bytes.</param>
    /// <returns>The encrypted bytes.</returns>
    private static byte[] EncryptContent(PublicKeyCipher cipher, byte[] key, byte[] plain)
    {
        if (cipher == PublicKeyCipher.Rc4)
        {
            var data = plain.ToArray();
            Rc4.Apply(ObjectKey(key, false), data);
            return data;
        }

        using var aes = Aes.Create();
        aes.Key = cipher == PublicKeyCipher.Aes256 ? key : ObjectKey(key, true);
        var iv = RandomNumberGenerator.GetBytes(ShortKeyLength);
        return [.. iv, .. aes.EncryptCbc(plain, iv)];
    }

    /// <summary>Works out the content stream's object key (algorithm 1).</summary>
    /// <param name="key">The file key.</param>
    /// <param name="aes">Whether the key is for AES-128.</param>
    /// <returns>The object key.</returns>
    private static byte[] ObjectKey(byte[] key, bool aes)
    {
        var input = new List<byte>(key);
        for (var i = 0; i < NumberBytes; i++)
        {
            input.Add((byte)(ContentObject >> (i * ByteBits)));
        }

        input.AddRange(new byte[GenerationBytes]);
        if (aes)
        {
            input.AddRange(AesSalt.ToArray());
        }

        var digest = new byte[Md5.HashLength];
        _ = Md5.HashData(input.ToArray(), digest);
        return digest;
    }

    /// <summary>Writes the file: catalog, pages, page, content stream and an optional /Encrypt dictionary.</summary>
    /// <param name="content">The content stream's bytes as stored.</param>
    /// <param name="encrypt">The /Encrypt dictionary, or an empty string.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(byte[] content, string encrypt)
    {
        var latin1 = Encoding.Latin1;
        List<string> objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R >>",
            string.Create(CultureInfo.InvariantCulture, $"<< /Length {content.Length} >>\nstream\n{latin1.GetString(content)}\nendstream"),
        ];
        if (encrypt.Length > 0)
        {
            objects.Add(encrypt);
        }

        var output = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(latin1.GetByteCount(output.ToString()));
            _ = output.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = latin1.GetByteCount(output.ToString());
        _ = output.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            _ = output.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        var encryptEntry = encrypt.Length > 0 ? string.Create(CultureInfo.InvariantCulture, $" /Encrypt {objects.Count} 0 R") : string.Empty;
        const string Id = "<00112233445566778899AABBCCDDEEFF>";
        _ = output.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R{encryptEntry} /ID [{Id} {Id}] >>\nstartxref\n{xref}\n%%EOF\n");
        return latin1.GetBytes(output.ToString());
    }
}
