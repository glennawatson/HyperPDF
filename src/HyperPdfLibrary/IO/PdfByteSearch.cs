// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.IO;

/// <summary>
/// Vectorised byte searches over a <see cref="PdfByteSource"/>, in windows that overlap by the needle's length so a match
/// across a window edge is found. Windows start small, as matches are usually near, and double up to a cap.
/// </summary>
internal static class PdfByteSearch
{
    /// <summary>The first window's length.</summary>
    private const int FirstWindow = 4096;

    /// <summary>The longest window.</summary>
    private const int LargestWindow = 1 << 20;

    /// <summary>The growth factor between windows.</summary>
    private const int Growth = 2;

    /// <summary>The bytes read at a time when skipping or matching a short run.</summary>
    private const int ProbeLength = 64;

    /// <summary>Gets the space byte, as skipped before a stream's line end.</summary>
    internal static SearchValues<byte> Space { get; } = SearchValues.Create(" "u8);

    /// <summary>Finds the first occurrence of bytes wholly inside a range.</summary>
    /// <param name="source">The source.</param>
    /// <param name="value">The bytes to find; not empty.</param>
    /// <param name="start">The first offset searched.</param>
    /// <param name="end">The offset after the last byte searched.</param>
    /// <returns>The offset of the match, or -1.</returns>
    internal static long IndexOf(PdfByteSource source, ReadOnlySpan<byte> value, long start, long end)
    {
        start = Math.Max(0, start);
        end = Math.Min(end, source.Length);
        if (end - start < value.Length)
        {
            return -1;
        }

        if (source.WholeArray is { } array)
        {
            var found = array.AsSpan((int)start, (int)(end - start)).IndexOf(value);
            return found < 0 ? -1 : start + found;
        }

        return IndexOfInWindows(source, value, start, end);
    }

    /// <summary>Finds the first occurrence of bytes from an offset to the end of the source.</summary>
    /// <param name="source">The source.</param>
    /// <param name="value">The bytes to find; not empty.</param>
    /// <param name="start">The first offset searched.</param>
    /// <returns>The offset of the match, or -1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long IndexOf(PdfByteSource source, ReadOnlySpan<byte> value, long start) => IndexOf(source, value, start, source.Length);

    /// <summary>Finds the last occurrence of bytes that ends at or before an offset.</summary>
    /// <param name="source">The source.</param>
    /// <param name="value">The bytes to find; not empty.</param>
    /// <param name="end">The offset after the last byte searched.</param>
    /// <returns>The offset of the match, or -1.</returns>
    internal static long LastIndexOf(PdfByteSource source, ReadOnlySpan<byte> value, long end)
    {
        end = Math.Min(end, source.Length);
        if (end < value.Length)
        {
            return -1;
        }

        return source.WholeArray is { } array ? array.AsSpan(0, (int)end).LastIndexOf(value) : LastIndexOfInWindows(source, value, end);
    }

    /// <summary>Moves past a run of the given bytes.</summary>
    /// <param name="source">The source.</param>
    /// <param name="position">The offset to start at.</param>
    /// <param name="skipped">The bytes to skip.</param>
    /// <returns>The offset of the first other byte, or the end of the source.</returns>
    internal static long SkipAny(PdfByteSource source, long position, SearchValues<byte> skipped)
    {
        Span<byte> probe = stackalloc byte[ProbeLength];
        var read = source.Read(position, probe);
        var other = probe[..read].IndexOfAnyExcept(skipped);
        while (other < 0 && read == probe.Length)
        {
            position += read;
            read = source.Read(position, probe);
            other = probe[..read].IndexOfAnyExcept(skipped);
        }

        return other >= 0 ? position + other : position + read;
    }

    /// <summary>Determines whether bytes appear at an offset.</summary>
    /// <param name="source">The source.</param>
    /// <param name="position">The offset.</param>
    /// <param name="value">The bytes, no longer than <see cref="ProbeLength"/>.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    internal static bool StartsWith(PdfByteSource source, long position, ReadOnlySpan<byte> value)
    {
        Span<byte> probe = stackalloc byte[ProbeLength];
        var read = source.Read(position, probe[..value.Length]);
        return read == value.Length && probe[..read].SequenceEqual(value);
    }

    /// <summary>Searches forwards window by window.</summary>
    /// <param name="source">The source.</param>
    /// <param name="value">The bytes to find.</param>
    /// <param name="start">The first offset searched.</param>
    /// <param name="end">The offset after the last byte searched.</param>
    /// <returns>The offset of the match, or -1.</returns>
    private static long IndexOfInWindows(PdfByteSource source, ReadOnlySpan<byte> value, long start, long end)
    {
        var window = FirstWindow;
        var position = start;
        while (end - position >= value.Length)
        {
            var length = (int)Math.Min(window, end - position);
            using (var lease = source.Lease(position, length))
            {
                var found = lease.Span.IndexOf(value);
                if (found >= 0)
                {
                    return position + found;
                }
            }

            if (position + length >= end)
            {
                break;
            }

            // Step back by one less than the needle, so a match across the edge is in the next window.
            position += length - value.Length + 1;
            window = Math.Min(window * Growth, LargestWindow);
        }

        return -1;
    }

    /// <summary>Searches backwards window by window.</summary>
    /// <param name="source">The source.</param>
    /// <param name="value">The bytes to find.</param>
    /// <param name="end">The offset after the last byte searched.</param>
    /// <returns>The offset of the match, or -1.</returns>
    private static long LastIndexOfInWindows(PdfByteSource source, ReadOnlySpan<byte> value, long end)
    {
        var window = FirstWindow;
        while (end >= value.Length)
        {
            var length = (int)Math.Min(window, end);
            var start = end - length;
            using (var lease = source.Lease(start, length))
            {
                var found = lease.Span.LastIndexOf(value);
                if (found >= 0)
                {
                    return start + found;
                }
            }

            if (start == 0)
            {
                break;
            }

            end = start + value.Length - 1;
            window = Math.Min(window * Growth, LargestWindow);
        }

        return -1;
    }
}
