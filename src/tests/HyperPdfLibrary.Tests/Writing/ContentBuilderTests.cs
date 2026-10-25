// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Tests for <see cref="PdfContentBuilder"/>.</summary>
public sealed class ContentBuilderTests
{
    /// <summary>A coordinate used in the samples.</summary>
    private const float Origin = 72;

    /// <summary>A fractional value used in the samples.</summary>
    private const float Half = 0.5F;

    /// <summary>A small whole number used in the samples.</summary>
    private const float Two = 2;

    /// <summary>The sample miter limit.</summary>
    private const float MiterLimit = 10;

    /// <summary>The sample dash length.</summary>
    private const float DashLength = 3;

    /// <summary>The sample horizontal scaling.</summary>
    private const float Scaling = 100;

    /// <summary>The sample leading.</summary>
    private const float Leading = 14;

    /// <summary>The entries in a matrix.</summary>
    private const int MatrixEntries = 6;

    /// <summary>The bevel line join.</summary>
    private const int BevelJoin = 2;

    /// <summary>A font size used in the samples.</summary>
    private const float FontSize = 12;

    /// <summary>A kerning adjustment used in the samples.</summary>
    private const float Kerning = -120;

    /// <summary>The width of the sample form.</summary>
    private const float FormWidth = 200;

    /// <summary>The height of the sample form.</summary>
    private const float FormHeight = 50;

    /// <summary>The line repeats in the compressible sample.</summary>
    private const int Repeats = 50;

    /// <summary>A third, which needs rounding to six decimals.</summary>
    private const float Third = 1F / 3;

    /// <summary>The tokens the sample content parses into.</summary>
    private static readonly string[] SampleTokens =
    [
        "q", "1", "0", "0", "1", "72", "72", "cm", "0.5", "w", "1", "J", "2", "j", "10", "M", "[", "3", "1", "]", "0", "d",
        "Perceptual", "ri", "1", "i", "GS1", "gs",
        "0", "0", "m", "72", "0", "l", "0", "0", "72", "72", "1", "1", "c", "1", "1", "2", "2", "v", "1", "1", "2", "2", "y",
        "h", "0", "0", "72", "72", "re", "S", "s", "f", "f*", "B", "B*", "b", "b*", "n", "W", "W*",
        "0.5", "g", "0.5", "G", "1", "0", "0", "rg", "0", "1", "0", "RG", "0", "0", "0", "1", "k", "1", "0", "0", "0", "K",
        "DeviceRGB", "cs", "DeviceGray", "CS", "0.333333", "sc", "1", "SC", "P#201", "scn", "0", "0", "1", "SCN",
        "BT", "F1", "12", "Tf", "72", "72", "Td", "0", "-12", "TD", "1", "0", "0", "1", "72", "72", "Tm", "T*", "1", "Tc",
        "2", "Tw", "100", "Tz", "14", "TL", "0", "Tr", "0.5", "Ts", "a(b)c", "Tj", "[", "A", "-120", "B", "]", "TJ", "ET",
        "Im1", "Do", "Span", "BMC", "EMC", "OC", "MC0", "BDC", "EMC", "Q",
    ];

    /// <summary>Every operator writes PDF syntax that the lexer reads back token by token.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContentParsesBackTokenByToken()
    {
        var content = BuildSample();
        var tokens = Lex(content, out var lines);

        await Assert.That(tokens).IsEquivalentTo(SampleTokens);
        await Assert.That(lines.TrueForAll(static line => line.Length > 0 && !line.EndsWith(' '))).IsTrue();
    }

    /// <summary>A Form XObject holds the content, compressed when that is smaller, with its box and matrix.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormXObjectCompressesWhenSmaller()
    {
        var small = BuildForm(1, out var smallContent);
        var large = BuildForm(Repeats, out var largeContent);

        await Assert.That(small.Dictionary.ContainsKey(KnownName.Filter)).IsFalse();
        await Assert.That(large.Dictionary.IsName(KnownName.Filter, KnownName.FlateDecode)).IsTrue();
        await Assert.That(large.RawLength).IsLessThan(largeContent.Length);
        await Assert.That(large.DecodeToArray()).IsEquivalentTo(largeContent);
        await Assert.That(small.DecodeToArray()).IsEquivalentTo(smallContent);
        await Assert.That(large.Dictionary.IsName(KnownName.Subtype, KnownName.Form)).IsTrue();
        await Assert.That(large.Dictionary.GetArray(KnownName.BBox)!.GetNumber(PdfRectangle.Coordinates - 1)).IsEqualTo(FormHeight);
        await Assert.That(large.Dictionary.GetArray(KnownName.Matrix)!.Count).IsEqualTo(MatrixEntries);
    }

    /// <summary>The content copies to a buffer writer unchanged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesToBufferWriter()
    {
        var content = BuildSample();
        var output = new ArrayBufferWriter<byte>();
        CopySample(output);

        await Assert.That(output.WrittenSpan.ToArray()).IsEquivalentTo(content);
    }

