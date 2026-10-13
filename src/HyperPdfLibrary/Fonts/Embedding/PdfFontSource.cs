// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// A TrueType or OpenType font file that can go into a PDF: one face of the file, its licence and its metrics. Open it
/// with <see cref="Open(byte[], int)"/>, which refuses a font that must not be embedded and says why. The same source can
/// be embedded in any number of documents.
/// </summary>
[DebuggerDisplay("PdfFontSource: {PostScriptName} ({Outlines})")]
public sealed class PdfFontSource
{
    /// <summary>Thousandths in one em, the unit of PDF glyph widths.</summary>
    internal const float EmDivisor = 1000F;

    /// <summary>Thousandths in one em, the unit of PDF glyph widths.</summary>
    private const float Thousand = EmDivisor;

    /// <summary>The offset of the metric count in /vhea.</summary>
    private const int VheaMetricCount = 34;

    /// <summary>The font face.</summary>
    private readonly SfntFont _font;

    /// <summary>The face's facts.</summary>
    private readonly FontFacts _facts;

    /// <summary>The lock that guards the lazily made character map reader.</summary>
    private readonly Lock _gate = new();

    /// <summary>The reader that maps characters to glyphs, made on first use.</summary>
    private TrueTypeProgram? _program;

    /// <summary>The shaper used when none is named, made on first use.</summary>
    private PdfOpenTypeShaper? _shaper;

    /// <summary>Initializes a new instance of the <see cref="PdfFontSource"/> class.</summary>
    /// <param name="font">The face.</param>
    /// <param name="facts">The face's facts.</param>
    /// <param name="outlines">The kind of outlines.</param>
    private PdfFontSource(SfntFont font, FontFacts facts, PdfFontOutlines outlines)
    {
        _font = font;
        _facts = facts;
        Outlines = outlines;
    }

    /// <summary>Gets the kind of outlines the font has.</summary>
    public PdfFontOutlines Outlines { get; }

    /// <summary>Gets the PostScript name, cleaned for use as a PDF name.</summary>
    public string PostScriptName => _facts.PostScriptName;

    /// <summary>Gets the family name.</summary>
    public string FamilyName => _facts.Family;

    /// <summary>Gets the index of the face in the file.</summary>
    public int FaceIndex => _font.FaceIndex;

    /// <summary>Gets the number of faces in the file; more than one for a TrueType collection.</summary>
    public int FaceCount => _font.FaceCount;

    /// <summary>Gets the number of glyphs.</summary>
    public int GlyphCount => _facts.GlyphCount;

    /// <summary>Gets the design units in one em.</summary>
    public int UnitsPerEm => _facts.UnitsPerEm;

    /// <summary>Gets the licence's embedding flags (OS/2 <c>fsType</c>); zero when the font has no licence table.</summary>
    public int EmbeddingFlags => _facts.EmbeddingFlags;

    /// <summary>Gets a value indicating whether the licence lets the embedded font keep only the glyphs used; otherwise the whole font is embedded.</summary>
    public bool CanSubset => FontLicence.CanSubset(_facts.EmbeddingFlags);

    /// <summary>Gets a value indicating whether the face is bold.</summary>
    public bool IsBold => (_facts.Style & FaceStyleFlags.Bold) != 0;

    /// <summary>Gets a value indicating whether the face is italic or oblique.</summary>
    public bool IsItalic => (_facts.Style & FaceStyleFlags.Italic) != 0;

    /// <summary>Gets a value indicating whether every glyph has the same advance.</summary>
    public bool IsFixedPitch => (_facts.Style & FaceStyleFlags.FixedPitch) != 0;

    /// <summary>Gets a value indicating whether the font has vertical metrics ('vmtx') for vertical writing.</summary>
    public bool HasVerticalMetrics => _font.Has(SfntTags.Vmtx) && _font.Has(SfntTags.Vhea);

    /// <summary>Gets the height above the baseline in thousandths of an em.</summary>
    public float Ascent => _facts.Ascent * Thousand / _facts.UnitsPerEm;

    /// <summary>Gets the depth below the baseline in thousandths of an em, negative.</summary>
    public float Descent => _facts.Descent * Thousand / _facts.UnitsPerEm;

    /// <summary>Gets the shaper used for this font when the options name none: one that reads the font's own layout tables, made on first use.</summary>
    internal IPdfGlyphShaper DefaultShaper
    {
        get
        {
            lock (_gate)
            {
                return _shaper ??= new PdfOpenTypeShaper(this);
            }
        }
    }

    /// <summary>Gets the face.</summary>
    internal SfntFont Font => _font;

    /// <summary>Gets the face's facts.</summary>
    internal FontFacts Facts => _facts;

    /// <summary>Opens the first face of a font file.</summary>
    /// <param name="fontFile">The font file's bytes; the source keeps a reference to them.</param>
    /// <returns>The font, or the plain reason it cannot be embedded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fontFile"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFontOpenResult Open(byte[] fontFile) => Open(fontFile, 0);

