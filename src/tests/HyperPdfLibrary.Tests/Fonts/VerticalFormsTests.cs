// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Shaping;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Vertical text takes the upright forms a font's GSUB <c>vert</c> feature offers for brackets, punctuation and the long vowel mark.</summary>
public sealed class VerticalFormsTests
{
    /// <summary>The glyph of the long vowel mark in horizontal text.</summary>
    private const int HorizontalGlyph = 10;

    /// <summary>The glyph the long vowel mark takes in a column.</summary>
    private const int VerticalGlyph = 20;

    /// <summary>The Unicode long vowel mark.</summary>
    private const int LongVowelMark = 0x30FC;

    /// <summary>A glyph the font does not change.</summary>
    private const int OtherGlyph = 11;

    /// <summary>Where the script list starts: after the 10 bytes of header.</summary>
    private const int ScriptListOffset = 10;

    /// <summary>Where the feature list starts: after the 20 bytes of script list.</summary>
    private const int FeatureListOffset = 30;

    /// <summary>Where the lookup list starts: after the 14 bytes of feature list.</summary>
    private const int LookupListOffset = 44;

    /// <summary>Where the table of a list's first record sits, from the start of the list: its count and the record come first.</summary>
    private const int FirstRecordTable = 8;

    /// <summary>Where the default language system sits from the start of the script table.</summary>
    private const int DefaultLangSysOffset = 4;

    /// <summary>Where the lookup table sits from the start of the lookup list.</summary>
    private const int LookupTableOffset = 4;

    /// <summary>Where the substitution sits from the start of its lookup table.</summary>
    private const int SubtableOffset = 8;

    /// <summary>Where the coverage table sits from the start of the substitution.</summary>
    private const int CoverageOffset = 6;

    /// <summary>A glyph with a vertical form gets it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReplacesAGlyphThatHasAVerticalForm()
    {
        var forms = new VerticalForms(SubstitutionWith("vert"), default);

        await Assert.That(forms.Substitute(HorizontalGlyph, LongVowelMark)).IsEqualTo(VerticalGlyph);
    }

    /// <summary>A glyph without a vertical form is left as it is.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsAGlyphWithNoVerticalForm()
    {
        var forms = new VerticalForms(SubstitutionWith("vert"), default);

        await Assert.That(forms.Substitute(OtherGlyph, LongVowelMark)).IsEqualTo(OtherGlyph);
    }

    /// <summary>The newer <c>vrt2</c> feature is used as well.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UsesTheVrt2Feature()
    {
        var forms = new VerticalForms(SubstitutionWith("vrt2"), default);

        await Assert.That(forms.Substitute(HorizontalGlyph, LongVowelMark)).IsEqualTo(VerticalGlyph);
    }

    /// <summary>A feature meant for other text, such as small capitals, does not change a vertical glyph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IgnoresFeaturesThatAreNotVertical()
    {
        var forms = new VerticalForms(SubstitutionWith("smcp"), default);

        await Assert.That(forms.Substitute(HorizontalGlyph, LongVowelMark)).IsEqualTo(HorizontalGlyph);
    }

    /// <summary>A system face that hands over its GSUB table gives the vertical form of a glyph, and none for a font that offers no tables.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ASubstituteFaceReadsTheTablesOfItsFont()
    {
        using var withTable = new SubstituteFace(new TableFace(SubstitutionWith("vert")), true);
        using var withoutTable = new SubstituteFace(new TableFace([]), true);

        await Assert.That(withTable.GetVerticalGlyph(HorizontalGlyph, LongVowelMark)).IsEqualTo(VerticalGlyph);
        await Assert.That(withTable.GetVerticalGlyph(OtherGlyph, LongVowelMark)).IsEqualTo(OtherGlyph);
        await Assert.That(withoutTable.GetVerticalGlyph(HorizontalGlyph, LongVowelMark)).IsEqualTo(HorizontalGlyph);
    }

    /// <summary>A font with no GSUB table has nothing to substitute.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HasNothingToDoWithoutAGsubTable()
    {
        var forms = new VerticalForms(default, default);

        await Assert.That(forms.IsPresent).IsFalse();
    }

    /// <summary>Builds a GSUB table with one feature and one single substitution lookup that turns glyph 10 into glyph 20.</summary>
    /// <param name="feature">The four-letter feature tag.</param>
    /// <returns>The table.</returns>
    private static byte[] SubstitutionWith(string feature)
    {
        var table = new List<byte>();

        // Header: version 1.0, then the offsets of the script, feature and lookup lists.
        Add32(table, 0x00010000);
        Add16(table, ScriptListOffset);
        Add16(table, FeatureListOffset);
        Add16(table, LookupListOffset);

        // Script list: one DFLT script whose default language system uses feature 0.
        Add16(table, 1);
        Add("DFLT", table);
        Add16(table, FirstRecordTable);
        Add16(table, DefaultLangSysOffset);
        Add16(table, 0);
        Add16(table, 0);
        Add16(table, 0xFFFF);
        Add16(table, 1);
        Add16(table, 0);

        // Feature list: one feature that runs lookup 0.
        Add16(table, 1);
        Add(feature, table);
        Add16(table, FirstRecordTable);
        Add16(table, 0);
        Add16(table, 1);
        Add16(table, 0);

        // Lookup list: one single substitution lookup.
        Add16(table, 1);
        Add16(table, LookupTableOffset);
        Add16(table, 1);
        Add16(table, 0);
        Add16(table, 1);
        Add16(table, SubtableOffset);

        // Single substitution, format 1: a delta of ten for the glyphs in the coverage table, which holds glyph 10.
        Add16(table, 1);
        Add16(table, CoverageOffset);
        Add16(table, VerticalGlyph - HorizontalGlyph);
        Add16(table, 1);
        Add16(table, 1);
        Add16(table, HorizontalGlyph);
        return [.. table];
    }

    /// <summary>Adds a big-endian 16-bit number.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="value">The number.</param>
    private static void Add16(List<byte> bytes, int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)value);
        bytes.AddRange(buffer);
    }

    /// <summary>Adds a big-endian 32-bit number.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="value">The number.</param>
    private static void Add32(List<byte> bytes, uint value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        bytes.AddRange(buffer);
    }

    /// <summary>Adds the four ASCII letters of a tag.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="bytes">The bytes.</param>
    private static void Add(string tag, List<byte> bytes) => bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(tag));

    /// <summary>A backend face whose only table is the GSUB table it was given.</summary>
    /// <param name="gsub">The GSUB table, or empty.</param>
    private sealed class TableFace(byte[] gsub) : IPdfFontFace
    {
        /// <summary>The one number the face gives for its weight, glyph count, metrics and advances, which the tests do not read.</summary>
        private const int FaceNumber = 500;

        /// <inheritdoc/>
        public string FamilyName => "Test";

        /// <inheritdoc/>
        public int Weight => FaceNumber;

        /// <inheritdoc/>
        public bool Italic => false;

        /// <inheritdoc/>
        public int GlyphCount => FaceNumber;

        /// <inheritdoc/>
        public float Ascent => FaceNumber;

        /// <inheritdoc/>
        public float Descent => -FaceNumber;

        /// <inheritdoc/>
        public int GetGlyph(int unicode) => unicode;

        /// <inheritdoc/>
        public float GetAdvance(int glyph) => FaceNumber;

        /// <inheritdoc/>
        public ReadOnlyMemory<byte> GetTable(string tag) => tag == "GSUB" ? gsub : default;

        /// <inheritdoc/>
        public PdfPath? BuildOutline(int glyph) => null;

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