    /// <summary>Builds the sample content into a buffer writer.</summary>
    /// <param name="output">The buffer writer.</param>
    private static void CopySample(ArrayBufferWriter<byte> output)
    {
        var builder = default(PdfContentBuilder);
        try
        {
            WriteSample(ref builder);
            builder.WriteTo(output);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Builds content that uses every operator.</summary>
    /// <returns>The content.</returns>
    private static byte[] BuildSample()
    {
        var builder = default(PdfContentBuilder);
        try
        {
            WriteSample(ref builder);
            return builder.ToArray();
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Writes content that uses every operator.</summary>
    /// <param name="builder">The builder.</param>
    private static void WriteSample(ref PdfContentBuilder builder)
    {
        builder.SaveState();
        builder.Transform(1, 0, 0, 1, Origin, Origin);
        builder.SetLineWidth(Half);
        builder.SetLineCap(1);
        builder.SetLineJoin(BevelJoin);
        builder.SetMiterLimit(MiterLimit);
        builder.SetDash([DashLength, 1], 0);
        builder.SetRenderingIntent("Perceptual"u8);
        builder.SetFlatness(1);
        builder.SetGraphicsState("GS1"u8);
        WritePath(ref builder);
        WriteColours(ref builder);
        WriteText(ref builder);
        builder.DrawXObject("Im1"u8);
        builder.BeginMarkedContent("Span"u8);
        builder.EndMarkedContent();
        builder.BeginMarkedContent("OC"u8, "MC0"u8);
        builder.EndMarkedContent();
        builder.RestoreState();
    }

    /// <summary>Writes the path construction and painting operators.</summary>
    /// <param name="builder">The builder.</param>
    private static void WritePath(ref PdfContentBuilder builder)
    {
        builder.MoveTo(0, 0);
        builder.LineTo(Origin, 0);
        builder.CurveTo(0, 0, Origin, Origin, 1, 1);
        builder.CurveToFromCurrent(1, 1, Two, Two);
        builder.CurveToEnd(1, 1, Two, Two);
        builder.ClosePath();
        builder.Rectangle(0, 0, Origin, Origin);
        builder.Stroke();
        builder.CloseAndStroke();
        builder.Fill();
        builder.FillEvenOdd();
        builder.FillAndStroke();
        builder.FillEvenOddAndStroke();
        builder.CloseFillAndStroke();
        builder.CloseFillEvenOddAndStroke();
        builder.EndPath();
        builder.Clip();
        builder.ClipEvenOdd();
    }

    /// <summary>Writes the colour operators.</summary>
    /// <param name="builder">The builder.</param>
    private static void WriteColours(ref PdfContentBuilder builder)
    {
        builder.SetFillGray(Half);
        builder.SetStrokeGray(Half);
        builder.SetFillRgb(1, 0, 0);
        builder.SetStrokeRgb(0, 1, 0);
        builder.SetFillCmyk(0, 0, 0, 1);
        builder.SetStrokeCmyk(1, 0, 0, 0);
        builder.SetFillColorSpace("DeviceRGB"u8);
        builder.SetStrokeColorSpace("DeviceGray"u8);
        builder.SetFillColor([Third]);
        builder.SetStrokeColor([1]);
        builder.SetFillColorN([], "P 1"u8);
        builder.SetStrokeColorN([0, 0, 1]);
    }

    /// <summary>Writes the text operators.</summary>
    /// <param name="builder">The builder.</param>
    private static void WriteText(ref PdfContentBuilder builder)
    {
        builder.BeginText();
        builder.SetFont("F1"u8, FontSize);
        builder.MoveText(Origin, Origin);
        builder.MoveTextAndSetLeading(0, -FontSize);
        builder.SetTextMatrix(1, 0, 0, 1, Origin, Origin);
        builder.NextLine();
        builder.SetCharacterSpacing(1);
        builder.SetWordSpacing(Two);
        builder.SetHorizontalScaling(Scaling);
        builder.SetLeading(Leading);
        builder.SetTextRenderingMode(0);
        builder.SetTextRise(Half);
        builder.ShowText("a(b)c"u8);
        builder.BeginTextArray();
        builder.AddTextArrayString("A"u8);
        builder.AddTextArrayAdjustment(Kerning);
        builder.AddTextArrayString("B"u8);
        builder.EndTextArray();
        builder.EndText();
    }

    /// <summary>Builds a Form XObject from repeated content.</summary>
    /// <param name="repeats">How many times to repeat a rectangle fill.</param>
    /// <param name="content">The content written.</param>
    /// <returns>The form.</returns>
    private static PdfStream BuildForm(int repeats, out byte[] content)
    {
        var builder = default(PdfContentBuilder);
        try
        {
            for (var i = 0; i < repeats; i++)
            {
                builder.Rectangle(0, 0, FormWidth, FormHeight);
                builder.Fill();
            }

            content = builder.ToArray();
            return builder.ToFormXObject(null, new(0, 0, FormWidth, FormHeight), [1, 0, 0, 1, 0, 0], new(null));
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Lexes content into token text, decoding names and literal strings.</summary>
    /// <param name="content">The content.</param>
    /// <param name="lines">The content's lines.</param>
    /// <returns>The tokens.</returns>
    private static List<string> Lex(byte[] content, out List<string> lines)
    {
        lines = [.. Encoding.ASCII.GetString(content).TrimEnd('\n').Split('\n')];
        var tokens = new List<string>();
        var lexer = new PdfLexer(content);
        for (var kind = lexer.Next(); kind != PdfTokenKind.EndOfData; kind = lexer.Next())
        {
            tokens.Add(kind switch
            {
                PdfTokenKind.ArrayStart => "[",
                PdfTokenKind.ArrayEnd => "]",
                PdfTokenKind.LiteralString => DecodeLiteral(lexer.Lexeme),
                _ => Encoding.ASCII.GetString(lexer.Lexeme),
            });
        }

        return tokens;
    }

    /// <summary>Decodes a literal string token.</summary>
    /// <param name="raw">The bytes between the parentheses.</param>
    /// <returns>The string as ASCII text.</returns>
    private static string DecodeLiteral(ReadOnlySpan<byte> raw)
    {
        var decoded = new byte[raw.Length];
        return Encoding.ASCII.GetString(decoded, 0, PdfStringDecoder.DecodeLiteral(raw, decoded));
    }
}
