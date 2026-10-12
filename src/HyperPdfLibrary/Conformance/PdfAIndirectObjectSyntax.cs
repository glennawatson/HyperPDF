// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads one indirect object's bytes for the PDF/A file structure rules: the spacing of <c>obj</c> and <c>endobj</c>
/// (ISO 19005-2, 6.1.9), the implementation limits of every value inside it, and the stream keywords. It reads the way
/// veraPDF does, including how a bare integer object makes veraPDF test the bytes after <c>endobj</c>.
/// </summary>
internal static class PdfAIndirectObjectSyntax
{
    /// <summary>The numbers in an object header: the object number and the generation.</summary>
    private const int HeaderNumbers = 2;

    /// <summary>Gets the keyword that starts a stream's data.</summary>
    private static ReadOnlySpan<byte> StreamWord => "stream"u8;

    /// <summary>Gets the reference keyword.</summary>
    private static ReadOnlySpan<byte> ReferenceWord => "R"u8;

    /// <summary>Checks the object that starts at a position.</summary>
    /// <param name="reader">The reader, positioned where the cross-reference table says the object starts.</param>
    /// <param name="scan">The pass, which supplies stream lengths and receives the broken rules.</param>
    /// <param name="number">The object number.</param>
    /// <returns><see langword="false"/> when the object cannot be read, so none of its findings count.</returns>
    internal static bool Check(ref PdfASyntaxReader reader, PdfAFileScan scan, int number)
    {
        if (!ReadHeader(ref reader, out var headerSpacing))
        {
            return false;
        }

        var findings = scan.Findings;
        reader.Next();
        bool endSpacing;
        if (reader.Kind == PdfASyntaxToken.Integer)
        {
            endSpacing = ReadIntegerObject(ref reader, findings);
        }
        else
        {
            var value = PdfASyntaxValues.FromToken(ref reader, findings, 0);
            if (value == PdfASyntaxValue.Broken || (value == PdfASyntaxValue.Dictionary && !ReadStreamIfPresent(ref reader, scan, number)))
            {
                return false;
            }

            endSpacing = ReadEnd(ref reader);
        }

        findings.Require(headerSpacing && endSpacing, PdfARule.IndirectObjectSpacing);
        return true;
    }

    /// <summary>Checks an object packed in an object stream, which has no header or <c>endobj</c>.</summary>
    /// <param name="reader">The reader, at the object.</param>
    /// <param name="findings">Receives the broken rules.</param>
    /// <returns><see langword="false"/> when the object cannot be read.</returns>
    internal static bool CheckPacked(ref PdfASyntaxReader reader, PdfAObjectFindings findings) =>
        PdfASyntaxValues.Read(ref reader, findings, 0) != PdfASyntaxValue.Broken;

    /// <summary>Gets a stream's /Length as the document reads it, following a reference.</summary>
    /// <param name="stream">The stream, or <see langword="null"/> when the document could not read it.</param>
    /// <returns>The length, or <see langword="null"/> when it is missing or not a number.</returns>
    private static long? DeclaredLength(PdfStream? stream)
    {
        var length = stream?.Dictionary.Get(KnownName.Length) ?? default;
        return length.IsNumber ? length.AsInteger() : null;
    }

    /// <summary>Reads <c>N G obj</c>, checking the line end before it, the single spaces inside it and the line end after it.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="spacing">Receives whether the spacing conforms.</param>
    /// <returns><see langword="false"/> when no object header starts there.</returns>
    private static bool ReadHeader(ref PdfASyntaxReader reader, out bool spacing)
    {
        var data = reader.Data;
        reader.SkipSpaces(false);
        spacing = reader.Position > 0 && PdfASyntaxCharacters.IsLineEnd(data[reader.Position - 1]);
        for (var part = 0; part < HeaderNumbers; part++)
        {
            reader.Next();
            if (reader.Kind != PdfASyntaxToken.Integer)
            {
                return false;
            }

            spacing &= SingleSpaceFollows(ref reader);
        }

        reader.Next();
        if (reader.Kind != PdfASyntaxToken.Keyword)
        {
            return false;
        }

        spacing &= reader.Position < data.Length && PdfASyntaxCharacters.IsLineEnd(data[reader.Position]);
        return true;
    }

