// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Parses real fonts installed on the machine; each test is skipped when its font is absent.</summary>
public sealed class SystemFontTests
{
    /// <summary>The fewest contours the letter A has: its outline and its counter.</summary>
    private const int ContoursOfA = 2;

    /// <summary>The rectangle contour in the generated font's A.</summary>
    private const int SyntheticContoursOfA = 1;

    /// <summary>The code point of Á, a composite glyph in most TrueType fonts.</summary>
    private const int AAcute = 0xC1;

    /// <summary>The format that explicitly omits PostScript glyph names.</summary>
    private const uint NamelessPostVersion = 0x00030000;

    /// <summary>The size of an SFNT directory header.</summary>
    private const int DirectoryHeaderSize = 12;

    /// <summary>The size of an SFNT table record.</summary>
    private const int TableRecordSize = 16;

    /// <summary>The table offset within its directory record.</summary>
    private const int TableOffsetPosition = 8;

    /// <summary>TrueType fonts to try, in order.</summary>
    private static readonly string[] TrueTypeFonts =
    [
        "/usr/share/fonts/dejavu-sans-fonts/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/liberation-sans-fonts/LiberationSans-Regular.ttf",
        "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
        "/System/Library/Fonts/Supplemental/Arial.ttf",
        "C:\\Windows\\Fonts\\arial.ttf",
    ];

    /// <summary>OpenType fonts with CFF outlines to try, in order.</summary>
    private static readonly string[] CffFonts =
    [
        "/usr/share/fonts/urw-base35/NimbusSans-Regular.otf",
        "/usr/share/fonts/abattis-cantarell-fonts/Cantarell-Regular.otf",
        "/usr/share/fonts/adobe-source-code-pro-fonts/SourceCodePro-Regular.otf",
        "/usr/share/fonts/opentype/urw-base35/NimbusSans-Regular.otf",
    ];

    /// <summary>The URW Nimbus Sans Type 1 font, whose outlines match its OpenType twin.</summary>
    private static readonly string[] Type1Fonts =
    [
        "/usr/share/fonts/urw-base35/NimbusSans-Regular.t1",
        "/usr/share/fonts/type1/urw-base35/NimbusSans-Regular.t1",
    ];

    /// <summary>A TrueType font parses, maps A through its cmap, and draws A inside its bounding box.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TrueTypeFontDrawsA()
    {
        var data = await ReadFirstAsync(TrueTypeFonts);
        var result = Inspect(TrueTypeProgram.TryParse(data, out var program) ? program : null);

        await Assert.That(result.Parsed).IsTrue();
        await Assert.That(program!.HasCffOutlines).IsFalse();
        await AssertDrawsA(result, ExpectedTrueTypeName(data), ContoursOfA);
        await Assert.That(Decode(program, AAcute).Count(static command => command == "Z")).IsGreaterThan(ContoursOfA);
    }

    /// <summary>A format 3 font omits glyph names but retains Unicode, name fallback and outlines.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamelessTrueTypeFontKeepsLookupsAndOutlines()
    {
        var data = TestFont.Create();
        await Assert.That(ReadPostVersion(data)).IsEqualTo(NamelessPostVersion);
        var result = Inspect(TrueTypeProgram.TryParse(data, out var program) ? program : null);

        await Assert.That(result.Parsed).IsTrue();
        await Assert.That(program!.HasCffOutlines).IsFalse();
        await Assert.That(result.Glyph).IsEqualTo((int)TestFont.GlyphOf('A'));
        await Assert.That(result.Width).IsEqualTo((float)TestFont.AdvanceOf('A'));
        await AssertDrawsA(result, ExpectedTrueTypeName(data), SyntheticContoursOfA);
    }

    /// <summary>An OpenType CFF font parses through its 'CFF ' table and draws A inside its bounding box.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpenTypeCffFontDrawsA()
    {
        var data = await ReadFirstAsync(CffFonts);
        var result = Inspect(TrueTypeProgram.TryParse(data, out var program) ? program : null);

        await Assert.That(result.Parsed).IsTrue();
        await Assert.That(program!.HasCffOutlines).IsTrue();
        await Assert.That(Decode(program, 'O').Exists(static command => command.StartsWith('C'))).IsTrue();
        await AssertDrawsA(result, "A", ContoursOfA);
    }

    /// <summary>A Type 1 font draws A exactly as its OpenType CFF twin does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Type1FontMatchesItsOpenTypeTwin()
    {
        var type1Data = await ReadFirstAsync(Type1Fonts);
        var openTypeData = await ReadFirstAsync(CffFonts[..1]);
        var type1 = Inspect(Type1Program.TryParse(type1Data, out var type1Program) ? type1Program : null);
        var openType = Inspect(TrueTypeProgram.TryParse(openTypeData, out var openTypeProgram) ? openTypeProgram : null);

        await Assert.That(type1.Parsed).IsTrue();
        await Assert.That(type1.GlyphCount).IsEqualTo(openType.GlyphCount);
        await Assert.That(type1.Width).IsEqualTo(openType.Width);
        await Assert.That(type1.Commands).IsEquivalentTo(openType.Commands);
        await AssertDrawsA(type1, "A", ContoursOfA);
    }

    /// <summary>Reads the first font that exists, or skips the test.</summary>
    /// <param name="paths">The paths to try.</param>
    /// <returns>The font bytes.</returns>
    private static async Task<byte[]> ReadFirstAsync(string[] paths)
    {
        var path = paths.FirstOrDefault(File.Exists);
        if (path is null)
        {
            Skip.Test("No suitable system font is installed.");
        }

        return await File.ReadAllBytesAsync(path!);
    }

