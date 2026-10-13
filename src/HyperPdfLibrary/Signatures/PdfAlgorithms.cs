// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Cms;

namespace HyperPdfLibrary.Signatures;

/// <summary>Maps algorithm OIDs to hash algorithms and verifies X.509-style signed structures.</summary>
internal static class PdfAlgorithms
{
    /// <summary>The longest hash produced: SHA-512's, SHA3-512's and SHAKE256's.</summary>
    internal const int MaxHashLength = CmsDigests.MaxLength;

    /// <summary>The RSA PKCS #1 v1.5 and ECDSA signature algorithms checked, with SHA-1, SHA-2 and SHA-3 (RFC 5754, NIST CSOR).</summary>
    private static readonly SignatureAlgorithm[] SignatureAlgorithms =
    [
        new("1.2.840.113549.1.1.5", CmsDigestAlgorithm.Sha1, false),
        new("1.2.840.113549.1.1.11", CmsDigestAlgorithm.Sha256, false),
        new("1.2.840.113549.1.1.12", CmsDigestAlgorithm.Sha384, false),
        new("1.2.840.113549.1.1.13", CmsDigestAlgorithm.Sha512, false),
        new("2.16.840.1.101.3.4.3.14", CmsDigestAlgorithm.Sha3_256, false),
        new("2.16.840.1.101.3.4.3.15", CmsDigestAlgorithm.Sha3_384, false),
        new("2.16.840.1.101.3.4.3.16", CmsDigestAlgorithm.Sha3_512, false),
        new("1.2.840.10045.4.1", CmsDigestAlgorithm.Sha1, true),
        new("1.2.840.10045.4.3.2", CmsDigestAlgorithm.Sha256, true),
        new("1.2.840.10045.4.3.3", CmsDigestAlgorithm.Sha384, true),
        new("1.2.840.10045.4.3.4", CmsDigestAlgorithm.Sha512, true),
        new("2.16.840.1.101.3.4.3.10", CmsDigestAlgorithm.Sha3_256, true),
        new("2.16.840.1.101.3.4.3.11", CmsDigestAlgorithm.Sha3_384, true),
        new("2.16.840.1.101.3.4.3.12", CmsDigestAlgorithm.Sha3_512, true),
    ];

    /// <summary>Hashes data with the algorithm an OID names.</summary>
    /// <param name="oid">The digest algorithm OID.</param>
    /// <param name="data">The data.</param>
    /// <param name="destination">A buffer of at least <see cref="MaxHashLength"/> bytes.</param>
    /// <returns>The number of bytes written, or 0 when the algorithm is not supported here.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Hash(string? oid, ReadOnlySpan<byte> data, Span<byte> destination) => CmsDigests.Hash(CmsDigests.FromOid(oid), data, destination);

    /// <summary>Verifies a signature made over data with an issuer's key.</summary>
    /// <param name="data">The signed data, such as a DER TBSCertList.</param>
    /// <param name="algorithm">The signature algorithm OID.</param>
    /// <param name="signature">The signature value.</param>
    /// <param name="issuer">The certificate whose public key made the signature.</param>
    /// <returns><see langword="true"/> when the signature is good; <see langword="false"/> also when the hash is not available here.</returns>
    internal static bool Verify(ReadOnlySpan<byte> data, string? algorithm, ReadOnlySpan<byte> signature, X509Certificate2 issuer)
    {
        var digest = SignatureHash(algorithm, out var isEcdsa);
        if (CmsDigests.Status(digest) != CmsDigestStatus.Available || CmsDigests.HashName(digest) is not { Name: not null } hash)
        {
            return false;
        }

        try
        {
            if (isEcdsa)
            {
                using var ecdsa = issuer.GetECDsaPublicKey();
                return ecdsa?.VerifyData(data, signature, hash, DSASignatureFormat.Rfc3279DerSequence) == true;
            }

            using var rsa = issuer.GetRSAPublicKey();
            return rsa?.VerifyData(data, signature, hash, RSASignaturePadding.Pkcs1) == true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>Maps an RSA PKCS #1 v1.5 or ECDSA signature algorithm OID, with SHA-1, SHA-2 or SHA-3, to its digest.</summary>
    /// <param name="algorithm">The OID.</param>
    /// <param name="isEcdsa">Whether the algorithm is ECDSA.</param>
    /// <returns>The digest, or <see cref="CmsDigestAlgorithm.Unknown"/> when the algorithm is not supported.</returns>
    private static CmsDigestAlgorithm SignatureHash(string? algorithm, out bool isEcdsa)
    {
        foreach (var (oid, digest, ecdsa) in SignatureAlgorithms)
        {
            if (!string.Equals(oid, algorithm, StringComparison.Ordinal))
            {
                continue;
            }

            isEcdsa = ecdsa;
            return digest;
        }

        isEcdsa = false;
        return CmsDigestAlgorithm.Unknown;
    }
}
