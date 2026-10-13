// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HyperPdf.Cms;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Security;

/// <summary>Digests the byte ranges a PDF MAC covers (ISO/TS 32004:2024 6.6), reading the file a window at a time.</summary>
internal static class PdfMacDigest
{
    /// <summary>The bytes read from the file at a time.</summary>
    private const int Window = 1 << 20;

    /// <summary>The numbers per range: offset and length.</summary>
    private const int Pair = 2;

    /// <summary>Digests the ranges of a file in order.</summary>
    /// <param name="file">The file.</param>
    /// <param name="digest">The digest algorithm; it must be available on this computer.</param>
    /// <param name="ranges">The ranges as offset and length pairs, all inside the file.</param>
    /// <param name="destination">A buffer of at least the digest's length.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The digest length.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static int Hash(PdfByteSource file, CmsDigestAlgorithm digest, ReadOnlySpan<long> ranges, Span<byte> destination, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(CmsDigests.HashName(digest));
        for (var i = 0; i + 1 < ranges.Length; i += Pair)
        {
            Append(hash, file, ranges[i], ranges[i + 1], cancellationToken);
        }

        return hash.GetHashAndReset(destination);
    }

    /// <summary>Digests a value held in memory.</summary>
    /// <param name="digest">The digest algorithm; it must be available on this computer.</param>
    /// <param name="data">The value.</param>
    /// <param name="destination">A buffer of at least the digest's length.</param>
    /// <returns>The digest length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Hash(CmsDigestAlgorithm digest, ReadOnlySpan<byte> data, Span<byte> destination) => CmsDigests.Hash(digest, data, destination);

    /// <summary>Adds a range of the file to a running digest.</summary>
    /// <param name="hash">The digest.</param>
    /// <param name="file">The file.</param>
    /// <param name="start">The first byte.</param>
    /// <param name="length">The number of bytes.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    internal static void Append(IncrementalHash hash, PdfByteSource file, long start, long length, CancellationToken cancellationToken)
    {
        var end = start + length;
        for (var position = start; position < end; position += Window)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var lease = file.Lease(position, (int)Math.Min(Window, end - position));
            hash.AppendData(lease.Span);
        }
    }
}
