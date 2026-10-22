// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Checks that a signature's /ByteRange covers the whole file but the /Contents string (ISO 19005-2, 6.4.3), locating
/// the string and the end of the signed revision the way veraPDF does: up to the first <c>%%EOF</c> after the next
/// cross-reference section, outside any stream, plus one line end.
/// </summary>
internal static class PdfASignatureByteRange
{
    /// <summary>The entries of a byte range.</summary>
    private const int RangeLength = 4;

    /// <summary>The index of the range entry where the signed bytes resume after /Contents.</summary>
    private const int ContentsEndEntry = 2;

    /// <summary>The index of the last range entry.</summary>
    private const int LastEntry = 3;

    /// <summary>The bytes read past an object's extent.</summary>
    private const int TrailingBytes = 256;

    /// <summary>Gets the end-of-file marker.</summary>
    private static ReadOnlySpan<byte> EndMarker => "%%EOF"u8;

    /// <summary>Gets the /Contents key.</summary>
    private static ReadOnlySpan<byte> ContentsKey => "Contents"u8;

    /// <summary>Determines whether a signature's byte range covers the file.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="layout">The cross-reference sections.</param>
    /// <param name="signature">The signature dictionary.</param>
    /// <param name="reference">The field's /V value; a direct signature dictionary cannot be located.</param>
    /// <returns><see langword="true"/> when it covers the file.</returns>
    internal static bool CoversFile(PdfAFileScan scan, PdfAXrefLayout layout, PdfDictionary signature, PdfValue reference)
    {
        var range = signature.Get(KnownName.ByteRange).AsArray();
        if (!reference.IsReference || range is not { Count: >= RangeLength } || ContentsSpan(scan, reference.AsReference().Number) is not { } contents)
        {
            return false;
        }

        var nextSection = NextSection(layout, contents.End);
        var end = RevisionEnd(scan, nextSection + scan.Store.HeaderOffset);
        var expected = end.End - contents.End;
        var last = range.Get(LastEntry).AsInteger();
        var lastFits = end.AtFileEnd ? last == expected : last >= expected - end.LineEnd && last <= expected;
        return range.Get(0).AsInteger() == 0 && range.Get(1).AsInteger() == contents.Start && range.Get(ContentsEndEntry).AsInteger() == contents.End && lastFits;
    }

    /// <summary>Finds where the /Contents value of the signature object starts and ends.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="number">The signature's object number.</param>
    /// <returns>The span, or <see langword="null"/> when the object or the key cannot be found.</returns>
    private static PdfASignatureSpan? ContentsSpan(PdfAFileScan scan, int number) =>
        ObjectWindowStart(scan, number) is { } offset ? ReadAt(scan, offset, number, true) : null;

    /// <summary>Gets where an object starts in the file, as the cross-reference table gives it.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="number">The object number.</param>
    /// <returns>The offset, or <see langword="null"/> for an object in an object stream or a missing one.</returns>
    private static long? ObjectWindowStart(PdfAFileScan scan, int number)
    {
        var xref = scan.Store.XrefState;
        return xref.GetType(number) == XrefEntryType.InFile ? xref.GetLocation(number) : null;
    }

    /// <summary>Reads the object at an offset: the /Contents entry of a dictionary, or a string value of an indirect /Contents.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="offset">The object's offset.</param>
    /// <param name="number">The object number, for its extent.</param>
    /// <param name="dictionary">Whether the object is the signature dictionary rather than the /Contents value.</param>
    /// <returns>The span, or <see langword="null"/>.</returns>
    private static PdfASignatureSpan? ReadAt(PdfAFileScan scan, long offset, int number, bool dictionary)
    {
        var source = scan.Source;
        var length = StoreExtents.TryGetExtent(scan.Store, number, out _, out var extent) ? extent + TrailingBytes : source.Length - offset;
        if (offset < 0 || offset >= source.Length)
        {
            return null;
        }

        using var window = source.Lease(offset, (int)Math.Min(Math.Min(length, source.Length - offset), int.MaxValue));
        var reader = new PdfASyntaxReader(window.Span, 0, scan.Buffer);
        reader.Next();
        reader.Next();
        reader.Next();
        if (!dictionary)
        {
            return ReadValue(scan, ref reader, offset);
        }

        reader.Next();
        return reader.Kind == PdfASyntaxToken.DictionaryStart ? FindContents(scan, ref reader, offset) : null;
    }

    /// <summary>Finds the /Contents entry among a dictionary's entries.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="reader">The reader, after the opening brackets.</param>
    /// <param name="offset">The window's offset in the file.</param>
    /// <returns>The span, or <see langword="null"/>.</returns>
    private static PdfASignatureSpan? FindContents(PdfAFileScan scan, ref PdfASyntaxReader reader, long offset)
    {
        var scratch = scan.Findings;
        reader.Next();
        while (reader.Kind == PdfASyntaxToken.Name)
        {
            if (reader.Value.SequenceEqual(ContentsKey))
            {
                return ReadValue(scan, ref reader, offset);
            }

            var value = PdfASyntaxValues.Read(ref reader, scratch, 1);
            scratch.Discard();
            if (value == PdfASyntaxValue.Broken)
            {
                return null;
            }

            reader.Next();
        }

        return null;
    }

