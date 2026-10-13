// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>
/// A Compact Font Format program: a bare FontFile3 (Type1C or CIDFontType0C) or the 'CFF ' table of an OpenType font.
/// Outlines run through a Type 2 charstring interpreter. Damaged data gives empty outlines rather than errors.
/// </summary>
[DebuggerDisplay("CffProgram: {GlyphCount} glyphs")]
public sealed class CffProgram : FontProgram
{
    /// <summary>The CFF major version this reader supports.</summary>
    private const int SupportedMajor = 1;

    /// <summary>The offset of the header size in the header.</summary>
    private const int HeaderSizeOffset = 2;

    /// <summary>The smallest valid header.</summary>
    private const int MinHeaderSize = 4;

    /// <summary>The largest number of Font DICTs read from an FDArray.</summary>
    private const int MaxFontDicts = 256;

    /// <summary>The font data.</summary>
    private readonly ReadOnlyMemory<byte> _data;

    /// <summary>The CharStrings INDEX.</summary>
    private readonly CffIndex _charStrings;

    /// <summary>The String INDEX.</summary>
    private readonly CffIndex _strings;

    /// <summary>The Global Subr INDEX.</summary>
    private readonly CffIndex _globalSubrs;

    /// <summary>The Private DICT of each Font DICT; one entry for a font that is not CID-keyed.</summary>
    private readonly CffPrivate[] _privates;

    /// <summary>The Font DICT of each glyph, for CID-keyed fonts.</summary>
    private readonly byte[]? _fontDictSelect;

    /// <summary>The SID of each glyph, or its CID in a CID-keyed font.</summary>
    private readonly ushort[] _charset;

    /// <summary>The glyph of each CID in a CID-keyed font, zero when unmapped.</summary>
    private readonly ushort[]? _cidToGlyph;

    /// <summary>The glyph of each code of the built-in encoding, -1 when unmapped.</summary>
    private readonly short[]? _encoding;

    /// <summary>Initializes a new instance of the <see cref="CffProgram"/> class.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="top">The Top DICT.</param>
    /// <param name="indexes">The CharStrings, String and Global Subr INDEXes.</param>
    private CffProgram(ReadOnlyMemory<byte> data, in CffTopDict top, in CffIndexes indexes)
    {
        _data = data;
        var span = data.Span;
        (_charStrings, _strings, _globalSubrs) = indexes;
        GlyphCount = _charStrings.Count;
        IsCidKeyed = top.IsCid;
        BoundingBox = top.BoundingBox;
        var matrix = top.Matrix ?? FontMatrix.Default;
        if (top.IsCid)
        {
            (_privates, var fontMatrix) = ReadFontDicts(span, top.FdArray);
            matrix = fontMatrix is { } inner && top.Matrix is { } outer ? inner.Multiply(outer) : fontMatrix ?? matrix;
            _fontDictSelect = CffCharsets.ReadFdSelect(span, top.FdSelect, GlyphCount, _privates.Length);
        }
        else
        {
            _privates = [CffPrivate.Read(span, top.PrivateSize, top.PrivateOffset)];
        }

        FontMatrix = matrix;
        _charset = CffCharsets.ReadCharset(span, top.Charset, GlyphCount, top.IsCid);
        if (top.IsCid)
        {
            _cidToGlyph = CffCharsets.InvertCharset(_charset);
        }
        else
        {
            _encoding = CffCharsets.ReadEncoding(span, top.Encoding, this);
        }
    }

    /// <inheritdoc/>
    public override int GlyphCount { get; }

    /// <inheritdoc/>
    public override FontMatrix FontMatrix { get; }

    /// <inheritdoc/>
    public override float UnitsPerEm => FontMatrix.A > 0 ? MathF.Round(1 / FontMatrix.A) : MathF.Round(1 / FontMatrix.Default.A);

    /// <inheritdoc/>
    public override PdfRectangle BoundingBox { get; }

    /// <summary>Gets a value indicating whether the font is CID-keyed.</summary>
    public bool IsCidKeyed { get; }

    /// <summary>Parses a CFF font.</summary>
    /// <param name="data">The CFF data; the program keeps a reference to it.</param>
    /// <param name="program">The program.</param>
    /// <returns><see langword="true"/> when the data is a CFF version 1 font with charstrings.</returns>
    public static bool TryParse(ReadOnlyMemory<byte> data, [NotNullWhen(true)] out CffProgram? program)
    {
        program = null;
        var span = data.Span;
        if (span.Length < MinHeaderSize || span[0] != SupportedMajor)
        {
            return false;
        }

        _ = CffIndex.Read(span, span[HeaderSizeOffset], out var position);
        var topDicts = CffIndex.Read(span, position, out position);
        var strings = CffIndex.Read(span, position, out position);
        var globalSubrs = CffIndex.Read(span, position, out _);
        var top = CffTopDict.Read(topDicts.Get(span, 0));
        var charStrings = top.CharStrings > 0 ? CffIndex.Read(span, top.CharStrings, out _) : default;
        if (charStrings.Count == 0)
        {
            return false;
        }

        program = new(data, top, new CffIndexes(charStrings, strings, globalSubrs));
        return true;
    }

