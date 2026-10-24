// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Tests.Syntax;

/// <summary>Tests for the repair, parser, lexer and inline image fixes.</summary>
public sealed class StructureFixTests
{
    /// <summary>The object number that appears only inside a stream body.</summary>
    private const int HiddenObject = 9;

    /// <summary>The object number of the stream with no end markers.</summary>
    private const int OpenStream = 4;

    /// <summary>The bytes in the unterminated stream's data.</summary>
    private const int OpenStreamLength = 3;

    /// <summary>The bytes in the inline image sample.</summary>
    private const int InlineLength = 4;

    /// <summary>A header, a catalog, a page tree and a stream that holds the text of another object, with no table.</summary>
    private const string StreamHidingObject =
            "%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Kids [] /Count 0 >>\nendobj\n"
            + "3 0 obj\n<< /Length 32 >>\nstream\n9 0 obj\n<< /Fake true >>\nendobj\nendstream\nendobj\ntrailer\n<< /Root 1 0 R /Size 10 >>\n%%EOF";

    /// <summary>A stream with neither /Length nor endstream.</summary>
    private const string StreamWithoutEnd =
            "%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Kids [] /Count 0 >>\nendobj\n"
            + "4 0 obj\n<< >>\nstream\nabc\nendobj\ntrailer\n<< /Root 1 0 R /Size 5 >>\n%%EOF";

    /// <summary>Image data that holds a fake end marker, then the real one and a restore operator.</summary>
    private const string InlineImage = "BI /W 4 /H 1 /BPC 8 /CS /G ID x EI\nEI\nQ";

    /// <summary>An object header inside a stream body is not indexed during repair.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepairSkipsStreamBodies()
    {
        using var store = StoreOpening.Open(Encoding.Latin1.GetBytes(StreamHidingObject), null);
        await Assert.That(store.WasRepaired).IsTrue();
        await Assert.That(StoreReading.GetObject(store, new(HiddenObject, 0)).IsNull).IsTrue();
    }

    /// <summary>A stream with neither /Length nor endstream runs to endobj.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StreamWithoutLengthOrEndstreamRunsToEndobj()
    {
        using var store = StoreOpening.Open(Encoding.Latin1.GetBytes(StreamWithoutEnd), null);
        await Assert.That(StoreReading.GetObject(store, new(OpenStream, 0)).AsStream()!.RawLength).IsEqualTo(OpenStreamLength);
    }

    /// <summary>A dictionary key with no value before endobj leaves the keyword for the caller.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DictionaryKeyBeforeEndobjRewinds()
    {
        var result = ParseDamagedDictionary();
        await Assert.That(result.Value).IsEqualTo(1);
        await Assert.That(result.Next).IsEqualTo(PdfKeyword.EndObj);
    }

    /// <summary>A stray closing parenthesis is a one-byte token.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrayParenthesisIsOneByte()
    {
        var length = LexStrayParenthesis();
        await Assert.That(length).IsEqualTo(1);
    }

    /// <summary>An unfiltered inline image ends at its computed length even when its data holds "EI".</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InlineImageUsesComputedLength()
    {
        var (length, next) = ReadInlineImage();
        await Assert.That(length).IsEqualTo(InlineLength);
        await Assert.That(next).IsEqualTo(ContentOperator.Restore);
    }

    /// <summary>Parses a dictionary missing its value and close, then reads the next token.</summary>
    /// <returns>The value of /A and the keyword left for the caller.</returns>
    private static DamagedResult ParseDamagedDictionary()
    {
        var parser = new PdfParser(Encoding.Latin1.GetBytes("<< /A 1 /B endobj"), 0, null, new());
        var dictionary = parser.ParseValue().AsDictionary()!;
        _ = parser.NextToken();
        return new((int)dictionary.GetInteger(KnownName.A), parser.Keyword);
    }

    /// <summary>Reads a lone closing parenthesis.</summary>
    /// <returns>The length of the token, or -1 when it is not a keyword.</returns>
    private static int LexStrayParenthesis()
    {
        var lexer = new PdfLexer(") abc"u8);
        return lexer.Next() == PdfTokenKind.Keyword ? lexer.Lexeme.Length : -1;
    }

    /// <summary>Reads the sample inline image and the operator after it.</summary>
    /// <returns>The data length and the next operator.</returns>
    private static InlineResult ReadInlineImage()
    {
        var content = Encoding.Latin1.GetBytes(InlineImage);
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, new(), operands);
        _ = reader.Next(out _);
        var length = reader.InlineImageData.End.Value - reader.InlineImageData.Start.Value;
        _ = reader.Next(out var next);
        return new(length, next);
    }

    /// <summary>The outcome of parsing a damaged dictionary.</summary>
    /// <param name="Value">The value of /A.</param>
    /// <param name="Next">The keyword after the dictionary.</param>
    private readonly record struct DamagedResult(int Value, PdfKeyword Next);

    /// <summary>The outcome of reading an inline image.</summary>
    /// <param name="Length">The data length.</param>
    /// <param name="Next">The operator after the image.</param>
    private readonly record struct InlineResult(int Length, ContentOperator Next);
}
