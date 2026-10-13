// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdf.Cms;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>Maps the digests a PDF signature may use to the CMS digests and hashes the signed bytes.</summary>
internal static class PdfSigningDigests
{
    /// <summary>The slots in the pool: one for every <see cref="CmsDigestAlgorithm"/> value.</summary>
    private const int PoolSize = (int)CmsDigestAlgorithm.Shake128 + 1;

    /// <summary>The idle hash objects, by <see cref="CmsDigestAlgorithm"/> value; a signature takes one out and puts it back.</summary>
    private static readonly IncrementalHash?[] Pool = new IncrementalHash?[PoolSize];

    /// <summary>The idle SHAKE256 object, or <see langword="null"/>.</summary>
    private static Shake256? _pooledShake256;

    /// <summary>Gets the CMS digest for a digest method.</summary>
    /// <param name="method">The method.</param>
    /// <returns>The digest, or <see cref="CmsDigestAlgorithm.Unknown"/> for MD5, SHA-1 and RIPEMD-160, which a new signature never uses.</returns>
    internal static CmsDigestAlgorithm ToCms(PdfDigestMethod method) => method switch
    {
        PdfDigestMethod.Sha256 => CmsDigestAlgorithm.Sha256,
        PdfDigestMethod.Sha384 => CmsDigestAlgorithm.Sha384,
        PdfDigestMethod.Sha512 => CmsDigestAlgorithm.Sha512,
        PdfDigestMethod.Sha3_256 => CmsDigestAlgorithm.Sha3_256,
        PdfDigestMethod.Sha3_384 => CmsDigestAlgorithm.Sha3_384,
        PdfDigestMethod.Sha3_512 => CmsDigestAlgorithm.Sha3_512,
        PdfDigestMethod.Shake256 => CmsDigestAlgorithm.Shake256,
        _ => CmsDigestAlgorithm.Unknown,
    };

    /// <summary>Gets the name PDF uses for a digest in /DigestMethod (ISO 32000-2, Table 256; ISO/TS 32001:2022, 5.1.2).</summary>
    /// <param name="method">The method.</param>
    /// <returns>The name without the slash.</returns>
    internal static string Name(PdfDigestMethod method) => method switch
    {
        PdfDigestMethod.Sha256 => "SHA256",
        PdfDigestMethod.Sha384 => "SHA384",
        PdfDigestMethod.Sha512 => "SHA512",
        PdfDigestMethod.Sha3_256 => "SHA3-256",
        PdfDigestMethod.Sha3_384 => "SHA3-384",
        PdfDigestMethod.Sha3_512 => "SHA3-512",
        PdfDigestMethod.Shake256 => "SHAKE256",
        _ => string.Empty,
    };

    /// <summary>Gets a plain-English name for a digest.</summary>
    /// <param name="method">The method.</param>
    /// <returns>The name, such as <c>SHA-256</c>.</returns>
    internal static string DisplayName(PdfDigestMethod method) => method switch
    {
        PdfDigestMethod.Sha256 => "SHA-256",
        PdfDigestMethod.Sha384 => "SHA-384",
        PdfDigestMethod.Sha512 => "SHA-512",
        PdfDigestMethod.Sha3_256 => "SHA3-256",
        PdfDigestMethod.Sha3_384 => "SHA3-384",
        PdfDigestMethod.Sha3_512 => "SHA3-512",
        PdfDigestMethod.Shake256 => "SHAKE256",
        _ => "an unsupported digest",
    };

    /// <summary>Determines whether a digest is one of the SHA-3 family ISO/TS 32001 adds, which needs the ISO_ developer extension.</summary>
    /// <param name="method">The method.</param>
    /// <returns><see langword="true"/> for SHA3-256, SHA3-384, SHA3-512 and SHAKE256.</returns>
    internal static bool IsSha3Family(PdfDigestMethod method) =>
        method is PdfDigestMethod.Sha3_256 or PdfDigestMethod.Sha3_384 or PdfDigestMethod.Sha3_512 or PdfDigestMethod.Shake256;

    /// <summary>Hashes two ranges of a file, one after the other, as a signature's /ByteRange describes.</summary>
    /// <param name="digest">The digest.</param>
    /// <param name="first">The bytes before the signature value.</param>
    /// <param name="second">The bytes after it.</param>
    /// <param name="destination">A buffer of at least <see cref="CmsDigests.MaxLength"/> bytes.</param>
    /// <returns>The digest length, or 0 when the digest is not available on this computer.</returns>
    internal static int Hash(CmsDigestAlgorithm digest, ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, Span<byte> destination)
    {
        if (CmsDigests.Status(digest) != CmsDigestStatus.Available)
        {
            return 0;
        }

        if (digest == CmsDigestAlgorithm.Shake256)
        {
            return HashShake256(first, second, destination);
        }

        // A hash object is a native context; the pool keeps one per algorithm for the next signature to take.
        var slot = (int)digest;
        var hash = Interlocked.Exchange(ref Pool[slot], null) ?? IncrementalHash.CreateHash(CmsDigests.HashName(digest));
        hash.AppendData(first);
        hash.AppendData(second);
        var length = hash.GetHashAndReset(destination);
        if (Interlocked.CompareExchange(ref Pool[slot], hash, null) is not null)
        {
            hash.Dispose();
        }

        return length;
    }

    /// <summary>Hashes two ranges with SHAKE256, taking its 64-byte output.</summary>
    /// <param name="first">The first range.</param>
    /// <param name="second">The second range.</param>
    /// <param name="destination">A buffer of at least <see cref="CmsDigests.MaxLength"/> bytes.</param>
    /// <returns>The length written.</returns>
    private static int HashShake256(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, Span<byte> destination)
    {
        var shake = Interlocked.Exchange(ref _pooledShake256, null) ?? new Shake256();
        shake.AppendData(first);
        shake.AppendData(second);
        shake.GetHashAndReset(destination[..CmsDigests.MaxLength]);
        if (Interlocked.CompareExchange(ref _pooledShake256, shake, null) is not null)
        {
            shake.Dispose();
        }

        return CmsDigests.MaxLength;
    }
}
