// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Checks the file header and the end of the file (ISO 19005-2, 6.1.2 and 6.1.3): the <c>%PDF-n.m</c> line at byte zero,
/// the binary comment after it, and that nothing but one line end follows the last <c>%%EOF</c>.
/// </summary>
internal static class PdfAFileHeader
{
    /// <summary>The bytes of the file's end searched for the last end-of-file marker.</summary>
    private const int TailWindow = 1024;

    /// <summary>The bytes of the file's start searched for the header.</summary>
    private const int HeadWindow = 1024;

    /// <summary>The bytes of the binary comment: a percent sign and four bytes above 127.</summary>
    private const int CommentLength = 5;

    /// <summary>The largest byte value a binary comment byte may not have.</summary>
    private const byte TextByte = 127;

    /// <summary>The bytes a CR LF line end takes.</summary>
    private const int PairLength = 2;

    /// <summary>The length of a version such as <c>1.7</c>.</summary>
    private const int VersionLength = 3;

    /// <summary>Gets the header's start.</summary>
    private static ReadOnlySpan<byte> HeaderStart => "%PDF-"u8;

    /// <summary>Gets the end-of-file marker.</summary>
    private static ReadOnlySpan<byte> EndMarker => "%%EOF"u8;

    /// <summary>Records the header and end-of-file rules the file breaks.</summary>
    /// <param name="scan">The pass.</param>
    internal static void Check(PdfAFileScan scan)
    {
        var source = scan.Source;
        var context = scan.Context;
        using (var head = source.Lease(0, (int)Math.Min(HeadWindow, source.Length)))
        {
            CheckHeader(context, head.Span);
        }

        var tail = (int)Math.Min(TailWindow, source.Length);
        using var end = source.Lease(source.Length - tail, tail);
        context.Require(DataAfterEnd(end.Span) == 0, PdfARule.DataAfterEndOfFile, PdfAFileStructureRules.FileContext);
    }

    /// <summary>Checks the header line and the comment after it.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="head">The file's first bytes.</param>
    private static void CheckHeader(PdfAValidationContext context, ReadOnlySpan<byte> head)
    {
        var lineStart = FindHeaderLine(head);
        var lineEnd = lineStart < 0 ? 0 : LineEnd(head, lineStart);
        var line = lineStart == 0 ? head[..lineEnd] : default;
        var atStart = line.StartsWith(HeaderStart);
        var version = atStart ? line[HeaderStart.Length..] : default;
        context.Require(atStart && ContainsVersion(line), PdfARule.HeaderPdfA1, PdfAFileStructureRules.FileContext);
        context.Require(atStart && IsVersion(version, (byte)'1', (byte)'7'), PdfARule.HeaderPdf17, PdfAFileStructureRules.FileContext);
        context.Require(atStart && IsVersion(version, (byte)'2', (byte)'9'), PdfARule.HeaderPdf20, PdfAFileStructureRules.FileContext);

        var commentStart = SkipLineEnd(head, lineEnd);
        var comment = head[commentStart..LineEnd(head, commentStart)];
        context.Require(lineStart >= 0 && IsBinaryComment(comment), PdfARule.HeaderBinaryComment, PdfAFileStructureRules.FileContext);
    }

    /// <summary>
    /// Finds the header line as veraPDF does: the first line holding <c>PDF-</c>, where CR and LF each end a line, stopping
    /// early at a line that starts with a digit.
    /// </summary>
    /// <param name="head">The file's first bytes.</param>
    /// <returns>The offset of the line, or -1 when the first bytes hold none.</returns>
    private static int FindHeaderLine(ReadOnlySpan<byte> head)
    {
        var start = 0;
        while (start < head.Length)
        {
            var end = LineEnd(head, start);
            var line = head[start..end];
            if (line.IndexOf(HeaderStart[1..]) >= 0 || (!line.IsEmpty && PdfASyntaxCharacters.IsDigit(line[0])))
            {
                return start;
            }

            start = end + 1;
        }

        return -1;
    }

