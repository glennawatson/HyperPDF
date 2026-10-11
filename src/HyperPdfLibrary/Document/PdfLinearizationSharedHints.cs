// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Document;

/// <summary>Checks the shared-object and optional outline sections of a linearization hint stream.</summary>
internal static class PdfLinearizationSharedHints
{
    /// <summary>The fixed shared-object header size in bytes.</summary>
    private const int HeaderBytes = 24;

    /// <summary>The fixed outline table size in bytes.</summary>
    private const int OutlineBytes = 16;

    /// <summary>The bits in one byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The number of 32-bit fields in one MD5 signature.</summary>
    private const int SignatureWords = 4;

    /// <summary>The bit width of each signature word.</summary>
    private const int SignatureWordBits = 32;

    /// <summary>The byte offset of the second 32-bit hint field.</summary>
    private const int SecondFieldOffset = 4;

    /// <summary>The byte offset of the third 32-bit hint field.</summary>
    private const int ThirdFieldOffset = 8;

    /// <summary>The byte offset of the fourth 32-bit hint field.</summary>
    private const int FourthFieldOffset = 12;

    /// <summary>Checks cancellation once per 64 shared objects.</summary>
    private const int CancellationBatchMask = 63;

    /// <summary>Reads the fixed shared-object header at /S.</summary>
    /// <param name="data">The decoded hint bytes.</param>
    /// <param name="offset">The /S byte offset.</param>
    /// <returns>The header, or null when its bounds or fields are invalid.</returns>
    internal static SharedTable? ReadHeader(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < 0 || offset > data.Length - HeaderBytes)
        {
            return null;
        }