    /// <summary>Checks the glyph count, the A lookups and that A's outline lies inside the font bounding box.</summary>
    /// <param name="result">The inspection.</param>
    /// <param name="expectedName">The name supplied by the font format.</param>
    /// <param name="minimumContours">The fewest contours in the fixture's A.</param>
    /// <returns>A task.</returns>
    private static async Task AssertDrawsA(FontInspection result, string expectedName, int minimumContours)
    {
        await Assert.That(result.GlyphCount).IsGreaterThan(1);
        await Assert.That(result.Glyph).IsGreaterThanOrEqualTo(0);
        await Assert.That(result.Name).IsEqualTo(expectedName);
        await Assert.That(result.GlyphByName).IsEqualTo(result.Glyph);
        await Assert.That(result.Width).IsGreaterThan(0);
        await Assert.That(result.Recorder.Contours).IsGreaterThanOrEqualTo(minimumContours);
        await Assert.That(result.Recorder.MinX).IsGreaterThanOrEqualTo(result.Program!.BoundingBox.Left);
        await Assert.That(result.Recorder.MaxX).IsLessThanOrEqualTo(result.Program.BoundingBox.Right);
        await Assert.That(result.Recorder.MinY).IsGreaterThanOrEqualTo(result.Program.BoundingBox.Bottom);
        await Assert.That(result.Recorder.MaxY).IsLessThanOrEqualTo(result.Program.BoundingBox.Top);
    }

    /// <summary>Derives the name expectation from the actual table header rather than the font parser.</summary>
    /// <param name="data">The SFNT bytes.</param>
    /// <returns>The expected name of A.</returns>
    /// <exception cref="InvalidDataException">The post format does not define the test's name expectation.</exception>
    private static string ExpectedTrueTypeName(ReadOnlySpan<byte> data) => ReadPostVersion(data) switch
    {
        NamelessPostVersion => string.Empty,
        0x00010000 or 0x00020000 or 0x00025000 => "A",
        _ => throw new InvalidDataException("Unsupported post version in the system font fixture."),
    };

    /// <summary>Reads the post version directly from the SFNT directory, including a collection's first face.</summary>
    /// <param name="data">The font bytes.</param>
    /// <returns>The raw 16.16 post version.</returns>
    /// <exception cref="InvalidDataException">The font has no post table.</exception>
    private static uint ReadPostVersion(ReadOnlySpan<byte> data)
    {
        var directory = data[..sizeof(uint)].SequenceEqual("ttcf"u8)
            ? checked((int)BinaryPrimitives.ReadUInt32BigEndian(data[DirectoryHeaderSize..]))
            : 0;
        var count = BinaryPrimitives.ReadUInt16BigEndian(data[(directory + sizeof(uint))..]);
        for (var i = 0; i < count; i++)
        {
            var record = data.Slice(directory + DirectoryHeaderSize + (i * TableRecordSize), TableRecordSize);
            if (!record[..sizeof(uint)].SequenceEqual("post"u8))
            {
                continue;
            }

            var offset = checked((int)BinaryPrimitives.ReadUInt32BigEndian(record[TableOffsetPosition..]));
            return BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
        }

        throw new InvalidDataException("The system font fixture has no post table.");
    }

    /// <summary>Decodes the glyph of a code point.</summary>
    /// <param name="program">The program.</param>
    /// <param name="codePoint">The code point.</param>
    /// <returns>The commands.</returns>
    private static List<string> Decode(FontProgram program, int codePoint)
    {
        var recorder = new OutlineRecorder();
        program.DecodeGlyph(program.GetGlyphByUnicode(codePoint), ref recorder);
        return recorder.Commands;
    }

    /// <summary>Looks up and decodes A.</summary>
    /// <param name="program">The program, or <see langword="null"/> when parsing failed.</param>
    /// <returns>The inspection.</returns>
    private static FontInspection Inspect(FontProgram? program)
    {
        var recorder = new OutlineRecorder();
        if (program is null)
        {
            return new(null, false, 0, -1, string.Empty, -1, 0, recorder);
        }

        var glyph = program.GetGlyphByUnicode('A');
        program.DecodeGlyph(glyph, ref recorder);
        return new(
            program,
            true,
            program.GlyphCount,
            glyph,
            Encoding.ASCII.GetString(program.GetGlyphName(glyph)),
            program.GetGlyphByName("A"u8),
            program.GetAdvanceWidth(glyph),
            recorder);
    }

    /// <summary>What decoding A found.</summary>
    /// <param name="Program">The program.</param>
    /// <param name="Parsed">Whether the font parsed.</param>
    /// <param name="GlyphCount">The glyph count.</param>
    /// <param name="Glyph">The glyph of A by Unicode.</param>
    /// <param name="Name">The glyph's name.</param>
    /// <param name="GlyphByName">The glyph of A by name.</param>
    /// <param name="Width">The advance width.</param>
    /// <param name="Recorder">The recorded outline.</param>
    private sealed record FontInspection(FontProgram? Program, bool Parsed, int GlyphCount, int Glyph, string Name, int GlyphByName, float Width, OutlineRecorder Recorder)
    {
        /// <summary>Gets the recorded commands.</summary>
        internal List<string> Commands => Recorder.Commands;
    }
}
