// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Security.Cryptography;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>
/// Builds a PDF MAC token from ISO/TS 32004:2024 clause 6 and RFC 5869 (HKDF), RFC 3394 (key wrap) and RFC 5652 9 (CMS
/// AuthenticatedData), with its own HKDF, key wrap and DER, so tests do not share the library's code.
/// </summary>
internal static class MacToken
{
    /// <summary>The object identifier of id-ct-authData.</summary>
    internal const string AuthDataOid = "1.2.840.113549.1.9.16.1.2";

    /// <summary>The object identifier of id-ct-pdfMacIntegrityInfo (6.2).</summary>
    internal const string IntegrityInfoOid = "1.0.32004.1.0";

    /// <summary>The object identifier of id-kdf-pdfMacWrapKdf (6.4).</summary>
    internal const string WrapKdfOid = "1.0.32004.1.1";

    /// <summary>The object identifier of id-attr-pdfMacData (6.5.2).</summary>
    internal const string MacDataOid = "1.0.32004.1.2";

    /// <summary>The object identifier of AES-256 key wrap without padding (6.3.3, Table 7).</summary>
    internal const string Aes256WrapOid = "2.16.840.1.101.3.4.1.45";

    /// <summary>The object identifier of HMAC with SHA-256 (6.3.5, Table 9).</summary>
    internal const string HmacSha256Oid = "1.2.840.113549.2.9";

    /// <summary>The object identifier of id-contentType (RFC 5652 11.1).</summary>
    internal const string ContentTypeOid = "1.2.840.113549.1.9.3";

    /// <summary>The object identifier of id-messageDigest (RFC 5652 11.2).</summary>
    internal const string MessageDigestOid = "1.2.840.113549.1.9.4";

    /// <summary>The object identifier of id-aa-CMSAlgorithmProtection (RFC 8933).</summary>
    internal const string AlgorithmProtectionOid = "1.2.840.113549.1.9.52";

    /// <summary>The length of the key-encryption key.</summary>
    internal const int KeyLength = 32;

    /// <summary>The ordinary digest name.</summary>
    internal const string Sha256 = "SHA-256";

    /// <summary>The number of rounds of RFC 3394 key wrap.</summary>
    private const int WrapRounds = 6;

    /// <summary>The length of a key wrap half block.</summary>
    private const int HalfBlock = 8;

    /// <summary>The byte every half block of the RFC 3394 2.2.3.1 initial value repeats.</summary>
    private const byte InitialByte = 0xA6;

    /// <summary>The MAC key length of HMAC with SHA-256.</summary>
    private const int MacKeyLength = 32;

    /// <summary>The seed of the MAC key, fixed so the tests are repeatable.</summary>
    private const byte MacKeySeed = 0x90;

    /// <summary>The identifier octet of the constructed <c>[2]</c> attributes in the token.</summary>
    private const byte StoredAttributesTag = 0xA2;

    /// <summary>The context tag number of the explicit content and the KDF identifier.</summary>
    private const int FirstContext = 0;

    /// <summary>The context tag number of the digest algorithm.</summary>
    private const int DigestContext = 1;

    /// <summary>The context tag number of the MAC algorithm in the protection attribute.</summary>
    private const int MacContext = 2;

    /// <summary>The context tag number of the password recipient.</summary>
    private const int RecipientContext = 3;

    /// <summary>Gets the MAC key every test token uses.</summary>
    internal static byte[] MacKey { get; } = [.. Enumerable.Range(0, MacKeyLength).Select(static i => (byte)(MacKeySeed + i))];

    /// <summary>Gets the dotted object identifier of a digest.</summary>
    /// <param name="digest">The digest name: SHA-256, SHA-384, SHA-512, SHA3-256, SHA3-384 or SHA3-512.</param>
    /// <returns>The identifier from NIST CSOR.</returns>
    /// <exception cref="ArgumentException">The digest is not one of Table 8.</exception>
    internal static string AlgorithmIdentifier(string digest) => digest switch
    {
        Sha256 => "2.16.840.1.101.3.4.2.1",
        "SHA-384" => "2.16.840.1.101.3.4.2.2",
        "SHA-512" => "2.16.840.1.101.3.4.2.3",
        "SHA3-256" => "2.16.840.1.101.3.4.2.8",
        "SHA3-384" => "2.16.840.1.101.3.4.2.9",
        "SHA3-512" => "2.16.840.1.101.3.4.2.10",
        _ => throw new ArgumentException("Unknown digest.", nameof(digest)),
    };

    /// <summary>Gets the .NET name of a digest.</summary>
    /// <param name="digest">The digest name.</param>
    /// <returns>The hash algorithm name.</returns>
    /// <exception cref="ArgumentException">The digest is not one of Table 8.</exception>
    internal static HashAlgorithmName HashName(string digest) => digest switch
    {
        Sha256 => HashAlgorithmName.SHA256,
        "SHA-384" => HashAlgorithmName.SHA384,
        "SHA-512" => HashAlgorithmName.SHA512,
        "SHA3-256" => HashAlgorithmName.SHA3_256,
        "SHA3-384" => HashAlgorithmName.SHA3_384,
        "SHA3-512" => HashAlgorithmName.SHA3_512,
        _ => throw new ArgumentException("Unknown digest.", nameof(digest)),
    };

