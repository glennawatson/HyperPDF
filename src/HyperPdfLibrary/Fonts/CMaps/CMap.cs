// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Fonts.CMaps;

/// <summary>
/// A CMap that splits the bytes of a string into character codes and maps codes to CIDs. Mappings are stored as
/// sorted ranges, so large CJK ranges stay small. A CMap is immutable and safe to read from many threads; reading codes
/// and mapping CIDs do not allocate.
/// </summary>
[DebuggerDisplay("CMap: {_codespaces.Length} codespaces, {_cids.Count} CID ranges")]
public sealed class CMap
{
    /// <summary>The code length a CID font uses when no codespace matches.</summary>
    private const int DefaultCidCodeLength = 2;

    /// <summary>The longest code.</summary>
    private const int MaxCodeLength = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The writing mode of vertical CMaps.</summary>
    private const int VerticalMode = 1;

    /// <summary>The codespace ranges, shortest first, including those of the base CMap.</summary>
    private readonly CodespaceRange[] _codespaces;

    /// <summary>The CID ranges.</summary>
    private readonly CodeRangeMap _cids;

    /// <summary>The notdef ranges.</summary>
    private readonly CodeRangeMap _notdefs;

    /// <summary>The CMap named by usecmap, or <see langword="null"/>.</summary>
    private readonly CMap? _parent;

    /// <summary>Whether this is Identity-H or Identity-V.</summary>
    private readonly bool _identity;

    /// <summary>Initializes a new instance of the <see cref="CMap"/> class.</summary>
    /// <param name="codespaces">The codespace ranges, shortest first.</param>
    /// <param name="cids">The CID ranges.</param>
    /// <param name="notdefs">The notdef ranges.</param>
    /// <param name="parent">The base CMap.</param>
    /// <param name="writingMode">The writing mode.</param>
    /// <param name="identity">Whether this is an identity CMap.</param>
    private CMap(CodespaceRange[] codespaces, CodeRangeMap cids, CodeRangeMap notdefs, CMap? parent, int writingMode, bool identity)
    {
        _codespaces = codespaces;
        _cids = cids;
        _notdefs = notdefs;
        _parent = parent;
        WritingMode = writingMode;
        _identity = identity;
    }

    /// <summary>Gets the horizontal identity CMap, which reads two-byte codes and uses each code as its CID.</summary>
    public static CMap IdentityH { get; } = new([new(DefaultCidCodeLength, 0, ushort.MaxValue)], CodeRangeMap.Empty, CodeRangeMap.Empty, null, 0, true);

    /// <summary>Gets the vertical identity CMap.</summary>
    public static CMap IdentityV { get; } = new([new(DefaultCidCodeLength, 0, ushort.MaxValue)], CodeRangeMap.Empty, CodeRangeMap.Empty, null, VerticalMode, true);

    /// <summary>Gets the writing mode: 0 for horizontal, 1 for vertical.</summary>
    public int WritingMode { get; }

    /// <summary>Gets a value indicating whether text using this CMap is written vertically.</summary>
    public bool IsVertical => WritingMode == VerticalMode;

    /// <summary>Gets a value indicating whether this is Identity-H or Identity-V.</summary>
    public bool IsIdentity => _identity;

    /// <summary>Finds a CMap the library holds: Identity-H and Identity-V.</summary>
    /// <param name="name">The CMap name's bytes.</param>
    /// <returns>The CMap, or <see langword="null"/>.</returns>
    public static CMap? GetPredefined(ReadOnlySpan<byte> name)
    {
        if (name.SequenceEqual("Identity-H"u8))
        {
            return IdentityH;
        }

        return name.SequenceEqual("Identity-V"u8) ? IdentityV : null;
    }

    /// <summary>Parses an embedded CMap stream.</summary>
    /// <param name="data">The decoded stream.</param>
    /// <returns>The CMap.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CMap Parse(ReadOnlySpan<byte> data) => Parse(data, null);

    /// <summary>Parses an embedded CMap stream, resolving usecmap through a callback.</summary>
    /// <param name="data">The decoded stream.</param>
    /// <param name="resolver">Finds CMaps by name; Identity-H and Identity-V are found without it.</param>
    /// <returns>The CMap.</returns>
    public static CMap Parse(ReadOnlySpan<byte> data, CMapResolver? resolver)
    {
        var content = CMapParser.Parse(data);
        var parent = content.HasUseCMap ? GetPredefined(content.UseCMap.Span) ?? resolver?.Invoke(content.UseCMap.Span) : null;
        return Create(content, parent);
    }

    /// <summary>Reads one character code, using two bytes when no codespace range matches.</summary>
    /// <param name="bytes">The string bytes still to read.</param>
    /// <param name="code">The code.</param>
    /// <returns>The number of bytes the code used; zero only for empty input.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadCode(ReadOnlySpan<byte> bytes, out int code) => ReadCode(bytes, DefaultCidCodeLength, out code);

