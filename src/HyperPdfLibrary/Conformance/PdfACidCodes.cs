// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.CMaps;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads a composite font's character codes and CIDs the way veraPDF does. Identity CMaps read two bytes and map a
/// code to itself. Other CMaps read byte by byte until a codespace range holds the code; a prefix no range can start
/// skips to the length of the shortest range that matched so far and reads as code 0.
/// </summary>
[DebuggerDisplay("PdfACidCodes: identity {Identity}")]
internal sealed class PdfACidCodes
{
    /// <summary>The longest code veraPDF reads.</summary>
    private const int MaxCodeBytes = 5;

    /// <summary>The bytes of an identity CMap code.</summary>
    private const int IdentityCodeBytes = 2;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The byte a read past the end of a string gives.</summary>
    private const int PastEnd = 0xFF;

    /// <summary>The codespace ranges in parse order.</summary>
    private readonly CodespaceRange[] _codespaces;

    /// <summary>The CID ranges, the last parsed first.</summary>
    private readonly CodeRangeEntry[] _cids;

    /// <summary>The notdef ranges, the last parsed first.</summary>
    private readonly CodeRangeEntry[] _notdefs;

    /// <summary>The shortest codespace range length.</summary>
    private readonly int _shortest;

    /// <summary>Initializes a new instance of the <see cref="PdfACidCodes"/> class.</summary>
    /// <param name="identity">Whether the CMap is Identity-H or Identity-V.</param>
    /// <param name="codespaces">The codespace ranges.</param>
    /// <param name="cids">The CID ranges in parse order.</param>
    /// <param name="notdefs">The notdef ranges in parse order.</param>
    private PdfACidCodes(bool identity, CodespaceRange[] codespaces, CodeRangeEntry[] cids, CodeRangeEntry[] notdefs)
    {
        Identity = identity;
        _codespaces = codespaces;
        Array.Reverse(cids);
        Array.Reverse(notdefs);
        _cids = cids;
        _notdefs = notdefs;
        _shortest = int.MaxValue;
        foreach (var range in codespaces)
        {
            _shortest = Math.Min(_shortest, range.Length);
        }
    }

    /// <summary>Gets the reader of Identity-H and Identity-V.</summary>
    internal static PdfACidCodes IdentityCodes { get; } = new(true, [], [], []);

    /// <summary>Gets a value indicating whether codes are their own CIDs.</summary>
    internal bool Identity { get; }

    /// <summary>Makes the reader of an embedded CMap.</summary>
    /// <param name="content">The parsed CMap.</param>
    /// <returns>The reader.</returns>
    internal static PdfACidCodes FromContent(CMapContent content) =>
        new(false, [.. content.Codespaces], [.. content.Cids.Entries], [.. content.Notdefs.Entries]);

    /// <summary>Reads one code.</summary>
    /// <param name="bytes">The string bytes still to read; not empty.</param>
    /// <param name="code">The code.</param>
    /// <returns>The number of bytes the code used.</returns>
    internal int ReadCode(ReadOnlySpan<byte> bytes, out int code)
    {
        if (Identity)
        {
            code = (bytes[0] << ByteBits) + (bytes.Length > 1 ? bytes[1] : -1);
            return Math.Min(IdentityCodeBytes, bytes.Length);
        }

        Span<byte> read = stackalloc byte[MaxCodeBytes];
        var previous = _shortest;
        for (var i = 0; i < MaxCodeBytes; i++)
        {
            read[i] = i < bytes.Length ? bytes[i] : (byte)PastEnd;
            if (FindComplete(read[..(i + 1)]))
            {
                code = Number(read[..(i + 1)]);
                return Math.Min(i + 1, bytes.Length);
            }

            var shortest = ShortestPartial(read[..(i + 1)]);
            if (shortest == int.MaxValue && _codespaces.Length > 0)
            {
                code = 0;
                return Math.Min(bytes.Length, i + 1 + Math.Max(0, previous - i - 1));
            }

            previous = shortest;
        }

        code = 0;
        return Math.Min(bytes.Length, MaxCodeBytes);
    }

    /// <summary>Maps a code to a CID: the CID ranges, then the notdef ranges, else 0.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The CID.</returns>
    internal int ToCid(int code) => Identity ? code : PdfACidCodeRanges.Find(_cids, code) ?? PdfACidCodeRanges.Find(_notdefs, code) ?? 0;

    /// <summary>Determines whether a CID or notdef range maps a code; an identity CMap maps every code but 0.</summary>
    /// <param name="code">The code.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal bool ContainsCode(int code) =>
        Identity ? code != 0 : PdfACidCodeRanges.Find(_cids, code) is not null || PdfACidCodeRanges.Find(_notdefs, code) is not null;

    /// <summary>Reads bytes as a big-endian number.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The number.</returns>
    private static int Number(ReadOnlySpan<byte> bytes)
    {
        var value = 0;
        foreach (var b in bytes)
        {
            value = (value << ByteBits) | b;
        }

        return value;
    }

    /// <summary>Determines whether a codespace range holds the bytes read so far as a whole code.</summary>
    /// <param name="read">The bytes read.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private bool FindComplete(ReadOnlySpan<byte> read)
    {
        foreach (var range in _codespaces)
        {
            if (range.Length == read.Length && PdfACidCodeRanges.IsValid(range) && range.Matches(read))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds the shortest codespace range that every byte read so far could start. veraPDF stops looking at the first
    /// range shorter than the bytes read, keeping what it found before it.
    /// </summary>
    /// <param name="read">The bytes read.</param>
    /// <returns>The length, or <see cref="int.MaxValue"/>.</returns>
    private int ShortestPartial(ReadOnlySpan<byte> read)
    {
        var shortest = int.MaxValue;
        foreach (var range in _codespaces)
        {
            var length = PdfACidCodeRanges.IsValid(range) ? range.Length : 0;
            if (length < read.Length)
            {
                break;
            }

            if (PdfACidCodeRanges.StartsWith(range, read))
            {
                shortest = Math.Min(shortest, length);
            }
        }

        return shortest;
    }
}