    /// <summary>Gets the offset of the line end at or after a position.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="from">The position.</param>
    /// <returns>The offset of the first CR or LF, or the end of the data.</returns>
    private static int LineEnd(ReadOnlySpan<byte> data, int from)
    {
        var found = data[from..].IndexOfAny(PdfASyntaxCharacters.CarriageReturn, PdfASyntaxCharacters.LineFeed);
        return found < 0 ? data.Length : from + found;
    }

    /// <summary>Skips one line end: CR, LF or CR LF.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="at">The position of the line end.</param>
    /// <returns>The position after it.</returns>
    private static int SkipLineEnd(ReadOnlySpan<byte> data, int at)
    {
        if (at < data.Length && data[at] == PdfASyntaxCharacters.CarriageReturn)
        {
            at++;
        }

        if (at < data.Length && data[at] == PdfASyntaxCharacters.LineFeed)
        {
            at++;
        }

        return at;
    }

    /// <summary>Determines whether a line holds <c>%PDF-</c> followed by a digit, a point and a digit anywhere.</summary>
    /// <param name="line">The line.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool ContainsVersion(ReadOnlySpan<byte> line)
    {
        var rest = line;
        for (var found = rest.IndexOf(HeaderStart); found >= 0; found = rest.IndexOf(HeaderStart))
        {
            var version = rest[(found + HeaderStart.Length)..];
            if (version.Length >= VersionLength && PdfASyntaxCharacters.IsDigit(version[0]) && version[1] == '.' && PdfASyntaxCharacters.IsDigit(version[VersionLength - 1]))
            {
                return true;
            }

            rest = rest[(found + 1)..];
        }

        return false;
    }

    /// <summary>Determines whether the rest of a header line is exactly a version with a given major number.</summary>
    /// <param name="version">The bytes after <c>%PDF-</c>.</param>
    /// <param name="major">The major version digit.</param>
    /// <param name="highestMinor">The highest minor version digit allowed.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsVersion(ReadOnlySpan<byte> version, byte major, byte highestMinor) =>
        version.Length == VersionLength && version[0] == major && version[1] == '.' && version[VersionLength - 1] is >= (byte)'0' && version[VersionLength - 1] <= highestMinor;

    /// <summary>Determines whether a line is a percent sign followed by at least four bytes above 127.</summary>
    /// <param name="comment">The line.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsBinaryComment(ReadOnlySpan<byte> comment) =>
        comment.Length >= CommentLength && comment[0] == '%' && comment[1..CommentLength].IndexOfAnyInRange((byte)0, TextByte) < 0;

    /// <summary>
    /// Counts the bytes after the last <c>%%EOF</c>, less one line end, with veraPDF's arithmetic: the count is a signed
    /// byte, and a file without the marker counts -1.
    /// </summary>
    /// <param name="tail">The file's last bytes.</param>
    /// <returns>The count.</returns>
    private static sbyte DataAfterEnd(ReadOnlySpan<byte> tail)
    {
        var marker = LastMarker(tail);
        if (marker < 0)
        {
            return -1;
        }

        var after = marker + EndMarker.Length;
        var size = unchecked((sbyte)(tail.Length - marker - EndMarker.Length));
        if (size <= 0)
        {
            return size;
        }

        if (tail[after] == PdfASyntaxCharacters.CarriageReturn)
        {
            var pair = after + 1 < tail.Length && tail[after + 1] == PdfASyntaxCharacters.LineFeed;
            return unchecked((sbyte)(size - (pair ? PairLength : 1)));
        }

        return tail[after] == PdfASyntaxCharacters.LineFeed ? unchecked((sbyte)(size - 1)) : size;
    }

    /// <summary>Finds the last end-of-file marker the way veraPDF scans for it, backwards from the end.</summary>
    /// <param name="tail">The file's last bytes.</param>
    /// <returns>The offset of the marker, or -1.</returns>
    private static int LastMarker(ReadOnlySpan<byte> tail)
    {
        var marker = EndMarker;
        var expected = marker.Length - 1;
        for (var i = tail.Length - 1; i >= 0; i--)
        {
            if (tail[i] == marker[expected])
            {
                if (expected == 0)
                {
                    return i;
                }

                expected--;
            }
            else
            {
                // veraPDF starts the marker again without testing this byte against its last character.
                expected = marker.Length - 1;
            }
        }

        return -1;
    }
}
