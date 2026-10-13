// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Loads simple TrueType fonts embedded with the generated test font.</summary>
public sealed class TrueTypeFontTests
{
    /// <summary>The code of A.</summary>
    private const int CodeA = 'A';

    /// <summary>The code of B.</summary>
    private const int CodeB = 'B';

    /// <summary>The code of m, whose test glyph is wide.</summary>
    private const int CodeM = 'm';

    /// <summary>The left edge of a test font glyph.</summary>
    private const float GlyphLeft = 40;

    /// <summary>The right edge of A: its 500 advance less 40.</summary>
    private const float GlyphRightA = 460;

    /// <summary>The top of A: 500 plus 65 modulo 200.</summary>
    private const float TopA = 565;

    /// <summary>The top of B.</summary>
    private const float TopB = 566;

    /// <summary>The PDF width of A in text space.</summary>
    private const float WidthA = 0.5F;

    /// <summary>The PDF width of B in text space.</summary>
    private const float WidthB = 0.7F;

    /// <summary>The program advance of m in text space.</summary>
    private const float AdvanceM = 0.8F;

    /// <summary>The /MissingWidth in text space.</summary>
    private const float Missing = 0.25F;

    /// <summary>The tolerance of width comparisons.</summary>
    private const float Tolerance = 0.0001F;

    /// <summary>The descriptor flags of a non-symbolic font.</summary>
    private const int Nonsymbolic = 32;

    /// <summary>The descriptor flags of a symbolic font.</summary>
    private const int Symbolic = 4;

    /// <summary>The descriptor flags of an AllCap non-symbolic font.</summary>
    private const int AllCapNonsymbolic = (int)(FontFlags.Nonsymbolic | FontFlags.AllCap);

    /// <summary>The WinAnsi encoding entry.</summary>
    private const string WinAnsi = "/Encoding /WinAnsiEncoding";

    /// <summary>The longest text a code maps to.</summary>
    private const int TextLength = 8;

    /// <summary>The code with a ligature in the ToUnicode test.</summary>
    private const int LigatureCode = 1;

    /// <summary>The code with a surrogate pair in the ToUnicode test.</summary>
    private const int EmojiCode = 2;

    /// <summary>The renders measured for allocations.</summary>
    private const int Iterations = 100;

    /// <summary>A WinAnsi font's codes map through glyph names and the (3,1) cmap; /Widths give the advances.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonsymbolicFontUsesNamesAndWidths()
    {
        using var document = new FontTestDocument(Spec(Nonsymbolic, "/FirstChar 65 /LastChar 66 /Widths [500 700] /Encoding /WinAnsiEncoding"));
        var font = document.Font;
        var bounds = FontProbe.Bounds(font, CodeA);

        await Assert.That(FontProbe.ReadCodes(font, "AB"u8.ToArray())).IsEquivalentTo([new CodeRead(CodeA, 1), new CodeRead(CodeB, 1)]);
        await Assert.That(font.GetWidth(CodeA)).IsEqualTo(WidthA).Within(Tolerance);
        await Assert.That(font.GetWidth(CodeB)).IsEqualTo(WidthB).Within(Tolerance);
        await Assert.That(font.GetWidth(CodeM)).IsEqualTo(0F);
        await Assert.That(bounds.Left).IsEqualTo(GlyphLeft).Within(Tolerance);
        await Assert.That(bounds.Right).IsEqualTo(GlyphRightA).Within(Tolerance);
        await Assert.That(bounds.Bottom).IsEqualTo(TopA).Within(Tolerance);
        await Assert.That(FontProbe.Text(font, CodeA)).IsEqualTo("A");
        await Assert.That(font.GetOutline(' ')).IsNull();
        await Assert.That(font.IsWordSpace(' ', 1)).IsTrue();
    }

    /// <summary>/Differences renames codes, and the renamed code draws and reads as the new glyph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DifferencesRenameCodes()
    {
        using var document = new FontTestDocument(Spec(Nonsymbolic, "/Encoding << /BaseEncoding /WinAnsiEncoding /Differences [1 /B] >>"));

        await Assert.That(FontProbe.Bounds(document.Font, 1).Bottom).IsEqualTo(TopB).Within(Tolerance);
        await Assert.That(FontProbe.Text(document.Font, 1)).IsEqualTo("B");
    }

    /// <summary>Codes outside /FirstChar to /LastChar take /MissingWidth.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingWidthFillsUnlistedCodes()
    {
        var spec = Spec(Nonsymbolic, "/FirstChar 65 /LastChar 65 /Widths [500] /Encoding /WinAnsiEncoding") with { DescriptorEntries = "/MissingWidth 250" };
        using var document = new FontTestDocument(spec);

        await Assert.That(document.Font.GetWidth(CodeA)).IsEqualTo(WidthA).Within(Tolerance);
        await Assert.That(document.Font.GetWidth(CodeM)).IsEqualTo(Missing).Within(Tolerance);
    }

