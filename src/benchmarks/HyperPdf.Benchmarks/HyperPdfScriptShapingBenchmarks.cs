// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Engine;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdf.Benchmarks;

/// <summary>Shapes a word of Khmer, Myanmar, Sinhala, Mongolian and Tibetan with the fonts that ship with the app, once the shapers and their plans exist.</summary>
public class HyperPdfScriptShapingBenchmarks
{
    /// <summary>A Khmer word with a subscript ro and a vowel sign written before the base.</summary>
    private const string Khmer = "ក្រេកើ";

    /// <summary>A Myanmar word with a kinzi, a medial ra and a vowel sign written before the base.</summary>
    private const string Myanmar = "င်္ကမြကေ";

    /// <summary>A Sinhala word with vowel signs before and around the consonant, and a conjunct.</summary>
    private const string Sinhala = "කෙකොක්‍ෂ";

    /// <summary>A Mongolian word, whose letters join.</summary>
    private const string Mongolian = "ᠠᠡᠢᠣᠤ";

    /// <summary>A Tibetan word with a stack.</summary>
    private const string Tibetan = "བོདཀྵ";

    /// <summary>The shaped glyphs, reused so the benchmark does not allocate them.</summary>
    private readonly List<PdfShapedGlyph> _glyphs = [];

    /// <summary>The Khmer shaper.</summary>
    private PdfOpenTypeShaper _khmer = null!;

    /// <summary>The Myanmar shaper.</summary>
    private PdfOpenTypeShaper _myanmar = null!;

    /// <summary>The Sinhala shaper.</summary>
    private PdfOpenTypeShaper _sinhala = null!;

    /// <summary>The Mongolian shaper.</summary>
    private PdfOpenTypeShaper _mongolian = null!;

    /// <summary>The Tibetan shaper.</summary>
    private PdfOpenTypeShaper _tibetan = null!;

    /// <summary>Opens the bundled fonts and shapes each word once so the plans exist.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var fonts = new BundledScriptFonts();
        _khmer = ShaperFor(fonts, Khmer);
        _myanmar = ShaperFor(fonts, Myanmar);
        _sinhala = ShaperFor(fonts, Sinhala);
        _mongolian = ShaperFor(fonts, Mongolian);
        _tibetan = ShaperFor(fonts, Tibetan);
        _ = ShapeKhmer();
        _ = ShapeMyanmar();
        _ = ShapeSinhala();
        _ = ShapeMongolian();
        _ = ShapeTibetan();
    }

    /// <summary>Shapes the Khmer word.</summary>
    /// <returns>The number of glyphs.</returns>
    [Benchmark]
    public int ShapeKhmer() => Shape(_khmer, Khmer);

    /// <summary>Shapes the Myanmar word.</summary>
    /// <returns>The number of glyphs.</returns>
    [Benchmark]
    public int ShapeMyanmar() => Shape(_myanmar, Myanmar);

    /// <summary>Shapes the Sinhala word.</summary>
    /// <returns>The number of glyphs.</returns>
    [Benchmark]
    public int ShapeSinhala() => Shape(_sinhala, Sinhala);

    /// <summary>Shapes the Mongolian word.</summary>
    /// <returns>The number of glyphs.</returns>
    [Benchmark]
    public int ShapeMongolian() => Shape(_mongolian, Mongolian);

    /// <summary>Shapes the Tibetan word.</summary>
    /// <returns>The number of glyphs.</returns>
    [Benchmark]
    public int ShapeTibetan() => Shape(_tibetan, Tibetan);

    /// <summary>Makes the shaper of the font a text needs.</summary>
    /// <param name="fonts">The bundled fonts.</param>
    /// <param name="text">The text.</param>
    /// <returns>The shaper.</returns>
    /// <exception cref="InvalidOperationException">No bundled font has the text.</exception>
    private static PdfOpenTypeShaper ShaperFor(BundledScriptFonts fonts, string text) =>
        new((fonts.OpenFor(text) ?? throw new InvalidOperationException("No bundled font has the text.")).Source);

    /// <summary>Shapes a word into the reused list.</summary>
    /// <param name="shaper">The shaper.</param>
    /// <param name="text">The word.</param>
    /// <returns>The number of glyphs.</returns>
    private int Shape(PdfOpenTypeShaper shaper, string text)
    {
        _glyphs.Clear();
        _ = shaper.Shape(text, PdfShapingOptions.Default, _glyphs);
        return _glyphs.Count;
    }
}
