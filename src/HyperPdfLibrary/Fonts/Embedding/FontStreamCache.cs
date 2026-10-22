// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// Keeps the Flate-compressed form of recently embedded font programs. A subset is deterministic for a face and a set of
/// glyphs, so the same program bytes need compressing once. A program is matched by its bytes, never by a face or a
/// document, so a hit returns exactly what compressing would. The cache is a small copy-on-write list: lookups read one
/// snapshot without locking, and the oldest entries leave first when the count or the byte budget is reached.
/// </summary>
internal static class FontStreamCache
{
    /// <summary>The most programs kept.</summary>
    private const int MaxEntries = 16;

    /// <summary>The most bytes kept, counting each program and its compressed form.</summary>
    private const long MaxBytes = 8L * 1024 * 1024;

    /// <summary>The smallest program kept; smaller ones compress faster than they are looked up.</summary>
    private const int MinProgramLength = 1024;

    /// <summary>Gets the Flate-compressed form of a program, compressing it only when it has not been seen.</summary>
    /// <param name="owner">The document's embedding state.</param>
    /// <param name="data">The immutable program, retained by the document on a miss.</param>
    /// <returns>The compressed bytes, shared with other callers: they must not be changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte[] GetOrCompress(PdfFontEmbeddingState owner, byte[] data) =>
        GetOrCompress(ref owner.Streams, data, MaxEntries, MaxBytes, data);

    /// <summary>Gets the Flate-compressed form of a program from a snapshot, compressing and keeping it on a miss.</summary>
    /// <param name="state">The snapshot to read and publish to.</param>
    /// <param name="data">The program.</param>
    /// <param name="maxEntries">The most programs kept.</param>
    /// <param name="maxBytes">The most bytes kept.</param>
    /// <returns>The compressed bytes, shared with other callers: they must not be changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte[] GetOrCompress(ref FontStreamCacheState state, ReadOnlySpan<byte> data, int maxEntries, long maxBytes) =>
        GetOrCompress(ref state, data, maxEntries, maxBytes, null);

    /// <summary>Compresses a program, sharing owned immutable source storage when supplied.</summary>
    /// <param name="state">The cache snapshot.</param>
    /// <param name="data">The source bytes.</param>
    /// <param name="maxEntries">The entry limit.</param>
    /// <param name="maxBytes">The retained byte limit.</param>
    /// <param name="owned">The immutable source array, or null to copy the source.</param>
    /// <returns>The compressed program.</returns>
    private static byte[] GetOrCompress(ref FontStreamCacheState state, ReadOnlySpan<byte> data, int maxEntries, long maxBytes, byte[]? owned)
    {
        if (data.Length < MinProgramLength)
        {
            return Compress(data);
        }

        var hash = Hash(data);
        if (Find(Volatile.Read(ref state), hash, data) is { } cached)
        {
            return cached;
        }

        var entry = new FontStreamCacheEntry(hash, owned ?? data.ToArray(), Compress(data));
        return Publish(ref state, entry, maxEntries, maxBytes);
    }

    /// <summary>Compresses a program.</summary>
    /// <param name="data">The program.</param>
    /// <returns>The zlib bytes.</returns>
    private static byte[] Compress(ReadOnlySpan<byte> data)
    {
        var compressed = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(data, ref compressed);
            return compressed.ToArray();
        }
        finally
        {
            compressed.Dispose();
        }
    }

    /// <summary>Hashes a program.</summary>
    /// <param name="data">The program.</param>
    /// <returns>The hash.</returns>
    private static int Hash(ReadOnlySpan<byte> data)
    {
        var hash = default(HashCode);
        hash.AddBytes(data);
        return hash.ToHashCode();
    }

    /// <summary>Finds the compressed form of a program in a snapshot.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="hash">The program's hash.</param>
    /// <param name="data">The program.</param>
    /// <returns>The compressed bytes, or <see langword="null"/> when the snapshot lacks the program.</returns>
    private static byte[]? Find(FontStreamCacheState snapshot, int hash, ReadOnlySpan<byte> data)
    {
        foreach (var entry in snapshot.Entries)
        {
            if (entry.Hash == hash && data.SequenceEqual(entry.Source))
            {
                return entry.Compressed;
            }
        }

        return null;
    }

    /// <summary>Adds an entry to a snapshot, dropping the oldest entries that no longer fit.</summary>
    /// <param name="state">The snapshot to publish to.</param>
    /// <param name="entry">The new entry.</param>
    /// <param name="maxEntries">The most programs kept.</param>
    /// <param name="maxBytes">The most bytes kept.</param>
    /// <returns>The compressed bytes of the program: the entry's, or an equal entry another thread published first.</returns>
    private static byte[] Publish(ref FontStreamCacheState state, FontStreamCacheEntry entry, int maxEntries, long maxBytes)
    {
        var size = (long)entry.Source.Length + entry.Compressed.Length;
        if (size > maxBytes)
        {
            return entry.Compressed;
        }

        var current = Volatile.Read(ref state);
        var existing = Find(current, entry.Hash, entry.Source);
        while (existing is null)
        {
            var seen = Interlocked.CompareExchange(ref state, Append(current, entry, size, maxEntries, maxBytes), current);
            if (ReferenceEquals(seen, current))
            {
                return entry.Compressed;
            }

            current = seen;
            existing = Find(current, entry.Hash, entry.Source);
        }

        return existing;
    }

    /// <summary>Builds the snapshot that holds a new entry first and as many older entries as fit.</summary>
    /// <param name="current">The snapshot.</param>
    /// <param name="entry">The new entry.</param>
    /// <param name="size">The new entry's bytes.</param>
    /// <param name="maxEntries">The most programs kept.</param>
    /// <param name="maxBytes">The most bytes kept.</param>
    /// <returns>The new snapshot.</returns>
    private static FontStreamCacheState Append(FontStreamCacheState current, FontStreamCacheEntry entry, long size, int maxEntries, long maxBytes)
    {
        var kept = 1;
        var bytes = size;
        foreach (var older in current.Entries)
        {
            var olderSize = (long)older.Source.Length + older.Compressed.Length;
            if (kept >= maxEntries || bytes + olderSize > maxBytes)
            {
                break;
            }

            kept++;
            bytes += olderSize;
        }

        var entries = new FontStreamCacheEntry[kept];
        entries[0] = entry;
        Array.Copy(current.Entries, 0, entries, 1, kept - 1);
        return new(entries, bytes);
    }
}
