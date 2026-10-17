// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdf.Cms;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Signing;

/// <summary>Maps the digests a PDF signature may use to the CMS digests and hashes the signed bytes.</summary>
internal static class PdfSigningDigests
{
    /// <summary>The maximum signed bytes hashed between cancellation checks.</summary>
    private const int HashChunkBytes = 64 * 1024;

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
    /// <param name="scratchPools">The caller's native-context owner.</param>
    /// <param name="cancellationToken">Cancels hashing before leasing native state.</param>
    /// <returns>The digest length, or 0 when the digest is not available on this computer.</returns>
    internal static int Hash(CmsDigestAlgorithm digest, ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, Span<byte> destination, ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (CmsDigests.Status(digest) != CmsDigestStatus.Available)
        {
            return 0;
        }

        var pool = scratchPools.Objects<PdfSigningHashState>();
        var state = pool.Take() ?? new PdfSigningHashState();
        var reusable = false;
        try
        {
            var length = digest == CmsDigestAlgorithm.Shake256
                ? HashShake256(state, first, second, destination, cancellationToken)
                : HashFixed(state, digest, first, second, destination, cancellationToken);
            reusable = true;
            return length;
        }
        finally
        {
            // A failed or cancelled hash may hold partial input and must not enter another operation.
            if (!reusable || !pool.Offer(state))
            {
                state.Dispose();
            }
        }
    }

    /// <summary>Hashes two ranges using an exclusively leased fixed-length context.</summary>
    /// <param name="state">The native-context lease.</param>
    /// <param name="digest">The digest algorithm.</param>
    /// <param name="first">The first range.</param>
    /// <param name="second">The second range.</param>
    /// <param name="destination">Receives the digest.</param>
    /// <param name="cancellationToken">Cancels between ranges.</param>
    /// <returns>The written length.</returns>
    private static int HashFixed(PdfSigningHashState state, CmsDigestAlgorithm digest, ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, Span<byte> destination, CancellationToken cancellationToken)
    {
        ref var slot = ref state.Hash(digest);
        var hash = slot ??= IncrementalHash.CreateHash(CmsDigests.HashName(digest));
        Append(hash, first, cancellationToken);
        Append(hash, second, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return hash.GetHashAndReset(destination);
    }

    /// <summary>Hashes two ranges with SHAKE256, taking its 64-byte output.</summary>
    /// <param name="state">The exclusively leased native contexts.</param>
    /// <param name="first">The first range.</param>
    /// <param name="second">The second range.</param>
    /// <param name="destination">A buffer of at least <see cref="CmsDigests.MaxLength"/> bytes.</param>
    /// <param name="cancellationToken">Cancels between ranges.</param>
    /// <returns>The length written.</returns>
    private static int HashShake256(PdfSigningHashState state, ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, Span<byte> destination, CancellationToken cancellationToken)
    {
        var shake = state.Shake ??= new Shake256();
        Append(shake, first, cancellationToken);
        Append(shake, second, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        shake.GetHashAndReset(destination[..CmsDigests.MaxLength]);
        return CmsDigests.MaxLength;
    }

    /// <summary>Appends fixed-length digest input in cancellable chunks.</summary>
    /// <param name="hash">The exclusively leased native context.</param>
    /// <param name="bytes">The signed input.</param>
    /// <param name="cancellationToken">Cancels between chunks.</param>
    private static void Append(IncrementalHash hash, ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        while (!bytes.IsEmpty)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var length = Math.Min(bytes.Length, HashChunkBytes);
            hash.AppendData(bytes[..length]);
            bytes = bytes[length..];
        }
    }

    /// <summary>Appends variable-length digest input in cancellable chunks.</summary>
    /// <param name="shake">The exclusively leased native context.</param>
    /// <param name="bytes">The signed input.</param>
    /// <param name="cancellationToken">Cancels between chunks.</param>
    private static void Append(Shake256 shake, ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        while (!bytes.IsEmpty)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var length = Math.Min(bytes.Length, HashChunkBytes);
            shake.AppendData(bytes[..length]);
            bytes = bytes[length..];
        }
    }
}
