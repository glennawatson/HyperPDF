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
/// Opens CMS key agreement recipients (RFC 5652 6.2.2) that use ephemeral-static ECDH (RFC 5753 3.1): the shared secret
/// from the originator's ephemeral key and the certificate's private key, the X9.63 KDF over ECC-CMS-SharedInfo, the
/// AES key unwrap and the AES-CBC content. The private key stays in its provider; only derived key material leaves it.
/// </summary>
internal static class CmsKeyAgreement
{
    /// <summary>The first byte of an uncompressed elliptic curve point (SEC 1 2.3.3).</summary>
    private const byte UncompressedPoint = 0x04;

    /// <summary>The bytes of the X9.63 KDF counter and of the SharedInfo key length.</summary>
    private const int CounterLength = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The coordinates in an uncompressed point.</summary>
    private const int Coordinates = 2;

    /// <summary>Opens a KeyAgreeRecipientInfo when one of its recipient encrypted keys names the certificate.</summary>
    /// <param name="recipientInfo">The KeyAgreeRecipientInfo's contents.</param>
    /// <param name="encryptedContentInfo">The envelope's EncryptedContentInfo contents.</param>
    /// <param name="identity">The certificate.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="AsnContentException">The recipient info is damaged before a recipient for the certificate is found.</exception>
    internal static CmsRecipientResult Open(ReadOnlySpan<byte> recipientInfo, ReadOnlySpan<byte> encryptedContentInfo, CmsRecipientIdentity identity)
    {
        _ = CmsDer.ReadContents(ref recipientInfo, Asn1Tag.Integer);
        var originator = CmsDer.ReadContents(ref recipientInfo, CmsDer.Constructed0);
        var ukm = CmsDer.StartsWith(recipientInfo, CmsDer.Constructed1) ? ReadUkm(ref recipientInfo) : default;
        var keyDerivation = CmsDer.ReadAlgorithm(ref recipientInfo, out var wrapAlgorithm);
        var keys = CmsDer.ReadContents(ref recipientInfo, Asn1Tag.Sequence);
        if (!TryFindEncryptedKey(keys, identity, out var encryptedKey))
        {
            return CmsRecipientResult.NotForCertificate;
        }

        try
        {
            return OpenMatched(new(originator, ukm, keyDerivation, wrapAlgorithm, encryptedKey, encryptedContentInfo), identity.Certificate);
        }
        catch (Exception ex) when (ex is AsnContentException or CryptographicException or ArgumentException)
        {
            // The recipient names this certificate, so a damaged key, point or content is reported, not skipped.
            return CmsRecipientResult.Damaged;
        }
    }

    /// <summary>Checks the algorithms of a recipient for the certificate, then decrypts it with the private key.</summary>
    /// <param name="recipient">The recipient.</param>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The outcome.</returns>
    private static CmsRecipientResult OpenMatched(CmsKeyAgreeRecipient recipient, X509Certificate2 certificate)
    {
        var scheme = ReadScheme(recipient);
        if (scheme.WrapLength == 0 || !TryReadOriginatorPoint(recipient.Originator, out var point))
        {
            return CmsRecipientResult.Unsupported;
        }

        if (recipient.EncryptedKey.Length != scheme.ContentLength + AesKeyWrapping.BlockLength)
        {
            return CmsRecipientResult.Damaged;
        }

        if (!certificate.HasPrivateKey)
        {
            return CmsRecipientResult.PrivateKeyMissing;
        }

        using var key = PrivateKey(certificate);
        return key is null ? CmsRecipientResult.Unsupported : Agree(key, recipient, point, scheme);
    }

    /// <summary>Agrees the key with the originator's ephemeral key and decrypts the recipient.</summary>
    /// <param name="key">The certificate's private key.</param>
    /// <param name="recipient">The recipient.</param>
    /// <param name="point">The originator's uncompressed point.</param>
    /// <param name="scheme">The recipient's algorithms.</param>
    /// <returns>The outcome.</returns>
    private static CmsRecipientResult Agree(ECDiffieHellman key, CmsKeyAgreeRecipient recipient, ReadOnlySpan<byte> point, in CmsKeyAgreementScheme scheme)
    {
        var parameters = key.ExportParameters(false);
        if (scheme.Cofactor && !CmsAlgorithms.IsCofactorOne(parameters.Curve))
        {
            return CmsRecipientResult.Unsupported;
        }

        using var peer = CreatePeer(parameters, point);
        if (peer is null)
        {
            return CmsRecipientResult.Damaged;
        }

        using var peerKey = peer.PublicKey;
        var sharedInfo = SharedInfo(recipient.WrapAlgorithm, recipient.Ukm, scheme.WrapLength);
        return Decrypt(new(key, peerKey, scheme.Hash, sharedInfo), recipient, scheme);
    }

