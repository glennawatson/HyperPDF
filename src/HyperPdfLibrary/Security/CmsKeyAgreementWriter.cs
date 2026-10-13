// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdfLibrary.Compat;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Writes CMS key agreement recipients (RFC 5652 6.2.2) for elliptic curve certificates, the writing side of
/// <see cref="CmsKeyAgreement"/>: ephemeral-static ECDH (RFC 5753 3.1) with the ANSI X9.63 KDF over ECC-CMS-SharedInfo
/// (RFC 5753 7.2), and the content-encryption key wrapped with AES-256 key wrap (RFC 3394).
/// </summary>
internal static class CmsKeyAgreementWriter
{
    /// <summary>The version of a KeyAgreeRecipientInfo.</summary>
    private const int KeyAgreementVersion = 3;

    /// <summary>The first byte of an uncompressed elliptic curve point (SEC 1 2.3.3).</summary>
    private const byte UncompressedPoint = 0x04;

    /// <summary>The bytes of the X9.63 KDF counter and of the SharedInfo key length.</summary>
    private const int CounterLength = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The curve size, in bits, up to which SHA-256 is used for the key derivation.</summary>
    private const int Sha256Curves = 256;

    /// <summary>The curve size, in bits, up to which SHA-384 is used for the key derivation.</summary>
    private const int Sha384Curves = 384;

