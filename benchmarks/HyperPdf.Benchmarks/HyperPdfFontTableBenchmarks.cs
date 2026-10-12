// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Text;

namespace HyperPdf.Benchmarks;

/// <summary>Measures lookups over the built-in font, appearance and text tables.</summary>
public class HyperPdfFontTableBenchmarks
{
    /// <summary>The destination for one decoded glyph name.</summary>
    private const int UnicodeBufferLength = 32;

    /// <summary>The size of text in an appearance stream.</summary>
    private const float AppearanceFontSize = 12F;

    /// <summary>A standard string id near the start of the CFF table.</summary>
    private const int EarlyStandardString = 34;

    /// <summary>A glyph index near the start of the Macintosh order.</summary>
    private const int EarlyMacGlyph = 36;

    /// <summary>Known, composed, supplementary and unknown glyph names.</summary>
    private static readonly byte[][] GlyphListNames = ["A"u8.ToArray(), "uni20AC"u8.ToArray(), "f_f_i"u8.ToArray(), "u1F600"u8.ToArray(), "missingGlyphName"u8.ToArray()];

    /// <summary>Known and unknown names in the sorted name table.</summary>
    private static readonly byte[][] GlyphTableNames = ["A"u8.ToArray(), "Euro"u8.ToArray(), ".notdef"u8.ToArray(), "missingGlyphName"u8.ToArray()];

    /// <summary>The encodings sampled with their corresponding codes and names.</summary>
    private static readonly FontEncoding[] Encodings = [FontEncoding.Standard, FontEncoding.WinAnsi, FontEncoding.MacRoman, FontEncoding.ZapfDingbats, FontEncoding.Symbol];

    /// <summary>Codes sampled in each encoding.</summary>
    private static readonly byte[] EncodingCodes = [(byte)'A', (byte)'A', (byte)'a', (byte)'!', byte.MaxValue];

    /// <summary>Names searched in each encoding, including an absent name.</summary>
    private static readonly byte[][] EncodingNames = ["A"u8.ToArray(), "Euro"u8.ToArray(), "a"u8.ToArray(), "a1"u8.ToArray(), "missingGlyphName"u8.ToArray()];

    /// <summary>Standard fonts sampled with their corresponding glyph names.</summary>
    private static readonly StandardFont[] Fonts = [StandardFont.Helvetica, StandardFont.HelveticaBold, StandardFont.TimesRoman, StandardFont.Symbol];

    /// <summary>Glyph names searched in the corresponding standard fonts.</summary>
    private static readonly byte[][] WidthNames = ["A"u8.ToArray(), "space"u8.ToArray(), "Euro"u8.ToArray(), "missingGlyphName"u8.ToArray()];

    /// <summary>Valid and out-of-range CFF standard string ids.</summary>
    private static readonly int[] StandardStringIds = [0, EarlyStandardString, CffStandardData.StandardStringCount - 1, CffStandardData.StandardStringCount, -1];

    /// <summary>Valid and out-of-range Macintosh glyph indices.</summary>
    private static readonly int[] MacGlyphIndices = [0, EarlyMacGlyph, CffStandardData.MacGlyphCount - 1, CffStandardData.MacGlyphCount, -1];

    /// <summary>Appearance fonts sampled with their corresponding encoded text.</summary>
    private static readonly AppearanceFont[] AppearanceFonts = [AppearanceFont.Helvetica, AppearanceFont.HelveticaBold, AppearanceFont.TimesRoman, AppearanceFont.Courier];

    /// <summary>Text sampled with the corresponding appearance font.</summary>
    private static readonly byte[][] AppearanceCodes = ["AV"u8.ToArray(), "To"u8.ToArray(), "wm"u8.ToArray(), "A "u8.ToArray()];

    /// <summary>Latin, right-to-left, mirrored, ligature and decomposable characters.</summary>
    private static readonly char[] TextCharacters = ['A', '\u05D0', '\u0627', '(', ')', '\uFB03', '\u00E9', '\u4E2D'];

