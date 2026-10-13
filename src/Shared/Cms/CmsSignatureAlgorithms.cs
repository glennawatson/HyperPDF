// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;

namespace HyperPdf.Cms;

/// <summary>
/// Reads a CMS signer's signature algorithm identifier for the ECDSA and EdDSA cases of ISO/TS 32002:2022: the digest an
/// ecdsa-with-digest identifier names (RFC 5753, 2.1.1 and RFC 8702, 3.2.2) and id-Ed25519 or id-Ed448 (RFC 8419, 3.2).
/// </summary>
internal static class CmsSignatureAlgorithms
{
    /// <summary>The last arc of ecdsa-with-SHA1, under 1.2.840.10045.4.</summary>
    private const byte EcdsaSha1Arc = 0x01;

    /// <summary>The arc of the ecdsa-with-SHA2 family, 1.2.840.10045.4.3.</summary>
    private const byte EcdsaSha2Family = 0x03;

    /// <summary>The last arc of ecdsa-with-SHA256.</summary>
    private const byte EcdsaSha256Arc = 0x02;

    /// <summary>The last arc of ecdsa-with-SHA384.</summary>
    private const byte EcdsaSha384Arc = 0x03;

    /// <summary>The last arc of ecdsa-with-SHA512.</summary>
    private const byte EcdsaSha512Arc = 0x04;

    /// <summary>The last arc of ecdsa-with-SHA3-256.</summary>
    private const byte EcdsaSha3_256Arc = 0x0A;

    /// <summary>The last arc of ecdsa-with-SHA3-384.</summary>
    private const byte EcdsaSha3_384Arc = 0x0B;

    /// <summary>The last arc of ecdsa-with-SHA3-512.</summary>
    private const byte EcdsaSha3_512Arc = 0x0C;

    /// <summary>The length of an encoded 1.2.840.10045.4.x OID.</summary>
    private const int AnsiX962Length = 7;

    /// <summary>The length of an encoded 2.16.840.1.101.3.4.3.x OID.</summary>
    private const int NistSignatureLength = 9;

    /// <summary>Gets the encoded id-Ed25519 OID.</summary>
    private static ReadOnlySpan<byte> Ed25519Oid => [0x2B, 0x65, 0x70];

    /// <summary>Gets the encoded id-Ed448 OID.</summary>
    private static ReadOnlySpan<byte> Ed448Oid => [0x2B, 0x65, 0x71];

    /// <summary>Gets the encoded id-ecPublicKey OID, which some writers give as the signature algorithm.</summary>
    private static ReadOnlySpan<byte> EcPublicKeyOid => [0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x02, 0x01];

    /// <summary>Gets the encoded arcs of 1.2.840.10045.4, which hold ecdsa-with-SHA1 and the SHA2 family.</summary>
    private static ReadOnlySpan<byte> AnsiX962Prefix => [0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x04];

    /// <summary>Gets the encoded arcs of 2.16.840.1.101.3.4.3, which hold the SHA-3 signature algorithms.</summary>
    private static ReadOnlySpan<byte> NistSignaturePrefix => [0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x03];

    /// <summary>Gets the encoded id-ecdsa-with-shake256 OID, 1.3.6.1.5.5.7.6.33.</summary>
    private static ReadOnlySpan<byte> EcdsaShake256Oid => [0x2B, 0x06, 0x01, 0x05, 0x05, 0x07, 0x06, 0x21];

