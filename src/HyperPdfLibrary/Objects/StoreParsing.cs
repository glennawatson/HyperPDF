// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Objects;

/// <summary>Parses indirect objects and stream extents from the owned source.</summary>
public static class StoreParsing
{
    /// <summary>The deepest nesting of object parses, which a stream whose /Length refers to itself would recurse into.</summary>
    internal const int MaxParseDepth = 16;

    /// <summary>The first window an object is parsed from; most objects fit, and a larger one doubles it.</summary>
    internal const int InitialWindow = 8 * 1024;

    /// <summary>The growth factor of an object's window.</summary>
    internal const int WindowGrowth = 2;

    /// <summary>Parses the indirect object whose header is at an offset.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "offset">The offset of the object number.</param>
    /// <param name = "id">The object id from the header.</param>
    /// <param name = "value">The value.</param>
    /// <returns><see langword="true"/> when the offset holds an object header.</returns>
    internal static bool TryParseObjectAt(PdfObjectStore self, long offset, out PdfObjectId id, out PdfValue value)
    {
        id = default;
        value = default;
        if (offset < 0 || offset >= self.Source.Length)
        {
            return false;
        }

        lock (self.Gate)
        {
            if (self.ParseDepthState >= StoreParsing.MaxParseDepth)
            {
                PdfOpenContext.Report(self.Context, PdfDiagnosticCode.RecursionLimit, "Objects referred to each other too deeply while parsing.", 0, offset);
                return false;
            }

            self.ParseDepthState++;
            try
            {
                return self.ArrayState is not null
                    ? StoreParsing.TryParseInArray(self, self.ArrayState, (int)offset, out id, out value)
                    : StoreParsing.TryParseInWindows(self, offset, out id, out value);
            }
            finally
            {
                self.ParseDepthState--;
            }
        }
    }

    /// <summary>Parses a value at an offset, such as a trailer dictionary, from a window that grows until the value fits.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "offset">The offset of the value.</param>
    /// <returns>The value; null when there is none.</returns>
    internal static PdfValue ParseValueAt(PdfObjectStore self, long offset)
    {
        if (offset < 0 || offset >= self.Source.Length)
        {
            return default;
        }

        if (self.ArrayState is not null)
        {
            var parser = new PdfParser(self.ArrayState, (int)offset, self, self.Names);
            return parser.ParseValue();
        }

        var window = StoreParsing.FirstWindow(self, offset);
        PdfValue value;
        bool truncated;
        do
        {
            using var lease = self.Source.Lease(offset, window);
            var parser = PdfParser.ForWindow(lease.Span, 0, self, self.Names, null);
            value = parser.ParseValue();
            truncated = parser.ReachedEnd;
        }
        while (truncated && StoreParsing.TryGrowWindow(self, offset, ref window));

        return value;
    }

    /// <summary>Reads "number generation obj".</summary>
    /// <param name = "parser">The parser, at the header.</param>
    /// <param name = "id">The object id.</param>
    /// <returns><see langword="true"/> when a header was read.</returns>
    internal static bool TryReadHeader(ref PdfParser parser, out PdfObjectId id)
    {
        id = default;
        if (parser.NextToken() != PdfTokenKind.Number || !PdfNumber.TryParse(parser.Lexeme, out var number) || number.Kind != PdfKind.Integer)
        {
            return false;
        }

        if (parser.NextToken() != PdfTokenKind.Number || !PdfNumber.TryParse(parser.Lexeme, out var generation))
        {
            return false;
        }

        // Some writers omit the space in "0obj"; the generation token then carries the keyword.
        if (!parser.Lexeme.EndsWith(PdfKeywords.Obj) && (parser.NextToken() != PdfTokenKind.Keyword || parser.Keyword != PdfKeyword.Obj))
        {
            return false;
        }

        id = new((int)Math.Clamp(number.AsInteger(), 0, PdfLimits.MaxObjectNumber), (int)Math.Clamp(generation.AsInteger(), 0, ushort.MaxValue));
        return true;
    }