    /// <summary>Resolves glyph names to Unicode and individual code points.</summary>
    /// <returns>A sum of decoded characters and code points.</returns>
    [Benchmark]
    public int GlyphListLookup()
    {
        Span<char> text = stackalloc char[UnicodeBufferLength];
        var total = 0;
        foreach (var name in GlyphListNames)
        {
            if (GlyphList.TryGetUnicode(name, text, out var length))
            {
                total += length + text[0];
            }

            if (GlyphList.TryGetCodePoint(name, out var codePoint))
            {
                total += codePoint;
            }
        }

        return total;
    }

    /// <summary>Finds glyph name ids and reads their ASCII bytes.</summary>
    /// <returns>A sum of ids and name lengths.</returns>
    [Benchmark]
    public int GlyphNamesLookup()
    {
        var total = 0;
        foreach (var name in GlyphTableNames)
        {
            var id = GlyphNames.Find(name);
            total += id + GlyphNames.Get(id).Length;
        }

        return total;
    }

    /// <summary>Maps codes to glyph names and finds codes by name.</summary>
    /// <returns>A sum of code values and name lengths.</returns>
    [Benchmark]
    public int FontEncodingsLookup()
    {
        var total = 0;
        for (var i = 0; i < Encodings.Length; i++)
        {
            total += FontEncodings.GetGlyphName(Encodings[i], EncodingCodes[i]).Length;
            total += FontEncodings.GetCode(Encodings[i], EncodingNames[i]);
        }

        return total;
    }

    /// <summary>Looks up standard font widths and descriptor numbers.</summary>
    /// <returns>A sum of widths and metrics.</returns>
    [Benchmark]
    public float StandardFontsLookup()
    {
        float total = 0;
        for (var i = 0; i < Fonts.Length; i++)
        {
            var font = Fonts[i];
            if (StandardFonts.TryGetWidth(font, WidthNames[i], out var width))
            {
                total += width;
            }

            var metrics = StandardFonts.Get(font);
            total += metrics.Ascent + metrics.Descent + metrics.BoundingBox.Right;
        }

        return total;
    }

    /// <summary>Reads standard CFF strings and Macintosh glyph names.</summary>
    /// <returns>A sum of the returned name bytes.</returns>
    [Benchmark]
    public int CffStandardDataLookup()
    {
        var total = 0;
        foreach (var sid in StandardStringIds)
        {
            var name = CffStandardData.GetStandardString(sid);
            total += name.Length;
            if (!name.IsEmpty)
            {
                total += name[0];
            }
        }

        foreach (var index in MacGlyphIndices)
        {
            var name = CffStandardData.GetMacGlyphName(index);
            total += name.Length;
            if (!name.IsEmpty)
            {
                total += name[0];
            }
        }

        return total;
    }

    /// <summary>Measures WinAnsi text and reads an appearance glyph box.</summary>
    /// <returns>A sum of advances and glyph bounds.</returns>
    [Benchmark]
    public float AppearanceFontMetricsLookup()
    {
        float total = 0;
        for (var i = 0; i < AppearanceFonts.Length; i++)
        {
            var codes = AppearanceCodes[i];
            var font = AppearanceFonts[i];
            total += AppearanceFontMetrics.MeasureAdvance(font, codes, AppearanceFontSize);
            var box = AppearanceFontMetrics.GetGlyphBox(font, codes[0]);
            total += box.Left + box.Bottom + box.Right + box.Top;
        }

        return total;
    }

    /// <summary>Reads text direction, mirrored characters and decompositions.</summary>
    /// <returns>A sum of direction, mirror and decomposition values.</returns>
    [Benchmark]
    public int TextUnicodeLookup()
    {
        Span<char> decomposition = stackalloc char[TextUnicode.MaxDecomposition];
        var total = 0;
        foreach (var value in TextCharacters)
        {
            total += (int)TextUnicode.GetDirection(value);
            total += TextUnicode.GetMirror(value);
            var length = TextUnicode.Decompose(value, decomposition);
            total += length;
            if (length > 0)
            {
                total += decomposition[0];
            }
        }

        return total;
    }
}