    /// <inheritdoc/>
    public override void DecodeGlyph<TSink>(int glyph, ref TSink sink)
    {
        var outcome = RunCharstring(glyph, 0, 0, false, ref sink);
        if (!outcome.HasSeac || IsCidKeyed)
        {
            return;
        }

        _ = RunCharstring(GetStandardGlyph(outcome.BaseCode), 0, 0, false, ref sink);
        _ = RunCharstring(GetStandardGlyph(outcome.AccentCode), outcome.AccentX, outcome.AccentY, false, ref sink);
    }

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> GetGlyphName(int glyph)
    {
        if (IsCidKeyed || (uint)glyph >= (uint)_charset.Length)
        {
            return [];
        }

        int sid = _charset[glyph];
        return sid < CffStandardData.StandardStringCount
            ? CffStandardData.GetStandardString(sid)
            : _strings.Get(_data.Span, sid - CffStandardData.StandardStringCount);
    }

    /// <summary>Finds a glyph through the built-in encoding; a CID-keyed font treats the code as a CID.</summary>
    /// <param name="code">The character code.</param>
    /// <returns>The glyph id, or -1.</returns>
    public override int GetGlyphByCharCode(int code)
    {
        if (IsCidKeyed)
        {
            return GetGlyphByCid(code);
        }

        return _encoding is not null && (uint)code < (uint)_encoding.Length ? _encoding[code] : NotFound;
    }

    /// <summary>Finds the glyph of a CID. A font that is not CID-keyed uses the CID as the glyph id.</summary>
    /// <param name="cid">The CID.</param>
    /// <returns>The glyph id, or -1.</returns>
    public int GetGlyphByCid(int cid)
    {
        if (_cidToGlyph is null)
        {
            return (uint)cid < (uint)GlyphCount ? cid : NotFound;
        }

        if ((uint)cid >= (uint)_cidToGlyph.Length)
        {
            return NotFound;
        }

        var glyph = _cidToGlyph[cid];
        return glyph != 0 || cid == 0 ? glyph : NotFound;
    }

    /// <summary>Gets a glyph's width from its charstring, or the default width when it gives none.</summary>
    /// <param name="glyph">The glyph id.</param>
    /// <returns>The width in font units.</returns>
    public override float GetAdvanceWidth(int glyph)
    {
        var sink = default(NullOutlineSink);
        return RunCharstring(glyph, 0, 0, true, ref sink).Width;
    }

    /// <summary>Finds the glyph a charset gives a SID.</summary>
    /// <param name="sid">The SID.</param>
    /// <returns>The lowest glyph id, or -1.</returns>
    internal int GetGlyphBySid(int sid) => (uint)sid <= ushort.MaxValue ? Array.IndexOf(_charset, (ushort)sid) : NotFound;

    /// <summary>Finds the glyph a StandardEncoding code names, as the accented-character form of endchar uses.</summary>
    /// <param name="code">The StandardEncoding code.</param>
    /// <returns>The glyph id, or -1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetStandardGlyph(int code) => GetGlyphByName(FontEncodings.GetGlyphName(FontEncoding.Standard, code));

    /// <inheritdoc/>
    private protected override int GlyphOfCid(int cid) => GetGlyphByCid(cid);

    /// <summary>Reads the Font DICTs of a CID-keyed font.</summary>
    /// <param name="data">The CFF data.</param>
    /// <param name="fontDictArray">The offset of the FDArray INDEX.</param>
    /// <returns>The Private DICT of each Font DICT, and the first Font DICT's matrix.</returns>
    private static CffFontDicts ReadFontDicts(ReadOnlySpan<byte> data, int fontDictArray)
    {
        var index = fontDictArray > 0 ? CffIndex.Read(data, fontDictArray, out _) : default;
        var count = Math.Clamp(index.Count, 1, MaxFontDicts);
        var privates = new CffPrivate[count];
        FontMatrix? matrix = null;
        for (var i = 0; i < count; i++)
        {
            var fontDict = CffTopDict.Read(index.Get(data, i));
            privates[i] = CffPrivate.Read(data, fontDict.PrivateSize, fontDict.PrivateOffset);
            matrix ??= fontDict.Matrix;
        }

        return new(privates, matrix);
    }

    /// <summary>Gets the Private DICT values that apply to a glyph.</summary>
    /// <param name="glyph">The glyph id.</param>
    /// <returns>The values.</returns>
    private CffPrivate GetPrivate(int glyph)
    {
        var dict = _fontDictSelect is not null && (uint)glyph < (uint)_fontDictSelect.Length ? _fontDictSelect[glyph] : 0;
        return dict < _privates.Length ? _privates[dict] : _privates[0];
    }

    /// <summary>Runs a glyph's charstring.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="glyph">The glyph id.</param>
    /// <param name="x">The starting x.</param>
    /// <param name="y">The starting y.</param>
    /// <param name="widthOnly">Whether to stop once the width is known.</param>
    /// <param name="sink">The sink.</param>
    /// <returns>The width and any accented-character request; empty for an unknown glyph.</returns>
    private CharstringOutcome RunCharstring<TSink>(int glyph, float x, float y, bool widthOnly, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var span = _data.Span;
        var charstring = _charStrings.Get(span, glyph);
        if (charstring.IsEmpty)
        {
            return default;
        }

        // The scratch is pooled rather than stack allocated: a sink may be a ref struct, and the compiler cannot prove
        // that stack memory held by the interpreter would not escape into it.
        var scratch = ArrayPool<float>.Shared.Rent(CharstringLimits.ScratchSize);
        try
        {
            var interpreter = new Type2Interpreter(span, _globalSubrs, GetPrivate(glyph), widthOnly, scratch);
            interpreter.Run(charstring, x, y, ref sink);
            return interpreter.Outcome;
        }
        finally
        {
            ArrayPool<float>.Shared.Return(scratch);
        }
    }
}
