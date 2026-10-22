// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads an inline image after <c>BI</c> and checks its filters (ISO 19005-2, 6.1.10; ISO 19005-1, 6.1.10): only the
/// standard filters other than LZW, JBIG2 and JPX, by full or abbreviated name. It finds the image's end as veraPDF does.
/// </summary>
internal static class PdfAInlineImageSyntax
{
    /// <summary>The bytes of <c>EI</c>.</summary>
    private const int EndLength = 2;

    /// <summary>Gets the abbreviated filter key.</summary>
    private static ReadOnlySpan<byte> FilterAbbreviation => "F"u8;

    /// <summary>Gets the filter key.</summary>
    private static ReadOnlySpan<byte> FilterKey => "Filter"u8;

    /// <summary>Gets the abbreviated length key.</summary>
    private static ReadOnlySpan<byte> LengthAbbreviation => "L"u8;

    /// <summary>Gets the length key.</summary>
    private static ReadOnlySpan<byte> LengthKey => "Length"u8;

    /// <summary>Reads the image's dictionary and data and checks its filters.</summary>
    /// <param name="reader">The reader, after <c>BI</c>.</param>
    /// <param name="findings">Receives the broken rules.</param>
    /// <param name="scratch">Receives findings while other values are skipped; they are dropped.</param>
    /// <returns><see langword="false"/> when the image has no <c>ID</c>, which makes the content unreadable.</returns>
    internal static bool Read(ref PdfASyntaxReader reader, PdfAObjectFindings findings, PdfAObjectFindings scratch)
    {
        var parameters = new PdfAInlineImageParameters();
        reader.Next();
        while (reader.Kind == PdfASyntaxToken.Name)
        {
            var read = ReadEntry(ref reader, parameters, scratch);
            scratch.Discard();
            if (!read)
            {
                return false;
            }

            reader.Next();
        }

        if (!reader.IsWord("ID"u8))
        {
            return false;
        }

        var filters = parameters.HasAbbreviatedFilter ? parameters.AbbreviatedFilter : parameters.Filter;
        findings.Require(!filters.Lzw, PdfARule.InlineFilterLzwForbidden);
        findings.Require(filters.Allowed, PdfARule.InlineFilterAllowed);
        SkipData(ref reader, parameters.HasAbbreviatedLength ? parameters.AbbreviatedLength : parameters.Length);
        return true;
    }

    /// <summary>Reads one dictionary entry, keeping the filters and the data length.</summary>
    /// <param name="reader">The reader, on the key.</param>
    /// <param name="parameters">Receives the entry.</param>
    /// <param name="scratch">Receives findings while other values are skipped.</param>
    /// <returns><see langword="false"/> when the value cannot be read.</returns>
    private static bool ReadEntry(ref PdfASyntaxReader reader, PdfAInlineImageParameters parameters, PdfAObjectFindings scratch)
    {
        var key = reader.Value;
        if (key.SequenceEqual(FilterAbbreviation))
        {
            parameters.HasAbbreviatedFilter = true;
            parameters.AbbreviatedFilter = ReadFilters(ref reader, scratch);
            return reader.Kind != PdfASyntaxToken.Broken;
        }

        if (key.SequenceEqual(FilterKey))
        {
            parameters.Filter = ReadFilters(ref reader, scratch);
            return reader.Kind != PdfASyntaxToken.Broken;
        }

        var isAbbreviatedLength = key.SequenceEqual(LengthAbbreviation);
        var isLength = key.SequenceEqual(LengthKey);
        reader.Next();
        if (reader.Kind == PdfASyntaxToken.Integer && (isAbbreviatedLength || isLength))
        {
            parameters.SetLength(isAbbreviatedLength, reader.Integer);
            return true;
        }

        return PdfASyntaxValues.FromToken(ref reader, scratch, 0) != PdfASyntaxValue.Broken;
    }

