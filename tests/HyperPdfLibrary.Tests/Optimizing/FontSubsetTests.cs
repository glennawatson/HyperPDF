// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Embedded TrueType subsetting.</summary>
[NotInParallel]
public sealed class FontSubsetTests
{
    /// <summary>The offset of the table count in an sfnt.</summary>
    private const int TableCountOffset = 4;

    /// <summary>The size of the sfnt header.</summary>
    private const int HeaderSize = 12;

    /// <summary>The size of a table record.</summary>
    private const int RecordSize = 16;

    /// <summary>The offset of a record's table offset.</summary>
    private const int RecordOffset = 8;

    /// <summary>The offset of a record's table length.</summary>
    private const int RecordLength = 12;

    /// <summary>The bytes of a long /loca entry.</summary>
    private const int LongEntry = 4;

    /// <summary>The offset of the glyph count in /maxp.</summary>
    private const int MaxpGlyphs = 4;

    /// <summary>The tag of /loca.</summary>
    private const uint LocaTag = 0x6C6F6361;

    /// <summary>The tag of /maxp.</summary>
    private const uint MaxpTag = 0x6D617870;

    /// <summary>A font shown only by page text is cut to the glyphs used, keeps every glyph number, and draws and extracts the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SubsetsPageFonts()
    {
        var source = OptimizerSamples.EmbeddedFont(false);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.KeepQuality);
        var font = Program(result.Bytes);
        var original = TestFont.Create();

        await Assert.That(font.Length).IsLessThan(original.Length);
        await Assert.That(GlyphCount(font)).IsEqualTo(GlyphCount(original));
        await Assert.That(HasOutline(font, TestFont.GlyphOf('H'))).IsTrue();
        await Assert.That(HasOutline(font, TestFont.GlyphOf('Z'))).IsFalse();
        await Assert.That(result.Report.GetSaving(PdfOptimizeCategory.Fonts).Count).IsEqualTo(1);
        await Assert.That(OptimizerTestKit.Text(result.Bytes)).IsEquivalentTo(OptimizerTestKit.Text(source));
        await Assert.That(OptimizerTestKit.MaxDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsEqualTo(0);
    }

    /// <summary>A font the interactive form also uses keeps every glyph, so fields can still be filled with any text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsFormFontsWhole()
    {
        var result = OptimizerTestKit.Optimize(OptimizerSamples.EmbeddedFont(true), PdfOptimizeOptions.KeepQuality);

        await Assert.That(HasOutline(Program(result.Bytes), TestFont.GlyphOf('Z'))).IsTrue();
        await Assert.That(result.Report.Skipped.Any(static skip => skip.Category == PdfOptimizeCategory.Fonts)).IsTrue();
    }

    /// <summary>Subsetting can be turned off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SubsettingCanBeTurnedOff()
    {
        var result = OptimizerTestKit.Optimize(OptimizerSamples.EmbeddedFont(false), PdfOptimizeOptions.KeepQuality with { SubsetFonts = false });

        await Assert.That(Program(result.Bytes)).IsEquivalentTo(TestFont.Create());
    }

    /// <summary>Gets the decoded font program.</summary>
    /// <param name="pdf">The document.</param>
    /// <returns>The font file.</returns>
    private static byte[] Program(byte[] pdf)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        var font = PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(KnownName.Font)!.GetDictionary(document.Objects.Names.Intern("F1"u8))!;
        return font.GetDictionary(KnownName.FontDescriptor)!.GetStream(KnownName.FontFile2)!.DecodeToArray();
    }

    /// <summary>Finds a table.</summary>
    /// <param name="font">The font.</param>
    /// <param name="tag">The tag.</param>
    /// <param name="length">The table's length.</param>
    /// <returns>The table's offset.</returns>
    private static int Table(byte[] font, uint tag, out int length)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(TableCountOffset));
        for (var i = 0; i < count; i++)
        {
            var record = HeaderSize + (i * RecordSize);
            if (BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record)) != tag)
            {
                continue;
            }

            length = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + RecordLength));
            return (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + RecordOffset));
        }

        length = 0;
        return -1;
    }

    /// <summary>Reads the glyph count.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The count.</returns>
    private static int GlyphCount(byte[] font) => BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(Table(font, MaxpTag, out _) + MaxpGlyphs));

    /// <summary>Determines whether a glyph has outline data, reading a long or short /loca.</summary>
    /// <param name="font">The font.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns><see langword="true"/> when its data is not empty.</returns>
    private static bool HasOutline(byte[] font, int glyph)
    {
        var loca = Table(font, LocaTag, out var length);
        var entry = length / (GlyphCount(font) + 1);
        return entry == LongEntry
            ? BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(loca + ((glyph + 1) * LongEntry))) > BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(loca + (glyph * LongEntry)))
            : BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(loca + ((glyph + 1) * entry))) > BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(loca + (glyph * entry)));
    }
}
