// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Syntax;

/// <summary>
/// Parses PDF values from a buffer or from a window of a file. Strings without escapes point into a buffer instead of
/// being copied (a window's strings are copied, as the window does not outlive the parse), and arrays and dictionaries
/// are collected on an operation-local stack so each is allocated once at its final size.
/// </summary>
internal ref struct PdfParser
{
    /// <summary>The bits in a hexadecimal digit, which halves the length of a hexadecimal string.</summary>
    private const int HexDigitsPerByte = 2;

    /// <summary>The buffer strings may point into, or <see langword="null"/> when parsing a window that strings must copy from.</summary>
    private readonly byte[]? _buffer;

    /// <summary>The objects references resolve against.</summary>
    private readonly PdfObjectStore? _owner;

    /// <summary>The name table.</summary>
    private readonly PdfNameTable _names;

    /// <summary>Decrypts strings, or <see langword="null"/> when they are not encrypted.</summary>
    private readonly PdfSecurityHandler? _security;

    /// <summary>The tokenizer.</summary>
    private PdfLexer _lexer;

    /// <summary>Initializes a new instance of the <see cref="PdfParser"/> struct.</summary>
    /// <param name="buffer">The buffer strings may point into.</param>
    /// <param name="position">The first byte to read.</param>
    /// <param name="owner">The objects references resolve against.</param>
    /// <param name="names">The name table.</param>
    /// <param name="security">Decrypts strings, or <see langword="null"/>.</param>
    public PdfParser(byte[] buffer, int position, PdfObjectStore? owner, PdfNameTable names, PdfSecurityHandler? security)
        : this(buffer, buffer, position, owner, names, security)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfParser"/> struct for unencrypted data.</summary>
    /// <param name="buffer">The buffer strings may point into.</param>
    /// <param name="position">The first byte to read.</param>
    /// <param name="owner">The objects references resolve against.</param>
    /// <param name="names">The name table.</param>
    public PdfParser(byte[] buffer, int position, PdfObjectStore? owner, PdfNameTable names)
        : this(buffer, position, owner, names, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfParser"/> struct.</summary>
    /// <param name="data">The bytes to parse.</param>
    /// <param name="buffer">The array behind <paramref name="data"/> that strings may point into, or <see langword="null"/>.</param>
    /// <param name="position">The first byte to read.</param>
    /// <param name="owner">The objects references resolve against.</param>
    /// <param name="names">The name table.</param>
    /// <param name="security">Decrypts strings, or <see langword="null"/>.</param>
    private PdfParser(ReadOnlySpan<byte> data, byte[]? buffer, int position, PdfObjectStore? owner, PdfNameTable names, PdfSecurityHandler? security)
    {
        _buffer = buffer;
        _owner = owner;
        _names = names;
        _security = security;
        _lexer = new(data, position);
    }

    /// <summary>Gets a value indicating whether a token ran into the end of the data, so a window may have cut the value short.</summary>
    public readonly bool ReachedEnd => _lexer.ReachedEnd;

    /// <summary>Gets or sets the offset of the next byte to read.</summary>
    public int Position
    {
        readonly get => _lexer.Position;
        set => _lexer.Position = value;
    }

    /// <summary>Gets or sets the object whose strings are being read, for decryption.</summary>
    public PdfObjectId EncryptionId { get; set; }

    /// <summary>Gets the bytes of the last token read.</summary>
    public readonly ReadOnlySpan<byte> Lexeme => _lexer.Lexeme;

    /// <summary>Gets the start of the last token read.</summary>
    public readonly int LexemeStart => _lexer.LexemeStart;

    /// <summary>Gets the last token read as a structure keyword.</summary>
    public readonly PdfKeyword Keyword => _lexer.Keyword;

    /// <summary>Creates a parser over a window of a file, whose strings are copied out as the window is short-lived.</summary>
    /// <param name="window">The bytes to parse.</param>
    /// <param name="position">The first byte to read.</param>
    /// <param name="owner">The objects references resolve against.</param>
    /// <param name="names">The name table.</param>
    /// <param name="security">Decrypts strings, or <see langword="null"/>.</param>
    /// <returns>The parser.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfParser ForWindow(ReadOnlySpan<byte> window, int position, PdfObjectStore? owner, PdfNameTable names, PdfSecurityHandler? security) =>
        new(window, null, position, owner, names, security);

    /// <summary>Reads the next token.</summary>
    /// <returns>The token kind.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfTokenKind NextToken() => _lexer.Next();

    /// <summary>Reads the next token without consuming it.</summary>
    /// <returns>The token kind.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfTokenKind PeekToken() => _lexer.Peek();

    /// <summary>Parses the next value.</summary>
    /// <returns>The value; null at the end of the data or where a value is missing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfValue ParseValue()
    {
        var kind = _lexer.Next();
        if (kind is not (PdfTokenKind.ArrayStart or PdfTokenKind.DictionaryStart))
        {
            return ParseScalar(kind);
        }

        ParseScratch scratch = default;
        return ParseValue(kind, 0, ref scratch);
    }

    /// <summary>Interns a name token, decoding its escapes.</summary>
    /// <param name="raw">The token.</param>
    /// <returns>The name.</returns>
    internal readonly PdfName InternName(ReadOnlySpan<byte> raw)
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

    /// <summary>Parses a keyword in value position: <c>true</c>, <c>false</c> or <c>null</c>.</summary>
    /// <param name="keyword">The keyword.</param>
    /// <returns>The value; null for any other keyword.</returns>
    private static PdfValue ParseKeyword(PdfKeyword keyword) => keyword switch
    {
        PdfKeyword.True => PdfValue.FromBoolean(true),
        PdfKeyword.False => PdfValue.FromBoolean(false),
        _ => default,
    };

    /// <summary>Determines whether a keyword ends an object, so a damaged value must stop before it.</summary>
    /// <param name="keyword">The keyword.</param>
    /// <returns><see langword="true"/> for <c>endobj</c>, <c>stream</c> and <c>obj</c>.</returns>
    private static bool IsObjectEnd(PdfKeyword keyword) => keyword is PdfKeyword.EndObj or PdfKeyword.Stream or PdfKeyword.Obj;

    /// <summary>Parses a value whose first token has been read.</summary>
    /// <param name="kind">The first token's kind.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="scratch">The stack for this parse.</param>
    /// <returns>The value.</returns>
    private PdfValue ParseValue(PdfTokenKind kind, int depth, ref ParseScratch scratch) => kind switch
    {
        PdfTokenKind.ArrayStart => ParseArray(depth, ref scratch),
        PdfTokenKind.DictionaryStart => ParseDictionary(depth, ref scratch),
        _ => ParseScalar(kind),
    };

    /// <summary>Parses a value that cannot contain nested values.</summary>
    /// <param name="kind">The token kind.</param>
    /// <returns>The value.</returns>
    private PdfValue ParseScalar(PdfTokenKind kind) => kind switch
    {
        PdfTokenKind.Number => ParseNumberOrReference(),
        PdfTokenKind.Name => PdfValue.FromName(InternName(_lexer.Lexeme)),
        PdfTokenKind.LiteralString => ParseLiteralString(),
        PdfTokenKind.HexString => ParseHexString(),
        PdfTokenKind.Keyword => ParseKeyword(_lexer.Keyword),
        _ => default,
    };

    /// <summary>Parses a number, or a reference when it is followed by a generation and <c>R</c>.</summary>
    /// <returns>The value.</returns>
    private PdfValue ParseNumberOrReference()
    {
        if (!PdfNumber.TryParse(_lexer.Lexeme, out var value))
        {
            return default;
        }

        if (value.Kind != PdfKind.Integer || value.AsInteger() is <= 0 or > PdfLimits.MaxObjectNumber)
        {
            return value;
        }

        // Look ahead for "gen R" without disturbing the lexer when it is not there.
        var mark = _lexer.Position;
        if (_lexer.Next() == PdfTokenKind.Number
            && PdfNumber.TryParse(_lexer.Lexeme, out var generation)
            && generation.Kind == PdfKind.Integer
            && _lexer.Next() == PdfTokenKind.Keyword
            && _lexer.Keyword == PdfKeyword.Reference)
        {
            return PdfValue.FromReference(new((int)value.AsInteger(), (int)Math.Clamp(generation.AsInteger(), 0, ushort.MaxValue)));
        }

        _lexer.Position = mark;
        return value;
    }

    /// <summary>Parses a literal string, pointing into the buffer when it has no escapes.</summary>
    /// <returns>The value.</returns>
    private readonly PdfValue ParseLiteralString()
    {
        var raw = _lexer.Lexeme;
        if (_security is null && PdfStringDecoder.IsPlainLiteral(raw))
        {
            return _buffer is null ? PdfValue.FromString(raw.ToArray()) : PdfValue.FromString(_buffer, _lexer.LexemeStart, raw.Length);
        }

        var decoded = new byte[raw.Length];
        var length = PdfStringDecoder.DecodeLiteral(raw, decoded);
        return Finish(decoded, length);
    }

    /// <summary>Parses a hexadecimal string.</summary>
    /// <returns>The value.</returns>
    private readonly PdfValue ParseHexString()
    {
        var raw = _lexer.Lexeme;
        var decoded = new byte[(raw.Length + 1) / HexDigitsPerByte];
        var length = PdfStringDecoder.DecodeHex(raw, decoded);
        return Finish(decoded, length);
    }

    /// <summary>Decrypts a decoded string when needed.</summary>
    /// <param name="decoded">The decoded bytes.</param>
    /// <param name="length">The number of decoded bytes.</param>
    /// <returns>The value.</returns>
    private readonly PdfValue Finish(byte[] decoded, int length)
    {
        if (_security is null || !EncryptionId.IsValid)
        {
            return PdfValue.FromString(decoded, 0, length);
        }

        var plain = _security.DecryptString(EncryptionId, decoded.AsSpan(0, length));
        return PdfValue.FromString(plain);
    }

    /// <summary>Parses an array whose opening bracket has been read.</summary>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="scratch">The stack for this parse.</param>
    /// <returns>The value.</returns>
    private PdfValue ParseArray(int depth, ref ParseScratch scratch)
    {
        var mark = scratch.Count;
        while (depth < PdfLimits.MaxNesting)
        {
            var kind = _lexer.Next();
            if (kind is PdfTokenKind.ArrayEnd or PdfTokenKind.EndOfData)
            {
                break;
            }

            if (kind is PdfTokenKind.DictionaryEnd || (kind == PdfTokenKind.Keyword && IsObjectEnd(_lexer.Keyword)))
            {
                // A missing ']' ends at the enclosing structure; leave the token for the caller.
                _lexer.Position = _lexer.LexemeStart;
                break;
            }

            scratch.Push(0, ParseValue(kind, depth + 1, ref scratch));
        }

        return PdfValue.FromArray(scratch.PopArray(mark, _owner));
    }

    /// <summary>Parses a dictionary whose opening brackets have been read.</summary>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="scratch">The stack for this parse.</param>
    /// <returns>The value.</returns>
    private PdfValue ParseDictionary(int depth, ref ParseScratch scratch)
    {
        var mark = scratch.Count;
        var more = depth < PdfLimits.MaxNesting;
        while (more)
        {
            more = ParseEntry(ref scratch, depth);
        }

        return PdfValue.FromDictionary(scratch.PopDictionary(mark, _owner));
    }

    /// <summary>Parses one dictionary entry onto the scratch stack.</summary>
    /// <param name="scratch">The scratch stack.</param>
    /// <param name="depth">The dictionary's nesting depth.</param>
    /// <returns><see langword="false"/> at the end of the dictionary.</returns>
    private bool ParseEntry(ref ParseScratch scratch, int depth)
    {
        var kind = _lexer.Next();
        if (kind != PdfTokenKind.Name)
        {
            // '>>' ends the dictionary; a keyword such as endobj ends a damaged one and is left for the caller.
            if (kind == PdfTokenKind.Keyword && IsObjectEnd(_lexer.Keyword))
            {
                _lexer.Position = _lexer.LexemeStart;
            }

            return kind is not (PdfTokenKind.DictionaryEnd or PdfTokenKind.EndOfData or PdfTokenKind.Keyword);
        }

        var key = InternName(_lexer.Lexeme);
        var valueKind = _lexer.Next();
        if (valueKind == PdfTokenKind.DictionaryEnd)
        {
            return false;
        }

        if (valueKind == PdfTokenKind.Keyword && IsObjectEnd(_lexer.Keyword))
        {
            // A key with no value before endobj or stream: leave the keyword for the caller.
            _lexer.Position = _lexer.LexemeStart;
            return false;
        }

        var value = ParseValue(valueKind, depth + 1, ref scratch);
        if (!value.IsNull)
        {
            scratch.Push(key.Id, value);
        }

        return true;
    }
}
