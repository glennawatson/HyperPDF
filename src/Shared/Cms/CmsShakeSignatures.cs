// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Formats.Asn1;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdf.Cms;

/// <summary>
/// Checks a CMS signer whose digest is SHAKE256, which <c>SignedCms</c> does not support: the message-digest attribute
/// against SHAKE256 of the content, then the signature over SHAKE256 of the DER signed attributes (RFC 5652, 5.4),
/// with ECDSA (id-ecdsa-with-shake256) or RSASSA-PSS (id-RSASSA-PSS-SHAKE256) as RFC 8702, 3.2 specifies.
/// </summary>
internal static class CmsShakeSignatures
{
    /// <summary>The SET OF tag a signed attributes value is hashed with, in place of its <c>[0] IMPLICIT</c> tag.</summary>
    private const byte SetTag = 0x31;

    /// <summary>Gets the encoded id-messageDigest OID, 1.2.840.113549.1.9.4.</summary>
    private static ReadOnlySpan<byte> MessageDigestOid => [0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x09, 0x04];

    /// <summary>Gets the encoded id-ecdsa-with-shake256 OID, 1.3.6.1.5.5.7.6.33.</summary>
    private static ReadOnlySpan<byte> EcdsaShake256Oid => [0x2B, 0x06, 0x01, 0x05, 0x05, 0x07, 0x06, 0x21];

    /// <summary>Gets the encoded id-RSASSA-PSS-SHAKE256 OID, 1.3.6.1.5.5.7.6.31.</summary>
    private static ReadOnlySpan<byte> RsaPssShake256Oid => [0x2B, 0x06, 0x01, 0x05, 0x05, 0x07, 0x06, 0x1F];

    /// <summary>Gets the encoded id-ecPublicKey OID, 1.2.840.10045.2.1, which some writers give as the signature algorithm.</summary>
    private static ReadOnlySpan<byte> EcPublicKeyOid => [0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x02, 0x01];

    /// <summary>Checks the first signer of a container.</summary>
    /// <param name="der">The container.</param>
    /// <param name="layout">Where its first signer's parts are.</param>
    /// <param name="content">The signed content: the detached bytes, or the encapsulated content.</param>
    /// <param name="certificate">The signer's certificate.</param>
    /// <returns>The result.</returns>
    internal static CmsSignatureStatus Verify(ReadOnlySpan<byte> der, in CmsSignerLayout layout, ReadOnlySpan<byte> content, X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        var kind = SignatureKind(der[layout.SignatureAlgorithm]);
        if (kind == CmsShakeSignatureKind.Unsupported)
        {
            return CmsSignatureStatus.Unsupported;
        }

        Span<byte> hash = stackalloc byte[CmsDigests.MaxLength];
        var attributes = der[layout.SignedAttributes];
        if (attributes.IsEmpty)
        {
            Shake256.HashData(content, hash);
        }
        else if (!MessageDigestMatches(attributes, content) || !HashAttributes(attributes, hash))
        {
            return CmsSignatureStatus.Invalid;
        }

        return VerifyHash(kind, hash, der[layout.Signature], certificate) ? CmsSignatureStatus.Valid : CmsSignatureStatus.Invalid;
    }

