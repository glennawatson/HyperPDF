// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HyperPdf.Cms;
using HyperPdfLibrary.Compat;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Builds a PDF MAC token (ISO/TS 32004:2024 6): a CMS AuthenticatedData with one PasswordRecipientInfo, the MAC key
/// wrapped with AES-256 under a key derived from the file encryption key, HMAC with SHA-256, the three authenticated
/// attributes, and no unauthenticated attributes. A token's length depends only on its digest and on whether it carries a
/// signature digest, so the space for it can be set aside before it is computed.
/// </summary>
internal static class PdfMacTokenWriter
{
    /// <summary>The context-specific tag number of the PasswordRecipientInfo.</summary>
    private const int RecipientTagNumber = 3;

    /// <summary>The identifier octet of the constructed <c>[2]</c> authenticated attributes.</summary>
    private const byte AuthenticatedAttributesTagByte = 0xA2;

    /// <summary>Gets the tag of the <c>[3]</c> PasswordRecipientInfo.</summary>
    private static Asn1Tag RecipientTag { get; } = new(TagClass.ContextSpecific, RecipientTagNumber, true);

    /// <summary>Creates a token.</summary>
    /// <param name="fileKey">The file encryption key.</param>
    /// <param name="salt">The 32-byte <c>/KDFSalt</c>.</param>
    /// <param name="digest">The digest of the token; one of ISO/TS 32004 Table 8.</param>
    /// <param name="dataDigest">The digest of the document bytes the token covers.</param>
    /// <param name="signatureDigest">The digest of the signature value for a token attached to a signature, or an empty span.</param>
    /// <returns>The DER token.</returns>
    /// <exception cref="ArgumentException">The salt is not 32 bytes, the digest is not allowed, or a digest has the wrong length.</exception>
    /// <exception cref="CryptographicException">The computer's cryptography library cannot make the token.</exception>
    internal static byte[] Create(ReadOnlySpan<byte> fileKey, ReadOnlySpan<byte> salt, CmsDigestAlgorithm digest, ReadOnlySpan<byte> dataDigest, ReadOnlySpan<byte> signatureDigest)
    {
        var length = CmsDigests.Length(digest);
        if (salt.Length != PdfMacKeys.SaltLength || length == 0 || dataDigest.Length != length || (!signatureDigest.IsEmpty && signatureDigest.Length != length))
        {
            throw new ArgumentException("The salt or a digest has the wrong length.");
        }

        Span<byte> macKey = stackalloc byte[PdfMacKeys.MacKeyLength];
        RandomNumberGenerator.Fill(macKey);
        try
        {
            return Create(fileKey, salt, macKey, digest, dataDigest, signatureDigest);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(macKey);
        }
    }

    /// <summary>Creates the token of an unsigned revision with SHA-256, the digest the writers use.</summary>
    /// <param name="fileKey">The file encryption key.</param>
    /// <param name="salt">The 32-byte <c>/KDFSalt</c>.</param>
    /// <param name="dataDigest">The SHA-256 digest of the document bytes the token covers.</param>
    /// <returns>The DER token.</returns>
    /// <exception cref="ArgumentException">The salt is not 32 bytes or the digest is not 32 bytes.</exception>
    /// <exception cref="CryptographicException">The computer's cryptography library cannot make the token.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte[] CreateStandalone(ReadOnlySpan<byte> fileKey, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> dataDigest) =>
        Create(fileKey, salt, CmsDigestAlgorithm.Sha256, dataDigest, default);

    /// <summary>Gets the length of the tokens made for a digest, so a writer can set aside the space before the digests are known.</summary>
    /// <param name="digest">The digest.</param>
    /// <param name="withSignatureDigest">Whether the token carries a <c>signatureDigest</c>.</param>
    /// <returns>The length in bytes.</returns>
    /// <exception cref="ArgumentException">The digest is not allowed.</exception>
    internal static int Length(CmsDigestAlgorithm digest, bool withSignatureDigest)
    {
        var length = CmsDigests.Length(digest);
        Span<byte> data = stackalloc byte[CmsDigests.MaxLength];
        Span<byte> keys = stackalloc byte[PdfMacKeys.SaltLength];
        return Create(keys, keys, keys, digest, data[..length], withSignatureDigest ? data[..length] : default).Length;
    }