    /// <summary>Reads the byte after a header number, which must be one white-space byte not followed by another.</summary>
    /// <param name="reader">The reader, after the number.</param>
    /// <returns><see langword="true"/> when the spacing conforms.</returns>
    private static bool SingleSpaceFollows(ref PdfASyntaxReader reader)
    {
        var data = reader.Data;
        if (reader.Position >= data.Length)
        {
            return false;
        }

        var single = PdfASyntaxCharacters.IsSpace(data[reader.Position]);
        reader.Position++;
        return single && (reader.Position >= data.Length || !PdfASyntaxCharacters.IsSpace(data[reader.Position]));
    }

    /// <summary>
    /// Reads an object whose value starts with an integer. veraPDF reads ahead past the integer to rule out a reference,
    /// which takes in <c>endobj</c>, so for a bare integer it tests the white space after <c>endobj</c> instead.
    /// </summary>
    /// <param name="reader">The reader, on the integer.</param>
    /// <param name="findings">Receives the broken rules.</param>
    /// <returns><see langword="true"/> when the spacing veraPDF tests conforms.</returns>
    private static bool ReadIntegerObject(ref PdfASyntaxReader reader, PdfAObjectFindings findings)
    {
        var value = reader.Integer;
        reader.Next();
        if (reader.Kind == PdfASyntaxToken.Integer)
        {
            reader.Next();
            if (reader.IsWord(ReferenceWord))
            {
                return ReadEnd(ref reader);
            }
        }

        findings.CheckInteger(value);
        var after = reader.Position;
        reader.SkipSpaces(false);
        return reader.Position > after && PdfASyntaxCharacters.IsLineEnd(reader.Data[reader.Position - 1]);
    }

    /// <summary>Reads a stream when <c>stream</c> follows a dictionary; otherwise leaves the reader after the dictionary.</summary>
    /// <param name="reader">The reader, after the dictionary.</param>
    /// <param name="scan">The pass.</param>
    /// <param name="number">The object number.</param>
    /// <returns><see langword="false"/> when the stream has no end.</returns>
    private static bool ReadStreamIfPresent(ref PdfASyntaxReader reader, PdfAFileScan scan, int number)
    {
        var after = reader.Position;
        reader.Next();
        if (!reader.IsWord(StreamWord))
        {
            reader.Position = after;
            return true;
        }

        var stream = StoreReading.GetObject(scan.Store, new(number, 0)).AsStream();
        if (stream is not null)
        {
            PdfAStreamDictionaryRules.Check(stream, scan.Findings);
        }

        return PdfAStreamSyntax.Check(ref reader, DeclaredLength(stream), scan.Findings);
    }

    /// <summary>Reads <c>endobj</c>, checking the line end before and after it.</summary>
    /// <param name="reader">The reader, after the object's value.</param>
    /// <returns><see langword="true"/> when the spacing conforms.</returns>
    private static bool ReadEnd(ref PdfASyntaxReader reader)
    {
        var data = reader.Data;
        var before = reader.Position;
        reader.SkipSpaces(false);
        if (reader.Position != before)
        {
            reader.Position--;
        }

        var spacing = reader.Position < data.Length && PdfASyntaxCharacters.IsLineEnd(data[reader.Position]);
        var keyword = reader.Position;
        reader.Next();
        if (reader.Kind != PdfASyntaxToken.Keyword)
        {
            reader.Position = keyword;
        }

        return spacing && reader.Position < data.Length && PdfASyntaxCharacters.IsLineEnd(data[reader.Position]);
    }
}
