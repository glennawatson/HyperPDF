// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Text.Fonts;

/// <summary>
/// A font face loaded for writing text: its bytes, metrics, glyph advances, character map and pair kerning. An owner
/// can retain it so laying text out again reads nothing from disk.
/// </summary>
[DebuggerDisplay("FontProgram: {Face.Family} {Face.Style}")]
public sealed class FontProgram
{
    /// <summary>The offset of the units per em in the font header.</summary>
    private const int HeadUnitsPerEm = 18;

    /// <summary>The offset of the location format in the font header.</summary>
    private const int HeadLocaFormat = 50;

    /// <summary>The font header's bytes up to and including the location format.</summary>
    private const int HeadBytes = 52;

    /// <summary>The offset of the ascender in the horizontal header.</summary>
    private const int HheaAscender = 4;

    /// <summary>The offset of the descender in the horizontal header.</summary>
    private const int HheaDescender = 6;

    /// <summary>The offset of the line gap in the horizontal header.</summary>
    private const int HheaLineGap = 8;

    /// <summary>The offset of the metric count in the horizontal header.</summary>
    private const int HheaMetricCount = 34;

    /// <summary>The horizontal header's bytes.</summary>
    private const int HheaBytes = 36;

    /// <summary>The offset of the glyph count in the maximum profile.</summary>
    private const int MaxpGlyphs = 4;

    /// <summary>The maximum profile's bytes up to and including the glyph count.</summary>
    private const int MaxpBytes = 6;

    /// <summary>The bytes of one long horizontal metric.</summary>
    private const int MetricBytes = 4;

    /// <summary>The offset of the underline position in the PostScript table.</summary>
    private const int PostUnderlinePosition = 8;

    /// <summary>The offset of the underline thickness in the PostScript table.</summary>
    private const int PostUnderlineThickness = 10;

    /// <summary>The PostScript table's bytes up to and including the underline thickness.</summary>
    private const int PostBytes = 12;

    /// <summary>The bytes of the kerning table header.</summary>
    private const int KernHeaderBytes = 4;

    /// <summary>The bytes of a kerning subtable header.</summary>
    private const int KernSubtableBytes = 6;

    /// <summary>The bytes before a format 0 subtable's pairs.</summary>
    private const int KernPairsOffset = 14;

    /// <summary>The bytes of one kerning pair.</summary>
    private const int KernPairBytes = 6;

    /// <summary>The offset of a subtable's length.</summary>
    private const int KernLength = 2;

    /// <summary>The offset of a subtable's coverage.</summary>
    private const int KernCoverage = 4;

    /// <summary>The offset of a format 0 subtable's pair count.</summary>
    private const int KernPairCount = 6;

    /// <summary>The offset of a pair's value.</summary>
    private const int KernValue = 4;

    /// <summary>The horizontal bit of a kerning subtable's coverage.</summary>
    private const int KernHorizontal = 1;

    /// <summary>The coverage bits other than the format that a plain horizontal kerning subtable leaves clear.</summary>
    private const int KernOtherFlags = 0x0E;

    /// <summary>The bits of a kerning subtable's format in its coverage.</summary>
    private const int KernFormatShift = 8;

    /// <summary>The bits a glyph index takes in a kerning key.</summary>
    private const int GlyphBits = 16;

    /// <summary>The bytes of a 16-bit value.</summary>
    private const int Int16Bytes = 2;

    /// <summary>The units per em assumed when a header is damaged.</summary>
    private const int DefaultUnitsPerEm = 1000;

    /// <summary>The share of an em an underline sits below the baseline when the font does not say.</summary>
    private const float DefaultUnderlinePosition = -0.1F;

    /// <summary>The share of an em an underline is thick when the font does not say.</summary>
    private const float DefaultUnderlineThickness = 0.05F;

    /// <summary>The share of an em the ascent is when the font does not say.</summary>
    private const float DefaultAscent = 0.8F;

    /// <summary>The share of an em the descent is when the font does not say.</summary>
    private const float DefaultDescent = -0.2F;

    /// <summary>The advance of every glyph, in font units.</summary>
    private readonly ushort[] _advances;

    /// <summary>The kerning of glyph pairs, in font units, keyed by left glyph then right glyph.</summary>
    private readonly Dictionary<uint, short> _kerning;