    /// <summary>Creates a token with a chosen MAC key.</summary>
    /// <param name="fileKey">The file encryption key.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="macKey">The MAC key.</param>
    /// <param name="digest">The digest.</param>
    /// <param name="dataDigest">The data digest.</param>
    /// <param name="signatureDigest">The signature digest, or an empty span.</param>
    /// <returns>The DER token.</returns>
    /// <exception cref="CryptographicException">The digest is not available on this computer, or the MAC key could not be wrapped.</exception>
    private static byte[] Create(
        ReadOnlySpan<byte> fileKey,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> macKey,
        CmsDigestAlgorithm digest,
        ReadOnlySpan<byte> dataDigest,
        ReadOnlySpan<byte> signatureDigest)
    {
        var content = WriteIntegrityInfo(dataDigest, signatureDigest);
        Span<byte> hash = stackalloc byte[CmsDigests.MaxLength];
        var hashLength = CmsDigests.Hash(digest, content, hash);
        if (hashLength == 0)
        {
            throw new CryptographicException("The digest is not available on this computer.");
        }

        var wrapped = Wrap(fileKey, salt, macKey);
        var digestOid = CmsDigests.Oid(digest);
        var setOf = WriteAttributes(Asn1Tag.SetOf, digestOid, hash[..hashLength]);
        var mac = HMACSHA256.HashData(macKey, setOf);
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(PdfMacOids.AuthDataText);
            using (writer.PushSequence(CmsDer.Constructed0))
            {
                WriteAuthenticatedData(writer, wrapped, digestOid, content, setOf, mac);
            }
        }

