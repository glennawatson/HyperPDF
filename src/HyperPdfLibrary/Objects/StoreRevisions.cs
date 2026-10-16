// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Buffers;
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

    /// <summary>The revision snapshots kept on the stack before leasing bounded pooled storage.</summary>
    private const int StackRevisions = 16;

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
        Span<long> storage = stackalloc long[PdfLimits.MaxXrefSections];
        var count = StoreRevisions.ReadSectionOffsets(self, storage);
        var sections = storage[..count];
        var capacity = count == 0 ? PdfLimits.MaxXrefSections : count;
        var rented = capacity > StackRevisions ? ArrayPool<PdfRevision>.Shared.Rent(capacity) : null;
        Span<PdfRevision> revisions = rented is null ? stackalloc PdfRevision[StackRevisions] : rented;
        revisions = revisions[..capacity];
        try
        {
            var revisionCount = 0;
            if (!sections.IsEmpty)
            {
                sections.Sort();
                foreach (var offset in sections)
                {
                    PdfOpenContext.ThrowIfCancelled(self.Context);
                    revisionCount = StoreRevisions.AddRevision(self, revisions, revisionCount, offset);
                }
            }

            return revisionCount > 0 ? revisions[..revisionCount].ToArray() : StoreRevisions.ReadMarkerRevisions(self, revisions);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<PdfRevision>.Shared.Return(rented);
            }
        }
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
    /// <param name = "revisions">The caller-owned working storage.</param>
    /// <param name="count">The revisions stored so far.</param>
    /// <param name = "offset">The section's position.</param>
    /// <returns>The stored revision count.</returns>
    internal static int AddRevision(PdfObjectStore self, Span<PdfRevision> revisions, int count, long offset)
    {
        var length = self.Source.Length;
        var marker = PdfByteSearch.IndexOf(self.Source, StoreRevisions.EndMarker, Math.Clamp(offset, 0, length));
        var markerEnd = marker < 0 ? length : marker + StoreRevisions.EndMarkerLength;
        var end = marker < 0 ? length : StoreRevisions.SkipLineEnd(self, markerEnd);
        if (count > 0 && revisions[count - 1].MarkerEnd >= markerEnd)
        {
            return count;
        }

        revisions[count] = new(count, offset, markerEnd, end);
        return count + 1;
    }

    /// <summary>Lists a revision for every <c>%%EOF</c> marker, for files whose chain cannot be followed.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name="revisions">The caller-owned working storage.</param>
    /// <returns>The revisions.</returns>
    internal static PdfRevision[] ReadMarkerRevisions(PdfObjectStore self, Span<PdfRevision> revisions)
    {
        var count = 0;
        var position = 0L;
        while (count < revisions.Length)
        {
            PdfOpenContext.ThrowIfCancelled(self.Context);
            var marker = PdfByteSearch.IndexOf(self.Source, StoreRevisions.EndMarker, position);
            if (marker < 0)
            {
                break;
            }

            var markerEnd = marker + StoreRevisions.EndMarkerLength;
            revisions[count] = new(count, -1, markerEnd, StoreRevisions.SkipLineEnd(self, markerEnd));
            count++;
            position = markerEnd;
        }

        return count > 0 ? revisions[..count].ToArray() : [new(0, -1, self.Source.Length, self.Source.Length)];
    }

    /// <summary>Follows /Prev from the newest cross-reference section, collecting each section's position.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name="sections">Storage for at least the maximum supported cross-reference section count.</param>
    /// <returns>The number of positions written, newest first; zero when the chain cannot be followed.</returns>
    /// <exception cref="PdfException">A section offset exceeds the available file bytes.</exception>
    internal static int ReadSectionOffsets(PdfObjectStore self, Span<long> sections)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sections.Length, PdfLimits.MaxXrefSections);
        var count = 0;
        var offset = self.StartXref;
        while (offset >= 0 && count < PdfLimits.MaxXrefSections)
        {
            PdfOpenContext.ThrowIfCancelled(self.Context);
            if (offset >= self.Source.Length - self.XrefState.OffsetBase)
            {
                throw new PdfException(PdfError.Format, "The previous cross-reference section points outside the file.");
            }

            var position = offset + self.XrefState.OffsetBase;

            // The output span also forms the bounded visited set.
            if (sections[..count].Contains(position) || StoreRevisions.ReadSectionTrailer(self, position) is not { } trailer)
            {
                break;
            }

            sections[count] = position;
            count++;
            var previous = trailer.GetRaw(KnownName.Prev);
            offset = previous.Kind == PdfKind.Integer ? previous.AsInteger() : -1;
        }

        return count;
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
            Context = self.Context is { } context
                ? new PdfOpenContext(null, context.DrawingServices, context.Recovery, context.IgnoreXrefStreams, context.MaxDecompressedBytes, context.CancellationToken)
                : null,
            Security = security,
        };
}