    /// <summary>Opens one face of a font file.</summary>
    /// <param name="fontFile">The font file's bytes; the source keeps a reference to them.</param>
    /// <param name="faceIndex">The face's index in a TrueType collection; zero for a single font.</param>
    /// <returns>The font, or the plain reason it cannot be embedded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fontFile"/> is <see langword="null"/>.</exception>
    public static PdfFontOpenResult Open(byte[] fontFile, int faceIndex)
    {
        ArgumentNullException.ThrowIfNull(fontFile);
        if (!SfntFont.TryRead(fontFile, faceIndex, out var font, out var refusal))
        {
            return new(null, refusal);
        }

        if (Refuse(font) is { } outlineRefusal)
        {
            return new(null, outlineRefusal);
        }

        if (!FontFactsReader.TryRead(font, out var facts, out refusal))
        {
            return new(null, refusal);
        }

        if (FontLicence.Refusal(facts.EmbeddingFlags) is { } licence)
        {
            return new(null, licence);
        }

        var outlines = font.Has(SfntTags.Glyf) && font.Has(SfntTags.Loca) ? PdfFontOutlines.TrueType : PdfFontOutlines.CompactFontFormat;
        return new(new PdfFontSource(font, facts, outlines), null);
    }

    /// <summary>Gets the glyph a character is drawn with.</summary>
    /// <param name="codePoint">The Unicode code point.</param>
    /// <returns>The glyph, or zero (the missing glyph) when the font has none for it.</returns>
    public int GetGlyph(int codePoint)
    {
        var program = GetProgram();
        return program is null ? 0 : program.LookupUnicodeCmap(codePoint);
    }

    /// <summary>Gets a glyph's horizontal advance.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The advance in thousandths of an em.</returns>
    public float GetAdvance(int glyph) => GetDesignAdvance(glyph) * Thousand / _facts.UnitsPerEm;

    /// <summary>Gets the CID that shows each glyph when the whole font is embedded as an OpenType file: the glyph number, or for a CID-keyed font the CID its charset gives.</summary>
    /// <returns>The CID of each glyph; <see langword="null"/> when the font has no CFF outlines.</returns>
    internal int[]? GetWholeFontCids()
    {
        if (Outlines != PdfFontOutlines.CompactFontFormat || !CffFaceReader.TryRead(_font.TableMemory(SfntTags.Cff), out var face))
        {
            return null;
        }

        var cids = new int[GlyphCount];
        for (var i = 0; i < cids.Length; i++)
        {
            cids[i] = face.IsCid && i < face.Charset.Length ? face.Charset[i] : i;
        }

        return cids;
    }

    /// <summary>Gets a glyph's advance along a writing direction.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <param name="mode">The writing direction.</param>
    /// <returns>The advance in thousandths of an em; for vertical writing the 'vmtx' advance, or one em when the font has no vertical metrics.</returns>
    internal float GetAdvanceAlong(int glyph, PdfWritingMode mode) =>
        mode == PdfWritingMode.Vertical ? GetDesignVerticalAdvance(glyph) * Thousand / _facts.UnitsPerEm : GetAdvance(glyph);

    /// <summary>Gets a glyph's vertical advance in design units.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The advance, or one em when the font has no vertical metrics.</returns>
    internal int GetDesignVerticalAdvance(int glyph)
    {
        var count = VerticalMetricCount();
        return count > 0 ? SfntMetrics.Advance(_font.Table(SfntTags.Vmtx), count, glyph) : _facts.UnitsPerEm;
    }

    /// <summary>Gets the gap between a glyph's vertical origin line and the top of its outline in design units.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The top side bearing; zero when the font has no vertical metrics.</returns>
    internal int GetTopSideBearing(int glyph)
    {
        var count = VerticalMetricCount();
        return count > 0 ? SfntMetrics.Bearing(_font.Table(SfntTags.Vmtx), count, glyph) : 0;
    }

    /// <summary>Gets a glyph's horizontal advance in design units.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The advance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetDesignAdvance(int glyph) => SfntMetrics.Advance(_font.Table(SfntTags.Hmtx), _facts.MetricCount, glyph);

    /// <summary>Explains why a face's outlines cannot be embedded.</summary>
    /// <param name="font">The face.</param>
    /// <returns>The plain reason, or <see langword="null"/> when the outlines can be embedded.</returns>
    private static string? Refuse(SfntFont font)
    {
        if (font.Has(SfntTags.Cff2))
        {
            return "The font uses CFF2 outlines, which PDF cannot embed. Use a static font instead.";
        }

        if (font.Has(SfntTags.Fvar))
        {
            return "The font is a variable font, which PDF cannot embed. Use a static instance of it instead.";
        }

        var hasTrueType = font.Has(SfntTags.Glyf) && font.Has(SfntTags.Loca);
        return hasTrueType || font.Has(SfntTags.Cff) ? null : "The font has no outlines to embed (it holds only bitmaps).";
    }

    /// <summary>Gets the number of full entries in the vertical metrics.</summary>
    /// <returns>The count; zero when the font has no vertical metrics.</returns>
    private int VerticalMetricCount() => HasVerticalMetrics ? FontBytes.U16(_font.Table(SfntTags.Vhea), VheaMetricCount) : 0;

    /// <summary>Gets the reader that maps characters to glyphs.</summary>
    /// <returns>The reader, or <see langword="null"/> when the face cannot be read.</returns>
    private TrueTypeProgram? GetProgram()
    {
        lock (_gate)
        {
            if (_program is null)
            {
                _ = TrueTypeProgram.TryParse(_font.ToStandalone(), out _program);
            }

            return _program;
        }
    }
}