    /// <summary>Parses an object's header and value, noting where a <c>stream</c> keyword after a dictionary ends.</summary>
    /// <param name = "parser">The parser, at the header.</param>
    /// <param name = "id">The object id.</param>
    /// <param name = "value">The value.</param>
    /// <param name = "streamKeywordEnd">The parser position after the <c>stream</c> keyword, or -1 when no stream follows.</param>
    /// <returns><see langword="true"/> when a header was read.</returns>
    internal static bool TryParseObject(ref PdfParser parser, out PdfObjectId id, out PdfValue value, out int streamKeywordEnd)
    {
        value = default;
        streamKeywordEnd = -1;
        if (!StoreParsing.TryReadHeader(ref parser, out id))
        {
            return false;
        }

        parser.EncryptionId = id;
        value = parser.ParseValue();
        if (value.Kind != PdfKind.Dictionary)
        {
            return true;
        }

        var mark = parser.Position;
        if (parser.NextToken() == PdfTokenKind.Keyword && parser.Keyword == PdfKeyword.Stream)
        {
            streamKeywordEnd = parser.Position;
        }
        else
        {
            parser.Position = mark;
        }

        return true;
    }

    /// <summary>Parses an object in place from a file held in one array.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "array">The file.</param>
    /// <param name = "offset">The offset of the object number.</param>
    /// <param name = "id">The object id.</param>
    /// <param name = "value">The value.</param>
    /// <returns><see langword="true"/> when the offset holds an object header.</returns>
    internal static bool TryParseInArray(PdfObjectStore self, byte[] array, int offset, out PdfObjectId id, out PdfValue value)
    {
        var parser = new PdfParser(array, offset, self, self.Names, self.Security);
        if (!StoreParsing.TryParseObject(ref parser, out id, out value, out var keywordEnd))
        {
            return false;
        }

        if (keywordEnd >= 0)
        {
            value = StoreParsing.ReadStream(self, value.AsDictionary()!, keywordEnd, id);
        }

        return true;
    }

    /// <summary>Parses an object from a window of the file, growing the window while the object runs past its end.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "offset">The offset of the object number.</param>
    /// <param name = "id">The object id.</param>
    /// <param name = "value">The value.</param>
    /// <returns><see langword="true"/> when the offset holds an object header.</returns>
    internal static bool TryParseInWindows(PdfObjectStore self, long offset, out PdfObjectId id, out PdfValue value)
    {
        var window = StoreParsing.FirstWindow(self, offset);
        bool found;
        int keywordEnd;
        bool truncated;
        do
        {
            using var lease = self.Source.Lease(offset, window);
            var parser = PdfParser.ForWindow(lease.Span, 0, self, self.Names, self.Security);
            found = StoreParsing.TryParseObject(ref parser, out id, out value, out keywordEnd);
            truncated = parser.ReachedEnd;
        }
        while (truncated && StoreParsing.TryGrowWindow(self, offset, ref window));

        if (found && keywordEnd >= 0)
        {
            value = StoreParsing.ReadStream(self, value.AsDictionary()!, offset + keywordEnd, id);
        }

        return found;
    }

    /// <summary>Gets the first window for a parse at an offset.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "offset">The offset.</param>
    /// <returns>The window length.</returns>
    internal static int FirstWindow(PdfObjectStore self, long offset) => (int)Math.Min(StoreParsing.InitialWindow, self.Source.Length - offset);

    /// <summary>Doubles a window, up to the end of the file and the largest window allowed.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "offset">The window's offset.</param>
    /// <param name = "window">The window length, grown.</param>
    /// <returns><see langword="false"/> when the window cannot grow.</returns>
    internal static bool TryGrowWindow(PdfObjectStore self, long offset, ref int window)
    {
        var limit = Math.Min(self.Source.Length - offset, PdfLimits.MaxObjectWindow);
        if (window >= limit)
        {
            if (limit == PdfLimits.MaxObjectWindow)
            {
                PdfOpenContext.Report(self.Context, PdfDiagnosticCode.RecursionLimit, "An object was larger than the largest window read and was cut short.", 0, offset);
            }

            return false;
        }

        window = (int)Math.Min((long)window * StoreParsing.WindowGrowth, limit);
        return true;
    }

