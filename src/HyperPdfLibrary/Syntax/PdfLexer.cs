// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Syntax;

/// <summary>
/// Splits PDF bytes into tokens without allocating. White space, regular tokens and string bodies are found with
/// vectorised searches rather than byte-by-byte loops.
/// </summary>
/// <param name="data">The data.</param>
/// <param name="position">The first byte to read.</param>
internal ref struct PdfLexer(ReadOnlySpan<byte> data, int position)
{
    /// <summary>The length of the two-byte dictionary brackets.</summary>
    private const int DoubleBracket = 2;

    /// <summary>The data being read.</summary>
    private readonly ReadOnlySpan<byte> _data = data;

    /// <summary>Initializes a new instance of the <see cref="PdfLexer"/> struct at the start of the data.</summary>
    /// <param name="data">The data.</param>
    public PdfLexer(ReadOnlySpan<byte> data)
        : this(data, 0)
    {
    }

    /// <summary>Gets or sets the offset of the next byte to read.</summary>
    public int Position { get; set; } = position;

    /// <summary>Gets the data being read.</summary>
    public readonly ReadOnlySpan<byte> Data => _data;

    /// <summary>Gets the start of the last token read.</summary>
    public int LexemeStart { get; private set; }

    /// <summary>Gets the length of the last token read.</summary>
    public int LexemeLength { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a token has run into the end of the data, so a window of a larger file may have
    /// cut it short. It stays set once set, including after <see cref="Peek"/>.
    /// </summary>
    public bool ReachedEnd { get; private set; }

    /// <summary>Gets the bytes of the last token read.</summary>
    public readonly ReadOnlySpan<byte> Lexeme => _data.Slice(LexemeStart, LexemeLength);

    /// <summary>Gets the last token read as a structure keyword.</summary>
    public readonly PdfKeyword Keyword => PdfKeywords.Classify(Lexeme);

    /// <summary>Skips white space and comments.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SkipWhitespace()
    {
        while (Position < _data.Length)
        {
            var c = _data[Position];
            if (c == '%')
            {
                SkipLine();
                continue;
            }

            if (!PdfCharacters.IsWhitespace(c))
            {
                return;
            }

            var next = _data[Position..].IndexOfAnyExcept(PdfCharacters.Whitespace);
            Position = next < 0 ? _data.Length : Position + next;
        }
    }

    /// <summary>Moves past the end of the current line.</summary>
    internal void SkipLine()
    {
        var end = _data[Position..].IndexOfAny(PdfCharacters.LineEnd);
        Position = end < 0 ? _data.Length : Position + end;
        if (Position < _data.Length && _data[Position] == '\r')
        {
            Position++;
        }

        if (Position < _data.Length && _data[Position] == '\n')
        {
            Position++;
        }
    }

    /// <summary>Reads the next token.</summary>
    /// <returns>The token kind; the bytes are in <see cref="Lexeme"/>.</returns>
    internal PdfTokenKind Next()
    {
        SkipWhitespace();
        if (Position >= _data.Length)
        {
            LexemeStart = _data.Length;
            LexemeLength = 0;
            ReachedEnd = true;
            return PdfTokenKind.EndOfData;
        }

        var c = _data[Position];
        var kind = c switch
        {
            (byte)'/' => ReadName(),
            (byte)'(' => ReadLiteralString(),
            (byte)'<' => ReadAngle(),
            (byte)'>' => ReadDictionaryEnd(),
            (byte)'[' => Single(PdfTokenKind.ArrayStart),
            (byte)']' => Single(PdfTokenKind.ArrayEnd),
            (byte)'{' => Single(PdfTokenKind.BraceOpen),
            (byte)'}' => Single(PdfTokenKind.BraceClose),
            _ => ReadRegular(c),
        };

        ReachedEnd |= Position >= _data.Length;
        return kind;
    }

    /// <summary>Reads the next token without consuming it.</summary>
    /// <returns>The token kind.</returns>
    internal PdfTokenKind Peek()
    {
        var position = Position;
        var lexemeStart = LexemeStart;
        var lexemeLength = LexemeLength;
        var kind = Next();
        Position = position;
        LexemeStart = lexemeStart;
        LexemeLength = lexemeLength;
        return kind;
    }

    /// <summary>Reads a one-byte token.</summary>
    /// <param name="kind">The token kind.</param>
    /// <returns>The kind.</returns>
    private PdfTokenKind Single(PdfTokenKind kind)
    {
        LexemeStart = Position;
        LexemeLength = 1;
        Position++;
        return kind;
    }

    /// <summary>Reads a number or keyword. A stray ')' is a delimiter (ISO 32000-2 7.2.3), so it comes back alone as a one-byte keyword.</summary>
    /// <param name="first">The first byte.</param>
    /// <returns>The kind.</returns>
    private PdfTokenKind ReadRegular(byte first)
    {
        if (first == (byte)')')
        {
            return Single(PdfTokenKind.Keyword);
        }

        LexemeStart = Position;
        var end = _data[(Position + 1)..].IndexOfAny(PdfCharacters.TokenEnd);
        LexemeLength = end < 0 ? _data.Length - Position : end + 1;
        Position += LexemeLength;
        return first is (>= (byte)'0' and <= (byte)'9') or (byte)'-' or (byte)'+' or (byte)'.' ? PdfTokenKind.Number : PdfTokenKind.Keyword;
    }

    /// <summary>Reads a name.</summary>
    /// <returns>The kind.</returns>
    private PdfTokenKind ReadName()
    {
        LexemeStart = Position + 1;
        var end = _data[LexemeStart..].IndexOfAny(PdfCharacters.TokenEnd);
        LexemeLength = end < 0 ? _data.Length - LexemeStart : end;
        Position = LexemeStart + LexemeLength;
        return PdfTokenKind.Name;
    }

    /// <summary>Reads a hexadecimal string or the start of a dictionary.</summary>
    /// <returns>The kind.</returns>
    private PdfTokenKind ReadAngle()
    {
        if (Position + 1 < _data.Length && _data[Position + 1] == '<')
        {
            LexemeStart = Position;
            LexemeLength = DoubleBracket;
            Position += DoubleBracket;
            return PdfTokenKind.DictionaryStart;
        }

        LexemeStart = Position + 1;
        var end = _data[LexemeStart..].IndexOf((byte)'>');
        LexemeLength = end < 0 ? _data.Length - LexemeStart : end;
        Position = Math.Min(_data.Length, LexemeStart + LexemeLength + 1);
        return PdfTokenKind.HexString;
    }

    /// <summary>Reads the end of a dictionary, or a stray '&gt;'.</summary>
    /// <returns>The kind.</returns>
    private PdfTokenKind ReadDictionaryEnd()
    {
        LexemeStart = Position;
        if (Position + 1 < _data.Length && _data[Position + 1] == '>')
        {
            LexemeLength = DoubleBracket;
            Position += DoubleBracket;
            return PdfTokenKind.DictionaryEnd;
        }

        LexemeLength = 1;
        Position++;
        return PdfTokenKind.Keyword;
    }

    /// <summary>Reads a literal string, honouring nested parentheses and escapes.</summary>
    /// <returns>The kind.</returns>
    private PdfTokenKind ReadLiteralString()
    {
        LexemeStart = Position + 1;
        var depth = 1;
        var index = LexemeStart;
        while (index < _data.Length)
        {
            var found = _data[index..].IndexOfAny(PdfCharacters.LiteralStringSpecials);
            if (found < 0)
            {
                break;
            }

            index += found;
            var c = _data[index];
            if (c == '\\')
            {
                // Skip the escaped byte, whatever it is.
                index += DoubleBracket;
                continue;
            }

            depth += c == '(' ? 1 : -1;
            if (depth == 0)
            {
                LexemeLength = index - LexemeStart;
                Position = index + 1;
                return PdfTokenKind.LiteralString;
            }

            index++;
        }

        // An unterminated string runs to the end of the data, as other readers treat it.
        LexemeLength = _data.Length - LexemeStart;
        Position = _data.Length;
        return PdfTokenKind.LiteralString;
    }
}