    /// <summary>Initializes a new instance of the <see cref="FontProgram"/> class.</summary>
    /// <param name="face">The face.</param>
    /// <param name="data">The font file.</param>
    /// <param name="directory">The face's tables.</param>
    private FontProgram(FontFace face, byte[] data, SfntDirectory directory)
    {
        Face = face;
        Data = data;
        Directory = directory;
        var head = directory.Table(data, SfntTag.Head);
        var hhea = directory.Table(data, SfntTag.Hhea);
        var maxp = directory.Table(data, SfntTag.Maxp);
        UnitsPerEm = head.Length >= HeadBytes && BinaryPrimitives.ReadUInt16BigEndian(head[HeadUnitsPerEm..]) is > 0 and var units ? units : DefaultUnitsPerEm;
        IsLongLoca = head.Length >= HeadBytes && BinaryPrimitives.ReadInt16BigEndian(head[HeadLocaFormat..]) != 0;
        GlyphCount = maxp.Length >= MaxpBytes ? BinaryPrimitives.ReadUInt16BigEndian(maxp[MaxpGlyphs..]) : 0;
        ReadVerticalMetrics(hhea);
        ReadUnderline(directory.Table(data, SfntTag.Post));
        _advances = ReadAdvances(directory.Table(data, SfntTag.Hmtx), hhea, GlyphCount);
        _kerning = ReadKerning(directory.Table(data, SfntTag.Kern));
        _ = directory.TryFind(SfntTag.Cmap, out var cmapOffset, out var cmapLength);
        CharacterMap = new(data, cmapOffset, cmapLength);
    }

    /// <summary>Gets the face.</summary>
    public FontFace Face { get; }

    /// <summary>Gets the font file's bytes; a collection's bytes hold every face.</summary>
    public byte[] Data { get; }

    /// <summary>Gets the font units in one em.</summary>
    public int UnitsPerEm { get; }

    /// <summary>Gets the number of glyphs.</summary>
    public int GlyphCount { get; }

    /// <summary>Gets the height above the baseline, as a share of an em.</summary>
    public float Ascent { get; private set; } = DefaultAscent;

    /// <summary>Gets the depth below the baseline, as a negative share of an em.</summary>
    public float Descent { get; private set; } = DefaultDescent;

    /// <summary>Gets the gap the font asks for between lines, as a share of an em.</summary>
    public float LineGap { get; private set; }

    /// <summary>Gets where the underline sits, as a negative share of an em below the baseline.</summary>
    public float UnderlinePosition { get; private set; } = DefaultUnderlinePosition;

    /// <summary>Gets how thick the underline is, as a share of an em.</summary>
    public float UnderlineThickness { get; private set; } = DefaultUnderlineThickness;

    /// <summary>Gets the face's tables.</summary>
    internal SfntDirectory Directory { get; }

    /// <summary>Gets the character map.</summary>
    internal CharacterMap CharacterMap { get; }

    /// <summary>Gets a value indicating whether glyph locations are 32-bit.</summary>
    internal bool IsLongLoca { get; }