    /// <summary>Determines whether the signed message-digest attribute is SHAKE256 of the content.</summary>
    /// <param name="attributes">The whole signed attributes value.</param>
    /// <param name="content">The content.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    internal static bool MessageDigestMatches(ReadOnlySpan<byte> attributes, ReadOnlySpan<byte> content)
    {
        if (!TryFindMessageDigest(attributes, out var expected) || expected.Length != CmsDigests.MaxLength)
        {
            return false;
        }

        Span<byte> actual = stackalloc byte[CmsDigests.MaxLength];
        Shake256.HashData(content, actual);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Finds the message-digest attribute's value in signed attributes.</summary>
    /// <param name="attributes">The whole signed attributes value.</param>
    /// <param name="digest">The digest.</param>
    /// <returns><see langword="true"/> when found.</returns>
    internal static bool TryFindMessageDigest(ReadOnlySpan<byte> attributes, out ReadOnlySpan<byte> digest)
    {
        digest = default;
        if (!CmsDer.TryRead(attributes, out _, out var list, out _))
        {
            return false;
        }

        var offset = 0;
        while (CmsDer.TryNext(list, ref offset, out _, out _, out var attribute))
        {
            var inner = 0;
            if (CmsDer.TryNext(attribute, ref inner, out _, out _, out var type)
                && IsMessageDigest(type)
                && CmsDer.TryNext(attribute, ref inner, out _, out _, out var values))
            {
                return CmsDer.TryReadChild(values, Asn1Tag.PrimitiveOctetString, out digest);
            }
        }

        return false;
    }

    /// <summary>Determines whether an attribute type is id-messageDigest.</summary>
    /// <param name="type">The encoded OID.</param>
    /// <returns><see langword="true"/> for the message-digest attribute.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsMessageDigest(ReadOnlySpan<byte> type) => CryptographicOperations.FixedTimeEquals(type, MessageDigestOid);

    /// <summary>Hashes the signed attributes as their DER SET OF encoding, which is what the signer signs.</summary>
    /// <param name="attributes">The whole signed attributes value.</param>
    /// <param name="hash">Where the 64-byte hash goes.</param>
    /// <returns><see langword="true"/> when hashed.</returns>
    private static bool HashAttributes(ReadOnlySpan<byte> attributes, Span<byte> hash)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(attributes.Length);
        try
        {
            var copy = buffer.AsSpan(0, attributes.Length);
            attributes.CopyTo(copy);
            copy[0] = SetTag;
            Shake256.HashData(copy, hash[..CmsDigests.MaxLength]);
            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Reads which signature algorithm a SHAKE256 signer uses; RFC 8702 requires the parameters to be absent.</summary>
    /// <param name="identifier">The signature AlgorithmIdentifier.</param>
    /// <returns>The algorithm.</returns>
    private static CmsShakeSignatureKind SignatureKind(ReadOnlySpan<byte> identifier)
    {
        if (!CmsDer.TryReadChild(identifier, Asn1Tag.Sequence, out var body)
            || !CmsDer.TryRead(body, out var tag, out var oid, out var used)
            || tag != Asn1Tag.ObjectIdentifier)
        {
            return CmsShakeSignatureKind.Unsupported;
        }

        var bare = used == body.Length;
        if (oid.SequenceEqual(EcPublicKeyOid) || (bare && oid.SequenceEqual(EcdsaShake256Oid)))
        {
            return CmsShakeSignatureKind.Ecdsa;
        }

        return bare && oid.SequenceEqual(RsaPssShake256Oid) ? CmsShakeSignatureKind.RsaPss : CmsShakeSignatureKind.Unsupported;
    }

    /// <summary>Verifies the signature over the hash with the certificate's key.</summary>
    /// <param name="kind">The algorithm.</param>
    /// <param name="hash">The hash.</param>
    /// <param name="signature">The signature.</param>
    /// <param name="certificate">The certificate.</param>
    /// <returns><see langword="true"/> when good.</returns>
    private static bool VerifyHash(CmsShakeSignatureKind kind, ReadOnlySpan<byte> hash, ReadOnlySpan<byte> signature, X509Certificate2 certificate)
    {
        try
        {
            if (kind == CmsShakeSignatureKind.Ecdsa)
            {
                using var ecdsa = certificate.GetECDsaPublicKey();
                return ecdsa?.VerifyHash(hash, signature, DSASignatureFormat.Rfc3279DerSequence) == true;
            }

            using var rsa = certificate.GetRSAPublicKey();
            return rsa is not null && RsaPssShake256.Verify(rsa, hash, signature);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
