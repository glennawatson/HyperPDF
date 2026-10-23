// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Speech.Melo;

namespace HyperPdf.Core.Tests.Speech.Melo;

/// <summary>Tests ASCII preparation and Unicode normalization through both tokenizer inputs.</summary>
public sealed class WordPieceTokenizerTests
{
    /// <summary>The small vocabulary used by the normalization cases.</summary>
    private const string Vocabulary = "hello\nworld\ncafe\nnaive\n.\n!\n中\n";

    /// <summary>Span text keeps case folding, accent decomposition, word separation and dropped controls.</summary>
    /// <param name="text">The input text.</param>
    /// <param name="expected">The vocabulary pieces in order.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("HELLO world!", "hello world !")]
    [Arguments("Café naïve.", "cafe naive .")]
    [Arguments("CAFE\u0301", "cafe")]
    [Arguments("Hello\tworld", "hello world")]
    [Arguments("\0Hello", "hello")]
    [Arguments("HELLO中world", "hello 中 world")]
    public async Task NormalizesSpanAndStringText(string text, string expected)
    {
        var tokenizer = new WordPieceTokenizer(Vocabulary);
        var spanPieces = new List<int>();
        var stringPieces = new List<int>();

        tokenizer.Tokenize(text.AsSpan(), spanPieces);
        tokenizer.Tokenize(text, stringPieces);

        await Assert.That(string.Join(' ', spanPieces.Select(tokenizer.Piece))).IsEqualTo(expected);
        await Assert.That(string.Join(' ', stringPieces.Select(tokenizer.Piece))).IsEqualTo(expected);
    }
}
