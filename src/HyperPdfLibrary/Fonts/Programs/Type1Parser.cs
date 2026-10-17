// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>
/// Parses Type 1 font programs: PFB segments, PFA hexadecimal or binary eexec sections, and the raw FontFile streams of
/// PDF files. The cleartext gives the matrix, bounding box and encoding; the decrypted private part gives the
/// subroutines and charstrings.
/// </summary>
internal static class Type1Parser
{
    /// <summary>The marker that starts a PFB segment.</summary>
    private const byte PfbMarker = 0x80;

    /// <summary>The PFB segment type of ASCII text.</summary>
    private const int PfbAscii = 1;

    /// <summary>The PFB segment type of binary data.</summary>
    private const int PfbBinary = 2;

    /// <summary>The size of a PFB segment header.</summary>
    private const int PfbHeaderSize = 6;

    /// <summary>The offset of the length in a PFB segment header.</summary>
    private const int PfbLengthOffset = 2;

    /// <summary>The most white space allowed between eexec and the encrypted data when trusting Length1.</summary>
    private const int MaxEexecGap = 4;

    /// <summary>The default number of random bytes that start each charstring.</summary>
    private const int DefaultLenIV = 4;

    /// <summary>The number of values in a font matrix.</summary>
    private const int MatrixValues = 6;

    /// <summary>The number of values in a bounding box.</summary>
    private const int BoxValues = 4;

    /// <summary>The index of the third value.</summary>
    private const int Third = 2;

    /// <summary>The index of the fourth value.</summary>
    private const int Fourth = 3;

    /// <summary>The index of the fifth value.</summary>
    private const int Fifth = 4;

    /// <summary>The index of the sixth value.</summary>
    private const int Sixth = 5;

    /// <summary>The largest number of subroutines or charstrings read.</summary>
    private const int MaxEntries = 65_535;

    /// <summary>Parses a Type 1 font.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="length1">The length of the cleartext part from the stream's /Length1, or zero when unknown.</param>
    /// <param name="parts">The parsed pieces.</param>
    /// <returns><see langword="true"/> when the font has charstrings.</returns>
    internal static bool TryParse(ReadOnlySpan<byte> data, int length1, out Type1Parts? parts)
    {
        parts = null;
        var clear = default(PooledBuffer);
        var encrypted = default(PooledBuffer);
        try
        {
            if (!TrySplit(data, length1, ref clear, ref encrypted) || encrypted.Length <= Type1Encryption.EexecPrefix)
            {
                return false;
            }

            Type1Encryption.Decrypt(encrypted.Array.AsSpan(0, encrypted.Length), Type1Encryption.EexecKey);
            return TryReadParts(clear.WrittenSpan, encrypted.WrittenSpan[Type1Encryption.EexecPrefix..], out parts);
        }
        finally
        {
            encrypted.Dispose();
            clear.Dispose();
        }
    }

    /// <summary>Reads the decrypted font, rejecting a missing charstring section before creating parse builders.</summary>
    /// <param name="clear">The cleartext header.</param>
    /// <param name="secret">The decrypted private section without its random prefix.</param>
    /// <param name="parts">The owned parsed pieces.</param>
    /// <returns>True when at least one glyph was read.</returns>
    private static bool TryReadParts(ReadOnlySpan<byte> clear, ReadOnlySpan<byte> secret, out Type1Parts? parts)
    {
        parts = null;
        if (secret.IndexOf("/CharStrings"u8) < 0)
        {
            return false;
        }

        var names = new ArrayBufferWriter<byte>();
        var header = ReadCleartext(clear, names);
        var lenIV = FindLenIV(secret);
        var charData = new ArrayBufferWriter<byte>();
        var subrs = ReadSubrs(secret, lenIV, charData, out var subrsEnd);
        var charStringsAt = secret[subrsEnd..].IndexOf("/CharStrings"u8);
        if (charStringsAt < 0)
        {
            return false;
        }

        var (glyphs, glyphNames) = ReadCharStrings(secret, subrsEnd + charStringsAt, lenIV, charData, names);
        if (glyphs.Length == 0)
        {
            return false;
        }

        parts = new(charData.WrittenSpan.ToArray(), subrs, glyphs, names.WrittenSpan.ToArray(), glyphNames, header.Matrix, header.Box, header.Standard, header.Encoding);
        return true;
    }