    /// <summary>Loads a face from its file.</summary>
    /// <param name="face">The face.</param>
    /// <returns>The program, or <see langword="null"/> when the file cannot be read as a font.</returns>
    public static FontProgram? Load(FontFace face)
    {
        ArgumentNullException.ThrowIfNull(face);
        try
        {
            return FromBytes(face, File.ReadAllBytes(face.Path));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Reads a face from a font file's bytes, without caching it.</summary>
    /// <param name="face">The face; its path is only recorded.</param>
    /// <param name="data">The font file.</param>
    /// <returns>The program, or <see langword="null"/> when the bytes are not a font with that face.</returns>
    public static FontProgram? FromBytes(FontFace face, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(face);
        ArgumentNullException.ThrowIfNull(data);
        var offset = SfntDirectory.FaceOffset(data, face.FaceIndex);
        if (offset < 0 || offset >= data.Length)
        {
            return null;
        }

        var directory = SfntDirectory.Read(data.AsSpan((int)offset), data.Length);
        return directory is null || !directory.TryFind(SfntTag.Head, out _, out _) ? null : new(face, data, directory);
    }

    /// <summary>Gets the glyph for a character.</summary>
    /// <param name="codePoint">The Unicode code point.</param>
    /// <returns>The glyph, or 0 when the font lacks the character.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GlyphFor(int codePoint) => CharacterMap.Lookup(codePoint);

    /// <summary>Appends every character in a range the font has, with its glyph.</summary>
    /// <param name="first">The first code point.</param>
    /// <param name="last">The last code point.</param>
    /// <param name="output">Receives the characters and glyphs.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MapCharacters(int first, int last, List<(int CodePoint, ushort Glyph)> output) => CharacterMap.Map(first, last, output);

    /// <summary>Gets a glyph's advance in font units.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The advance.</returns>
    public int Advance(ushort glyph) => glyph < _advances.Length ? _advances[glyph] : 0;

    /// <summary>Gets the kerning between two glyphs, in font units, from the font's <c>kern</c> table.</summary>
    /// <param name="left">The left glyph.</param>
    /// <param name="right">The right glyph.</param>
    /// <returns>The adjustment; negative pulls the glyphs together.</returns>
    public int Kerning(ushort left, ushort right) => _kerning.Count == 0 ? 0 : _kerning.GetValueOrDefault(((uint)left << GlyphBits) | right);

    /// <summary>Reads every glyph's advance, repeating the last for glyphs past the long metrics.</summary>
    /// <param name="hmtx">The metrics table.</param>
    /// <param name="hhea">The horizontal header.</param>
    /// <param name="glyphs">The glyph count.</param>
    /// <returns>The advances.</returns>
    private static ushort[] ReadAdvances(ReadOnlySpan<byte> hmtx, ReadOnlySpan<byte> hhea, int glyphs)
    {
        var advances = new ushort[glyphs];
        var metrics = hhea.Length >= HheaBytes ? Math.Min(BinaryPrimitives.ReadUInt16BigEndian(hhea[HheaMetricCount..]), hmtx.Length / MetricBytes) : 0;
        ushort last = 0;
        for (var glyph = 0; glyph < glyphs; glyph++)
        {
            if (glyph < metrics)
            {
                last = BinaryPrimitives.ReadUInt16BigEndian(hmtx[(glyph * MetricBytes)..]);
            }

            advances[glyph] = last;
        }

        return advances;
    }

    /// <summary>Reads the horizontal pairs of a version 0 kerning table.</summary>
    /// <param name="kern">The table.</param>
    /// <returns>The pairs.</returns>
    private static Dictionary<uint, short> ReadKerning(ReadOnlySpan<byte> kern)
    {
        var pairs = new Dictionary<uint, short>();
        if (kern.Length < KernHeaderBytes || BinaryPrimitives.ReadUInt16BigEndian(kern) != 0)
        {
            return pairs;
        }

        var tables = BinaryPrimitives.ReadUInt16BigEndian(kern[Int16Bytes..]);
        var at = KernHeaderBytes;
        for (var i = 0; i < tables && at + KernSubtableBytes <= kern.Length; i++)
        {
            var length = BinaryPrimitives.ReadUInt16BigEndian(kern[(at + KernLength)..]);
            var coverage = BinaryPrimitives.ReadUInt16BigEndian(kern[(at + KernCoverage)..]);
            if ((coverage & KernHorizontal) != 0 && (coverage & KernOtherFlags) == 0 && coverage >> KernFormatShift == 0)
            {
                ReadPairs(kern[at..], pairs);
            }

            at += Math.Max((int)length, KernSubtableBytes);
        }

        return pairs;
    }

    /// <summary>Reads the pairs of a format 0 kerning subtable.</summary>
    /// <param name="subtable">The subtable.</param>
    /// <param name="pairs">Receives the pairs.</param>
    private static void ReadPairs(ReadOnlySpan<byte> subtable, Dictionary<uint, short> pairs)
    {
        if (subtable.Length < KernPairsOffset)
        {
            return;
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(subtable[KernPairCount..]);
        for (var p = 0; p < count && KernPairsOffset + ((p + 1) * KernPairBytes) <= subtable.Length; p++)
        {
            var pair = subtable.Slice(KernPairsOffset + (p * KernPairBytes), KernPairBytes);
            var key = ((uint)BinaryPrimitives.ReadUInt16BigEndian(pair) << GlyphBits) | BinaryPrimitives.ReadUInt16BigEndian(pair[Int16Bytes..]);
            pairs[key] = BinaryPrimitives.ReadInt16BigEndian(pair[KernValue..]);
        }
    }

    /// <summary>Reads the ascent, descent and line gap from the horizontal header.</summary>
    /// <param name="hhea">The horizontal header.</param>
    private void ReadVerticalMetrics(ReadOnlySpan<byte> hhea)
    {
        if (hhea.Length < HheaBytes)
        {
            return;
        }

        var ascender = BinaryPrimitives.ReadInt16BigEndian(hhea[HheaAscender..]);
        var descender = BinaryPrimitives.ReadInt16BigEndian(hhea[HheaDescender..]);
        if (ascender <= 0)
        {
            return;
        }

        Ascent = ascender / (float)UnitsPerEm;
        Descent = Math.Min(descender, (short)0) / (float)UnitsPerEm;
        LineGap = Math.Max(BinaryPrimitives.ReadInt16BigEndian(hhea[HheaLineGap..]), (short)0) / (float)UnitsPerEm;
    }

    /// <summary>Reads the underline position and thickness from the PostScript table.</summary>
    /// <param name="post">The table.</param>
    private void ReadUnderline(ReadOnlySpan<byte> post)
    {
        if (post.Length < PostBytes)
        {
            return;
        }

        var thickness = BinaryPrimitives.ReadInt16BigEndian(post[PostUnderlineThickness..]);
        if (thickness <= 0)
        {
            return;
        }

        UnderlinePosition = BinaryPrimitives.ReadInt16BigEndian(post[PostUnderlinePosition..]) / (float)UnitsPerEm;
        UnderlineThickness = thickness / (float)UnitsPerEm;
    }
}
