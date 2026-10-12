// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Text;
using HyperPdf.Core.Text.Fonts;
using HyperPdf.Core.Text.Layout;
using HyperPdf.TestAssets;

namespace HyperPdf.Core.Tests.Text;

/// <summary>Tests laying text boxes out: wrapping, alignment, spacing, comb boxes, right-to-left lines and kerning.</summary>
public sealed class TextBoxLayoutTests
{
    /// <summary>The font size; with the test font an ordinary character is 5 points wide.</summary>
    private const float Size = 10;

    /// <summary>An ordinary character's width at <see cref="Size"/>.</summary>
    private const float Char = 5;

    /// <summary>A space's width at <see cref="Size"/>.</summary>
    private const float Space = 2.5F;

    /// <summary>How close positions must be.</summary>
    private const float Tolerance = 0.001F;

    /// <summary>The first baseline at <see cref="Size"/>: the ascent below half the extra line height.</summary>
    private const float FirstBaseline = 9.25F;

    /// <summary>A wrap width fitting "abcd efgh" but not a third word.</summary>
    private const float Wrap = 50;

    /// <summary>Three lines.</summary>
    private const int ThreeLines = 3;

    /// <summary>Two lines.</summary>
    private const int TwoLines = 2;

    /// <summary>The comb boxes.</summary>
    private const int Cells = 4;

    /// <summary>The comb width: 10 points per box.</summary>
    private const float CombWidth = 40;

    /// <summary>A character spacing.</summary>
    private const float Spacing = 1;

    /// <summary>A line spacing multiple.</summary>
    private const float Lines = 2;

    /// <summary>Halves a length.</summary>
    private const float Half = 0.5F;

    /// <summary>The characters, spaces apart, on the first wrapped line.</summary>
    private const int FirstLineChars = 8;

    /// <summary>Where the second wrapped line starts.</summary>
    private const int SecondLineStart = 10;

    /// <summary>Where the second paragraph starts, after a carriage return and line feed.</summary>
    private const int ThirdLineStart = 16;

    /// <summary>The last character's index.</summary>
    private const int LastCluster = 17;

    /// <summary>The characters of a long word that fit the wrap width.</summary>
    private const int LongWordFit = 10;

    /// <summary>The shaper for the test font.</summary>
    private static readonly FontProgramShaper Shaper = new(LoadFont());

    /// <summary>The format used.</summary>
    private static readonly TextFormat Format = new(TestFont.Family, Size, 0);

    /// <summary>Lines wrap at the last space that fits; trailing spaces do not count towards width; paragraphs break lines.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrapsAtSpaces()
    {
        var layout = new TextBoxLayout();
        layout.Layout("abcd efgh ijkl\r\nmn", Format, Shaper, Wrap);

        await Assert.That(layout.Lines.Count).IsEqualTo(ThreeLines);
        await Assert.That(layout.Lines[0].Width).IsEqualTo((Char * FirstLineChars) + Space).Within(Tolerance);
        await Assert.That(layout.Lines[1].TextStart).IsEqualTo(SecondLineStart);
        await Assert.That(layout.Lines[2].TextStart).IsEqualTo(ThirdLineStart);
        await Assert.That(layout.Lines[0].Baseline).IsEqualTo(FirstBaseline).Within(Tolerance);
        await Assert.That(layout.Lines[1].Baseline - layout.Lines[0].Baseline).IsEqualTo(Size * TextFormat.DefaultLineSpacing).Within(Tolerance);
        await Assert.That(layout.Width).IsEqualTo(Wrap);
        await Assert.That(layout.Glyphs[^1].Cluster).IsEqualTo(LastCluster);
    }

    /// <summary>A word too long for the width breaks between characters instead of overflowing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BreaksLongWords()
    {
        var layout = new TextBoxLayout();
        layout.Layout("abcdefghijklmnop", Format, Shaper, Wrap);

        await Assert.That(layout.Lines.Count).IsEqualTo(TwoLines);
        await Assert.That(layout.Lines[0].GlyphCount).IsEqualTo(LongWordFit);
        await Assert.That(layout.Lines[0].Width).IsLessThanOrEqualTo(Wrap);
    }

    /// <summary>Without a wrap width the box is as wide as its widest line, and lines align within it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AlignsLines()
    {
        var centred = new TextBoxLayout();
        centred.Layout("abcd\nab", Format with { Alignment = TextBoxAlignment.Center }, Shaper, 0);
        var right = new TextBoxLayout();
        right.Layout("abcd\nab", Format with { Alignment = TextBoxAlignment.Right }, Shaper, 0);

        await Assert.That(centred.Width).IsEqualTo(Char * Cells).Within(Tolerance);
        await Assert.That(centred.Lines[1].Left).IsEqualTo(Char).Within(Tolerance);
        await Assert.That(centred.Glyphs[Cells].X).IsEqualTo(Char).Within(Tolerance);
        await Assert.That(right.Lines[1].Left).IsEqualTo(Char * TwoLines).Within(Tolerance);
    }