    /// <summary>
    /// Reads one character code by the codespace ranges. When none matches fully, the length of a range whose first
    /// byte matches is used, then the shortest range, then the fallback: one byte for simple fonts, two for CID fonts.
    /// </summary>
    /// <param name="bytes">The string bytes still to read.</param>
    /// <param name="fallbackLength">The code length when the CMap has no codespace ranges.</param>
    /// <param name="code">The code.</param>
    /// <returns>The number of bytes the code used; zero only for empty input.</returns>
    public int ReadCode(ReadOnlySpan<byte> bytes, int fallbackLength, out int code)
    {
        if (bytes.IsEmpty)
        {
            code = 0;
            return 0;
        }

        var length = _identity ? DefaultCidCodeLength : MatchLength(bytes, fallbackLength);
        length = Math.Clamp(length, 1, Math.Min(MaxCodeLength, bytes.Length));
        uint value = 0;
        foreach (var b in bytes[..length])
        {
            value = (value << ByteBits) | b;
        }

        code = (int)value;
        return length;
    }

    /// <summary>Maps a code to a CID: through the CID ranges, the base CMap, then the notdef ranges.</summary>
    /// <param name="code">The character code.</param>
    /// <returns>The CID, or zero when nothing maps the code.</returns>
    public int ToCid(int code)
    {
        if (_identity)
        {
            return code;
        }

        return TryGetCid((uint)code, out var cid) || TryGetNotdef((uint)code, out cid) ? cid : 0;
    }

    /// <summary>Parses an embedded CMap with base maps from one font resource owner.</summary>
    /// <param name="data">The decoded stream.</param>
    /// <param name="resources">The selected font resources.</param>
    /// <returns>The CMap.</returns>
    internal static CMap ParseWithResources(ReadOnlySpan<byte> data, PdfFontResources resources)
    {
        var content = CMapParser.Parse(data);
        var parent = content.HasUseCMap ? GetPredefined(content.UseCMap.Span) ?? PredefinedCMaps.Find(resources, content.UseCMap.Span)?.Map : null;
        return Create(content, parent);
    }

    /// <summary>Makes a CMap from tables already built, such as the predefined CMaps embedded in the library.</summary>
    /// <param name="codespaces">Every codespace range the CMap reads by, shortest first; the base CMap's are not added.</param>
    /// <param name="cids">The CID ranges.</param>
    /// <param name="parent">The base CMap looked up after <paramref name="cids"/>, or <see langword="null"/>.</param>
    /// <param name="writingMode">The writing mode: 0 for horizontal, 1 for vertical.</param>
    /// <returns>The CMap.</returns>
    internal static CMap Create(CodespaceRange[] codespaces, CodeRangeMap cids, CMap? parent, int writingMode) =>
        new(codespaces, cids, CodeRangeMap.Empty, parent, writingMode, false);

    /// <summary>Builds a CMap from parsed content and its resolved base map.</summary>
    /// <param name="content">The parsed mapping content.</param>
    /// <param name="parent">The resolved base map, if any.</param>
    /// <returns>The CMap.</returns>
    private static CMap Create(CMapContent content, CMap? parent)
    {
        var codespaces = new List<CodespaceRange>(content.Codespaces);
        if (parent is not null)
        {
            codespaces.AddRange(parent._codespaces);
        }

        codespaces.Sort(static (a, b) => a.Length.CompareTo(b.Length));
        return new([.. codespaces], content.Cids.Build(true), content.Notdefs.Build(false), parent, content.WritingMode, false);
    }

    /// <summary>Looks a code up in the CID ranges of this CMap and its bases.</summary>
    /// <param name="code">The code.</param>
    /// <param name="cid">The CID.</param>
    /// <returns><see langword="true"/> when mapped.</returns>
    private bool TryGetCid(uint code, out int cid)
    {
        if (_identity)
        {
            cid = (int)code;
            return true;
        }

        if (_cids.TryFind(code, out var offset, out var first))
        {
            cid = first + (int)offset;
            return true;
        }

        cid = 0;
        return _parent?.TryGetCid(code, out cid) == true;
    }

    /// <summary>Looks a code up in the notdef ranges of this CMap and its bases.</summary>
    /// <param name="code">The code.</param>
    /// <param name="cid">The notdef CID.</param>
    /// <returns><see langword="true"/> when a notdef range holds the code.</returns>
    private bool TryGetNotdef(uint code, out int cid)
    {
        if (_notdefs.TryFind(code, out _, out cid))
        {
            return true;
        }

        cid = 0;
        return _parent?.TryGetNotdef(code, out cid) == true;
    }

    /// <summary>Finds the length of the code at the start of some bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="fallbackLength">The length when there are no codespace ranges.</param>
    /// <returns>The length.</returns>
    private int MatchLength(ReadOnlySpan<byte> bytes, int fallbackLength)
    {
        foreach (var range in _codespaces)
        {
            if (range.Length <= bytes.Length && range.Matches(bytes))
            {
                return range.Length;
            }
        }

        foreach (var range in _codespaces)
        {
            if (range.MatchesFirst(bytes[0]))
            {
                return range.Length;
            }
        }

        return _codespaces.Length > 0 ? _codespaces[0].Length : fallbackLength;
    }
}
