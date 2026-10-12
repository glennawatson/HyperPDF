// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads a file's cross-reference sections the way veraPDF does, from the last <c>startxref</c> through /Prev and
/// /XRefStm, and checks the spacing of classic tables (ISO 19005-1, 6.1.4).
/// </summary>
internal static class PdfAXrefSections
{
    /// <summary>The bytes of the file's end searched for <c>startxref</c>.</summary>
    private const int TailWindow = 1024;

    /// <summary>The bytes read to classify a section's first token.</summary>
    private const int TokenWindow = 64;

    /// <summary>The cross-reference entries read between cancellation checks.</summary>
    private const int EntriesPerCheck = 4096;

    /// <summary>The most bytes of a trailer dictionary read.</summary>
    private const int TrailerWindow = 1 << 16;

    /// <summary>The tokens in a cross-reference entry: offset, generation and type.</summary>
    private const int EntryTokens = 3;

    /// <summary>Gets the keyword before the newest section's offset.</summary>
    private static ReadOnlySpan<byte> StartXrefWord => "startxref"u8;

    /// <summary>Gets the keyword that starts a classic table.</summary>
    private static ReadOnlySpan<byte> XrefWord => "xref"u8;

    /// <summary>Gets the keyword before a classic table's trailer.</summary>
    private static ReadOnlySpan<byte> TrailerWord => "trailer"u8;

    /// <summary>Reads the sections.</summary>
    /// <param name="scan">The pass.</param>
    /// <returns>What the sections hold.</returns>
    internal static PdfAXrefLayout Read(PdfAFileScan scan)
    {
        var layout = new PdfAXrefLayout();
        if (FindStartXref(scan) is not { } newest)
        {
            return layout;
        }

        var visited = new HashSet<long>();
        var previous = new Stack<long>();
        long? offset = newest;
        while ((offset is not null || previous.Count > 0) && layout.Sections.Count < PdfLimits.MaxXrefSections)
        {
            scan.CancellationToken.ThrowIfCancellationRequested();
            var next = offset ?? previous.Pop();
            offset = null;
            if (visited.Add(next) && !TryFollow(scan, layout, next, previous, out offset))
            {
                break;
            }
        }

        return layout;
    }

    /// <summary>Checks the lexical limits of the values in the newest classic table's trailer.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="layout">The sections.</param>
    internal static void CheckMainTrailer(PdfAFileScan scan, PdfAXrefLayout layout)
    {
        if (layout.Sections.Count == 0 || layout.Sections[0].IsStream)
        {
            return;
        }

        var source = scan.Source;
        var keyword = PdfByteSearch.IndexOf(source, TrailerWord, layout.Sections[0].Offset);
        if (keyword < 0)
        {
            return;
        }

        var start = keyword + TrailerWord.Length;
        var end = PdfByteSearch.IndexOf(source, StartXrefWord, start);
        using var window = source.Lease(start, (int)Math.Min((end < 0 ? source.Length : end) - start, int.MaxValue));
        var reader = new PdfASyntaxReader(window.Span, 0, scan.Buffer);
        reader.Next();
        var findings = scan.Findings;
        if (reader.Kind == PdfASyntaxToken.DictionaryStart && PdfASyntaxValues.ReadDictionary(ref reader, findings, 1) != PdfASyntaxValue.Broken)
        {
            findings.Commit(scan.Context, PdfAContexts.Trailer);
        }
        else
        {
            findings.Discard();
        }
    }

    /// <summary>Reads the section at an offset and notes the sections its trailer points to.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="layout">Receives the section.</param>
    /// <param name="offset">The offset the file gives, before the header offset is added.</param>
    /// <param name="previous">Receives the /Prev offset.</param>
    /// <param name="stream">Receives the /XRefStm offset, which is read next.</param>
    /// <returns><see langword="false"/> when no section starts there.</returns>
    private static bool TryFollow(PdfAFileScan scan, PdfAXrefLayout layout, long offset, Stack<long> previous, out long? stream)
    {
        stream = null;
        var section = ReadSection(scan, layout, offset + scan.Store.HeaderOffset);
        if (section is null)
        {
            return false;
        }

        layout.Sections.Add(section);
        if (section.Previous is { } prev)
        {
            previous.Push(prev);
        }

        stream = section.Hybrid;
        return true;
    }

    /// <summary>Finds the offset after the last <c>startxref</c> in the file's last kilobyte.</summary>
    /// <param name="scan">The pass.</param>
    /// <returns>The offset, or <see langword="null"/> when there is none.</returns>
    private static long? FindStartXref(PdfAFileScan scan)
    {
        var source = scan.Source;
        var tail = (int)Math.Min(TailWindow, source.Length);
        using var window = source.Lease(source.Length - tail, tail);
        var at = window.Span.LastIndexOf(StartXrefWord);
        if (at < 0)
        {
            return null;
        }

        var reader = new PdfASyntaxReader(window.Span, at + StartXrefWord.Length, scan.Buffer);
        reader.Next();
        return reader.Kind == PdfASyntaxToken.Integer ? reader.Integer : null;
    }

