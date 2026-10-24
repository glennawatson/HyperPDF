// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Syntax;
using HyperPdfLibrary.Text;

namespace HyperPdf.Benchmarks;

/// <summary>Measures decoding hex strings and folding page text for search; allocations come from the EventPipe trace.</summary>
public class TextAndHexBenchmarks
{
    /// <summary>The characters of the page text.</summary>
    private const int PageCharacters = 50_000;

    /// <summary>The hex digits of a long Identity-H string.</summary>
    private const int HexDigits = 1024;

    /// <summary>The letters in the alphabet the page text cycles through.</summary>
    private const int Letters = 26;

    /// <summary>The divisor that puts the smart quote in the middle of the text.</summary>
    private const int MiddleDivisor = 2;

    /// <summary>The text of one hex digit pair, repeated to make the strings.</summary>
    private const string HexPair = "00A1";

    /// <summary>A letter that is not ASCII, put in the middle of the page text.</summary>
    private const char SmartQuote = '’';

    /// <summary>The page text: ASCII with one smart quote.</summary>
    private string _page = string.Empty;

    /// <summary>The same text with no smart quote.</summary>
    private string _asciiPage = string.Empty;

    /// <summary>A hex string with no white space.</summary>
    private byte[] _hex = null!;

    /// <summary>A hex string wrapped in white space, as some producers write it.</summary>
    private byte[] _hexWithSpace = null!;

    /// <summary>The decoded bytes.</summary>
    private byte[] _decoded = null!;

    /// <summary>Builds the inputs.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var chars = new char[PageCharacters];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)('a' + (i % Letters));
        }

        _asciiPage = new(chars);
        chars[PageCharacters / MiddleDivisor] = SmartQuote;
        _page = new(chars);
        var pairs = HexDigits / HexPair.Length;
        _hex = System.Text.Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat(HexPair, pairs)));
        _hexWithSpace = System.Text.Encoding.ASCII.GetBytes(string.Join(' ', Enumerable.Repeat(HexPair, pairs)));
        _decoded = new byte[HexDigits];
    }

    /// <summary>Decodes a hex string with no white space.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public int DecodeHex() => PdfStringDecoder.DecodeHex(_hex, _decoded);

    /// <summary>Decodes a hex string that has white space between the pairs.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public int DecodeHexWithSpace() => PdfStringDecoder.DecodeHex(_hexWithSpace, _decoded);

    /// <summary>Folds page text that is all ASCII.</summary>
    /// <returns>The folded length.</returns>
    [Benchmark]
    public int FoldAscii()
    {
        using var folded = TextFolding.FoldPooled(_asciiPage, PdfTextSearchOptions.None);
        return folded.Length;
    }

    /// <summary>Folds page text that is ASCII except one smart quote.</summary>
    /// <returns>The folded length.</returns>
    [Benchmark]
    public int FoldMostlyAscii()
    {
        using var folded = TextFolding.FoldPooled(_page, PdfTextSearchOptions.None);
        return folded.Length;
    }
}