    /// <summary>Makes the stream whose <c>stream</c> keyword ends at an offset.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "dictionary">The stream dictionary.</param>
    /// <param name = "keywordEnd">The offset after the keyword.</param>
    /// <param name = "id">The object id.</param>
    /// <returns>The stream value.</returns>
    internal static PdfValue ReadStream(PdfObjectStore self, PdfDictionary dictionary, long keywordEnd, PdfObjectId id)
    {
        var start = StoreParsing.SkipStreamLineEnd(self, keywordEnd);
        var length = StoreParsing.StreamLength(self, dictionary, start, id);
        var encrypted = self.Security is not null && !dictionary.IsName(KnownName.Type, KnownName.XRef);
        var stream = self.ArrayState is not null ? new PdfStream(dictionary, self.ArrayState, (int)start, length, id, encrypted) : new PdfStream(dictionary, self.Source, start, length, id, encrypted);
        return PdfValue.FromStream(stream);
    }

    /// <summary>Skips the line end after the <c>stream</c> keyword, tolerating spaces before it.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "position">The offset after the keyword.</param>
    /// <returns>The offset of the data.</returns>
    internal static long SkipStreamLineEnd(PdfObjectStore self, long position)
    {
        position = PdfByteSearch.SkipAny(self.Source, position, PdfByteSearch.Space);
        if (self.Source.ByteAt(position) == '\r')
        {
            position++;
        }

        if (self.Source.ByteAt(position) == '\n')
        {
            position++;
        }

        return position;
    }

    /// <summary>Works out a stream's length from /Length, checking it against the <c>endstream</c> keyword.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "dictionary">The stream dictionary.</param>
    /// <param name = "start">The offset of the data.</param>
    /// <param name = "id">The stream's object id, for diagnostics.</param>
    /// <returns>The length of the data.</returns>
    /// <exception cref="PdfException">The declared length is invalid and recovery is disabled.</exception>
    internal static int StreamLength(PdfObjectStore self, PdfDictionary dictionary, long start, PdfObjectId id)
    {
        var declared = dictionary.Get(KnownName.Length).AsInteger(-1);
        if (declared is >= 0 and <= int.MaxValue && declared <= self.Source.Length - start && StoreParsing.EndsAt(self, start + declared))
        {
            return (int)declared;
        }

        if (self.Context is { Recovery: false })
        {
            throw new PdfException(PdfError.Format, "A stream's declared length does not match its data.");
        }

        PdfOpenContext.Report(self.Context, PdfDiagnosticCode.BadStreamLength, "A stream's /Length was missing or wrong.", id.Number, start);
        return StoreParsing.ScanStreamLength(self, start, id);
    }

    /// <summary>Finds a stream's length when /Length cannot be trusted: the data runs to "endstream", less the line end before it.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "start">The offset of the data.</param>
    /// <param name = "id">The stream's object id, for diagnostics.</param>
    /// <returns>The length of the data.</returns>
    internal static int ScanStreamLength(PdfObjectStore self, long start, PdfObjectId id)
    {
        var found = PdfByteSearch.IndexOf(self.Source, PdfKeywords.EndStream, start);
        if (found < 0)
        {
            // Neither /Length nor endstream: the data runs to the object's endobj.
            PdfOpenContext.Report(self.Context, PdfDiagnosticCode.MissingEndStream, "A stream has no endstream keyword.", id.Number, start);
            found = PdfByteSearch.IndexOf(self.Source, "endobj"u8, start);
        }

        var end = found < 0 ? self.Source.Length : found;
        if (end > start && self.Source.ByteAt(end - 1) == '\n')
        {
            end--;
        }

        if (end > start && self.Source.ByteAt(end - 1) == '\r')
        {
            end--;
        }

        return (int)Math.Min(end - start, int.MaxValue);
    }

    /// <summary>Determines whether <c>endstream</c> follows an offset, after optional white space.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "offset">The offset.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool EndsAt(PdfObjectStore self, long offset) => PdfByteSearch.StartsWith(self.Source, PdfByteSearch.SkipAny(self.Source, offset, PdfCharacters.Whitespace), PdfKeywords.EndStream);
}