        var header = new SharedHeader(data[offset..]);
        return header.IsValid && header.TotalObjects <= (long)(data.Length - offset - HeaderBytes) * ByteBits ? new(offset, header) : null;
    }

    /// <summary>Validates the packed shared-object ranges and optional outline location.</summary>
    /// <param name="document">The opened file and its object offsets.</param>
    /// <param name="info">The linearization header.</param>
    /// <param name="stream">The hint stream dictionary.</param>
    /// <param name="data">The decoded hint bytes.</param>
    /// <param name="table">The shared-object table offset and header.</param>
    /// <param name="pages">The decoded page group ranges.</param>
    /// <param name="cancellationToken">Cancels long tables.</param>
    /// <returns>Whether the shared and outline ranges fit their declared sections.</returns>
    internal static bool Validate(
        PdfDocument document,
        PdfLinearizationInfo info,
        PdfStream stream,
        ReadOnlySpan<byte> data,
        in SharedTable table,
        PdfHintPageRange[] pages,
        CancellationToken cancellationToken)
    {
        var header = table.Header;
        var bits = new PdfHintBits(data, (long)(table.Offset + HeaderBytes) * ByteBits);
        var lengths = new long[header.TotalObjects];
        if (!ReadLengths(header, ref bits, lengths, cancellationToken)
            || !SkipSignatures(header, ref bits, cancellationToken)
            || !CheckRanges(document.Objects, info, header, ref bits, lengths, pages[info.FirstPageIndex], cancellationToken))
        {
            return false;
        }

        var outlineOffset = stream.Dictionary.GetInt32(KnownName.O);
        if (outlineOffset > 0 && bits.PositionBits > (long)outlineOffset * ByteBits)
        {
            return false;
        }

        return CheckOutline(document.Objects, info, stream.Dictionary.GetRaw(KnownName.O), data);
    }

    /// <summary>Reads the byte length of each shared-object group.</summary>
    /// <param name="header">The shared-table fields.</param>
    /// <param name="bits">The packed values.</param>
    /// <param name="lengths">Receives group lengths.</param>
    /// <param name="cancellationToken">Cancels long tables.</param>
    /// <returns>Whether every group length is valid.</returns>
    private static bool ReadLengths(SharedHeader header, ref PdfHintBits bits, long[] lengths, CancellationToken cancellationToken)
    {
        for (var index = 0; index < lengths.Length; index++)
        {
            if ((index & CancellationBatchMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!bits.TryRead(header.DeltaLengthBits, out var delta))
            {
                return false;
            }

            lengths[index] = (long)header.LeastGroupLength + delta;
            if (lengths[index] <= 0)
            {
                return false;
            }
        }

        bits.Align();
        return true;
    }

    /// <summary>Skips the optional 128-bit signatures after their presence flags.</summary>
    /// <param name="header">The shared-table fields.</param>
    /// <param name="bits">The packed values.</param>
    /// <param name="cancellationToken">Cancels long tables.</param>
    /// <returns>Whether the signature fields are complete.</returns>
    private static bool SkipSignatures(SharedHeader header, ref PdfHintBits bits, CancellationToken cancellationToken)
    {
        var present = 0L;
        for (var index = 0; index < header.TotalObjects; index++)
        {
            if ((index & CancellationBatchMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!bits.TryRead(1, out var flag))
            {
                return false;
            }

            present += flag;
        }

        bits.Align();
        if (!bits.TrySkipValues(present * SignatureWords, SignatureWordBits))
        {
            return false;
        }

        bits.Align();
        return true;
    }

    /// <summary>Checks shared group numbers and byte positions against the first page and file.</summary>
    /// <param name="store">The file's cross-reference table.</param>
    /// <param name="info">The linearization header.</param>
    /// <param name="header">The shared-table fields.</param>
    /// <param name="bits">The packed object counts.</param>
    /// <param name="lengths">The group lengths.</param>
    /// <param name="firstPage">The first page group.</param>
    /// <param name="cancellationToken">Cancels long tables.</param>
    /// <returns>Whether all groups fit.</returns>
    private static bool CheckRanges(
        PdfObjectStore store,
        PdfLinearizationInfo info,
        SharedHeader header,
        ref PdfHintBits bits,
        long[] lengths,
        PdfHintPageRange firstPage,
        CancellationToken cancellationToken)
    {
        var number = (long)info.FirstPageObjectNumber;
        var offset = firstPage.Offset;
        for (var index = 0; index < lengths.Length; index++)
        {
            if ((index & CancellationBatchMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (index == header.FirstPageObjects)
            {
                number = header.FirstObjectNumber;
                offset = AdjustOffset(info, header.FirstObjectOffset);
            }

            if (!bits.TryRead(header.ObjectCountBits, out var delta)
                || !GroupFits(store, info, firstPage, header, new(index, number, offset, lengths[index], delta)))
            {
                return false;
            }

            number += delta + 1;
            offset += lengths[index];
        }

        bits.Align();
        return true;
    }

    /// <summary>Checks one shared group's object numbers and file interval.</summary>
    /// <param name="store">The file's cross-reference table.</param>
    /// <param name="info">The file bounds.</param>
    /// <param name="firstPage">The first-page bounds.</param>
    /// <param name="header">The shared-table partition.</param>
    /// <param name="group">The group position and count.</param>
    /// <returns>Whether the group lies in its declared section.</returns>
    private static bool GroupFits(PdfObjectStore store, PdfLinearizationInfo info, PdfHintPageRange firstPage, SharedHeader header, in SharedGroup group)
    {
        if (group.Number < 1 || group.Number + group.Delta >= PdfLimits.MaxObjectNumber
            || group.Offset <= 0 || group.Offset > info.FileLength - group.Length
            || (group.Index < header.FirstPageObjects && group.Offset + group.Length > firstPage.Offset + firstPage.Length))
        {
            return false;
        }

        var number = (int)group.Number;
        return store.XrefState.GetType(number) != XrefEntryType.Free
            && StoreRevisions.GetDefinitionOffset(store, number) == group.Offset;
    }

    /// <summary>Checks the optional four-field outline hint table.</summary>
    /// <param name="store">The file's cross-reference table.</param>
    /// <param name="info">The linearization header.</param>
    /// <param name="value">The /O hint dictionary entry.</param>
    /// <param name="data">The decoded hint bytes.</param>
    /// <returns>Whether the outline hint is absent or fits the file.</returns>
    private static bool CheckOutline(PdfObjectStore store, PdfLinearizationInfo info, PdfValue value, ReadOnlySpan<byte> data)
    {
        if (value.IsNull)
        {
            return true;
        }

        if (value.Kind != PdfKind.Integer || value.AsInteger() <= 0 || value.AsInteger() > data.Length - OutlineBytes)
        {
            return false;
        }

        var table = data[(int)value.AsInteger()..];
        var firstObject = BinaryPrimitives.ReadUInt32BigEndian(table);
        var firstOffset = BinaryPrimitives.ReadUInt32BigEndian(table[SecondFieldOffset..]);
        var count = BinaryPrimitives.ReadUInt32BigEndian(table[ThirdFieldOffset..]);
        var length = BinaryPrimitives.ReadUInt32BigEndian(table[FourthFieldOffset..]);
        return OutlineFits(store, info, firstObject, firstOffset, count, length);
    }

    /// <summary>Checks an outline table's object and byte interval against the file.</summary>
    /// <param name="store">The file's cross-reference table.</param>
    /// <param name="info">The file bounds.</param>
    /// <param name="firstObject">The first outline object number.</param>
    /// <param name="firstOffset">The first offset without the hint object.</param>
    /// <param name="count">The outline object count.</param>
    /// <param name="length">The outline group length.</param>
    /// <returns>Whether the declared group matches the xref.</returns>
    private static bool OutlineFits(PdfObjectStore store, PdfLinearizationInfo info, uint firstObject, uint firstOffset, uint count, uint length)
    {
        var actualOffset = AdjustOffset(info, firstOffset);
        return firstObject != 0 && count != 0 && (long)firstObject + count <= PdfLimits.MaxObjectNumber
            && length != 0 && actualOffset > 0 && actualOffset <= info.FileLength - length
            && StoreRevisions.GetDefinitionOffset(store, (int)firstObject) == actualOffset;
    }

    /// <summary>Adds the primary hint object's byte length to offsets after it.</summary>
    /// <param name="info">The primary hint range.</param>
    /// <param name="position">The hint-table position.</param>
    /// <returns>The actual file position.</returns>
    private static long AdjustOffset(PdfLinearizationInfo info, uint position) =>
        position >= info.Hints[0].Offset ? (long)position + info.Hints[0].Length : position;

    /// <summary>The fixed shared-object hint header.</summary>
    internal readonly record struct SharedHeader
    {
        /// <summary>The widest packed group field.</summary>
        private const int MaxWidth = 32;

        /// <summary>The byte offset of the shared object-count bit width.</summary>
        private const int SharedObjectBitsOffset = 16;

        /// <summary>The byte offset of the least shared-group length.</summary>
        private const int SharedLengthOffset = 18;

        /// <summary>The byte offset of the shared length-delta bit width.</summary>
        private const int SharedLengthBitsOffset = 22;

        /// <summary>Initializes a new instance of the <see cref="SharedHeader"/> struct.</summary>
        /// <param name="data">At least 24 bytes beginning at /S.</param>
        internal SharedHeader(ReadOnlySpan<byte> data)
        {
            FirstObjectNumber = BinaryPrimitives.ReadUInt32BigEndian(data);
            FirstObjectOffset = BinaryPrimitives.ReadUInt32BigEndian(data[SecondFieldOffset..]);
            FirstPageObjects = (int)BinaryPrimitives.ReadUInt32BigEndian(data[ThirdFieldOffset..]);
            TotalObjects = (int)BinaryPrimitives.ReadUInt32BigEndian(data[FourthFieldOffset..]);
            ObjectCountBits = BinaryPrimitives.ReadUInt16BigEndian(data[SharedObjectBitsOffset..]);
            LeastGroupLength = BinaryPrimitives.ReadUInt32BigEndian(data[SharedLengthOffset..]);
            DeltaLengthBits = BinaryPrimitives.ReadUInt16BigEndian(data[SharedLengthBitsOffset..]);
        }

        /// <summary>Gets the first shared object after the first-page section.</summary>
        internal uint FirstObjectNumber { get; }

        /// <summary>Gets its position without the primary hint object.</summary>
        internal uint FirstObjectOffset { get; }

        /// <summary>Gets the number of shared-table entries from the first-page group.</summary>
        internal int FirstPageObjects { get; }

        /// <summary>Gets the total number of shared-table entries.</summary>
        internal int TotalObjects { get; }

        /// <summary>Gets the bits in each group object-count delta.</summary>
        internal int ObjectCountBits { get; }

        /// <summary>Gets the smallest shared-group length.</summary>
        internal uint LeastGroupLength { get; }

        /// <summary>Gets the bits in each group-length delta.</summary>
        internal int DeltaLengthBits { get; }

        /// <summary>Gets whether the header's counts and packed widths are bounded.</summary>
        internal bool IsValid => TotalObjects >= 0 && TotalObjects <= PdfLimits.MaxObjectNumber
            && FirstPageObjects >= 0 && FirstPageObjects <= TotalObjects
            && ObjectCountBits <= MaxWidth && DeltaLengthBits <= MaxWidth
            && (TotalObjects == 0 || LeastGroupLength != 0);
    }

    /// <summary>The byte position and fixed fields of a shared-object table.</summary>
    /// <param name="Offset">The /S byte offset.</param>
    /// <param name="Header">The decoded fields.</param>
    internal readonly record struct SharedTable(int Offset, SharedHeader Header);

    /// <summary>The position and size of one shared-object group.</summary>
    /// <param name="Index">The group's table index.</param>
    /// <param name="Number">The first object number.</param>
    /// <param name="Offset">The first byte in the file.</param>
    /// <param name="Length">The group length in bytes.</param>
    /// <param name="Delta">The object count less one.</param>
    private readonly record struct SharedGroup(int Index, long Number, long Offset, long Length, uint Delta);
}
