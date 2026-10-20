// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Redaction;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Redaction;

/// <summary>Redaction cuts the embedded font programs of the fonts that lost text, so the outlines of redacted characters are not left in the file.</summary>
public sealed class RedactionFontTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 200;

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

    /// <summary>The length of a subset tag and its plus sign.</summary>
    private const int TaggedPrefix = 7;

    /// <summary>The page text, with the secret on the second line.</summary>
    private const string Content = "BT /F1 20 Tf 20 150 Td (Public text) Tj 0 -30 Td (SECRET-ALPHA) Tj ET";

    /// <summary>The name of the embedded test font.</summary>
    private const string FontName = "PVLTestSans";

    /// <summary>A page whose only text is the secret.</summary>
    private const string OnlySecret = "BT /F1 20 Tf 20 120 Td (SECRET-ALPHA) Tj ET";

    /// <summary>The outlines of letters only the redacted line uses are gone; the letters the page still shows keep theirs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RedactedLettersLoseTheirOutlines()
    {
        var pdf = Build();
        var saved = RedactionSamples.Redact(pdf, PdfRedactionOptions.Default, RedactionSamples.SecretLine);
        var before = Program(pdf);
        var after = Program(saved);

        await Assert.That(HasOutline(before, TestFont.GlyphOf('S'))).IsTrue();
        await Assert.That(HasOutline(after, TestFont.GlyphOf('S'))).IsFalse();
        await Assert.That(HasOutline(after, TestFont.GlyphOf('H'))).IsFalse();
        await Assert.That(HasOutline(after, TestFont.GlyphOf('u'))).IsTrue();
        await Assert.That(HasOutline(after, TestFont.GlyphOf('P'))).IsTrue();
        await Assert.That(after.Length).IsLessThan(before.Length);
        await Assert.That(RedactionSamples.TextOf(saved)).Contains(RedactionSamples.Public);
    }

    /// <summary>The font that was cut down carries a subset tag.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CutFontGetsASubsetTag()
    {
        var saved = RedactionSamples.Redact(Build(), PdfRedactionOptions.Default, RedactionSamples.SecretLine);
        using var document = PdfDocumentReader.Open(saved, null);
        var name = System.Text.Encoding.ASCII.GetString(document.Objects.Names.GetSpelling(Font(document).GetName(KnownName.BaseFont)));

        await Assert.That(name[TaggedPrefix - 1]).IsEqualTo('+');
        await Assert.That(name[TaggedPrefix..]).IsEqualTo(FontName);
    }

    /// <summary>With resource cleanup off, the font program is left as it was.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProgramStaysWholeWhenCleanupIsOff()
    {
        var pdf = Build();
        var saved = RedactionSamples.Redact(pdf, PdfRedactionOptions.Default with { RemoveUnusedResources = false }, RedactionSamples.SecretLine);

        await Assert.That(HasOutline(Program(saved), TestFont.GlyphOf('S'))).IsTrue();
    }

    /// <summary>A page with no redaction leaves its font alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontWithoutRedactionIsUntouched()
    {
        var pdf = Build();
        var saved = RedactionSamples.Redact(pdf, PdfRedactionOptions.Default, new PdfRectangle(0, 0, 1, 1));

        await Assert.That(HasOutline(Program(saved), TestFont.GlyphOf('S'))).IsTrue();
    }

    /// <summary>A font whose every glyph was redacted keeps no outline at all.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontOfAFullyRedactedPageKeepsNoOutline()
    {
        var pdf = Build(OnlySecret);
        var saved = RedactionSamples.Redact(pdf, PdfRedactionOptions.Default, new PdfRectangle(0, 0, Size, Size));
        var after = Program(saved);

        await Assert.That(HasOutline(Program(pdf), TestFont.GlyphOf('S'))).IsTrue();
        foreach (var letter in "SECRET-ALPHA")
        {
            await Assert.That(HasOutline(after, TestFont.GlyphOf(letter))).IsFalse();
        }
    }

    /// <summary>Builds a page that shows two lines in the embedded test TrueType font.</summary>
    /// <returns>The PDF.</returns>
    private static byte[] Build() => Build(Content);

    /// <summary>Builds a page that shows some content in the embedded test TrueType font.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The PDF.</returns>
    private static byte[] Build(string content)
    {
        var program = TestFont.Create();
        var pdf = new RenderTestPdf(Size, Size) { Content = content };
        var stream = pdf.AddStream($"/Length1 {program.Length}", program);
        var descriptor = pdf.AddObject(
            $"<< /Type /FontDescriptor /FontName /PVLTestSans /Flags 32 /FontBBox [0 -200 1000 800] /ItalicAngle 0 /Ascent 800 /Descent -200 /CapHeight 700 /StemV 80 /FontFile2 {stream} 0 R >>");
        var font = pdf.AddObject(
            $"<< /Type /Font /Subtype /TrueType /BaseFont /PVLTestSans /FirstChar 32 /LastChar 126 /Widths [{Widths()}] /Encoding /WinAnsiEncoding /FontDescriptor {descriptor} 0 R >>");
        pdf.Resources = $"/Font << /F1 {font} 0 R >>";
        return pdf.ToBytes();
    }

    /// <summary>Lists a width for every code from space to tilde.</summary>
    /// <returns>The widths as PDF text.</returns>
    private static string Widths() => string.Join(' ', Enumerable.Repeat("600", '~' - ' ' + 1));

    /// <summary>Gets the font of the first page.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The font dictionary.</returns>
    private static PdfDictionary Font(PdfDocument document) =>
        PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(KnownName.Font)!.GetDictionary(document.Objects.Names.Intern("F1"u8))!;

    /// <summary>Gets the decoded font program of the first page's font.</summary>
    /// <param name="pdf">The document.</param>
    /// <returns>The font file.</returns>
    private static byte[] Program(byte[] pdf)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        return Font(document).GetDictionary(KnownName.FontDescriptor)!.GetStream(KnownName.FontFile2)!.DecodeToArray();
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