    /// <summary>Derives the key-encryption key, unwraps the content-encryption key and decrypts the content.</summary>
    /// <param name="agreement">The key agreement inputs.</param>
    /// <param name="recipient">The recipient.</param>
    /// <param name="scheme">The recipient's algorithms.</param>
    /// <returns>The outcome.</returns>
    private static CmsRecipientResult Decrypt(in CmsKeyAgreementInputs agreement, CmsKeyAgreeRecipient recipient, in CmsKeyAgreementScheme scheme)
    {
        Span<byte> kek = stackalloc byte[CmsAlgorithms.Aes256KeyLength];
        Span<byte> cek = stackalloc byte[CmsAlgorithms.Aes256KeyLength];
        kek = kek[..scheme.WrapLength];
        cek = cek[..scheme.ContentLength];
        try
        {
            DeriveKeyEncryptionKey(agreement, kek);
            using var aes = Aes.Create();
            aes.SetKey(kek);
            return AesKeyWrapping.TryUnwrap(aes, recipient.EncryptedKey, cek)
                ? CmsContentDecryption.Decrypt(recipient.EncryptedContentInfo, cek)
                : CmsRecipientResult.Damaged;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
            CryptographicOperations.ZeroMemory(cek);
        }
    }

    /// <summary>
    /// Runs the ANSI X9.63 KDF (RFC 5753 7.2): hashes of the shared secret, a big-endian counter from 1 and the SharedInfo,
    /// joined until the key is long enough. The provider hashes the secret, so it never leaves the key.
    /// </summary>
    /// <param name="agreement">The key agreement inputs; the SharedInfo's first four bytes take the counter.</param>
    /// <param name="key">Receives the key.</param>
    private static void DeriveKeyEncryptionKey(in CmsKeyAgreementInputs agreement, Span<byte> key)
    {
        var counter = 1;
        var written = 0;
        while (written < key.Length)
        {
            BinaryPrimitives.WriteInt32BigEndian(agreement.SharedInfo, counter);
            var block = agreement.PrivateKey.DeriveKeyFromHash(agreement.PeerKey, agreement.Hash, null, agreement.SharedInfo);
            var count = Math.Min(block.Length, key.Length - written);
            block.AsSpan(0, count).CopyTo(key[written..]);
            CryptographicOperations.ZeroMemory(block);
            written += count;
            counter++;
        }
    }

    /// <summary>Reads the key derivation, key wrap and content cipher of a recipient.</summary>
    /// <param name="recipient">The recipient.</param>
    /// <returns>The algorithms, or a default value when any of them is not supported.</returns>
    /// <exception cref="AsnContentException">An algorithm identifier is damaged.</exception>
    private static CmsKeyAgreementScheme ReadScheme(CmsKeyAgreeRecipient recipient)
    {
        var hash = CmsAlgorithms.KeyDerivationHash(recipient.KeyDerivation, out var cofactor);
        var wrapAlgorithm = recipient.WrapAlgorithm;
        var wrapLength = wrapAlgorithm.IsEmpty ? 0 : CmsAlgorithms.AesWrapKeyLength(CmsDer.ReadAlgorithm(ref wrapAlgorithm, out _));
        var contentLength = CmsContentDecryption.KeyLength(recipient.EncryptedContentInfo);
        return hash.Name is null || wrapLength == 0 || contentLength == 0 ? default : new(hash, cofactor, wrapLength, contentLength);
    }

