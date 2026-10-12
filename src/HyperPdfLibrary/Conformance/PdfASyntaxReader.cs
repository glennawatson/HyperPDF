// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads PDF tokens and keeps the lexical facts the PDF/A file structure rules test: hexadecimal digit counts, string and
/// name lengths, and number values as written. It reads leniently, like the veraPDF parser, so the facts match what veraPDF
/// reports: literal strings stop collecting after 65,536 bytes, and malformed numbers take the largest value.
/// </summary>
/// <param name="data">The bytes to read.</param>
/// <param name="position">The first byte to read.</param>
/// <param name="value">The buffer that receives the decoded bytes of strings, names and words.</param>
internal ref struct PdfASyntaxReader(ReadOnlySpan<byte> data, int position, PdfASyntaxBuffer value)
{
    /// <summary>The most bytes a literal string collects; later bytes are read but dropped.</summary>
    private const int LiteralLimit = 65_535;

    /// <summary>The hexadecimal digits of a name escape.</summary>
    private const int EscapeDigits = 2;

    /// <summary>The number of octal digits an escape may hold.</summary>
    private const int OctalDigits = 3;

    /// <summary>The bits one octal digit holds.</summary>
    private const int OctalBits = 3;

    /// <summary>The bits one hexadecimal digit holds.</summary>
    private const int HexBits = 4;

    /// <summary>The half added before rounding down.</summary>
    private const double Half = 0.5;

    /// <summary>The bytes read.</summary>
    private readonly ReadOnlySpan<byte> _data = data;

    /// <summary>The decoded token bytes.</summary>
    private readonly PdfASyntaxBuffer _value = value;

    /// <summary>Gets or sets the offset of the next byte to read.</summary>
    internal int Position { get; set; } = position;

    /// <summary>Gets the bytes read.</summary>
    internal readonly ReadOnlySpan<byte> Data => _data;

    /// <summary>Gets the kind of the last token.</summary>
    internal PdfASyntaxToken Kind { get; private set; }

    /// <summary>Gets the integer value of the last number.</summary>
    internal long Integer { get; private set; }

    /// <summary>Gets the real value of the last number.</summary>
    internal double Real { get; private set; }

    /// <summary>Gets a value indicating whether the last hexadecimal string held only hexadecimal digits and white space.</summary>
    internal bool OnlyHex { get; private set; }

    /// <summary>Gets the number of bytes other than white space in the last hexadecimal string.</summary>
    internal long HexCount { get; private set; }

    /// <summary>Gets the decoded bytes of the last string, name or word.</summary>
    internal readonly ReadOnlySpan<byte> Value => _value.Span;

    /// <summary>Determines whether the last token is a word with the given spelling.</summary>
    /// <param name="word">The spelling.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly bool IsWord(ReadOnlySpan<byte> word) => Kind == PdfASyntaxToken.Keyword && _value.Span.SequenceEqual(word);

    /// <summary>Skips white space, and comments when asked.</summary>
    /// <param name="comments">Whether comments count as white space.</param>
    internal void SkipSpaces(bool comments)
    {
        while (Position < _data.Length)
        {
            var c = _data[Position];
            if (PdfASyntaxCharacters.IsSpace(c))
            {
                Position++;
            }
            else if (c == '%' && comments)
            {
                SkipComment();
            }
            else
            {
                return;
            }
        }
    }

    /// <summary>Reads the next token.</summary>
    internal void Next()
    {
        SkipSpaces(true);
        _value.Clear();
        if (Position >= _data.Length)
        {
            Kind = PdfASyntaxToken.End;
            return;
        }

        var c = _data[Position];
        Position++;
        Kind = c switch
        {
            (byte)'(' => ReadLiteral(),
            (byte)'<' => ReadAngle(),
            (byte)'>' => ReadDictionaryEnd(),
            (byte)'[' => PdfASyntaxToken.ArrayStart,
            (byte)']' => PdfASyntaxToken.ArrayEnd,
            (byte)'/' => ReadName(),
            (byte)')' or (byte)'{' or (byte)'}' => PdfASyntaxToken.None,
            _ => ReadOther(c),
        };
    }

    /// <summary>Rounds a real to the nearest integer, saturating, like Java's <c>Math.round</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The integer.</returns>
    private static long Round(double value)
    {
        var rounded = Math.Floor(value + Half);
        return rounded >= long.MaxValue ? long.MaxValue : (long)rounded;
    }

    /// <summary>Reads a number or a word whose first byte has been read.</summary>
    /// <param name="first">The first byte.</param>
    /// <returns>The token kind.</returns>
    private PdfASyntaxToken ReadOther(byte first)
    {
        switch (first)
        {
            case (byte)'+':
            {
                return ReadNumber();
            }

            case (byte)'-':
            {
                var kind = ReadNumber();
                Integer = -Integer;
                Real = -Real;
                return kind;
            }

            default:
            {
                Position--;
                return PdfASyntaxCharacters.IsDigit(first) || first == '.' ? ReadNumber() : ReadWord();
            }
        }
    }

    /// <summary>Skips a comment and the line end that closes it.</summary>
    private void SkipComment()
    {
        while (Position < _data.Length)
        {
            var c = _data[Position];
            Position++;
            if (PdfASyntaxCharacters.IsLineEnd(c))
            {
                return;
            }
        }
    }

    /// <summary>Reads digits and points as a number; malformed numbers take the largest value, as veraPDF does.</summary>
    /// <returns>The token kind.</returns>
    private PdfASyntaxToken ReadNumber()
    {
        var start = Position;
        var real = false;
        while (Position < _data.Length)
        {
            var c = _data[Position];
            if (c == '.')
            {
                real = true;
            }
            else if (!PdfASyntaxCharacters.IsDigit(c))
            {
                break;
            }

            Position++;
        }

        var text = _data[start..Position];
        if (real)
        {
            var parsed = double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number);
            Real = parsed ? number : double.MaxValue;
            Integer = parsed ? PdfASyntaxReader.Round(number) : long.MaxValue;
            return PdfASyntaxToken.Real;
        }

        var whole = long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var integer);
        Integer = whole ? integer : long.MaxValue;
        Real = whole ? integer : double.MaxValue;
        return PdfASyntaxToken.Integer;
    }

    /// <summary>Reads a word up to the next white space or delimiter.</summary>
    /// <returns>The token kind.</returns>
    private PdfASyntaxToken ReadWord()
    {
        while (Position < _data.Length && !PdfASyntaxCharacters.IsTokenEnd(_data[Position]))
        {
            _value.Add(_data[Position]);
            Position++;
        }

        return PdfASyntaxToken.Keyword;
    }

    /// <summary>Reads what follows an opening angle bracket.</summary>
    /// <returns>The token kind.</returns>
    private PdfASyntaxToken ReadAngle()
    {
        if (Position < _data.Length && _data[Position] == '<')
        {
            Position++;
            return PdfASyntaxToken.DictionaryStart;
        }

        if (Position < _data.Length && _data[Position] == '~')
        {
            Position++;
            return ReadAscii85();
        }

        return ReadHex();
    }

    /// <summary>Reads what follows a closing angle bracket.</summary>
    /// <returns>The token kind.</returns>
    private PdfASyntaxToken ReadDictionaryEnd()
    {
        if (Position < _data.Length && _data[Position] == '>')
        {
            Position++;
            return PdfASyntaxToken.DictionaryEnd;
        }

        return PdfASyntaxToken.Broken;
    }

    /// <summary>Reads a hexadecimal string, counting the bytes other than white space and noting any that are not digits.</summary>
    /// <returns>The token kind.</returns>
    private PdfASyntaxToken ReadHex()
    {
        OnlyHex = true;
        HexCount = 0;
        var pending = PdfASyntaxCharacters.NotHex;
        while (Position < _data.Length)
        {
            var c = _data[Position];
            Position++;
            if (c == '>')
            {
                break;
            }

            if (PdfASyntaxCharacters.IsSpace(c))
            {
                continue;
            }

            HexCount++;
            var digit = PdfASyntaxCharacters.HexValue(c);
            if (digit == PdfASyntaxCharacters.NotHex)
            {
                OnlyHex = false;
                continue;
            }

            pending = AddHexDigit(pending, digit);
        }

        if (pending != PdfASyntaxCharacters.NotHex)
        {
            _value.Add((byte)(pending << HexBits));
        }

        return PdfASyntaxToken.HexString;
    }

    /// <summary>Adds a hexadecimal digit, writing a byte when it completes a pair.</summary>
    /// <param name="pending">The first digit of the pair, or <see cref="PdfASyntaxCharacters.NotHex"/>.</param>
    /// <param name="digit">The digit.</param>
    /// <returns>The pending digit after this one.</returns>
    private readonly int AddHexDigit(int pending, int digit)
    {
        if (pending == PdfASyntaxCharacters.NotHex)
        {
            return digit;
        }

        _value.Add((byte)((pending << HexBits) | digit));
        return PdfASyntaxCharacters.NotHex;
    }

    /// <summary>Skips an ASCII base-85 string, which veraPDF reads as a hexadecimal string with no digits.</summary>
    /// <returns>The token kind.</returns>
    private PdfASyntaxToken ReadAscii85()
    {
        var end = _data[Position..].IndexOf("~>"u8);
        Position = end < 0 ? _data.Length : Position + end + "~>"u8.Length;
        OnlyHex = false;
        HexCount = 0;
        return PdfASyntaxToken.HexString;
    }

    /// <summary>Reads a name, decoding <c>#xx</c> escapes.</summary>
    /// <returns>The token kind.</returns>
    private PdfASyntaxToken ReadName()
    {
        while (Position < _data.Length)
        {
            var c = _data[Position];
            if (PdfASyntaxCharacters.IsTokenEnd(c))
            {
                break;
            }

            Position++;
            if (c != '#' || !TryReadNameEscape())
            {
                _value.Add(c);
            }
        }

        return PdfASyntaxToken.Name;
    }

    /// <summary>Reads the two hexadecimal digits after a number sign in a name.</summary>
    /// <returns><see langword="false"/> when they are not two digits, leaving the number sign to be kept as written.</returns>
    private bool TryReadNameEscape()
    {
        if (Position + 1 >= _data.Length)
        {
            return false;
        }

        var high = PdfASyntaxCharacters.HexValue(_data[Position]);
        var low = PdfASyntaxCharacters.HexValue(_data[Position + 1]);
        if (high == PdfASyntaxCharacters.NotHex || low == PdfASyntaxCharacters.NotHex)
        {
            return false;
        }

        _value.Add((byte)((high << HexBits) | low));
        Position += EscapeDigits;
        return true;
    }

    /// <summary>Reads a literal string, keeping at most 65,536 decoded bytes.</summary>
    /// <returns>The token kind.</returns>
    private PdfASyntaxToken ReadLiteral()
    {
        var depth = 0;
        while (Position < _data.Length)
        {
            var c = _data[Position];
            Position++;
            if (c == ')' && depth == 0)
            {
                break;
            }

            depth += c switch
            {
                (byte)'(' => 1,
                (byte)')' => -1,
                _ => 0,
            };
            AddLiteral(c == '\\' ? ReadEscape() : c);
        }

        return PdfASyntaxToken.LiteralString;
    }

    /// <summary>Adds a decoded literal string byte unless the string has reached its limit.</summary>
    /// <param name="decoded">The byte, or -1 when the escape adds nothing.</param>
    private readonly void AddLiteral(int decoded)
    {
        if (decoded >= 0 && _value.Length <= LiteralLimit)
        {
            _value.Add((byte)decoded);
        }
    }

    /// <summary>Reads the byte or bytes after a backslash in a literal string.</summary>
    /// <returns>The decoded byte, or -1 for a line continuation.</returns>
    private int ReadEscape()
    {
        if (Position >= _data.Length)
        {
            return -1;
        }

        var c = _data[Position];
        Position++;
        return c switch
        {
            (byte)'n' => '\n',
            (byte)'r' => '\r',
            (byte)'t' => '\t',
            (byte)'b' => '\b',
            (byte)'f' => '\f',
            PdfASyntaxCharacters.LineFeed => -1,
            PdfASyntaxCharacters.CarriageReturn => SkipContinuation(),
            >= (byte)'0' and <= (byte)'7' => ReadOctal(c),
            _ => c,
        };
    }

    /// <summary>Skips the line feed of a carriage-return line continuation.</summary>
    /// <returns>-1, as a continuation adds nothing.</returns>
    private int SkipContinuation()
    {
        if (Position < _data.Length && _data[Position] == PdfASyntaxCharacters.LineFeed)
        {
            Position++;
        }

        return -1;
    }

    /// <summary>Reads up to three octal digits.</summary>
    /// <param name="first">The first digit.</param>
    /// <returns>The byte they encode.</returns>
    private int ReadOctal(byte first)
    {
        var code = first - '0';
        for (var i = 1; i < OctalDigits && Position < _data.Length; i++)
        {
            var c = _data[Position];
            if (c is < (byte)'0' or > (byte)'7')
            {
                break;
            }

            code = (code << OctalBits) + (c - '0');
            Position++;
        }

        return code & byte.MaxValue;
    }
}