    /// <summary>Writes a KeyAgreeRecipientInfo.</summary>
    /// <param name="writer">The DER writer, inside the RecipientInfos set.</param>
    /// <param name="certificate">The recipient's certificate.</param>
    /// <param name="contentKey">The 32-byte content-encryption key.</param>
    /// <exception cref="PdfException">The certificate has no usable elliptic curve key, or its curve is not a named curve.</exception>
    internal static void Write(AsnWriter writer, X509Certificate2 certificate, ReadOnlySpan<byte> contentKey)
    {
        using var recipientKey = certificate.GetECDiffieHellmanPublicKey() ?? throw Unusable(certificate, "has no elliptic curve public key");
        using var peer = recipientKey.PublicKey;
        var recipient = recipientKey.ExportParameters(false);
        var curveOid = recipient.Curve.Oid?.Value;
        if (curveOid is null || !recipient.Curve.IsNamed)
        {
            throw Unusable(certificate, "uses a curve that is not a named curve");
        }

        using var ephemeral = ECDiffieHellman.Create(recipient.Curve);
        var point = EncodePoint(ephemeral.ExportParameters(false).Q);
        var scheme = SchemeFor(recipient.Q.X!.Length * ByteBits);
        var wrapAlgorithm = EncodeWrapAlgorithm();
        var wrapped = Wrap(ephemeral, peer, scheme, wrapAlgorithm, contentKey);
        using (writer.PushSequence(CmsDer.Constructed1))
        {
            writer.WriteInteger(KeyAgreementVersion);
            WriteOriginator(writer, curveOid, point);
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(scheme.Oid);
                writer.WriteEncodedValue(wrapAlgorithm);
            }

            using (writer.PushSequence())
            using (writer.PushSequence())
            {
                CmsRecipientWriting.WriteIssuerAndSerial(writer, certificate);
                writer.WriteOctetString(wrapped);
            }
        }
    }

    /// <summary>Picks the key derivation scheme by the curve size: SHA-256 up to 256 bits, SHA-384 up to 384 and SHA-512 above.</summary>
    /// <param name="curveBits">The curve's field size rounded up to whole bytes, in bits.</param>
    /// <returns>The scheme.</returns>
    private static DerivationScheme SchemeFor(int curveBits) => curveBits switch
    {
        <= Sha256Curves => new(CmsWriteOids.StandardDhSha256, HashAlgorithmName.SHA256),
        <= Sha384Curves => new(CmsWriteOids.StandardDhSha384, HashAlgorithmName.SHA384),
        _ => new(CmsWriteOids.StandardDhSha512, HashAlgorithmName.SHA512),
    };

    /// <summary>Encodes an uncompressed point: 0x04, then the X and Y coordinates.</summary>
    /// <param name="point">The point.</param>
    /// <returns>The encoded point.</returns>
    private static byte[] EncodePoint(ECPoint point)
    {
        var x = point.X!;
        var y = point.Y!;
        var encoded = new byte[1 + x.Length + y.Length];
        encoded[0] = UncompressedPoint;
        x.CopyTo(encoded, 1);
        y.CopyTo(encoded, 1 + x.Length);
        return encoded;
    }

    /// <summary>Encodes the key wrap AlgorithmIdentifier: AES-256 key wrap, with no parameters (RFC 3565 2.3.2).</summary>
    /// <returns>The DER.</returns>
    private static byte[] EncodeWrapAlgorithm()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(CmsWriteOids.Aes256Wrap);
        }

        return writer.Encode();
    }

    /// <summary>Writes the originator: <c>[0] EXPLICIT</c> an <c>[1] IMPLICIT</c> OriginatorPublicKey holding the ephemeral key (RFC 5753 3.1.1).</summary>
    /// <param name="writer">The DER writer.</param>
    /// <param name="curveOid">The named curve's dotted OID.</param>
    /// <param name="point">The ephemeral key's uncompressed point.</param>
    private static void WriteOriginator(AsnWriter writer, string curveOid, ReadOnlySpan<byte> point)
    {
        using (writer.PushSequence(CmsDer.Constructed0))
        using (writer.PushSequence(CmsDer.Constructed1))
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(CmsWriteOids.EcPublicKey);
                writer.WriteObjectIdentifier(curveOid);
            }

            writer.WriteBitString(point);
        }
    }

    /// <summary>Derives the key-encryption key from the agreement and wraps the content-encryption key with it.</summary>
    /// <param name="ephemeral">The originator's fresh key pair.</param>
    /// <param name="peer">The recipient's public key.</param>
    /// <param name="scheme">The key derivation scheme.</param>
    /// <param name="wrapAlgorithm">The key wrap AlgorithmIdentifier as written in the recipient.</param>
    /// <param name="contentKey">The content-encryption key.</param>
    /// <returns>The wrapped key, 8 bytes longer than the content-encryption key.</returns>
    /// <exception cref="PdfException">The content key cannot be wrapped.</exception>
    private static byte[] Wrap(ECDiffieHellman ephemeral, ECDiffieHellmanPublicKey peer, DerivationScheme scheme, ReadOnlySpan<byte> wrapAlgorithm, ReadOnlySpan<byte> contentKey)
    {
        Span<byte> kek = stackalloc byte[CmsAlgorithms.Aes256KeyLength];
        try
        {
            DeriveKeyEncryptionKey(ephemeral, peer, scheme.Hash, SharedInfo(wrapAlgorithm, kek.Length), kek);
            using var aes = Aes.Create();
            aes.SetKey(kek);
            var wrapped = new byte[contentKey.Length + AesKeyWrapping.BlockLength];
            return AesKeyWrapping.TryWrap(aes, contentKey, wrapped) ? wrapped : throw new PdfException(PdfError.Security, "The content key could not be wrapped.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    /// <summary>
    /// Runs the ANSI X9.63 KDF (RFC 5753 7.2): hashes of the shared secret, a big-endian counter from 1 and the SharedInfo,
    /// joined until the key is long enough. The provider hashes the secret, so it never leaves the key.
    /// </summary>
    /// <param name="ephemeral">The originator's key pair.</param>
    /// <param name="peer">The recipient's public key.</param>
    /// <param name="hash">The KDF hash.</param>
    /// <param name="sharedInfo">The counter space followed by the SharedInfo DER; its first four bytes take the counter.</param>
    /// <param name="key">Receives the key.</param>
    private static void DeriveKeyEncryptionKey(ECDiffieHellman ephemeral, ECDiffieHellmanPublicKey peer, HashAlgorithmName hash, byte[] sharedInfo, Span<byte> key)
    {
        var counter = 1;
        var written = 0;
        while (written < key.Length)
        {
            BinaryPrimitives.WriteInt32BigEndian(sharedInfo, counter);
            var block = ephemeral.DeriveKeyFromHash(peer, hash, null, sharedInfo);
            var count = Math.Min(block.Length, key.Length - written);
            block.AsSpan(0, count).CopyTo(key[written..]);
            CryptographicOperations.ZeroMemory(block);
            written += count;
            counter++;
        }
    }

    /// <summary>
    /// Encodes ECC-CMS-SharedInfo (RFC 5753 7.2): the key wrap algorithm and the key-encryption key's length in bits, after
    /// four bytes left for the KDF counter. No user keying material is used, because the ephemeral key is fresh.
    /// </summary>
    /// <param name="wrapAlgorithm">The encoded key wrap AlgorithmIdentifier.</param>
    /// <param name="wrapLength">The key-encryption key length in bytes.</param>
    /// <returns>The counter space followed by the SharedInfo DER.</returns>
    private static byte[] SharedInfo(ReadOnlySpan<byte> wrapAlgorithm, int wrapLength)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteEncodedValue(wrapAlgorithm);
            Span<byte> bits = stackalloc byte[CounterLength];
            BinaryPrimitives.WriteInt32BigEndian(bits, wrapLength * ByteBits);
            using (writer.PushSequence(CmsDer.Constructed2))
            {
                writer.WriteOctetString(bits);
            }
        }

        var buffer = new byte[CounterLength + writer.GetEncodedLength()];
        _ = writer.Encode(buffer.AsSpan(CounterLength));
        return buffer;
    }

    /// <summary>Makes the exception for a certificate that cannot receive a key agreement recipient.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <param name="reason">Why, finishing the sentence "The certificate for X ...".</param>
    /// <returns>The exception.</returns>
    private static PdfException Unusable(X509Certificate2 certificate, string reason) =>
        new(PdfError.Certificate, $"The certificate for {certificate.Subject} {reason}, so a file cannot be encrypted for it.");

    /// <summary>The key derivation scheme of a recipient.</summary>
    /// <param name="Oid">The dotted OID of the key agreement scheme.</param>
    /// <param name="Hash">The hash its KDF uses.</param>
    private readonly record struct DerivationScheme(string Oid, HashAlgorithmName Hash);
}