    /// <summary>Without /Widths the program's advances are used.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProgramAdvancesStandInForMissingWidths()
    {
        using var document = new FontTestDocument(Spec(Nonsymbolic, WinAnsi));

        await Assert.That(document.Font.GetWidth(CodeM)).IsEqualTo(AdvanceM).Within(Tolerance);
    }

    /// <summary>A symbolic font without a (3,0) cmap reads codes as Unicode values through its Unicode cmap.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SymbolicFontReadsCodesThroughTheUnicodeCmap()
    {
        using var document = new FontTestDocument(Spec(Symbolic, string.Empty));

        await Assert.That(FontProbe.Bounds(document.Font, CodeA).Bottom).IsEqualTo(TopA).Within(Tolerance);
        await Assert.That(FontProbe.Text(document.Font, CodeA)).IsEqualTo("A");
    }

    /// <summary>/ToUnicode overrides glyph names, with bfrange arrays, ligatures and surrogate pairs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ToUnicodeOverridesGlyphNames()
    {
        const string cmap = "/CIDInit /ProcSet findresource begin begincmap 1 begincodespacerange <00> <FF> endcodespacerange "
            + "1 beginbfchar <41> <0042> endbfchar 1 beginbfrange <01> <02> [<00660069> <D83DDE00>] endbfrange endcmap";
        using var document = new FontTestDocument(Spec(Nonsymbolic, WinAnsi) with { ToUnicode = cmap });

        await Assert.That(FontProbe.Text(document.Font, CodeA)).IsEqualTo("B");
        await Assert.That(FontProbe.Text(document.Font, LigatureCode)).IsEqualTo("fi");
        await Assert.That(FontProbe.Text(document.Font, EmojiCode)).IsEqualTo("\U0001F600");
        await Assert.That(FontProbe.Text(document.Font, CodeB)).IsEqualTo("B");
    }

    /// <summary>An AllCap font draws lowercase codes with the capitals' glyphs when the lowercase glyph is missing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AllCapFontKeepsEmbeddedLowercase()
    {
        using var document = new FontTestDocument(Spec(AllCapNonsymbolic, WinAnsi));

        await Assert.That(document.Font.GetOutline('a')).IsNotNull();
        await Assert.That(FontProbe.Bounds(document.Font, 'a')).IsNotEqualTo(FontProbe.Bounds(document.Font, CodeA));
    }

    /// <summary>Outlines are built once and the same path is returned afterwards.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutlinesAreCached()
    {
        using var document = new FontTestDocument(Spec(Nonsymbolic, WinAnsi));

        await Assert.That(document.Font.GetOutline(CodeA)).IsSameReferenceAs(document.Font.GetOutline(CodeA));
    }

    /// <summary>Reading codes, widths, text and cached outlines allocates nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LookupsDoNotAllocate()
    {
        using var document = new FontTestDocument(Spec(Nonsymbolic, "/FirstChar 65 /LastChar 66 /Widths [500 700] /Encoding /WinAnsiEncoding"));

        await Assert.That(MeasureLookups(document.Font)).IsEqualTo(0L);
    }

    /// <summary>Builds the spec of a TrueType font embedding the generated test font.</summary>
    /// <param name="flags">The descriptor flags.</param>
    /// <param name="entries">Extra font entries.</param>
    /// <returns>The spec.</returns>
    private static FontSpec Spec(int flags, string entries) => new() { Subtype = "TrueType", Program = TestFont.Create(), FileKey = "FontFile2", Flags = flags, Entries = entries, };

    /// <summary>Measures the bytes the lookups allocate after a warm-up.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The bytes allocated on this thread.</returns>
    private static long MeasureLookups(PdfFont font)
    {
        var bytes = "AB"u8.ToArray();
        _ = Lookups(font, bytes);
        return AllocationProbe.Minimum(() =>
        {
            for (var i = 0; i < Iterations; i++)
            {
                _ = Lookups(font, bytes);
            }
        });
    }

    /// <summary>Runs every per-glyph lookup over some bytes.</summary>
    /// <param name="font">The font.</param>
    /// <param name="bytes">The string bytes.</param>
    /// <returns>A sum so the work is not removed.</returns>
    private static float Lookups(PdfFont font, ReadOnlySpan<byte> bytes)
    {
        Span<char> text = stackalloc char[TextLength];
        float total = 0;
        while (!bytes.IsEmpty)
        {
            var used = font.ReadCode(bytes, out var code);
            total += font.GetWidth(code) + font.GetUnicode(code, text) + (font.GetOutline(code)?.PointCount ?? 0);
            bytes = bytes[used..];
        }

        return total;
    }
}