    /// <summary>Reads a signature AlgorithmIdentifier.</summary>
    /// <param name="identifier">The encoded AlgorithmIdentifier.</param>
    /// <returns>The kind and the digest it names.</returns>
    internal static CmsSignatureAlgorithm Read(ReadOnlySpan<byte> identifier)
    {
        if (!CmsDer.TryReadChild(identifier, Asn1Tag.Sequence, out var body)
            || !CmsDer.TryRead(body, out var tag, out var oid, out _)
            || tag != Asn1Tag.ObjectIdentifier)
        {
            return default;
        }

        if (oid.SequenceEqual(Ed25519Oid))
        {
            return new(CmsSignatureKind.Ed25519, CmsDigestAlgorithm.Unknown);
        }

        if (oid.SequenceEqual(Ed448Oid))
        {
            return new(CmsSignatureKind.Ed448, CmsDigestAlgorithm.Unknown);
        }

        var digest = EcdsaDigest(oid);
        return digest != CmsDigestAlgorithm.Unknown || oid.SequenceEqual(EcPublicKeyOid)
            ? new(CmsSignatureKind.Ecdsa, digest)
            : default;
    }

    /// <summary>Maps an ecdsa-with-digest OID to its digest.</summary>
    /// <param name="oid">The encoded OID's content bytes.</param>
    /// <returns>The digest, or <see cref="CmsDigestAlgorithm.Unknown"/> for any other OID.</returns>
    private static CmsDigestAlgorithm EcdsaDigest(ReadOnlySpan<byte> oid)
    {
        if (oid.SequenceEqual(EcdsaShake256Oid))
        {
            return CmsDigestAlgorithm.Shake256;
        }

        return oid.StartsWith(AnsiX962Prefix) ? AnsiDigest(oid) : NistDigest(oid);
    }

    /// <summary>Maps an ecdsa-with-SHA1 or ecdsa-with-SHA2 OID, under 1.2.840.10045.4, to its digest.</summary>
    /// <param name="oid">The encoded OID's content bytes.</param>
    /// <returns>The digest, or <see cref="CmsDigestAlgorithm.Unknown"/> for any other OID.</returns>
    private static CmsDigestAlgorithm AnsiDigest(ReadOnlySpan<byte> oid)
    {
        if (oid.Length == AnsiX962Length)
        {
            return oid[^1] == EcdsaSha1Arc ? CmsDigestAlgorithm.Sha1 : CmsDigestAlgorithm.Unknown;
        }

        return oid.Length == AnsiX962Length + 1 && oid[AnsiX962Prefix.Length] == EcdsaSha2Family ? Sha2Digest(oid[^1]) : CmsDigestAlgorithm.Unknown;
    }

    /// <summary>Maps the last arc of an ecdsa-with-SHA2 OID to its digest.</summary>
    /// <param name="arc">The last arc.</param>
    /// <returns>The digest.</returns>
    private static CmsDigestAlgorithm Sha2Digest(byte arc) => arc switch
    {
        EcdsaSha256Arc => CmsDigestAlgorithm.Sha256,
        EcdsaSha384Arc => CmsDigestAlgorithm.Sha384,
        EcdsaSha512Arc => CmsDigestAlgorithm.Sha512,
        _ => CmsDigestAlgorithm.Unknown,
    };

    /// <summary>Maps an ecdsa-with-SHA3 OID, under 2.16.840.1.101.3.4.3, to its digest.</summary>
    /// <param name="oid">The encoded OID's content bytes.</param>
    /// <returns>The digest, or <see cref="CmsDigestAlgorithm.Unknown"/> for any other OID.</returns>
    private static CmsDigestAlgorithm NistDigest(ReadOnlySpan<byte> oid) =>
        oid.Length == NistSignatureLength && oid.StartsWith(NistSignaturePrefix) ? Sha3Digest(oid[^1]) : CmsDigestAlgorithm.Unknown;

    /// <summary>Maps the last arc of an ecdsa-with-SHA3 OID to its digest.</summary>
    /// <param name="arc">The last arc.</param>
    /// <returns>The digest.</returns>
    private static CmsDigestAlgorithm Sha3Digest(byte arc) => arc switch
    {
        EcdsaSha3_256Arc => CmsDigestAlgorithm.Sha3_256,
        EcdsaSha3_384Arc => CmsDigestAlgorithm.Sha3_384,
        EcdsaSha3_512Arc => CmsDigestAlgorithm.Sha3_512,
        _ => CmsDigestAlgorithm.Unknown,
    };
}