    /// <summary>Reads a /Contents value: the string's span, or the span of the string an indirect reference points to.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="reader">The reader, before the value.</param>
    /// <param name="offset">The window's offset in the file.</param>
    /// <returns>The span, or <see langword="null"/>.</returns>
    private static PdfASignatureSpan? ReadValue(PdfAFileScan scan, ref PdfASyntaxReader reader, long offset)
    {
        reader.SkipSpaces(false);
        var start = reader.Position;
        reader.Next();
        if (reader.Kind is PdfASyntaxToken.HexString or PdfASyntaxToken.LiteralString)
        {
            return new(offset + start, offset + reader.Position);
        }

        if (reader.Kind != PdfASyntaxToken.Integer || reader.Integer is <= 0 or > int.MaxValue)
        {
            return null;
        }

        var number = (int)reader.Integer;
        return ObjectWindowStart(scan, number) is { } target ? ReadAt(scan, target, number, false) : null;
    }

    /// <summary>Gets the first cross-reference section after an offset, or the offset itself when none follows.</summary>
    /// <param name="layout">The sections.</param>
    /// <param name="after">The offset.</param>
    /// <returns>The section's offset.</returns>
    private static long NextSection(PdfAXrefLayout layout, long after)
    {
        var next = long.MaxValue;
        foreach (var section in layout.Sections)
        {
            if (section.Offset > after && section.Offset < next)
            {
                next = section.Offset;
            }
        }

        return next == long.MaxValue ? after : next;
    }

    /// <summary>Finds the end of the revision that starts its search at an offset.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="from">The offset.</param>
    /// <returns>The end.</returns>
    private static PdfARevisionEnd RevisionEnd(PdfAFileScan scan, long from)
    {
        var source = scan.Source;
        if (from < 0 || from >= source.Length)
        {
            return new(source.Length, 0, true);
        }

        using var window = source.Lease(from, (int)Math.Min(source.Length - from, int.MaxValue));
        var data = window.Span;
        var marker = FindMarker(data);
        return marker < 0 ? new(source.Length, 0, true) : LineEndAfter(data, from, marker + EndMarker.Length);
    }

    /// <summary>
    /// Finds the first <c>%%EOF</c> outside a stream that only a line end, spaces and one other byte precede, as veraPDF
    /// scans for it: it steps over <c>stream</c> to the next <c>endstream</c>.
    /// </summary>
    /// <param name="data">The bytes from the start of the search.</param>
    /// <returns>The marker's offset in the data, or -1.</returns>
    private static int FindMarker(ReadOnlySpan<byte> data)
    {
        var inStream = false;
        var marker = 0;
        var candidate = true;
        var next = 1;
        var result = -1;
        var scanning = true;
        while (scanning)
        {
            if (!inStream && candidate && IsMarker(data, marker))
            {
                result = marker;
                scanning = false;
            }
            else if (StepsOverKeyword(data, ref next, ref inStream))
            {
                candidate = false;
                scanning = next < data.Length;
            }
            else
            {
                marker = next;
                candidate = true;
                next++;
                scanning = marker + EndMarker.Length < data.Length || IsMarker(data, marker);
            }
        }

        return result;
    }

    /// <summary>Steps over <c>stream</c>, or <c>endstream</c> inside a stream, when it starts at an offset.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="next">The offset; moved past the keyword when it matches.</param>
    /// <param name="inStream">Whether the scan is inside a stream; flipped when the keyword matches.</param>
    /// <returns><see langword="true"/> when the keyword matched.</returns>
    private static bool StepsOverKeyword(ReadOnlySpan<byte> data, ref int next, ref bool inStream)
    {
        var keyword = inStream ? PdfAStreamSyntax.EndStream : "stream"u8;
        if (next + keyword.Length > data.Length || !data[next..].StartsWith(keyword))
        {
            return false;
        }

        inStream = !inStream;
        next += keyword.Length;
        return true;
    }

    /// <summary>Determines whether <c>%%EOF</c> starts at an offset and only spaces and one byte separate it from a line end.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="at">The offset.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsMarker(ReadOnlySpan<byte> data, int at)
    {
        if (at + EndMarker.Length > data.Length || !data[at..].StartsWith(EndMarker))
        {
            return false;
        }

        // veraPDF accepts the byte just before the marker unseen, then needs spaces back to a line end.
        for (var i = at - 1; i >= 0; i--)
        {
            if (PdfASyntaxCharacters.IsLineEnd(data[i]))
            {
                return true;
            }

            if (i < at - 1 && data[i] != PdfASyntaxCharacters.Space)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Adds the one line end veraPDF allows after <c>%%EOF</c>.</summary>
    /// <param name="data">The bytes from the start of the search.</param>
    /// <param name="from">The search's offset in the file.</param>
    /// <param name="after">The offset after the marker in the data.</param>
    /// <returns>The end.</returns>
    private static PdfARevisionEnd LineEndAfter(ReadOnlySpan<byte> data, long from, int after)
    {
        var end = after;
        if (end < data.Length && data[end] == PdfASyntaxCharacters.CarriageReturn)
        {
            end++;
        }

        if (end < data.Length && data[end] == PdfASyntaxCharacters.LineFeed && (end == after || data[end - 1] == PdfASyntaxCharacters.CarriageReturn))
        {
            end++;
        }

        return new(from + end, end - after, end >= data.Length);
    }
}