    /// <summary>Digests data.</summary>
    /// <param name="digest">The digest name.</param>
    /// <param name="data">The data.</param>
    /// <returns>The digest.</returns>
    internal static byte[] Hash(string digest, byte[] data) => CryptographicOperations.HashData(HashName(digest), data);

    /// <summary>HKDF (RFC 5869) with SHA-256: extract, then expand.</summary>
    /// <param name="inputKey">The input key material.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="info">The info.</param>
    /// <param name="length">The output length.</param>
    /// <returns>The output key material.</returns>
    internal static byte[] Hkdf(byte[] inputKey, byte[] salt, byte[] info, int length)
    {
        var pseudoRandomKey = HMACSHA256.HashData(salt, inputKey);
        List<byte> output = [];
        byte[] previous = [];
        for (byte counter = 1; output.Count < length; counter++)
        {
            byte[] block = [.. previous, .. info, counter];
            previous = HMACSHA256.HashData(pseudoRandomKey, block);
            output.AddRange(previous);
        }

        return [.. output.Take(length)];
    }

    /// <summary>Wraps a key as RFC 3394 2.2.1 describes.</summary>
    /// <param name="kek">The key-encryption key.</param>
    /// <param name="key">The key to wrap, a whole number of 64-bit blocks.</param>
    /// <returns>The wrapped key, 8 bytes longer.</returns>
    internal static byte[] Wrap(byte[] kek, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = kek;
        var blocks = key.Length / HalfBlock;
        var a = new byte[HalfBlock];
        Array.Fill(a, InitialByte);
        var r = Enumerable.Range(0, blocks).Select(i => key.AsSpan(i * HalfBlock, HalfBlock).ToArray()).ToArray();
        for (var j = 0; j < WrapRounds; j++)
        {
            for (var i = 1; i <= blocks; i++)
            {
                var b = aes.EncryptEcb([.. a, .. r[i - 1]], PaddingMode.None);
                a = b[..HalfBlock];
                var t = new byte[HalfBlock];
                BinaryPrimitives.WriteUInt64BigEndian(t, (ulong)((blocks * j) + i));
                for (var k = 0; k < HalfBlock; k++)
                {
                    a[k] ^= t[k];
                }

                r[i - 1] = b[HalfBlock..];
            }
        }

        return [.. a, .. r.SelectMany(static block => block)];
    }

    /// <summary>Builds a token.</summary>
    /// <param name="fileKey">The file encryption key.</param>
    /// <param name="salt">The 32-byte salt.</param>
    /// <param name="digest">The digest name.</param>
    /// <param name="dataDigest">The digest of the document bytes.</param>
    /// <param name="signatureDigest">The digest of the signature value, or <see langword="null"/> for an unsigned revision.</param>
    /// <returns>The DER token.</returns>
    internal static byte[] Build(byte[] fileKey, byte[] salt, string digest, byte[] dataDigest, byte[]? signatureDigest)
    {
        var wrapped = Wrap(Hkdf(fileKey, salt, "PDFMAC"u8.ToArray(), KeyLength), MacKey);
        var content = MacDer.Sequence(
            MacDer.Integer(0),
            MacDer.Octets(dataDigest),
            signatureDigest is null ? [] : MacDer.Context(FirstContext, false, signatureDigest));
        var algorithm = MacDer.Oid(AlgorithmIdentifier(digest));
        var protection = MacDer.Sequence(MacDer.Sequence(algorithm), MacDer.Context(MacContext, true, MacDer.Oid(HmacSha256Oid), MacDer.Null()));
        var attributes = MacDer.SetOf(
            Attribute(ContentTypeOid, MacDer.Oid(IntegrityInfoOid)),
            Attribute(MessageDigestOid, MacDer.Octets(Hash(digest, content))),
            Attribute(AlgorithmProtectionOid, protection));

        // RFC 5652 9.2: the MAC is over the DER of the attributes with a SET OF tag; they are stored with the [2] tag.
        var mac = HMACSHA256.HashData(MacKey, attributes);
        var stored = attributes.ToArray();
        stored[0] = StoredAttributesTag;
        var recipient = MacDer.Context(
            RecipientContext,
            true,
            MacDer.Integer(0),
            MacDer.Context(FirstContext, true, MacDer.Oid(WrapKdfOid)),
            MacDer.Sequence(MacDer.Oid(Aes256WrapOid)),
            MacDer.Octets(wrapped));
        var authenticated = MacDer.Sequence(
            MacDer.Integer(0),
            MacDer.SetOf(recipient),
            MacDer.Sequence(MacDer.Oid(HmacSha256Oid), MacDer.Null()),
            MacDer.Context(DigestContext, true, algorithm),
            MacDer.Sequence(MacDer.Oid(IntegrityInfoOid), MacDer.Context(FirstContext, true, MacDer.Octets(content))),
            stored,
            MacDer.Octets(mac));
        return MacDer.Sequence(MacDer.Oid(AuthDataOid), MacDer.Context(FirstContext, true, authenticated));
    }

    /// <summary>Encodes one attribute with one value.</summary>
    /// <param name="oid">The attribute type.</param>
    /// <param name="value">The encoded value.</param>
    /// <returns>The attribute.</returns>
    private static byte[] Attribute(string oid, byte[] value) => MacDer.Sequence(MacDer.Oid(oid), MacDer.SetOf(value));
}