    /// <summary>
    /// Encodes ECC-CMS-SharedInfo (RFC 5753 7.2): the key wrap algorithm, the user keying material when present and the
    /// key-encryption key's length in bits, after four bytes left for the KDF counter.
    /// </summary>
    /// <param name="wrapAlgorithm">The encoded key wrap AlgorithmIdentifier.</param>
    /// <param name="ukm">The user keying material, or an empty span.</param>
    /// <param name="wrapLength">The key-encryption key length in bytes.</param>
    /// <returns>The counter space followed by the SharedInfo DER.</returns>
    private static byte[] SharedInfo(ReadOnlySpan<byte> wrapAlgorithm, ReadOnlySpan<byte> ukm, int wrapLength)
    {
        var writer = new AsnWriter(AsnEncodingRules.BER);
        using (writer.PushSequence())
        {
            writer.WriteEncodedValue(wrapAlgorithm);
            if (!ukm.IsEmpty)
            {
                using (writer.PushSequence(CmsDer.Constructed0))
                {
                    writer.WriteOctetString(ukm);
                }
            }

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

    /// <summary>Finds the recipient encrypted key whose identifier names the certificate.</summary>
    /// <param name="keys">The RecipientEncryptedKeys' contents.</param>
    /// <param name="identity">The certificate.</param>
    /// <param name="encryptedKey">The wrapped key.</param>
    /// <returns><see langword="true"/> when one names the certificate.</returns>
    /// <exception cref="AsnContentException">The keys are damaged.</exception>
    private static bool TryFindEncryptedKey(ReadOnlySpan<byte> keys, CmsRecipientIdentity identity, out ReadOnlySpan<byte> encryptedKey)
    {
        while (!keys.IsEmpty)
        {
            var entry = CmsDer.ReadContents(ref keys, Asn1Tag.Sequence);
            var matches = IdentifierMatches(ref entry, identity);
            encryptedKey = CmsDer.ReadContents(ref entry, Asn1Tag.PrimitiveOctetString);
            if (matches)
            {
                return true;
            }
        }

        encryptedKey = default;
        return false;
    }

    /// <summary>
    /// Reads a KeyAgreeRecipientIdentifier: an IssuerAndSerialNumber, or a <c>[0] IMPLICIT</c> RecipientKeyIdentifier
    /// that starts with the subject key identifier.
    /// </summary>
    /// <param name="source">The source, advanced past the identifier.</param>
    /// <param name="identity">The certificate.</param>
    /// <returns><see langword="true"/> when the identifier names the certificate.</returns>
    /// <exception cref="AsnContentException">The identifier is damaged.</exception>
    private static bool IdentifierMatches(scoped ref ReadOnlySpan<byte> source, CmsRecipientIdentity identity)
    {
        if (CmsDer.StartsWith(source, CmsDer.Constructed0))
        {
            var keyIdentifier = CmsDer.ReadContents(ref source, CmsDer.Constructed0);
            return CmsRecipientMatching.SubjectKeyMatches(CmsDer.ReadContents(ref keyIdentifier, Asn1Tag.PrimitiveOctetString), identity);
        }

        return CmsRecipientMatching.IssuerAndSerialMatches(CmsDer.ReadContents(ref source, Asn1Tag.Sequence), identity);
    }

    /// <summary>Reads the <c>[1] EXPLICIT</c> user keying material.</summary>
    /// <param name="source">The source, advanced past the value.</param>
    /// <returns>The keying material.</returns>
    /// <exception cref="AsnContentException">The value is damaged.</exception>
    private static ReadOnlySpan<byte> ReadUkm(scoped ref ReadOnlySpan<byte> source)
    {
        var wrapper = CmsDer.ReadContents(ref source, CmsDer.Constructed1);
        return CmsDer.ReadContents(ref wrapper, Asn1Tag.PrimitiveOctetString);
    }

    /// <summary>
    /// Reads the originator's ephemeral public key: an <c>[1]</c> OriginatorPublicKey with <c>id-ecPublicKey</c> and an
    /// uncompressed point, as RFC 5753 3.1.1 requires for ephemeral-static ECDH.
    /// </summary>
    /// <param name="originator">The OriginatorIdentifierOrKey.</param>
    /// <param name="point">The point, from its 0x04 byte.</param>
    /// <returns><see langword="true"/> for a supported key.</returns>
    /// <exception cref="AsnContentException">The value is damaged.</exception>
    private static bool TryReadOriginatorPoint(ReadOnlySpan<byte> originator, out ReadOnlySpan<byte> point)
    {
        point = default;
        if (!CmsDer.StartsWith(originator, CmsDer.Constructed1))
        {
            return false;
        }

        var key = CmsDer.ReadContents(ref originator, CmsDer.Constructed1);
        var algorithm = CmsDer.ReadAlgorithm(ref key, out _);
        var bits = CmsDer.ReadContents(ref key, Asn1Tag.PrimitiveBitString);
        if (!algorithm.SequenceEqual(CmsOids.EcPublicKey) || bits is not [0, UncompressedPoint, ..])
        {
            return false;
        }

        point = bits[1..];
        return true;
    }

    /// <summary>Creates the originator's public key on the recipient's curve.</summary>
    /// <param name="recipient">The recipient key's public parameters.</param>
    /// <param name="point">The originator's uncompressed point.</param>
    /// <returns>The key, or <see langword="null"/> when the point does not fit the curve.</returns>
    private static ECDiffieHellman? CreatePeer(ECParameters recipient, ReadOnlySpan<byte> point)
    {
        var size = recipient.Q.X?.Length ?? 0;
        if (size == 0 || point.Length != 1 + (size * Coordinates))
        {
            return null;
        }

        var coordinates = new ECPoint { X = point.Slice(1, size).ToArray(), Y = point[(1 + size)..].ToArray() };
        try
        {
            return ECDiffieHellman.Create(new ECParameters { Curve = recipient.Curve, Q = coordinates });
        }
        catch (Exception exception) when (exception is CryptographicException || (OperatingSystem.IsWindows() && exception is PlatformNotSupportedException))
        {
            // The point is not on the curve. Windows (CNG) reports a point that fails validation as PlatformNotSupportedException
            // ("the specified curve or its parameters are not valid for this platform") instead of CryptographicException. The
            // curve itself is good here, because the recipient's own key was exported on it.
            return null;
        }
    }

    /// <summary>Gets the certificate's ECDH private key.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The key, or <see langword="null"/> when the certificate's key is not an elliptic curve key usable for ECDH.</returns>
    private static ECDiffieHellman? PrivateKey(X509Certificate2 certificate)
    {
        try
        {
            return certificate.GetECDiffieHellmanPrivateKey();
        }
        catch (CryptographicException)
        {
            // The provider holds the key but cannot use it for key agreement.
            return null;
        }
    }
}
