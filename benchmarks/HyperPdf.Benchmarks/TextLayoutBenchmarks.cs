// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Text;
using HyperPdf.Core.Text.Fonts;
using HyperPdf.Core.Text.Layout;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures laying out typed text: one line, a wrapped paragraph, a row of character boxes, the relayout after each
/// keystroke, and cutting the font down to the letters used before it is embedded.
/// Allocations are checked from the EventPipe trace.
/// </summary>
public class TextLayoutBenchmarks
{
    /// <summary>The text size in points.</summary>
    private const float Size = 12;

    /// <summary>The wrap width of the paragraph in points.</summary>
    private const float WrapWidth = 200;

    /// <summary>No wrapping.</summary>
    private const float NoWrap = 0;

    /// <summary>The character boxes in the comb.</summary>
    private const int CombCells = 8;

    /// <summary>The line typed.</summary>
    private const string Line = "Ada Lovelace, 10 Analytical Way";

    /// <summary>The comb's characters.</summary>
    private const string CombText = "AB123456";

    /// <summary>The paragraph typed, a few wrapped lines.</summary>
    private const string Paragraph = "Please return the signed form by Friday. Write your name above the line, and the code one letter to a box. Thank you for your help.";

    /// <summary>The layout, reused as the editor reuses it.</summary>
    private readonly TextBoxLayout _layout = new();

    /// <summary>The format of plain text.</summary>
    private readonly TextFormat _format = TextFormat.Default with { FontFamily = TestFont.Family, FontSize = Size };

    /// <summary>The format of a comb.</summary>
    private TextFormat _comb = null!;

    /// <summary>The font.</summary>
    private FontProgram _font = null!;

    /// <summary>The shaper.</summary>
    private FontProgramShaper _shaper = null!;

    /// <summary>The glyphs of the paragraph, subset before embedding.</summary>
    private ushort[] _glyphs = [];

    /// <summary>How much of the paragraph has been typed.</summary>
    private int _typed;

    /// <summary>Loads the font.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var face = new FontFace(TestFont.Family, "Regular", "test.ttf", 0) { HasTrueTypeOutlines = true };
        _font = FontProgram.FromBytes(face, TestFont.Create())!;
        _shaper = new(_font);
        _comb = _format with { CombCells = CombCells };
        _layout.Layout(Paragraph, _format, _shaper, WrapWidth);
        var glyphs = new HashSet<ushort>();
        foreach (var glyph in _layout.Glyphs)
        {
            _ = glyphs.Add(glyph.Glyph);
        }

        _glyphs = [.. glyphs];
    }

    /// <summary>Lays out one line.</summary>
    /// <returns>The width.</returns>
    [Benchmark]
    public float LayoutLine()
    {
        _layout.Layout(Line, _format, _shaper, NoWrap);
        return _layout.Width;
    }

    /// <summary>Lays out a paragraph wrapped to a box.</summary>
    /// <returns>The height.</returns>
    [Benchmark]
    public float LayoutParagraph()
    {
        _layout.Layout(Paragraph, _format, _shaper, WrapWidth);
        return _layout.Height;
    }

    /// <summary>Lays out a row of character boxes.</summary>
    /// <returns>The width.</returns>
    [Benchmark]
    public float LayoutComb()
    {
        _layout.Layout(CombText, _comb, _shaper, NoWrap);
        return _layout.Width;
    }

    /// <summary>Lays out the paragraph after one more character is typed, as the live preview does per keystroke.</summary>
    /// <returns>The height.</returns>
    [Benchmark]
    public float TypeCharacter()
    {
        _typed = _typed >= Paragraph.Length ? 1 : _typed + 1;
        _layout.Layout(Paragraph.AsSpan(0, _typed), _format, _shaper, WrapWidth);
        return _layout.Height;
    }

    /// <summary>Cuts the font down to the paragraph's glyphs, as embedding does.</summary>
    /// <returns>The subset's glyph count.</returns>
    [Benchmark]
    public int SubsetFont() => FontSubsetter.Create(_font, _glyphs)?.GlyphCount ?? 0;
}
