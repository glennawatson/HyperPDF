// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts.Programs;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Tests.Fonts;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Subsetting of embedded CFF and OpenType programs by the optimiser, and the subset tag on the names of a font it cut down.</summary>
[NotInParallel]
public sealed class CffFontOptimizationTests
{
    /// <summary>The size of the sfnt directory header.</summary>
    private const int DirectoryHeader = 12;

    /// <summary>The size of an sfnt table record.</summary>
    private const int RecordSize = 16;

    /// <summary>The offset of the table count in the directory.</summary>
    private const int CountOffset = 4;

    /// <summary>The offset of a table's offset in its record.</summary>
    private const int RecordOffset = 8;

    /// <summary>The offset of a table's length in its record.</summary>
    private const int RecordLength = 12;

    /// <summary>The tag of the CFF table.</summary>
    private const uint CffTag = 0x43464620;

    /// <summary>The length of a subset tag and its plus sign.</summary>
    private const int TaggedPrefix = 7;

    /// <summary>A CID-keyed OpenType font shown by page text is cut to the glyphs used, and draws and extracts the same.</summary>
    /// <param name="openType">Whether the program is the OpenType file or the bare CFF program.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task CidKeyedProgramIsCutDown(bool openType)
    {
        var source = OptimizerSamples.EmbeddedCffFont(openType);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.KeepQuality);
        var original = Cff(OptimizerSamples.EmbeddedCffFont(openType), openType);
        var cut = Cff(result.Bytes, openType);

        var originalProgram = Parse(original);
        var cutProgram = Parse(cut);
        await Assert.That(cut.Length).IsLessThan(original.Length);
        await Assert.That(cutProgram.GlyphCount).IsEqualTo(originalProgram.GlyphCount);
        await Assert.That(Outline(cutProgram, TestFont.GlyphOf('H'))).IsEquivalentTo(Outline(originalProgram, TestFont.GlyphOf('H')));
        await Assert.That(Outline(cutProgram, TestFont.GlyphOf('Z')).Count).IsEqualTo(0);
        await Assert.That(result.Report.GetSaving(PdfOptimizeCategory.Fonts).Count).IsEqualTo(1);
        await Assert.That(OptimizerTestKit.Text(result.Bytes)).IsEquivalentTo(OptimizerTestKit.Text(source));
        await Assert.That(OptimizerTestKit.MaxDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsEqualTo(0);
    }

    /// <summary>The font, its descendant and its descriptor all carry the same new subset tag.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontNamesGetASubsetTag()
    {
        var result = OptimizerTestKit.Optimize(OptimizerSamples.EmbeddedCffFont(true), PdfOptimizeOptions.KeepQuality);
        using var document = PdfDocumentReader.Open(result.Bytes, null);
        var font = Font(document);
        var descendant = font.GetArray(KnownName.DescendantFonts)!.GetDictionary(0)!;
        var descriptor = descendant.GetDictionary(KnownName.FontDescriptor)!;
        var names = document.Objects.Names;

        var fontName = Spell(names, font.GetName(KnownName.BaseFont));
        await Assert.That(fontName.Length).IsGreaterThan(TaggedPrefix);
        await Assert.That(fontName[TaggedPrefix - 1]).IsEqualTo('+');
        await Assert.That(fontName[..(TaggedPrefix - 1)].All(static letter => letter is >= 'A' and <= 'Z')).IsTrue();
        await Assert.That(fontName[TaggedPrefix..]).IsEqualTo("PVLTestCidCff");
        await Assert.That(Spell(names, descendant.GetName(KnownName.BaseFont))).IsEqualTo(fontName);
        await Assert.That(Spell(names, descriptor.GetName(KnownName.FontName))).IsEqualTo(fontName);
    }

    /// <summary>A font the optimiser does not cut down keeps its name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UncutFontKeepsItsName()
    {
        var result = OptimizerTestKit.Optimize(OptimizerSamples.EmbeddedCffFont(true), PdfOptimizeOptions.KeepQuality with { SubsetFonts = false });
        using var document = PdfDocumentReader.Open(result.Bytes, null);

        await Assert.That(Spell(document.Objects.Names, Font(document).GetName(KnownName.BaseFont))).IsEqualTo("PVLTestCidCff");
    }

    /// <summary>A TrueType font that is cut down gets a subset tag as well.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TrueTypeFontNamesGetASubsetTag()
    {
        var result = OptimizerTestKit.Optimize(OptimizerSamples.EmbeddedFont(false), PdfOptimizeOptions.KeepQuality);
        using var document = PdfDocumentReader.Open(result.Bytes, null);
        var font = PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(KnownName.Font)!.GetDictionary(document.Objects.Names.Intern("F1"u8))!;
        var name = Spell(document.Objects.Names, font.GetName(KnownName.BaseFont));
        var descriptorName = Spell(document.Objects.Names, font.GetDictionary(KnownName.FontDescriptor)!.GetName(KnownName.FontName));

        await Assert.That(name[TaggedPrefix - 1]).IsEqualTo('+');
        await Assert.That(name[TaggedPrefix..]).IsEqualTo("PVLTestSans");
        await Assert.That(descriptorName).IsEqualTo(name);
    }

    /// <summary>Reads the font of the first page.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The font dictionary.</returns>
    private static PdfDictionary Font(PdfDocument document) =>
        PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(KnownName.Font)!.GetDictionary(document.Objects.Names.Intern("F1"u8))!;

    /// <summary>Gets a name as text.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="name">The name.</param>
    /// <returns>The spelling.</returns>
    private static string Spell(PdfNameTable names, PdfName name) => System.Text.Encoding.ASCII.GetString(names.GetSpelling(name));

    /// <summary>Reads the decoded CFF program out of the first page's font.</summary>
    /// <param name="pdf">The document.</param>
    /// <param name="openType">Whether the embedded program is an OpenType file.</param>
    /// <returns>The bare CFF program.</returns>
    private static byte[] Cff(byte[] pdf, bool openType)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        var descendant = Font(document).GetArray(KnownName.DescendantFonts)!.GetDictionary(0)!;
        var program = descendant.GetDictionary(KnownName.FontDescriptor)!.GetStream(KnownName.FontFile3)!.DecodeToArray();
        return openType ? Table(program) : program;
    }

    /// <summary>Finds the CFF table of an OpenType font.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The table's bytes.</returns>
    /// <exception cref="InvalidOperationException">The font has no CFF table.</exception>
    private static byte[] Table(byte[] font)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(CountOffset));
        for (var i = 0; i < count; i++)
        {
            var record = font.AsSpan(DirectoryHeader + (i * RecordSize));
            if (BinaryPrimitives.ReadUInt32BigEndian(record) != CffTag)
            {
                continue;
            }

            var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(record[RecordOffset..]);
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(record[RecordLength..]);
            return font.AsSpan(offset, length).ToArray();
        }

        throw new InvalidOperationException("The font has no CFF table.");
    }

    /// <summary>Parses a CFF program.</summary>
    /// <param name="cff">The program.</param>
    /// <returns>The parsed program.</returns>
    /// <exception cref="InvalidOperationException">The data is not a CFF program.</exception>
    private static CffProgram Parse(byte[] cff) =>
        CffProgram.TryParse(cff, out var program) ? program : throw new InvalidOperationException("The data is not a CFF program.");

    /// <summary>Records a glyph's outline.</summary>
    /// <param name="program">The program.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The outline commands.</returns>
    private static List<string> Outline(CffProgram program, int glyph)
    {
        var recorder = new OutlineRecorder();
        program.DecodeGlyph(glyph, ref recorder);
        return recorder.Commands;
    }
}