        return writer.Encode();
    }

    /// <summary>Writes the AuthenticatedData (RFC 5652 9.1).</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="wrapped">The wrapped MAC key.</param>
    /// <param name="digestOid">The digest's dotted object identifier.</param>
    /// <param name="content">The encapsulated PdfMacIntegrityInfo.</param>
    /// <param name="setOf">The attributes encoded as a universal SET OF; the same attributes are written with the <c>[2]</c> tag.</param>
    /// <param name="mac">The MAC.</param>
    private static void WriteAuthenticatedData(AsnWriter writer, ReadOnlySpan<byte> wrapped, string digestOid, ReadOnlySpan<byte> content, ReadOnlySpan<byte> setOf, ReadOnlySpan<byte> mac)
    {
        using (writer.PushSequence())
        {
            writer.WriteInteger(0);
            WriteRecipient(writer, wrapped);
            WriteHmacIdentifier(writer, Asn1Tag.Sequence);
            using (writer.PushSequence(CmsDer.Constructed1))
            {
                writer.WriteObjectIdentifier(digestOid);
            }

            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(PdfMacOids.IntegrityInfoText);
                using (writer.PushSequence(CmsDer.Constructed0))
                {
                    writer.WriteOctetString(content);
                }
            }

            // The attributes in the token carry the [2] tag; the MAC was computed over the same bytes with a SET OF tag.
            var tagged = setOf.ToArray();
            tagged[0] = AuthenticatedAttributesTagByte;
            writer.WriteEncodedValue(tagged);
            writer.WriteOctetString(mac);
        }
    }

    /// <summary>Writes the one PasswordRecipientInfo (RFC 5652 6.2.4) with the pdfMacWrapKdf and AES-256 key wrap.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="wrapped">The wrapped MAC key.</param>
    private static void WriteRecipient(AsnWriter writer, ReadOnlySpan<byte> wrapped)
    {
        using (writer.PushSetOf())
        {
            using (writer.PushSequence(RecipientTag))
            {
                writer.WriteInteger(0);
                using (writer.PushSequence(CmsDer.Constructed0))
                {
                    writer.WriteObjectIdentifier(PdfMacOids.WrapKdfText);
                }

                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier(PdfMacOids.Aes256WrapText);
                }

                writer.WriteOctetString(wrapped);
            }
        }
    }

    /// <summary>Writes the HMAC with SHA-256 algorithm identifier, with NULL parameters as RFC 8018 B.1.2 encodes it.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="tag">The identifier's tag.</param>
    private static void WriteHmacIdentifier(AsnWriter writer, Asn1Tag tag)
    {
        using (writer.PushSequence(tag))
        {
            writer.WriteObjectIdentifier(PdfMacOids.HmacSha256Text);
            writer.WriteNull();
        }
    }

    /// <summary>Writes the PdfMacIntegrityInfo (ISO/TS 32004 6.2).</summary>
    /// <param name="dataDigest">The data digest.</param>
    /// <param name="signatureDigest">The signature digest, or an empty span.</param>
    /// <returns>The DER.</returns>
    private static byte[] WriteIntegrityInfo(ReadOnlySpan<byte> dataDigest, ReadOnlySpan<byte> signatureDigest)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(0);
            writer.WriteOctetString(dataDigest);
            if (!signatureDigest.IsEmpty)
            {
                writer.WriteOctetString(signatureDigest, CmsDer.Primitive0);
            }
        }

        return writer.Encode();
    }

    /// <summary>Writes the content type, message digest and algorithm protection attributes (ISO/TS 32004 6.3.6).</summary>
    /// <param name="tag">The set's tag.</param>
    /// <param name="digestOid">The digest's dotted object identifier.</param>
    /// <param name="messageDigest">The digest of the encapsulated content.</param>
    /// <returns>The DER.</returns>
    private static byte[] WriteAttributes(Asn1Tag tag, string digestOid, ReadOnlySpan<byte> messageDigest)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSetOf(tag))
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(PdfMacOids.ContentTypeText);
                using (writer.PushSetOf())
                {
                    writer.WriteObjectIdentifier(PdfMacOids.IntegrityInfoText);
                }
            }

            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(PdfMacOids.MessageDigestText);
                using (writer.PushSetOf())
                {
                    writer.WriteOctetString(messageDigest);
                }
            }

            WriteProtection(writer, digestOid);
        }

        return writer.Encode();
    }

    /// <summary>Writes the algorithm protection attribute (RFC 6211 3): the digest and MAC algorithms of the AuthenticatedData.</summary>
    /// <param name="writer">The writer, inside the attribute set.</param>
    /// <param name="digestOid">The digest's dotted object identifier.</param>
    private static void WriteProtection(AsnWriter writer, string digestOid)
    {
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(PdfMacOids.AlgorithmProtectionText);
            using (writer.PushSetOf())
            {
                using (writer.PushSequence())
                {
                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier(digestOid);
                    }

                    WriteHmacIdentifier(writer, CmsDer.Constructed2);
                }
            }
        }
    }

    /// <summary>Derives the key-encryption key and wraps the MAC key.</summary>
    /// <param name="fileKey">The file encryption key.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="macKey">The MAC key.</param>
    /// <returns>The wrapped key.</returns>
    /// <exception cref="CryptographicException">The key could not be wrapped.</exception>
    private static byte[] Wrap(ReadOnlySpan<byte> fileKey, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> macKey)
    {
        Span<byte> kek = stackalloc byte[PdfMacKeys.KeyEncryptionKeyLength];
        try
        {
            PdfMacKeys.DeriveKeyEncryptionKey(fileKey, salt, kek);
            using var aes = Aes.Create();
            aes.SetKey(kek);
            var wrapped = new byte[macKey.Length + AesKeyWrapping.BlockLength];
            return AesKeyWrapping.TryWrap(aes, macKey, wrapped) ? wrapped : throw new CryptographicException("The MAC key could not be wrapped.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }
}
