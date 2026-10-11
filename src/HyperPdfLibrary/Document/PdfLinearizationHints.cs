// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Decodes the page-offset hint table of a linearized PDF.</summary>
public static class PdfLinearizationHints
{
    /// <summary>The page-offset table header length in bytes.</summary>
    private const int PageHeaderLength = 36;

    /// <summary>The shared-object table header length in bytes.</summary>
    private const int SharedHeaderLength = 24;

    /// <summary>The number of bits in one byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Checks cancellation once per 64 pages.</summary>
    private const int CancellationBatchMask = 63;

    /// <summary>Gets page group offsets and lengths, or null when the hints are absent or damaged.</summary>
    /// <param name="document">The open document.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The page ranges, or null.</returns>
    public static PdfHintPageRange[]? GetPageRanges(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        var info = PdfDocumentLinearization.GetInfo(document);
        if (info is null || info.Hints.IsDefaultOrEmpty || TryLoadStream(document, info.Hints[0]) is not { } stream)
        {
            return null;
        }

        var data = stream.DecodeToArray(cancellationToken);
        if (info.Hints.Length > 1)
        {
            if (TryLoadStream(document, info.Hints[1]) is not { } second)
            {
                return null;
            }

            var combined = Join(data, second.DecodeToArray(cancellationToken));
            if (combined is null)
            {
                return null;
            }

            data = combined;
        }

        return Decode(document, info, stream, data, cancellationToken);
    }

    /// <summary>Reads the hint stream with async I/O, then decodes its page group ranges.</summary>
    /// <param name="document">The open document.</param>
    /// <param name="cancellationToken">Cancels reading and decoding.</param>
    /// <returns>The page ranges, or null when absent or damaged.</returns>
    public static async ValueTask<PdfHintPageRange[]?> GetPageRangesAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        var info = await PdfDocumentLinearization.GetInfoAsync(document, cancellationToken).ConfigureAwait(false);
        if (info is null || info.Hints.IsDefaultOrEmpty)
        {
            return null;
        }

        var hint = info.Hints[0];
        await document.Objects.Source.PrefetchAsync(hint.Offset, Math.Min(hint.Length, StoreParsing.InitialWindow), cancellationToken).ConfigureAwait(false);
        if (TryLoadStream(document, hint) is not { } stream)
        {
            return null;
        }

        var data = await DecodeStreamAsync(stream, cancellationToken).ConfigureAwait(false);
        if (info.Hints.Length > 1)
        {
            var secondHint = info.Hints[1];
            await document.Objects.Source.PrefetchAsync(secondHint.Offset, Math.Min(secondHint.Length, StoreParsing.InitialWindow), cancellationToken).ConfigureAwait(false);
            if (TryLoadStream(document, secondHint) is not { } second)
            {
                return null;
            }

            var combined = Join(data, await DecodeStreamAsync(second, cancellationToken).ConfigureAwait(false));
            if (combined is null)
            {
                return null;
            }

            data = combined;
        }

