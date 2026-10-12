// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Checks the bytes of a stream object (ISO 19005-2, 6.1.7.1): the line end after <c>stream</c>, the line end before
/// <c>endstream</c>, and that /Length matches the data. It measures the data as veraPDF does, so the results agree.
/// </summary>
internal static class PdfAStreamSyntax
{
    /// <summary>The line end bytes a CR LF pair counts as.</summary>
    private const int PairLength = 2;

    /// <summary>Gets the keyword that ends a stream's data.</summary>
    internal static ReadOnlySpan<byte> EndStream => "endstream"u8;

    /// <summary>Checks the stream whose <c>stream</c> keyword the reader has just read.</summary>
    /// <param name="reader">The reader, after the keyword.</param>
    /// <param name="declared">The /Length value, or <see langword="null"/> when it is missing or not a number.</param>
    /// <param name="findings">Receives the broken rules.</param>
    /// <returns><see langword="false"/> when no <c>endstream</c> follows, which makes the object unreadable.</returns>
    internal static bool Check(ref PdfASyntaxReader reader, long? declared, PdfAObjectFindings findings)
    {
        var data = reader.Data;
        var keywordEnd = SkipKeywordLineEnd(data, reader.Position, out var start);
        int end;
        if (declared is { } size && size >= 0 && start + size <= data.Length && EndsAt(ref reader, (int)(start + size)))
        {
            end = (int)(start + size);
        }
        else
        {
            end = FindEnd(data, start);
            if (end < 0)
            {
                return false;
            }
        }

        reader.Position = end;
        reader.SkipSpaces(false);
        var measured = reader.Position - start;
        var lineEnd = LineEndBefore(data, reader.Position, measured - (declared ?? 0));
        findings.Require(lineEnd > 0 && keywordEnd, PdfARule.StreamKeywordLineEnds);
        findings.Require(declared == measured - Math.Max(lineEnd, 0), PdfARule.StreamLength);
        reader.Next();
        return true;
    }

    /// <summary>Skips the line end after <c>stream</c>: CR LF or LF, or anything else as written.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="position">The offset after the keyword.</param>
    /// <param name="start">Receives the offset of the data.</param>
    /// <returns><see langword="true"/> when the line end is CR LF or LF.</returns>
    private static bool SkipKeywordLineEnd(ReadOnlySpan<byte> data, int position, out int start)
    {
        start = position;
        if (start >= data.Length)
        {
            return false;
        }

        if (data[start] == PdfASyntaxCharacters.LineFeed)
        {
            start++;
            return true;
        }

        if (data[start] != PdfASyntaxCharacters.CarriageReturn)
        {
            return false;
        }

        start++;
        if (start < data.Length && data[start] == PdfASyntaxCharacters.LineFeed)
        {
            start++;
            return true;
        }

        return false;
    }

    /// <summary>Determines whether <c>endstream</c> is the next token at an offset.</summary>
    /// <param name="reader">The reader; its position is kept.</param>
    /// <param name="offset">The offset.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool EndsAt(ref PdfASyntaxReader reader, int offset)
    {
        var position = reader.Position;
        reader.Position = offset;
        reader.Next();
        var found = reader.IsWord(EndStream);
        reader.Position = position;
        return found;
    }

    /// <summary>Finds the end of a stream's data when /Length is wrong: the first <c>endstream</c>, less the line end before it.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="start">The offset of the data.</param>
    /// <returns>The offset after the data, or -1 when no <c>endstream</c> follows.</returns>
    private static int FindEnd(ReadOnlySpan<byte> data, int start)
    {
        var from = start;
        while (from < data.Length)
        {
            var found = data[from..].IndexOf(EndStream);
            if (found < 0)
            {
                return -1;
            }

            var at = from + found;
            var after = at + EndStream.Length;
            if (after >= data.Length || PdfASyntaxCharacters.IsTokenEnd(data[after]))
            {
                return at - LineEndLength(data, start, at);
            }

            from = at + 1;
        }

        return -1;
    }

    /// <summary>Gets the length of the line end just before an offset, counting only bytes inside the data.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="start">The offset of the data.</param>
    /// <param name="at">The offset.</param>
    /// <returns>2 for CR LF, 1 for CR or LF, otherwise 0.</returns>
    private static int LineEndLength(ReadOnlySpan<byte> data, int start, int at)
    {
        if (at - 1 < start)
        {
            return 0;
        }

        var last = data[at - 1];
        if (last == PdfASyntaxCharacters.CarriageReturn)
        {
            return 1;
        }

        if (last != PdfASyntaxCharacters.LineFeed)
        {
            return 0;
        }

        return at - PairLength >= start && data[at - PairLength] == PdfASyntaxCharacters.CarriageReturn ? PairLength : 1;
    }

    /// <summary>Gets the line end before <c>endstream</c> as veraPDF counts it.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="keyword">The offset of the keyword, after white space is skipped.</param>
    /// <param name="excess">The measured length less the declared length, which decides whether CR LF counts as one byte or two.</param>
    /// <returns>The bytes the line end counts as, or -1 when no line end precedes the keyword.</returns>
    private static int LineEndBefore(ReadOnlySpan<byte> data, int keyword, long excess)
    {
        var last = keyword >= 1 ? data[keyword - 1] : 0;
        var previous = keyword >= PairLength ? data[keyword - PairLength] : 0;
        if (last == PdfASyntaxCharacters.LineFeed)
        {
            return previous == PdfASyntaxCharacters.CarriageReturn && excess > 1 ? PairLength : 1;
        }

        return last == PdfASyntaxCharacters.CarriageReturn ? 1 : -1;
    }
}