    /// <summary>Splits a font into its cleartext and its still-encrypted eexec part.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="length1">The cleartext length from /Length1, or zero.</param>
    /// <param name="clear">Receives the cleartext.</param>
    /// <param name="encrypted">Receives the encrypted part, in binary.</param>
    /// <returns><see langword="true"/> when an eexec part was found.</returns>
    private static bool TrySplit(ReadOnlySpan<byte> data, int length1, ref PooledBuffer clear, ref PooledBuffer encrypted)
    {
        if (!data.IsEmpty && data[0] == PfbMarker)
        {
            return ReadPfb(data, ref clear, ref encrypted);
        }

        var eexec = data.IndexOf("eexec"u8);
        if (eexec < 0)
        {
            return false;
        }

        var clearEnd = eexec + "eexec"u8.Length;
        clear.Write(data[..clearEnd]);
        var start = clearEnd;
        var trustLength = length1 >= clearEnd && length1 - clearEnd <= MaxEexecGap && length1 < data.Length && !data[clearEnd..length1].ContainsAnyExcept(PdfCharacters.Whitespace);
        if (trustLength)
        {
            start = length1;
        }
        else
        {
            var skip = data[clearEnd..].IndexOfAnyExcept(PdfCharacters.Whitespace);
            start = skip < 0 ? data.Length : clearEnd + skip;
        }

        var body = data[start..];
        if (Type1Encryption.IsHex(body))
        {
            Type1Encryption.DecodeHex(body, ref encrypted);
        }
        else
        {
            encrypted.Write(body);
        }

        return true;
    }

    /// <summary>Reads the segments of a PFB file.</summary>
    /// <param name="data">The file.</param>
    /// <param name="clear">The ASCII segments.</param>
    /// <param name="encrypted">The binary segments.</param>
    /// <returns><see langword="true"/> when there was a binary segment.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    private static bool ReadPfb(ReadOnlySpan<byte> data, ref PooledBuffer clear, ref PooledBuffer encrypted)
    {
        var length = 0;
        for (var position = 0; position <= data.Length - PfbHeaderSize && data[position] == PfbMarker; position += PfbHeaderSize + length)
        {
            var type = data[position + 1];
            var encodedLength = BinaryPrimitives.ReadUInt32LittleEndian(data[(position + PfbLengthOffset)..]);
            if (encodedLength > data.Length - position - PfbHeaderSize)
            {
                throw new PdfException(PdfError.Format, "The Type 1 segment length exceeds its data.");
            }

            length = (int)encodedLength;
            var segment = data.Slice(position + PfbHeaderSize, length);
            switch (type)
            {
                case PfbAscii:
                    {
                        clear.Write(segment);
                        break;
                    }

                case PfbBinary:
                    {
                        encrypted.Write(segment);
                        break;
                    }

                default:
                    {
                        return encrypted.Length > 0;
                    }
            }
        }

        return encrypted.Length > 0;
    }

    /// <summary>Reads the font matrix, bounding box and encoding from the cleartext.</summary>
    /// <param name="clear">The cleartext.</param>
    /// <param name="names">Receives the encoding's glyph names.</param>
    /// <returns>The values.</returns>
    private static Type1Header ReadCleartext(ReadOnlySpan<byte> clear, ArrayBufferWriter<byte> names)
    {
        Span<float> values = stackalloc float[MatrixValues];
        var matrix = FontMatrix.Default;
        var box = default(PdfRectangle);
        var standard = false;
        Type1EncodingEntry[] encoding = [];
        var lexer = new PdfLexer(clear);
        for (var kind = lexer.Next(); kind != PdfTokenKind.EndOfData; kind = lexer.Next())
        {
            if (kind != PdfTokenKind.Name)
            {
                continue;
            }

            var key = lexer.Lexeme;
            if (key.SequenceEqual("FontMatrix"u8) && ReadNumbers(ref lexer, values) == MatrixValues)
            {
                matrix = new(values[0], values[1], values[Third], values[Fourth], values[Fifth], values[Sixth]);
            }
            else if (key.SequenceEqual("FontBBox"u8) && ReadNumbers(ref lexer, values[..BoxValues]) == BoxValues)
            {
                box = PdfRectangle.FromCorners(values[0], values[1], values[Third], values[Fourth]);
            }
            else if (key.SequenceEqual("Encoding"u8))
            {
                (standard, encoding) = ReadEncoding(ref lexer, names);
            }
        }

        return new(matrix, box, standard, encoding);
    }

