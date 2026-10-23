// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Tests.Syntax;

/// <summary>Tests for <see cref="PdfParser"/>, <see cref="PdfLexer"/> and <see cref="PdfNumber"/>.</summary>
public sealed class ParserTests
{
    /// <summary>The /Count in the sample dictionary.</summary>
    private const int SampleCount = 3;

    /// <summary>The /Scale in the sample dictionary.</summary>
    private const double SampleScale = -1.5;

    /// <summary>The number of kids in the sample dictionary, and the object number of the last.</summary>
    private const int SampleKids = 2;

    /// <summary>The allowed error when comparing parsed reals.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>A dictionary with every kind of value parses into the matching values.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParsesEveryValueKind()
    {
        var names = new PdfNameTable();
        var value = Parse("<< /Type /Page /Count 3 /Scale -1.5 /Kids [1 0 R 2 0 R] /On true /Off false /Nothing null /Text (Hi) /Hex <4869> >>", names);
        var dictionary = value.AsDictionary()!;

        await Assert.That(value.Kind).IsEqualTo(PdfKind.Dictionary);
        await Assert.That(dictionary.GetRaw(KnownName.Type).IsName(KnownName.Page)).IsTrue();
        await Assert.That(dictionary.GetInteger(KnownName.Count)).IsEqualTo(SampleCount);
        await Assert.That(dictionary.GetRaw(names.Intern("Scale")).AsNumber()).IsEqualTo(SampleScale);
        var kids = dictionary.GetArray(KnownName.Kids)!;
        await Assert.That(kids.Count).IsEqualTo(SampleKids);
        await Assert.That(kids.GetRaw(1).AsReference()).IsEqualTo(new(SampleKids, 0));
        await Assert.That(dictionary.GetRaw(names.Intern("On")).AsBoolean()).IsTrue();
        await Assert.That(dictionary.ContainsKey(names.Intern("Nothing"))).IsFalse();
        await Assert.That(Encoding.ASCII.GetString(dictionary.GetRaw(names.Intern("Text")).AsStringBytes())).IsEqualTo("Hi");
        await Assert.That(Encoding.ASCII.GetString(dictionary.GetRaw(names.Intern("Hex")).AsStringBytes())).IsEqualTo("Hi");
    }

    /// <summary>Literal string escapes, nesting and line ends decode as the specification describes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodesLiteralStringEscapes()
    {
        var value = Parse("(a\\(b\\)c (nested) \\101\\60\\n\\\r\nend\r\nx)", new());

        await Assert.That(Encoding.ASCII.GetString(value.AsStringBytes())).IsEqualTo("a(b)c (nested) A0\nend\nx");
    }

    /// <summary>A plain literal string points into the buffer instead of being copied.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlainStringsAreNotCopied()
    {
        var buffer = "(plain text)"u8.ToArray();
        var overlaps = ParseOverlaps(buffer, out var text);

        await Assert.That(text).IsEqualTo("plain text");
        await Assert.That(overlaps).IsTrue();
    }

    /// <summary>Names decode #xx escapes and intern to the same id.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamesDecodeEscapesAndIntern()
    {
        var names = new PdfNameTable();
        var escaped = Parse("/A#20B", names).AsName();
        var plain = names.Intern("A B");

        await Assert.That(escaped).IsEqualTo(plain);
        await Assert.That(Parse("/Type", names).AsName().Is(KnownName.Type)).IsTrue();
        await Assert.That(names.GetString(escaped)).IsEqualTo("A B");
    }

    /// <summary>Numbers parse leniently, as viewers do.</summary>
    /// <param name="text">The number token.</param>
    /// <param name="expected">The expected value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("123", 123.0)]
    [Arguments("-.002", -0.002)]
    [Arguments("4.", 4.0)]
    [Arguments("+17", 17.0)]
    [Arguments("--5", -5.0)]
    [Arguments("0.000000000000000000001", 0.0)]
    public async Task ParsesNumbers(string text, double expected)
    {
        var parsed = PdfNumber.TryParse(Encoding.ASCII.GetBytes(text), out var value);

        await Assert.That(parsed).IsTrue();
        await Assert.That(Math.Abs(value.AsNumber() - expected)).IsLessThan(Tolerance);
    }

    /// <summary>A damaged dictionary missing its close stops at the object end.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedDictionaryStopsAtEndobj()
    {
        var value = Parse("<< /Count 2 /Kids [ 1 0 R endobj", new());

        await Assert.That(value.AsDictionary()!.GetInteger(KnownName.Count)).IsEqualTo(SampleKids);
        await Assert.That(value.AsDictionary()!.GetArray(KnownName.Kids)!.Count).IsEqualTo(1);
    }

    /// <summary>The lexer skips comments and splits delimiters.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LexerSkipsCommentsAndSplitsDelimiters()
    {
        var kinds = Lex("% comment\n[/Name(str)<<>>1.5 key]");

        await Assert.That(kinds).IsEquivalentTo(
        [
            PdfTokenKind.ArrayStart, PdfTokenKind.Name, PdfTokenKind.LiteralString, PdfTokenKind.DictionaryStart,
            PdfTokenKind.DictionaryEnd, PdfTokenKind.Number, PdfTokenKind.Keyword, PdfTokenKind.ArrayEnd,
        ]);
    }

    /// <summary>Parses one value from text.</summary>
    /// <param name="text">The PDF syntax.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The value.</returns>
    private static PdfValue Parse(string text, PdfNameTable names)
    {
        var parser = new PdfParser(Encoding.Latin1.GetBytes(text), 0, null, names);
        return parser.ParseValue();
    }

    /// <summary>Parses a string and checks whether its bytes point into the buffer.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="text">The string's text.</param>
    /// <returns><see langword="true"/> when the string was not copied.</returns>
    private static bool ParseOverlaps(byte[] buffer, out string text)
    {
        var parser = new PdfParser(buffer, 0, null, new());
        var bytes = parser.ParseValue().AsStringBytes();
        text = Encoding.ASCII.GetString(bytes);
        return bytes.Overlaps(buffer);
    }

    /// <summary>Tokenises text.</summary>
    /// <param name="text">The PDF syntax.</param>
    /// <returns>The token kinds.</returns>
    private static List<PdfTokenKind> Lex(string text)
    {
        var lexer = new PdfLexer(Encoding.ASCII.GetBytes(text));
        var kinds = new List<PdfTokenKind>();
        for (var kind = lexer.Next(); kind != PdfTokenKind.EndOfData; kind = lexer.Next())
        {
            kinds.Add(kind);
        }

        return kinds;
    }
}
