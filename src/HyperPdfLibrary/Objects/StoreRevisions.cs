// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Structure;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Objects;

/// <summary>Reads cross-reference sections and earlier document revisions.</summary>
public static class StoreRevisions
{
    /// <summary>The length of the <c>%%EOF</c> marker.</summary>
    internal const int EndMarkerLength = 5;

    /// <summary>The bytes read at a section's start to find its first token.</summary>
    internal const int SectionTokenWindow = 1024;

    /// <summary>Gets the end-of-file marker that closes each revision.</summary>
    private static ReadOnlySpan<byte> EndMarker => "%%EOF"u8;

    /// <summary>
    /// Gets where an object's current definition starts in the file: its own offset, or its object stream's offset when it
    /// is compressed.
    /// </summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "number">The object number.</param>
    /// <returns>The position in the file, or -1 when the object is free or missing.</returns>
    internal static long GetDefinitionOffset(PdfObjectStore self, int number)
    {
        var xref = self.XrefState;
        var type = xref.GetType(number);
        if (type == XrefEntryType.Compressed)
        {
            number = (int)xref.GetLocation(number);
            type = xref.GetType(number);
        }

        return type == XrefEntryType.InFile ? xref.GetLocation(number) + xref.OffsetBase : -1;
    }

    /// <summary>
    /// Opens the objects of an earlier revision: the file cut at <paramref name = "length"/>. The copy borrows this store's
    /// security handler, so disposing the copy leaves the handler open; it owns nothing else.
    /// </summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "length">The length of the earlier revision.</param>
    /// <returns>The earlier revision's objects.</returns>
    /// <exception cref = "PdfException">The earlier revision cannot be read.</exception>
    internal static PdfObjectStore OpenRevision(PdfObjectStore self, long length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, self.Source.Length);

        // The trailer and the /Encrypt dictionary are never encrypted, so the structure is read without the key; the key,
        // which is known already, is applied afterwards.
        var store = CreateRevision(self, length, null);
        StoreOpening.ReadStructure(store);
        if (self.Security is { } security && store.Trailer.GetDictionary(KnownName.Encrypt) is { } encrypt)
        {
            store.SecurityBorrowed = true;
            StoreOpening.Adopt(store, security, encrypt);
        }

