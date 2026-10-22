// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Content;

/// <summary>
/// Reads a content stream one operator at a time, collecting its operands on an inline buffer. Strings, arrays and
/// dictionaries are recorded as byte ranges and skipped with the lexer, so reading a page allocates nothing. Operands
/// beyond the buffer's capacity keep the most recent ones, which is what operators read.
/// </summary>
internal ref struct ContentReader
{
    /// <summary>The operand slots callers allocate: enough for any operator, including 32-component DeviceN colours.</summary>
    internal const int OperandSlots = 40;

    /// <summary>The bytes after <c>ID</c> that are always white space.</summary>
    private const int InlineDataSeparator = 1;

    /// <summary>The length of the <c>EI</c> keyword.</summary>
    private const int EndImageLength = 2;

    /// <summary>How many bytes after <c>EI</c> are checked for binary data.</summary>
    private const int ProbeLength = 16;

    /// <summary>The first printable ASCII byte.</summary>
    private const byte PrintableFirst = (byte)' ';

    /// <summary>The last printable ASCII byte.</summary>
    private const byte PrintableLast = (byte)'~';

    /// <summary>The content.</summary>
    private readonly ReadOnlySpan<byte> _content;

    /// <summary>The name table operand names are interned in.</summary>
    private readonly PdfNameTable _names;

    /// <summary>The operands of the current operator.</summary>
    private readonly Span<ContentOperand> _operands;

    /// <summary>The tokenizer.</summary>
    private PdfLexer _lexer;

    /// <summary>Initializes a new instance of the <see cref="ContentReader"/> struct.</summary>
    /// <param name="content">The decoded content.</param>
    /// <param name="names">The document's name table.</param>
    /// <param name="operands">The operand buffer, usually <see cref="OperandSlots"/> stack slots.</param>
    internal ContentReader(ReadOnlySpan<byte> content, PdfNameTable names, Span<ContentOperand> operands)
    {
        _content = content;
        _names = names;
        _operands = operands;
        _lexer = new(content);
    }

    /// <summary>Gets the content.</summary>
    internal readonly ReadOnlySpan<byte> Content => _content;

    /// <summary>Gets or sets the offset of the next byte to read, so a long stream can be read in slices.</summary>
    internal int Position
    {
        readonly get => _lexer.Position;
        set => _lexer.Position = value;
    }

    /// <summary>Gets the number of operands of the current operator.</summary>
    internal int OperandCount { get; private set; }

    /// <summary>Gets the range of the current inline image's dictionary entries, between <c>BI</c> and <c>ID</c>.</summary>
    internal Range InlineImageDictionary { get; private set; }

    /// <summary>Gets the range of the current inline image's data.</summary>
    internal Range InlineImageData { get; private set; }

    /// <summary>Gets an operand counted from the first.</summary>
    /// <param name="index">The operand index.</param>
    /// <returns>The operand, or a missing operand when out of range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly ContentOperand Operand(int index) => (uint)index < (uint)OperandCount ? _operands[index] : default;

    /// <summary>Gets a numeric operand.</summary>
    /// <param name="index">The operand index.</param>
    /// <returns>The number, or zero when missing or not a number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly float Number(int index) => Operand(index).Number;

    /// <summary>Gets the bytes of a string, array or dictionary operand.</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly ReadOnlySpan<byte> Body(in ContentOperand operand) => _content.Slice(operand.Start, operand.Length);

    /// <summary>Reads the next operator and its operands.</summary>
    /// <param name="op">The operator.</param>
    /// <returns><see langword="false"/> at the end of the content.</returns>
    internal bool Next(out ContentOperator op)
    {
        OperandCount = 0;
        op = ContentOperator.Unknown;
        var found = false;
        var reading = true;
        while (reading)
        {
            var kind = _lexer.Next();
            if (kind == PdfTokenKind.EndOfData)
            {
                op = ContentOperator.Unknown;
                reading = false;
            }
            else if (kind == PdfTokenKind.Keyword && TryReadOperator(out op))
            {
                found = true;
                reading = false;
            }
            else
            {
                ReadOperand(kind);
            }
        }

        return found;
    }

    /// <summary>Handles a keyword: an operator, or true, false or null operands.</summary>
    /// <param name="op">The operator.</param>
    /// <returns><see langword="true"/> when the keyword is an operator, including unknown ones.</returns>
    private bool TryReadOperator(out ContentOperator op)
    {
        var lexeme = _lexer.Lexeme;
        op = OperatorTable.Lookup(lexeme);
        if (op == ContentOperator.BeginInlineImage)
        {
            ReadInlineImage();
            return true;
        }

        if (op != ContentOperator.Unknown)
        {
            return true;
        }

        var keyword = PdfKeywords.Classify(lexeme);
        if (keyword is PdfKeyword.True or PdfKeyword.False)
        {
            Push(new(ContentOperandKind.Boolean, keyword == PdfKeyword.True ? 1 : 0, default, 0, 0));
            return false;
        }

        // Null is an operand; any other unknown keyword is an operator the caller skips, clearing the operands.
        if (keyword == PdfKeyword.Null)
        {
            Push(default);
            return false;
        }

        return true;
    }

    /// <summary>Records an operand.</summary>
    /// <param name="kind">The token kind.</param>
    private void ReadOperand(PdfTokenKind kind)
    {
        switch (kind)
        {
            case PdfTokenKind.Number:
            {
                _ = PdfNumber.TryParseSingle(_lexer.Lexeme, out var number);
                Push(new(ContentOperandKind.Number, number, default, 0, 0));
                break;
            }

            case PdfTokenKind.Name:
            {
                Push(new(ContentOperandKind.Name, 0, InternName(_lexer.Lexeme), 0, 0));
                break;
            }

            case PdfTokenKind.LiteralString or PdfTokenKind.HexString:
            {
                var stringKind = kind == PdfTokenKind.LiteralString ? ContentOperandKind.LiteralString : ContentOperandKind.HexString;
                Push(new(stringKind, 0, default, _lexer.LexemeStart, _lexer.LexemeLength));
                break;
            }

            case PdfTokenKind.ArrayStart or PdfTokenKind.DictionaryStart:
            {
                ReadComposite(kind);
                break;
            }

            default:
            {
                // Stray closing brackets and braces are ignored.
                break;
            }
        }
    }

    /// <summary>Records an array or dictionary as a range, skipping its body with the lexer.</summary>
    /// <param name="kind">The opening token.</param>
    private void ReadComposite(PdfTokenKind kind)
    {
        var start = kind == PdfTokenKind.ArrayStart ? _lexer.Position : _lexer.LexemeStart;
        var depth = 1;
        var end = _content.Length;
        while (depth > 0)
        {
            var next = _lexer.Next();
            if (next == PdfTokenKind.EndOfData)
            {
                break;
            }

            depth += next is PdfTokenKind.ArrayStart or PdfTokenKind.DictionaryStart ? 1 : 0;
            depth -= next is PdfTokenKind.ArrayEnd or PdfTokenKind.DictionaryEnd ? 1 : 0;
            end = kind == PdfTokenKind.ArrayStart ? _lexer.LexemeStart : _lexer.Position;
        }

        var operandKind = kind == PdfTokenKind.ArrayStart ? ContentOperandKind.Array : ContentOperandKind.Dictionary;
        Push(new(operandKind, 0, default, start, Math.Max(0, end - start)));
    }

    /// <summary>Reads an inline image: its dictionary entries up to <c>ID</c>, then its data up to <c>EI</c>.</summary>
    private void ReadInlineImage()
    {
        var dictionaryStart = _lexer.Position;
        var dictionaryEnd = dictionaryStart;
        var kind = _lexer.Next();
        while (kind != PdfTokenKind.Keyword || !_lexer.Lexeme.SequenceEqual("ID"u8))
        {
            if (kind == PdfTokenKind.EndOfData)
            {
                InlineImageDictionary = new(dictionaryStart, _content.Length);
                InlineImageData = new(_content.Length, _content.Length);
                return;
            }

            dictionaryEnd = _lexer.Position;
            kind = _lexer.Next();
        }

        InlineImageDictionary = new(dictionaryStart, dictionaryEnd);
        var dataStart = Math.Min(_content.Length, _lexer.Position + InlineDataSeparator);
        var dataEnd = FindEndImage(dataStart, _content[dictionaryStart..dictionaryEnd], out var resume);
        InlineImageData = new(dataStart, dataEnd);
        _lexer.Position = resume;
    }

    /// <summary>
    /// Finds the end of inline image data. An unfiltered image of a known size ends where its size says, when <c>EI</c>
    /// follows; otherwise the end is the first white space plus <c>EI</c> that is followed by text rather than binary.
    /// </summary>
    /// <param name="start">The first data byte.</param>
    /// <param name="dictionary">The image dictionary entries.</param>
    /// <param name="resume">The offset after the <c>EI</c> keyword.</param>
    /// <returns>The offset of the end of the data.</returns>
    private readonly int FindEndImage(int start, ReadOnlySpan<byte> dictionary, out int resume)
    {
        resume = 0;
        var computed = InlineImageSize.TryGetDataLength(dictionary, out var length) && start + length <= _content.Length
            && TryMatchEndImage(start + (int)length, out resume);
        return computed ? start + (int)length : ScanForEndImage(start, out resume);
    }

    /// <summary>Checks for <c>EI</c> after optional white space at the end of computed image data.</summary>
    /// <param name="dataEnd">The offset where the data should end.</param>
    /// <param name="resume">The offset after the <c>EI</c> keyword.</param>
    /// <returns><see langword="true"/> when <c>EI</c> follows.</returns>
    private readonly bool TryMatchEndImage(int dataEnd, out int resume)
    {
        resume = 0;
        var rest = _content[dataEnd..];
        var skip = rest.IndexOfAnyExcept(PdfCharacters.Whitespace);
        if (skip < 0 || !rest[skip..].StartsWith("EI"u8))
        {
            return false;
        }

        var at = dataEnd + skip;
        if (!EndsAtToken(at) || !LooksLikeContent(at + EndImageLength))
        {
            return false;
        }

        resume = at + EndImageLength;
        return true;
    }

    /// <summary>Scans for the <c>EI</c> that ends inline image data: white space, then EI, then text.</summary>
    /// <param name="start">The first data byte.</param>
    /// <param name="resume">The offset after the <c>EI</c> keyword.</param>
    /// <returns>The offset of the end of the data.</returns>
    private readonly int ScanForEndImage(int start, out int resume)
    {
        var position = start;
        while (position < _content.Length)
        {
            var found = _content[position..].IndexOf("EI"u8);
            if (found < 0)
            {
                break;
            }

            var at = position + found;
            if (at > start && PdfCharacters.IsWhitespace(_content[at - 1]) && EndsAtToken(at) && LooksLikeContent(at + EndImageLength))
            {
                // Only the white space that delimits EI is dropped from the data.
                resume = at + EndImageLength;
                return at - 1;
            }

            position = at + 1;
        }

        resume = _content.Length;
        return _content.Length;
    }

    /// <summary>Determines whether the <c>EI</c> at an offset ends a token.</summary>
    /// <param name="at">The offset of <c>EI</c>.</param>
    /// <returns><see langword="true"/> when white space, a delimiter or the end follows.</returns>
    private readonly bool EndsAtToken(int at) => at + EndImageLength >= _content.Length || PdfCharacters.EndsToken(_content[at + EndImageLength]);

    /// <summary>Checks that the bytes after an <c>EI</c> are text: binary image data that happens to hold "EI" is not.</summary>
    /// <param name="from">The offset after <c>EI</c>.</param>
    /// <returns><see langword="true"/> when the following bytes are white space or printable ASCII.</returns>
    private readonly bool LooksLikeContent(int from)
    {
        var window = _content[from..Math.Min(_content.Length, from + ProbeLength)];
        foreach (var b in window)
        {
            if (b is (< PrintableFirst or > PrintableLast) && !PdfCharacters.IsWhitespace(b))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Interns a name operand, decoding #xx escapes.</summary>
    /// <param name="raw">The name's bytes.</param>
    /// <returns>The name.</returns>
    private readonly PdfName InternName(ReadOnlySpan<byte> raw)
    {
        if (raw.Length > PdfLimits.MaxNameLength)
        {
            raw = raw[..PdfLimits.MaxNameLength];
        }

        if (raw.IndexOf((byte)'#') < 0)
        {
            return _names.Intern(raw);
        }

        Span<byte> decoded = stackalloc byte[PdfLimits.MaxNameLength];
        var length = PdfStringDecoder.DecodeName(raw, decoded);
        return _names.Intern(decoded[..length]);
    }

    /// <summary>Pushes an operand, dropping the oldest when the buffer is full.</summary>
    /// <param name="operand">The operand.</param>
    private void Push(ContentOperand operand)
    {
        if (OperandCount == _operands.Length)
        {
            _operands[1..].CopyTo(_operands);
            OperandCount--;
        }

        _operands[OperandCount] = operand;
        OperandCount++;
    }
}