    /// <summary>Reads a filter name or an array of filter names.</summary>
    /// <param name="reader">The reader, on the key.</param>
    /// <param name="scratch">Receives findings while other values are skipped.</param>
    /// <returns>What the filters break; an array holding anything but names counts as no filters, as veraPDF reads it.</returns>
    private static PdfAInlineFilters ReadFilters(ref PdfASyntaxReader reader, PdfAObjectFindings scratch)
    {
        reader.Next();
        if (reader.Kind == PdfASyntaxToken.Name)
        {
            return PdfAInlineFilters.Of(reader.Value);
        }

        if (reader.Kind != PdfASyntaxToken.ArrayStart)
        {
            _ = PdfASyntaxValues.FromToken(ref reader, scratch, 0);
            return PdfAInlineFilters.None;
        }

        var result = PdfAInlineFilters.None;
        var names = true;
        reader.Next();
        while (reader.Kind is not (PdfASyntaxToken.ArrayEnd or PdfASyntaxToken.End or PdfASyntaxToken.Broken))
        {
            if (reader.Kind == PdfASyntaxToken.Name)
            {
                result = result.And(PdfAInlineFilters.Of(reader.Value));
            }
            else
            {
                names = false;
                _ = PdfASyntaxValues.FromToken(ref reader, scratch, 0);
            }

            reader.Next();
        }

        return names ? result : PdfAInlineFilters.None;
    }

    /// <summary>
    /// Skips the image data: one white-space byte after <c>ID</c>, then up to an <c>EI</c> that comes after /L bytes, is
    /// followed by white space and is not followed by an unknown operator.
    /// </summary>
    /// <param name="reader">The reader, after <c>ID</c>.</param>
    /// <param name="length">The /L value, or <see langword="null"/>.</param>
    private static void SkipData(ref PdfASyntaxReader reader, long? length)
    {
        var data = reader.Data;
        var start = reader.Position;
        if (start < data.Length && PdfASyntaxCharacters.IsSpace(data[start]))
        {
            start++;
        }

        var end = data.Length;
        for (var found = data[start..].IndexOf("EI"u8); found >= 0; found = NextEnd(data, start, found))
        {
            var candidate = start + found + EndLength;
            if (!IsImageEnd(ref reader, candidate, candidate - start >= (length ?? 0)))
            {
                continue;
            }

            end = candidate;
            break;
        }

        reader.Position = end;
    }

    /// <summary>Finds the next <c>EI</c> after one that did not end the image.</summary>
    /// <param name="data">The content.</param>
    /// <param name="start">The offset of the image data.</param>
    /// <param name="previous">The offset of the previous <c>EI</c>, relative to <paramref name="start"/>.</param>
    /// <returns>The next offset relative to <paramref name="start"/>, or -1.</returns>
    private static int NextEnd(ReadOnlySpan<byte> data, int start, int previous)
    {
        var from = start + previous + 1;
        var found = data[from..].IndexOf("EI"u8);
        return found < 0 ? -1 : from - start + found;
    }

    /// <summary>Determines whether an <c>EI</c> ends the image.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="end">The offset after <c>EI</c>.</param>
    /// <param name="pastLength">Whether the data before it is at least /L bytes long.</param>
    /// <returns><see langword="true"/> at the end of the content, or when white space and no unknown operator follow.</returns>
    private static bool IsImageEnd(ref PdfASyntaxReader reader, int end, bool pastLength)
    {
        var data = reader.Data;
        if (end == data.Length)
        {
            return true;
        }

        return pastLength && PdfASyntaxCharacters.IsSpace(data[end]) && IsKnownNext(ref reader, end);
    }

    /// <summary>Determines whether the token after a candidate <c>EI</c> is not an unknown operator.</summary>
    /// <param name="reader">The reader; its position is set by the caller afterwards.</param>
    /// <param name="at">The offset after <c>EI</c>.</param>
    /// <returns><see langword="true"/> when the image may end there.</returns>
    private static bool IsKnownNext(ref PdfASyntaxReader reader, int at)
    {
        reader.Position = at;
        reader.Next();
        return reader.Kind switch
        {
            PdfASyntaxToken.Broken => false,
            PdfASyntaxToken.Keyword => reader.IsWord("null"u8) || reader.IsWord("true"u8) || reader.IsWord("false"u8) || PdfASyntaxOperators.IsStandard(reader.Value),
            _ => true,
        };
    }
}