    /// <summary>Reads the section at an offset, or one byte before it, as veraPDF tolerates.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="layout">Receives the spacing findings.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The section, or <see langword="null"/> when none starts there.</returns>
    private static PdfAXrefSection? ReadSection(PdfAFileScan scan, PdfAXrefLayout layout, long offset)
    {
        for (var at = offset; at >= Math.Max(0, offset - 1); at--)
        {
            switch (Classify(scan, at))
            {
                case PdfASyntaxToken.Keyword:
                {
                    CheckTable(scan, layout, at);
                    var trailer = PdfByteSearch.IndexOf(scan.Source, TrailerWord, at);
                    return trailer < 0 ? new(at, false, null, null, null) : ReadTrailer(scan, at, trailer + TrailerWord.Length, false);
                }

                case PdfASyntaxToken.Integer:
                {
                    layout.HasStream = true;
                    return ReadTrailer(scan, at, at, true);
                }

                default:
                {
                    // Neither a table nor an object starts here; veraPDF tries one byte earlier.
                    break;
                }
            }
        }

        return null;
    }

    /// <summary>Reads a section's trailer: a classic table's dictionary after <c>trailer</c>, or a stream's dictionary after its object header.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="section">The section's offset.</param>
    /// <param name="start">Where to start reading.</param>
    /// <param name="isStream">Whether the section is a cross-reference stream, which starts with <c>N G obj</c>.</param>
    /// <returns>The section.</returns>
    private static PdfAXrefSection ReadTrailer(PdfAFileScan scan, long section, long start, bool isStream)
    {
        var source = scan.Source;
        using var window = source.Lease(start, (int)Math.Min(TrailerWindow, source.Length - start));
        var reader = new PdfASyntaxReader(window.Span, 0, scan.Buffer);
        if (isStream)
        {
            // Skip the object number, the generation and obj.
            reader.Next();
            reader.Next();
            reader.Next();
        }

        return PdfATrailerEntries.Read(ref reader, section, isStream, scan.Findings);
    }

    /// <summary>Classifies the token at an offset as a classic table, a stream object or neither.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="offset">The offset.</param>
    /// <returns><see cref="PdfASyntaxToken.Keyword"/> for <c>xref</c>, <see cref="PdfASyntaxToken.Integer"/> for a number, otherwise <see cref="PdfASyntaxToken.None"/>.</returns>
    private static PdfASyntaxToken Classify(PdfAFileScan scan, long offset)
    {
        var source = scan.Source;
        if (offset < 0 || offset >= source.Length)
        {
            return PdfASyntaxToken.None;
        }

        using var window = source.Lease(offset, (int)Math.Min(TokenWindow, source.Length - offset));
        var reader = new PdfASyntaxReader(window.Span, 0, scan.Buffer);
        reader.Next();
        if (reader.IsWord(XrefWord))
        {
            return PdfASyntaxToken.Keyword;
        }

        return reader.Kind == PdfASyntaxToken.Integer ? PdfASyntaxToken.Integer : PdfASyntaxToken.None;
    }

    /// <summary>Checks a classic table's line end after <c>xref</c> and the spacing of its subsection headers.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="layout">Receives the findings.</param>
    /// <param name="offset">The offset of the <c>xref</c> keyword.</param>
    private static void CheckTable(PdfAFileScan scan, PdfAXrefLayout layout, long offset)
    {
        var source = scan.Source;
        var trailer = PdfByteSearch.IndexOf(source, TrailerWord, offset);
        var end = trailer < 0 ? source.Length : Math.Min(source.Length, trailer + TrailerWord.Length);
        using var window = source.Lease(offset, (int)Math.Min(end - offset, int.MaxValue));
        var reader = new PdfASyntaxReader(window.Span, 0, scan.Buffer);
        reader.Next();
        layout.LineEnds &= LineEndThenDigit(ref reader);
        reader.Next();
        while (reader.Kind == PdfASyntaxToken.Integer)
        {
            layout.SubsectionSpacing &= SpaceThenDigit(ref reader);
            reader.Next();
            var count = reader.Integer;
            for (long entry = 0; entry < count * EntryTokens && reader.Kind != PdfASyntaxToken.End; entry++)
            {
                if (entry % EntriesPerCheck == 0)
                {
                    scan.CancellationToken.ThrowIfCancellationRequested();
                }

                reader.Next();
            }

            reader.Next();
        }
    }

    /// <summary>Reads the line end after <c>xref</c>, which must be one CR, LF or CR LF followed by a digit.</summary>
    /// <param name="reader">The reader, after the keyword.</param>
    /// <returns><see langword="true"/> when it conforms.</returns>
    private static bool LineEndThenDigit(ref PdfASyntaxReader reader)
    {
        var data = reader.Data;
        if (reader.Position >= data.Length)
        {
            return false;
        }

        var first = data[reader.Position];
        reader.Position++;
        if (first == PdfASyntaxCharacters.CarriageReturn && reader.Position < data.Length && data[reader.Position] == PdfASyntaxCharacters.LineFeed)
        {
            reader.Position++;
        }

        return PdfASyntaxCharacters.IsLineEnd(first) && reader.Position < data.Length && PdfASyntaxCharacters.IsDigit(data[reader.Position]);
    }

    /// <summary>Reads the byte after a subsection's first number, which must be one space followed by a digit.</summary>
    /// <param name="reader">The reader, after the number.</param>
    /// <returns><see langword="true"/> when it conforms.</returns>
    private static bool SpaceThenDigit(ref PdfASyntaxReader reader)
    {
        var data = reader.Data;
        if (reader.Position >= data.Length)
        {
            return false;
        }

        var space = data[reader.Position] == PdfASyntaxCharacters.Space;
        reader.Position++;
        return space && reader.Position < data.Length && PdfASyntaxCharacters.IsDigit(data[reader.Position]);
    }
}
