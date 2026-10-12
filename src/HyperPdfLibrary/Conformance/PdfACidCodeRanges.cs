// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.CMaps;

namespace HyperPdfLibrary.Conformance;

/// <summary>Range tests for <see cref="PdfACidCodes"/>.</summary>
internal static class PdfACidCodeRanges
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Finds a code's CID in ranges, the first holding range winning.</summary>
    /// <param name="ranges">The ranges.</param>
    /// <param name="code">The code.</param>
    /// <returns>The CID, or null.</returns>
    internal static int? Find(CodeRangeEntry[] ranges, int code)
    {
        foreach (var (low, high, value, _) in ranges)
        {
            if ((uint)code >= low && (uint)code <= high)
            {
                return value + (int)((uint)code - low);
            }
        }

        return null;
    }

    /// <summary>Determines whether bytes read so far lie within the bounds of a range's first bytes.</summary>
    /// <param name="range">The range, at least as long as the bytes.</param>
    /// <param name="read">The bytes read.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    internal static bool StartsWith(CodespaceRange range, ReadOnlySpan<byte> read)
    {
        for (var i = 0; i < read.Length; i++)
        {
            var shift = (range.Length - 1 - i) * ByteBits;
            if (read[i] < ((range.Low >> shift) & byte.MaxValue) || read[i] > ((range.High >> shift) & byte.MaxValue))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether a range's low bytes are each at most its high bytes; veraPDF empties ranges that are not.</summary>
    /// <param name="range">The range.</param>
    /// <returns><see langword="true"/> when the range is valid.</returns>
    internal static bool IsValid(CodespaceRange range)
    {
        for (var i = 0; i < range.Length; i++)
        {
            var shift = (range.Length - 1 - i) * ByteBits;
            if (((range.Low >> shift) & byte.MaxValue) > ((range.High >> shift) & byte.MaxValue))
            {
                return false;
            }
        }

        return true;
    }
}
