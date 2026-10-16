// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Fonts.CMaps;

/// <summary>
/// Parses the PostScript-like syntax of CMap and ToUnicode streams: codespace, CID, notdef and bf sections, /WMode and
/// usecmap. Unknown operators are skipped, so damaged streams keep what they define.
/// </summary>
internal static class CMapParser
{
    /// <summary>The longest code, in bytes.</summary>
    private const int MaxCodeBytes = 4;

    /// <summary>The longest string read, in bytes.</summary>
    private const int MaxStringBytes = 512;

    /// <summary>The longest destination text, in UTF-16 units.</summary>
    private const int MaxTextChars = MaxStringBytes / 2;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bytes in one UTF-16 unit.</summary>
    private const int Utf16Bytes = 2;

    /// <summary>The kind of a CMap section keyword.</summary>
    private enum Section
    {
        /// <summary>Not a section keyword.</summary>
        None = 0,

        /// <summary>A begincodespacerange section.</summary>
        Codespace = 1,

        /// <summary>A begincidrange section.</summary>
        CidRange = 2,

        /// <summary>A begincidchar section.</summary>
        CidChar = 3,

        /// <summary>A beginnotdefrange section.</summary>
        NotdefRange = 4,

        /// <summary>A beginnotdefchar section.</summary>
        NotdefChar = 5,

        /// <summary>A beginbfchar section.</summary>
        BfChar = 6,

        /// <summary>A beginbfrange section.</summary>
        BfRange = 7,

        /// <summary>The usecmap operator.</summary>
        UseCMap = 8,
    }

    /// <summary>Parses a CMap stream.</summary>
    /// <param name="data">The decoded stream.</param>
    /// <returns>What the stream defines.</returns>
    internal static CMapContent Parse(ReadOnlySpan<byte> data)
    {
        var content = new CMapContent();
        var lexer = new PdfLexer(data);
        ReadOnlySpan<byte> lastName = [];
        for (var kind = lexer.Next(); kind != PdfTokenKind.EndOfData; kind = lexer.Next())
        {
            if (kind == PdfTokenKind.Name)
            {
                lastName = lexer.Lexeme;
                ReadWritingMode(ref lexer, content);
            }
            else if (kind == PdfTokenKind.Keyword)
            {
                RunSection(ref lexer, Classify(lexer.Lexeme), content, lastName);
            }
        }

        return content;
    }

    /// <summary>Reads the value after /WMode.</summary>
    /// <param name="lexer">The lexer, after a name.</param>
    /// <param name="content">The content.</param>
    private static void ReadWritingMode(ref PdfLexer lexer, CMapContent content)
    {
        if (!lexer.Lexeme.SequenceEqual("WMode"u8) || lexer.Peek() != PdfTokenKind.Number)
        {
            return;
        }

        _ = lexer.Next();
        content.WritingMode = int.TryParse(lexer.Lexeme, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mode) ? mode : 0;
    }

    /// <summary>Classifies a section keyword.</summary>
    /// <param name="keyword">The keyword.</param>
    /// <returns>The section.</returns>
    private static Section Classify(ReadOnlySpan<byte> keyword)
    {
        if (keyword.SequenceEqual("begincodespacerange"u8))
        {
            return Section.Codespace;
        }

        if (keyword.SequenceEqual("begincidrange"u8))
        {
            return Section.CidRange;
        }

        if (keyword.SequenceEqual("begincidchar"u8))
        {
            return Section.CidChar;
        }

        if (keyword.SequenceEqual("beginnotdefrange"u8))
        {
            return Section.NotdefRange;
        }

        if (keyword.SequenceEqual("beginnotdefchar"u8))
        {
            return Section.NotdefChar;
        }

        if (keyword.SequenceEqual("beginbfchar"u8))
        {
            return Section.BfChar;
        }

        if (keyword.SequenceEqual("beginbfrange"u8))
        {
            return Section.BfRange;
        }

        return keyword.SequenceEqual("usecmap"u8) ? Section.UseCMap : Section.None;
    }