    /// <summary>Character spacing widens lines but not after the last character; line spacing moves baselines apart.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppliesSpacing()
    {
        var layout = new TextBoxLayout();
        layout.Layout("abc\nd", Format with { CharacterSpacing = Spacing, LineSpacing = Lines }, Shaper, 0);

        await Assert.That(layout.Lines[0].Width).IsEqualTo((Char * ThreeLines) + (Spacing * TwoLines)).Within(Tolerance);
        await Assert.That(layout.Glyphs[1].X).IsEqualTo(Char + Spacing).Within(Tolerance);
        await Assert.That(layout.Lines[1].Baseline - layout.Lines[0].Baseline).IsEqualTo(Size * Lines).Within(Tolerance);
        await Assert.That(layout.Height).IsEqualTo(Size * Lines * TwoLines).Within(Tolerance);
    }

    /// <summary>Comb boxes hold one centred character each; characters past the last box are left out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CentresCharactersInCombBoxes()
    {
        var layout = new TextBoxLayout();
        layout.Layout("abcdef\nignored", Format with { CombCells = Cells, Alignment = TextBoxAlignment.Right }, Shaper, CombWidth);
        const float cell = CombWidth / Cells;

        await Assert.That(layout.Lines.Count).IsEqualTo(1);
        await Assert.That(layout.Glyphs.Length).IsEqualTo(Cells);
        await Assert.That(layout.Glyphs[0].X).IsEqualTo((cell - Char) * Half).Within(Tolerance);
        await Assert.That(layout.Glyphs[^1].X).IsEqualTo((cell * ThreeLines) + ((cell - Char) * Half)).Within(Tolerance);
        await Assert.That(layout.Lines[0].TextLength).IsEqualTo(Cells);
    }

    /// <summary>Right-to-left paragraphs place their first letter at the right.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlacesRightToLeftText()
    {
        var layout = new TextBoxLayout();
        layout.Layout("אב", Format, Shaper, 0);

        await Assert.That(layout.Glyphs[0].X).IsGreaterThan(layout.Glyphs[1].X);
        await Assert.That(layout.Glyphs[1].X).IsEqualTo(0F).Within(Tolerance);
        await Assert.That(TextDirection.IsRightToLeft("1 אב")).IsTrue();
        await Assert.That(TextDirection.IsRightToLeft("a אב")).IsFalse();
    }

    /// <summary>Pair kerning pulls "AV" together, CJK text breaks between characters, and empty text still has a line.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KernsBreaksCjkAndKeepsEmptyLines()
    {
        var kerned = new TextBoxLayout();
        kerned.Layout("AV", Format, Shaper, 0);
        var cjk = new TextBoxLayout();
        cjk.Layout("中中中中中中", Format, Shaper, Wrap / TwoLines);
        var empty = new TextBoxLayout();
        empty.Layout(string.Empty, Format, Shaper, 0);

        await Assert.That(kerned.Glyphs[1].X).IsEqualTo(Char + (TestFont.AvKerning * Size / TestFont.UnitsPerEm)).Within(Tolerance);
        await Assert.That(cjk.Lines.Count).IsEqualTo(ThreeLines);
        await Assert.That(empty.Lines.Count).IsEqualTo(1);
        await Assert.That(empty.Height).IsEqualTo(Size * TextFormat.DefaultLineSpacing).Within(Tolerance);
        await Assert.That(kerned.UnderlineOffset).IsEqualTo(1F).Within(Tolerance);
    }

    /// <summary>Lays the same text out twice in one instance and gets the same answer, as the buffers are reused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReusesItsBuffers()
    {
        var layout = new TextBoxLayout();
        layout.Layout("abcd efgh ijkl", Format, Shaper, Wrap);
        var first = layout.Glyphs.ToArray();
        layout.Layout("abcd efgh ijkl", Format, Shaper, Wrap);

        await Assert.That(layout.Glyphs.ToArray()).IsEquivalentTo(first);
    }

    /// <summary>Loads the test font.</summary>
    /// <returns>The font.</returns>
    private static FontProgram LoadFont()
    {
        var face = new FontFace(TestFont.Family, "Regular", "test.ttf", 0) { HasTrueTypeOutlines = true };
        return FontProgram.FromBytes(face, TestFont.Create())!;
    }
}