    /// <summary>Reads numbers inside brackets or braces.</summary>
    /// <param name="lexer">The lexer, before the opening bracket.</param>
    /// <param name="values">Receives the numbers.</param>
    /// <returns>The count read.</returns>
    private static int ReadNumbers(ref PdfLexer lexer, scoped Span<float> values)
    {
        var count = 0;
        for (var kind = lexer.Next(); kind is not (PdfTokenKind.EndOfData or PdfTokenKind.ArrayEnd or PdfTokenKind.BraceClose) && count < values.Length; kind = lexer.Next())
        {
            if (kind != PdfTokenKind.Number || !float.TryParse(lexer.Lexeme, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            values[count] = value;
            count++;
        }

        return count;
    }

    /// <summary>Reads the built-in encoding: StandardEncoding or a list of <c>dup code /name put</c> entries.</summary>
    /// <param name="lexer">The lexer, after the /Encoding name.</param>
    /// <param name="names">Receives the glyph names.</param>
    /// <returns>Whether the encoding is StandardEncoding, and the explicit entries.</returns>
    private static Type1EncodingSection ReadEncoding(ref PdfLexer lexer, ArrayBufferWriter<byte> names)
    {
        if (lexer.Next() == PdfTokenKind.Keyword && lexer.Lexeme.SequenceEqual("StandardEncoding"u8))
        {
            return new(true, []);
        }

        var entries = new List<Type1EncodingEntry>();
        for (var kind = lexer.Next(); kind != PdfTokenKind.EndOfData; kind = lexer.Next())
        {
            if (kind != PdfTokenKind.Keyword)
            {
                continue;
            }

            if (lexer.Lexeme.SequenceEqual("def"u8))
            {
                break;
            }

            if (lexer.Lexeme.SequenceEqual("dup"u8) && TryReadEncodingEntry(ref lexer, names, out var entry))
            {
                entries.Add(entry);
            }
        }

        return new(false, [.. entries]);
    }

    /// <summary>Reads the code and name after <c>dup</c>.</summary>
    /// <param name="lexer">The lexer, after <c>dup</c>.</param>
    /// <param name="names">Receives the glyph name.</param>
    /// <param name="entry">The code and the name's range.</param>
    /// <returns><see langword="true"/> when a code and name followed.</returns>
    private static bool TryReadEncodingEntry(ref PdfLexer lexer, ArrayBufferWriter<byte> names, out Type1EncodingEntry entry)
    {
        entry = default;
        if (lexer.Next() != PdfTokenKind.Number || !TryParseInt(lexer.Lexeme, out var code) || (uint)code > byte.MaxValue)
        {
            return false;
        }

        if (lexer.Next() != PdfTokenKind.Name)
        {
            return false;
        }

        entry = new(code, Append(names, lexer.Lexeme));
        return true;
    }

    /// <summary>Finds /lenIV in the private part.</summary>
    /// <param name="secret">The decrypted private part.</param>
    /// <returns>The number of random bytes before each charstring; -1 means charstrings are not encrypted.</returns>
    private static int FindLenIV(ReadOnlySpan<byte> secret)
    {
        var at = secret.IndexOf("/lenIV"u8);
        if (at < 0)
        {
            return DefaultLenIV;
        }

        var lexer = new PdfLexer(secret, at);
        _ = lexer.Next();
        return lexer.Next() == PdfTokenKind.Number && TryParseInt(lexer.Lexeme, out var value) ? value : DefaultLenIV;
    }

    /// <summary>Reads the /Subrs array.</summary>
    /// <param name="secret">The decrypted private part.</param>
    /// <param name="lenIV">The charstring lenIV.</param>
    /// <param name="charData">Receives the decrypted subroutines.</param>
    /// <param name="end">The offset after the last subroutine read.</param>
    /// <returns>The range of each subroutine.</returns>
    private static TableRange[] ReadSubrs(ReadOnlySpan<byte> secret, int lenIV, ArrayBufferWriter<byte> charData, out int end)
    {
        end = 0;
        var at = secret.IndexOf("/Subrs"u8);
        if (at < 0)
        {
            return [];
        }

        var lexer = new PdfLexer(secret, at);
        _ = lexer.Next();
        if (lexer.Next() != PdfTokenKind.Number || !TryParseInt(lexer.Lexeme, out var count) || count <= 0)
        {
            return [];
        }

        var subrs = new TableRange[Math.Min(count, MaxEntries)];
        ReadSubrEntries(ref lexer, lenIV, charData, subrs);
        end = Math.Min(lexer.Position, secret.Length);
        return subrs;
    }

    /// <summary>Reads subroutine entries until the table is full or /CharStrings begins.</summary>
    /// <param name="lexer">The lexer, after the subroutine count.</param>
    /// <param name="lenIV">The charstring lenIV.</param>
    /// <param name="charData">Receives the decrypted subroutines.</param>
    /// <param name="subrs">The subroutine table.</param>
    private static void ReadSubrEntries(ref PdfLexer lexer, int lenIV, ArrayBufferWriter<byte> charData, TableRange[] subrs)
    {
        var found = 0;
        for (var kind = lexer.Next(); kind != PdfTokenKind.EndOfData && found < subrs.Length; kind = lexer.Next())
        {
            if (kind == PdfTokenKind.Name && lexer.Lexeme.SequenceEqual("CharStrings"u8))
            {
                break;
            }

            if (kind == PdfTokenKind.Keyword && lexer.Lexeme.SequenceEqual("dup"u8) && TryReadSubr(ref lexer, lenIV, charData, subrs))
            {
                found++;
            }
        }
    }

    /// <summary>Reads one <c>dup index length RD binary NP</c> subroutine entry.</summary>
    /// <param name="lexer">The lexer, after <c>dup</c>.</param>
    /// <param name="lenIV">The charstring lenIV.</param>
    /// <param name="charData">Receives the decrypted subroutine.</param>
    /// <param name="subrs">The subroutine table.</param>
    /// <returns><see langword="true"/> when an entry was read.</returns>
    private static bool TryReadSubr(ref PdfLexer lexer, int lenIV, ArrayBufferWriter<byte> charData, TableRange[] subrs)
    {
        if (lexer.Next() != PdfTokenKind.Number || !TryParseInt(lexer.Lexeme, out var index) || lexer.Next() != PdfTokenKind.Number)
        {
            return false;
        }

        var range = ReadBinary(ref lexer, lenIV, charData);
        if ((uint)index >= (uint)subrs.Length || range.Length < 0)
        {
            return false;
        }

        subrs[index] = range;
        return true;
    }

    /// <summary>Reads the /CharStrings dictionary.</summary>
    /// <param name="secret">The decrypted private part.</param>
    /// <param name="at">The offset of /CharStrings.</param>
    /// <param name="lenIV">The charstring lenIV.</param>
    /// <param name="charData">Receives the decrypted charstrings.</param>
    /// <param name="names">Receives the glyph names.</param>
    /// <returns>The range of each charstring and of each name.</returns>
    private static Type1CharStrings ReadCharStrings(ReadOnlySpan<byte> secret, int at, int lenIV, ArrayBufferWriter<byte> charData, ArrayBufferWriter<byte> names)
    {
        var lexer = new PdfLexer(secret, at);
        _ = lexer.Next();
        var capacity = lexer.Next() == PdfTokenKind.Number && TryParseInt(lexer.Lexeme, out var count) ? Math.Clamp(count, 0, MaxEntries) : 0;
        var glyphs = new List<TableRange>(capacity);
        var glyphNames = new List<TableRange>(capacity);
        for (var kind = lexer.Next(); kind != PdfTokenKind.EndOfData && glyphs.Count < MaxEntries; kind = lexer.Next())
        {
            if (kind == PdfTokenKind.Keyword && lexer.Lexeme.SequenceEqual("end"u8))
            {
                break;
            }

            if (kind != PdfTokenKind.Name)
            {
                continue;
            }

            var name = lexer.Lexeme;
            var start = lexer.Position;
            if (lexer.Next() != PdfTokenKind.Number)
            {
                lexer.Position = start;
                continue;
            }

            var range = ReadBinary(ref lexer, lenIV, charData);
            if (range.Length < 0)
            {
                continue;
            }

            glyphNames.Add(Append(names, name));
            glyphs.Add(range);
        }

        return new([.. glyphs], [.. glyphNames]);
    }

    /// <summary>Reads <c>length RD binary</c> after the length token and decrypts the binary charstring.</summary>
    /// <param name="lexer">The lexer, positioned after the length number.</param>
    /// <param name="lenIV">The charstring lenIV.</param>
    /// <param name="charData">Receives the decrypted charstring.</param>
    /// <returns>The charstring's range, or a range with length -1 when the data is truncated.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    private static TableRange ReadBinary(ref PdfLexer lexer, int lenIV, ArrayBufferWriter<byte> charData)
    {
        if (lexer.Lexeme.IsEmpty || !TryParseInt(lexer.Lexeme, out var length))
        {
            return new(0, -1);
        }

        if (length < 0)
        {
            throw new PdfException(PdfError.Format, "The Type 1 charstring length is negative.");
        }

        _ = lexer.Next();

        // Exactly one space separates the RD token from the binary data.
        if (lexer.Position >= lexer.Data.Length || length > lexer.Data.Length - lexer.Position - 1)
        {
            throw new PdfException(PdfError.Format, "The Type 1 charstring length exceeds its data.");
        }

        var start = lexer.Position + 1;
        lexer.Position = start + length;
        return AppendCharstring(charData, lexer.Data.Slice(start, length), lenIV);
    }

    /// <summary>Decrypts a charstring and appends it without its random prefix.</summary>
    /// <param name="charData">The charstring buffer.</param>
    /// <param name="encrypted">The encrypted charstring.</param>
    /// <param name="lenIV">The number of random bytes, or -1 when not encrypted.</param>
    /// <returns>The appended range.</returns>
    private static TableRange AppendCharstring(ArrayBufferWriter<byte> charData, ReadOnlySpan<byte> encrypted, int lenIV)
    {
        var offset = charData.WrittenCount;
        var target = charData.GetSpan(encrypted.Length)[..encrypted.Length];
        encrypted.CopyTo(target);
        if (lenIV < 0)
        {
            charData.Advance(encrypted.Length);
            return new(offset, encrypted.Length);
        }

        Type1Encryption.Decrypt(target, Type1Encryption.CharstringKey);
        var skip = Math.Min(lenIV, encrypted.Length);
        target[skip..].CopyTo(target);
        charData.Advance(encrypted.Length - skip);
        return new(offset, encrypted.Length - skip);
    }

    /// <summary>Appends a name.</summary>
    /// <param name="names">The name buffer.</param>
    /// <param name="name">The name.</param>
    /// <returns>The name's range.</returns>
    private static TableRange Append(ArrayBufferWriter<byte> names, ReadOnlySpan<byte> name)
    {
        var offset = names.WrittenCount;
        names.Write(name);
        return new(offset, name.Length);
    }

    /// <summary>Parses an integer token.</summary>
    /// <param name="lexeme">The token.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the token is an integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseInt(ReadOnlySpan<byte> lexeme, out int value) =>
        int.TryParse(lexeme, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
}