    /// <summary>Reads one section.</summary>
    /// <param name="lexer">The lexer, after the section keyword.</param>
    /// <param name="section">The section.</param>
    /// <param name="content">The content.</param>
    /// <param name="lastName">The name before the keyword, which usecmap refers to.</param>
    private static void RunSection(ref PdfLexer lexer, Section section, CMapContent content, ReadOnlySpan<byte> lastName)
    {
        switch (section)
        {
            case Section.Codespace:
            {
                ReadCodespaces(ref lexer, content);
                break;
            }

            case Section.CidRange or Section.NotdefRange:
            {
                ReadCidEntries(ref lexer, section == Section.CidRange ? content.Cids : content.Notdefs, true, section == Section.CidRange);
                break;
            }

            case Section.CidChar or Section.NotdefChar:
            {
                ReadCidEntries(ref lexer, section == Section.CidChar ? content.Cids : content.Notdefs, false, false);
                break;
            }

            case Section.BfChar or Section.BfRange:
            {
                ReadBfEntries(ref lexer, content, section == Section.BfRange);
                break;
            }

            case Section.UseCMap:
            {
                content.UseCMap = lastName.ToArray();
                content.HasUseCMap = true;
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Reads codespace ranges.</summary>
    /// <param name="lexer">The lexer.</param>
    /// <param name="content">The content.</param>
    private static void ReadCodespaces(ref PdfLexer lexer, CMapContent content)
    {
        for (var kind = lexer.Next(); IsString(kind); kind = lexer.Next())
        {
            var hasLow = TryReadCode(ref lexer, kind, out var low, out var length);
            var hasHigh = TryReadCode(ref lexer, lexer.Next(), out var high, out var highLength);
            if (hasLow && hasHigh && length == highLength)
            {
                content.Codespaces.Add(new(length, low, high));
            }
        }
    }

    /// <summary>Reads CID or notdef entries: a code or code range followed by a CID.</summary>
    /// <param name="lexer">The lexer.</param>
    /// <param name="target">The ranges to add to.</param>
    /// <param name="ranges">Whether entries are ranges rather than single codes.</param>
    /// <param name="sequential">Whether the destination CID increases through each range.</param>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    private static void ReadCidEntries(ref PdfLexer lexer, CodeRangeMapBuilder target, bool ranges, bool sequential)
    {
        for (var kind = lexer.Next(); IsString(kind); kind = lexer.Next())
        {
            var hasLow = TryReadCode(ref lexer, kind, out var low, out _);
            var high = low;
            var hasHigh = !ranges || TryReadCode(ref lexer, lexer.Next(), out high, out _);
            if (lexer.Next() != PdfTokenKind.Number || !hasLow || !hasHigh
                || !int.TryParse(lexer.Lexeme, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cid))
            {
                continue;
            }

            ValidateCidRange(low, high, cid, sequential);
            target.Add(low, high, cid);
        }
    }

    /// <summary>Checks the destination width without treating constant notdef ranges as sequential CID ranges.</summary>
    /// <param name="low">The first code.</param>
    /// <param name="high">The last code.</param>
    /// <param name="cid">The first CID.</param>
    /// <param name="sequential">Whether the CID increases with each code.</param>
    /// <exception cref="PdfException">The range is inverted, negative or exceeds the signed CID width.</exception>
    private static void ValidateCidRange(uint low, uint high, int cid, bool sequential)
    {
        if (cid < 0 || high < low || (sequential && (ulong)cid + high - low > int.MaxValue))
        {
            throw new PdfException(PdfError.Format, "The CMap CID range exceeds its signed width.");
        }
    }

    /// <summary>Reads bfchar or bfrange entries.</summary>
    /// <param name="lexer">The lexer.</param>
    /// <param name="content">The content.</param>
    /// <param name="ranges">Whether entries are ranges rather than single codes.</param>
    private static void ReadBfEntries(ref PdfLexer lexer, CMapContent content, bool ranges)
    {
        for (var kind = lexer.Next(); IsString(kind); kind = lexer.Next())
        {
            var hasLow = TryReadCode(ref lexer, kind, out var low, out _);
            var high = low;
            var hasHigh = !ranges || TryReadCode(ref lexer, lexer.Next(), out high, out _);
            var destination = lexer.Next();
            if (!hasLow || !hasHigh)
            {
                continue;
            }

            if (destination == PdfTokenKind.ArrayStart)
            {
                ReadBfArray(ref lexer, content, low, high);
            }
            else if (TryReadText(ref lexer, destination, content, out var start))
            {
                content.Unicode.Add(low, high, start);
            }
        }
    }

    /// <summary>Reads the array form of a bfrange, which gives each code its own text.</summary>
    /// <param name="lexer">The lexer, after the opening bracket.</param>
    /// <param name="content">The content.</param>
    /// <param name="low">The first code.</param>
    /// <param name="high">The last code.</param>
    private static void ReadBfArray(ref PdfLexer lexer, CMapContent content, uint low, uint high)
    {
        ulong code = low;
        for (var kind = lexer.Next(); kind is not (PdfTokenKind.ArrayEnd or PdfTokenKind.EndOfData); kind = lexer.Next())
        {
            if (TryReadText(ref lexer, kind, content, out var start) && code <= high)
            {
                content.Unicode.Add((uint)code, (uint)code, start);
            }

            code++;
        }
    }

    /// <summary>Reads a destination: UTF-16BE bytes or a glyph name.</summary>
    /// <param name="lexer">The lexer, at the destination token.</param>
    /// <param name="kind">The destination token's kind.</param>
    /// <param name="content">The content, which stores the text.</param>
    /// <param name="start">Where the text starts.</param>
    /// <returns><see langword="true"/> when the destination gave some text.</returns>
    private static bool TryReadText(ref PdfLexer lexer, PdfTokenKind kind, CMapContent content, out int start)
    {
        start = 0;
        Span<char> text = stackalloc char[MaxTextChars];
        int length;
        if (kind == PdfTokenKind.Name)
        {
            _ = GlyphList.TryGetUnicode(lexer.Lexeme, text, out length);
        }
        else if (IsString(kind))
        {
            Span<byte> bytes = stackalloc byte[MaxStringBytes];
            length = DecodeUtf16(bytes[..ReadString(ref lexer, kind, bytes)], text);
        }
        else
        {
            return false;
        }

        if (length == 0)
        {
            return false;
        }

        start = content.AddText(text[..length]);
        return true;
    }

    /// <summary>Decodes UTF-16BE bytes; a lone byte is taken as one unit, as some writers produce.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="text">Receives the UTF-16 units.</param>
    /// <returns>The number of units.</returns>
    private static int DecodeUtf16(ReadOnlySpan<byte> bytes, Span<char> text)
    {
        if (bytes.Length == 1)
        {
            text[0] = (char)bytes[0];
            return 1;
        }

        var count = bytes.Length / Utf16Bytes;
        for (var i = 0; i < count; i++)
        {
            text[i] = (char)((bytes[i * Utf16Bytes] << ByteBits) | bytes[(i * Utf16Bytes) + 1]);
        }

        return count;
    }

    /// <summary>Reads a string token as a code.</summary>
    /// <param name="lexer">The lexer, at the token.</param>
    /// <param name="kind">The token's kind.</param>
    /// <param name="code">The code, big-endian.</param>
    /// <param name="length">The code length in bytes.</param>
    /// <returns><see langword="true"/> when the token is a string of one to four bytes.</returns>
    private static bool TryReadCode(ref PdfLexer lexer, PdfTokenKind kind, out uint code, out int length)
    {
        code = 0;
        length = 0;
        if (!IsString(kind))
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[MaxStringBytes];
        length = ReadString(ref lexer, kind, bytes);
        if (length is 0 or > MaxCodeBytes)
        {
            return false;
        }

        foreach (var b in bytes[..length])
        {
            code = (code << ByteBits) | b;
        }

        return true;
    }

    /// <summary>Decodes a string token.</summary>
    /// <param name="lexer">The lexer, at the token.</param>
    /// <param name="kind">The token's kind.</param>
    /// <param name="destination">Receives the bytes; longer strings are cut.</param>
    /// <returns>The number of bytes.</returns>
    private static int ReadString(ref PdfLexer lexer, PdfTokenKind kind, scoped Span<byte> destination)
    {
        var raw = lexer.Lexeme;
        return kind == PdfTokenKind.HexString
            ? PdfStringDecoder.DecodeHex(raw[..Math.Min(raw.Length, destination.Length * Utf16Bytes)], destination)
            : PdfStringDecoder.DecodeLiteral(raw[..Math.Min(raw.Length, destination.Length)], destination);
    }

    /// <summary>Determines whether a token is a string.</summary>
    /// <param name="kind">The token kind.</param>
    /// <returns><see langword="true"/> for hexadecimal and literal strings.</returns>
    private static bool IsString(PdfTokenKind kind) => kind is PdfTokenKind.HexString or PdfTokenKind.LiteralString;
}