        store.Catalog = store.Trailer.GetDictionary(KnownName.Root) ?? throw new PdfException(PdfError.Format, "The earlier revision has no catalog.");
        return store;
    }

    /// <summary>
    /// Opens the structure of an earlier revision without the security handler, so nothing in it is decrypted. The trailer,
    /// the /Encrypt dictionary and signature contents are never encrypted, and a PDF MAC check reads exactly those. Objects
    /// that are encrypted read as encrypted bytes, and the copy has no catalog.
    /// </summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "length">The length of the earlier revision.</param>
    /// <returns>The earlier revision's raw structure.</returns>
    /// <exception cref = "PdfException">The earlier revision's cross-reference data cannot be read.</exception>
    internal static PdfObjectStore OpenRawRevision(PdfObjectStore self, long length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, self.Source.Length);
        var store = CreateRevision(self, length, null);
        try
        {
            StoreOpening.ReadStructure(store);
        }
        catch
        {
            store.Dispose();
            throw;
        }

        return store;
    }

    /// <summary>
    /// Lists the file's revisions, oldest first, by following the cross-reference chain from the newest section. Each
    /// revision ends at the first <c>%%EOF</c> after its section. A damaged chain falls back to every <c>%%EOF</c> marker.
    /// </summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <returns>The revisions.</returns>
    internal static PdfRevision[] ReadRevisions(PdfObjectStore self)
    {
        var sections = StoreRevisions.ReadSectionOffsets(self);
        var revisions = new List<PdfRevision>(sections.Count);
        if (sections.Count > 0)
        {
            sections.Sort();
            foreach (var offset in sections)
            {
                StoreRevisions.AddRevision(self, revisions, offset);
            }
        }

        return revisions.Count > 0 ? [.. revisions] : StoreRevisions.ReadMarkerRevisions(self);
    }

    /// <summary>Gets the end of the line that follows a position: one CR, LF or CRLF.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "position">The position after a marker.</param>
    /// <returns>The position after the line end.</returns>
    internal static long SkipLineEnd(PdfObjectStore self, long position)
    {
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

    /// <summary>Adds the revision whose cross-reference section starts at an offset, unless it ends where the last one did.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "revisions">The revisions so far.</param>
    /// <param name = "offset">The section's position.</param>
    internal static void AddRevision(PdfObjectStore self, List<PdfRevision> revisions, long offset)
    {
        var length = self.Source.Length;
        var marker = PdfByteSearch.IndexOf(self.Source, StoreRevisions.EndMarker, Math.Clamp(offset, 0, length));
        var markerEnd = marker < 0 ? length : marker + StoreRevisions.EndMarkerLength;
        var end = marker < 0 ? length : StoreRevisions.SkipLineEnd(self, markerEnd);
        if (revisions.Count > 0 && revisions[^1].MarkerEnd >= markerEnd)
        {
            return;
        }

        revisions.Add(new(revisions.Count, offset, markerEnd, end));
    }

    /// <summary>Lists a revision for every <c>%%EOF</c> marker, for files whose chain cannot be followed.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <returns>The revisions.</returns>
    internal static PdfRevision[] ReadMarkerRevisions(PdfObjectStore self)
    {
        var revisions = new List<PdfRevision>();
        var position = 0L;
        while (revisions.Count < PdfLimits.MaxXrefSections)
        {
            var marker = PdfByteSearch.IndexOf(self.Source, StoreRevisions.EndMarker, position);
            if (marker < 0)
            {
                break;
            }

            var markerEnd = marker + StoreRevisions.EndMarkerLength;
            revisions.Add(new(revisions.Count, -1, markerEnd, StoreRevisions.SkipLineEnd(self, markerEnd)));
            position = markerEnd;
        }

        return revisions.Count > 0 ? [.. revisions] : [new(0, -1, self.Source.Length, self.Source.Length)];
    }

    /// <summary>Follows /Prev from the newest cross-reference section, collecting each section's position.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <returns>The positions, newest first; empty when the chain cannot be followed.</returns>
    internal static List<long> ReadSectionOffsets(PdfObjectStore self)
    {
        var sections = new List<long>();
        var offset = self.StartXref;
        var visited = new HashSet<long>();
        while (offset >= 0 && sections.Count < PdfLimits.MaxXrefSections)
        {
            var position = offset + self.XrefState.OffsetBase;
            if (!visited.Add(position) || position >= self.Source.Length || StoreRevisions.ReadSectionTrailer(self, position) is not { } trailer)
            {
                break;
            }

            sections.Add(position);
            var previous = trailer.GetRaw(KnownName.Prev);
            offset = previous.Kind == PdfKind.Integer ? previous.AsInteger() : -1;
        }

        return sections;
    }

    /// <summary>Reads the trailer of the cross-reference section at a position: a classic table's trailer or a stream's dictionary.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "position">The section's position.</param>
    /// <returns>The trailer, or <see langword="null"/> when no section starts there.</returns>
    internal static PdfDictionary? ReadSectionTrailer(PdfObjectStore self, long position)
    {
        PdfTokenKind kind;
        bool isTable;
        long afterKeyword;
        using (var window = self.Source.Lease(position, (int)Math.Min(StoreRevisions.SectionTokenWindow, self.Source.Length - position)))
        {
            var lexer = new PdfLexer(window.Span);
            kind = lexer.Next();
            isTable = kind == PdfTokenKind.Keyword && lexer.Keyword == PdfKeyword.Xref;
            afterKeyword = position + lexer.Position;
        }

        if (isTable)
        {
            var trailer = PdfByteSearch.IndexOf(self.Source, PdfKeywords.Trailer, afterKeyword);
            return trailer < 0 ? null : StoreParsing.ParseValueAt(self, trailer + PdfKeywords.Trailer.Length).AsDictionary();
        }

        return kind == PdfTokenKind.Number && StoreParsing.TryParseObjectAt(self, position, out _, out var value) ? value.AsDictionary() : null;
    }

    /// <summary>Makes an unread store over the start of the file.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "length">The length of the revision.</param>
    /// <param name = "security">The handler to decrypt with, or <see langword="null"/>.</param>
    /// <returns>The store.</returns>
    private static PdfObjectStore CreateRevision(PdfObjectStore self, long length, PdfSecurityHandler? security) =>
        // A view of the file's start, read through this store's source, so the revision is not copied.
        new(new PrefixPdfByteSource(self.Source, length), true, new XrefTable())
        {
            HeaderOffset = self.HeaderOffset,
            Version = self.Version,
            Context = self.Context is { } context ? new PdfOpenContext(null, context.DrawingServices, CancellationToken.None) : null,
            Security = security,
        };
}