        return Decode(document, info, stream, data, cancellationToken);
    }

    /// <summary>Copies a hint stream with async I/O before decoding its filters.</summary>
    /// <param name="stream">The hint object.</param>
    /// <param name="cancellationToken">Cancels the read and decode.</param>
    /// <returns>The decoded table bytes.</returns>
    private static async ValueTask<byte[]> DecodeStreamAsync(PdfStream stream, CancellationToken cancellationToken)
    {
        var raw = await stream.CopyRawDataAsync(cancellationToken).ConfigureAwait(false);
        var buffered = new PdfStream(stream.Dictionary, raw, 0, raw.Length, stream.Id, stream.IsEncrypted);
        return buffered.DecodeToArray(cancellationToken);
    }

    /// <summary>Appends an overflow hint stream without exceeding the decoded-stream budget.</summary>
    /// <param name="first">The primary hint bytes.</param>
    /// <param name="second">The overflow hint bytes.</param>
    /// <returns>The joined data, or null when too large.</returns>
    private static byte[]? Join(byte[] first, byte[] second)
    {
        if ((long)first.Length + second.Length > PdfLimits.MaxDecodedLength)
        {
            return null;
        }

        if (second.Length == 0)
        {
            return first;
        }

        var joined = new byte[first.Length + second.Length];
        first.CopyTo(joined, 0);
        second.CopyTo(joined, first.Length);
        return joined;
    }

    /// <summary>Parses the indirect hint-stream object at its declared offset.</summary>
    /// <param name="document">The document.</param>
    /// <param name="hint">The declared range.</param>
    /// <returns>The stream, or null.</returns>
    private static PdfStream? TryLoadStream(PdfDocument document, PdfHintRange hint) =>
        StoreParsing.TryParseObjectAt(document.Objects, hint.Offset, out _, out var value) ? value.AsStream() : null;

    /// <summary>Decodes page offsets and checks the shared-table boundary.</summary>
    /// <param name="document">The open file and its cross-reference table.</param>
    /// <param name="info">The validated linearization header.</param>
    /// <param name="stream">The primary hint stream.</param>
    /// <param name="data">The decoded bytes.</param>
    /// <param name="cancellationToken">Cancels the page walk.</param>
    /// <returns>The page ranges, or null when damaged.</returns>
    private static PdfHintPageRange[]? Decode(PdfDocument document, PdfLinearizationInfo info, PdfStream stream, ReadOnlySpan<byte> data, CancellationToken cancellationToken)
    {
        var sharedOffset = stream.Dictionary.GetInt32(KnownName.S);
        if (data.Length < PageHeaderLength + SharedHeaderLength || sharedOffset < PageHeaderLength || sharedOffset > data.Length - SharedHeaderLength)
        {
            return null;
        }

        var header = new PageHintHeader(data);
        var shared = PdfLinearizationSharedHints.ReadHeader(data, sharedOffset);
        if (!header.IsValid || shared is null)
        {
            return null;
        }

        var bits = new PdfHintBits(data, PageHeaderLength * ByteBits);
        var pages = new PdfHintPageRange[info.PageCount];
        return ReadPageRows(info, header, ref bits, pages, cancellationToken)
            && ReadSharedReferences(header, ref bits, pages.Length, shared.Value.Header.TotalObjects, cancellationToken)
            && bits.PositionBits <= (long)sharedOffset * ByteBits
            && PdfLinearizationSharedHints.Validate(document, info, stream, data, shared.Value, pages, cancellationToken)
            ? pages
            : null;
    }

    /// <summary>Decodes the object counts, lengths and byte positions of the page groups.</summary>
    /// <param name="info">The linearization header.</param>
    /// <param name="header">The page table header.</param>
    /// <param name="bits">The packed values.</param>
    /// <param name="pages">Receives the page groups.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    /// <returns>Whether every page group is bounded.</returns>
    private static bool ReadPageRows(PdfLinearizationInfo info, in PageHintHeader header, ref PdfHintBits bits, PdfHintPageRange[] pages, CancellationToken cancellationToken) =>
        ReadObjectCounts(info, header, ref bits, pages, cancellationToken)
        && ReadPageLengths(header, ref bits, pages, cancellationToken)
        && ReadPageOffsets(info, header, pages);

    /// <summary>Reads each page's object count and first object number.</summary>
    /// <param name="info">The linearization header.</param>
    /// <param name="header">The hint header.</param>
    /// <param name="bits">The packed values.</param>
    /// <param name="pages">The ranges being built.</param>
    /// <param name="cancellationToken">Cancels long page walks.</param>
    /// <returns>Whether all object counts are valid.</returns>
    private static bool ReadObjectCounts(PdfLinearizationInfo info, in PageHintHeader header, ref PdfHintBits bits, PdfHintPageRange[] pages, CancellationToken cancellationToken)
    {
        var nextObject = 1L;
        for (var index = 0; index < pages.Length; index++)
        {
            if ((index & CancellationBatchMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!bits.TryRead(header.DeltaObjectBits, out var delta) || (long)header.LeastObjects + delta > PdfLimits.MaxObjectNumber)
            {
                return false;
            }

            var count = (int)(header.LeastObjects + delta);
            var firstObject = index == info.FirstPageIndex ? (long)info.FirstPageObjectNumber : nextObject;
            if (firstObject < 1 || firstObject + count > PdfLimits.MaxObjectNumber)
            {
                return false;
            }

            pages[index] = new(0, 0, (int)firstObject, count);
            if (index != info.FirstPageIndex)
            {
                nextObject += count;
            }
        }

        bits.Align();
        return true;
    }

    /// <summary>Reads the byte length of each page group.</summary>
    /// <param name="header">The hint header.</param>
    /// <param name="bits">The packed values.</param>
    /// <param name="pages">The ranges being built.</param>
    /// <param name="cancellationToken">Cancels long page walks.</param>
    /// <returns>Whether all lengths are present.</returns>
    private static bool ReadPageLengths(in PageHintHeader header, ref PdfHintBits bits, PdfHintPageRange[] pages, CancellationToken cancellationToken)
    {
        for (var index = 0; index < pages.Length; index++)
        {
            if ((index & CancellationBatchMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!bits.TryRead(header.DeltaPageLengthBits, out var delta))
            {
                return false;
            }

            pages[index] = pages[index] with { Length = (long)header.LeastPageLength + delta };
        }

        bits.Align();
        return true;
    }

    /// <summary>Places the first page at its hint location and the other pages after /E.</summary>
    /// <param name="info">The linearization header.</param>
    /// <param name="header">The hint header.</param>
    /// <param name="pages">The ranges being built.</param>
    /// <returns>Whether all ranges lie inside the file.</returns>
    private static bool ReadPageOffsets(PdfLinearizationInfo info, in PageHintHeader header, PdfHintPageRange[] pages)
    {
        var hint = info.Hints[0];
        var firstOffset = (long)header.FirstObjectLocation;
        if (firstOffset >= hint.Offset)
        {
            if (hint.Length > long.MaxValue - firstOffset)
            {
                return false;
            }

            firstOffset += hint.Length;
        }

        var nextOffset = info.FirstPageEndOffset;
        for (var index = 0; index < pages.Length; index++)
        {
            var offset = index == info.FirstPageIndex ? firstOffset : nextOffset;
            var length = pages[index].Length;
            if (offset <= 0 || length <= 0 || offset > info.FileLength - length)
            {
                return false;
            }

            pages[index] = pages[index] with { Offset = offset };
            if (index != info.FirstPageIndex)
            {
                nextOffset += length;
            }
        }

        return true;
    }

    /// <summary>Checks shared identifiers and skips fractional and content positions after the page lengths.</summary>
    /// <param name="header">The hint header.</param>
    /// <param name="bits">The packed values.</param>
    /// <param name="pageCount">The number of pages.</param>
    /// <param name="totalShared">The number of entries in the shared-object table.</param>
    /// <param name="cancellationToken">Cancels long page walks.</param>
    /// <returns>Whether all page-table fields are present.</returns>
    private static bool ReadSharedReferences(in PageHintHeader header, ref PdfHintBits bits, int pageCount, int totalShared, CancellationToken cancellationToken)
    {
        var shared = 0L;
        for (var index = 0; index < pageCount; index++)
        {
            if ((index & CancellationBatchMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!bits.TryRead(header.SharedObjectBits, out var count))
            {
                return false;
            }

            if (shared > long.MaxValue - count)
            {
                return false;
            }

            shared += count;
        }

        bits.Align();
        if (!ReadIdentifiers(ref bits, shared, header.SharedIdBits, totalShared, cancellationToken)
            || !bits.TrySkipValues(shared, header.SharedNumeratorBits))
        {
            return false;
        }

        bits.Align();
        if (!bits.TrySkipValues(pageCount, header.DeltaContentOffsetBits))
        {
            return false;
        }

        bits.Align();
        if (!bits.TrySkipValues(pageCount, header.DeltaContentLengthBits))
        {
            return false;
        }

        bits.Align();
        return true;
    }

    /// <summary>Checks each shared-object index against the shared table's size.</summary>
    /// <param name="bits">The packed values.</param>
    /// <param name="count">The number of indexes.</param>
    /// <param name="width">The bits in each index.</param>
    /// <param name="totalShared">The shared-table entry count.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    /// <returns>Whether every index is present and in bounds.</returns>
    private static bool ReadIdentifiers(ref PdfHintBits bits, long count, int width, int totalShared, CancellationToken cancellationToken)
    {
        for (var index = 0L; index < count; index++)
        {
            if ((index & CancellationBatchMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!bits.TryRead(width, out var identifier) || identifier >= totalShared)
            {
                return false;
            }
        }

        bits.Align();
        return true;
    }

    /// <summary>The fixed page-offset table header (ISO 32000-2 Annex F).</summary>
    [DebuggerDisplay("PageHintHeader: {LeastObjects} objects, {LeastPageLength} bytes")]
    private readonly record struct PageHintHeader
    {
        /// <summary>The largest packed integer width.</summary>
        private const int MaxBitWidth = 32;

        /// <summary>The offset of the first page object's location.</summary>
        private const int FirstObjectLocationOffset = 4;

        /// <summary>The offset of the object-count delta width.</summary>
        private const int DeltaObjectBitsOffset = 8;

        /// <summary>The offset of the least page length.</summary>
        private const int LeastPageLengthOffset = 10;

        /// <summary>The offset of the page-length delta width.</summary>
        private const int DeltaPageLengthBitsOffset = 14;

        /// <summary>The offset of the shared-object count width.</summary>
        private const int SharedObjectBitsOffset = 28;

        /// <summary>The offset of the content-offset delta width.</summary>
        private const int DeltaContentOffsetBitsOffset = 20;

        /// <summary>The offset of the content-length delta width.</summary>
        private const int DeltaContentLengthBitsOffset = 26;

        /// <summary>The offset of the shared-object identifier width.</summary>
        private const int SharedIdBitsOffset = 30;

        /// <summary>The offset of the shared-object position numerator width.</summary>
        private const int SharedNumeratorBitsOffset = 32;

        /// <summary>Initializes a new instance of the <see cref="PageHintHeader"/> class.</summary>
        /// <param name="data">The decoded hint stream.</param>
        internal PageHintHeader(ReadOnlySpan<byte> data)
        {
            LeastObjects = BinaryPrimitives.ReadUInt32BigEndian(data);
            FirstObjectLocation = BinaryPrimitives.ReadUInt32BigEndian(data[FirstObjectLocationOffset..]);
            DeltaObjectBits = BinaryPrimitives.ReadUInt16BigEndian(data[DeltaObjectBitsOffset..]);
            LeastPageLength = BinaryPrimitives.ReadUInt32BigEndian(data[LeastPageLengthOffset..]);
            DeltaPageLengthBits = BinaryPrimitives.ReadUInt16BigEndian(data[DeltaPageLengthBitsOffset..]);
            DeltaContentOffsetBits = BinaryPrimitives.ReadUInt16BigEndian(data[DeltaContentOffsetBitsOffset..]);
            DeltaContentLengthBits = BinaryPrimitives.ReadUInt16BigEndian(data[DeltaContentLengthBitsOffset..]);
            SharedObjectBits = BinaryPrimitives.ReadUInt16BigEndian(data[SharedObjectBitsOffset..]);
            SharedIdBits = BinaryPrimitives.ReadUInt16BigEndian(data[SharedIdBitsOffset..]);
            SharedNumeratorBits = BinaryPrimitives.ReadUInt16BigEndian(data[SharedNumeratorBitsOffset..]);
        }

        /// <summary>Gets the least number of objects on any page.</summary>
        internal uint LeastObjects { get; }

        /// <summary>Gets the first-page object's position without the hint stream.</summary>
        internal uint FirstObjectLocation { get; }

        /// <summary>Gets the bits in each object-count delta.</summary>
        internal int DeltaObjectBits { get; }

        /// <summary>Gets the least page-group length.</summary>
        internal uint LeastPageLength { get; }

        /// <summary>Gets the bits in each page-length delta.</summary>
        internal int DeltaPageLengthBits { get; }

        /// <summary>Gets the bits in each content-offset delta.</summary>
        internal int DeltaContentOffsetBits { get; }

        /// <summary>Gets the bits in each content-length delta.</summary>
        internal int DeltaContentLengthBits { get; }

        /// <summary>Gets the bits in each page's shared-object count.</summary>
        internal int SharedObjectBits { get; }

        /// <summary>Gets the bits in each shared-object identifier.</summary>
        internal int SharedIdBits { get; }

        /// <summary>Gets the bits in each shared-object position numerator.</summary>
        internal int SharedNumeratorBits { get; }

        /// <summary>Gets whether the packed integer widths and required values fit the format.</summary>
        internal bool IsValid => LeastObjects != 0 && FirstObjectLocation != 0 && LeastPageLength != 0
            && DeltaObjectBits <= MaxBitWidth && DeltaPageLengthBits <= MaxBitWidth
            && DeltaContentOffsetBits <= MaxBitWidth && DeltaContentLengthBits <= MaxBitWidth
            && SharedObjectBits <= MaxBitWidth && SharedIdBits <= MaxBitWidth && SharedNumeratorBits <= MaxBitWidth;
    }
}
