// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>Processes JBIG2 segments and transfers decoded regions to their owner or page.</summary>
internal static class Jbig2Segments
{
    /// <summary>The smallest segment header; fewer bytes left end processing.</summary>
    internal const int MinSegmentSize = 11;

    /// <summary>The bytes after an arithmetic-coded segment's data that the decoder may have read into.</summary>
    internal const int ArithmeticTrailer = 2;

    /// <summary>The bytes of the row count after a generic region of unknown length.</summary>
    internal const int RowCountBytes = 4;

    /// <summary>The page information bytes after the width and height: the resolutions, flags and striping.</summary>
    internal const int PageInfoTail = 11;

    /// <summary>The page information flag of the default pixel value.</summary>
    internal const int DefaultPixelFlag = 0x04;

    /// <summary>The offset of the page flags after the width and height.</summary>
    internal const int PageFlagsOffset = 8;

    /// <summary>The highest segment type number.</summary>
    internal const int TypeMask = 0x3F;

    /// <summary>The broad kinds of segment, as dispatched.</summary>
    internal enum SegmentKind
    {
        /// <summary>Segments whose data is skipped.</summary>
        Ignored = 0,

        /// <summary>A symbol dictionary.</summary>
        SymbolDictionary = 1,

        /// <summary>A pattern dictionary.</summary>
        PatternDictionary = 2,

        /// <summary>A Huffman table.</summary>
        Table = 3,

        /// <summary>The page information.</summary>
        PageInformation = 4,

        /// <summary>An end-of-page or end-of-file segment.</summary>
        End = 5,

        /// <summary>A text region.</summary>
        TextRegion = 6,

        /// <summary>A halftone region.</summary>
        HalftoneRegion = 7,

        /// <summary>A generic region.</summary>
        GenericRegion = 8,

        /// <summary>A refinement region.</summary>
        RefinementRegion = 9,
    }

    /// <summary>Gets the kind of each segment type number from 0 to 63.</summary>
    internal static ReadOnlySpan<byte> SegmentKinds =>
    [
        0x01, 0x00, 0x00, 0x00, 0x06, 0x00, 0x06, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x02, 0x00, 0x00, 0x00, 0x07, 0x00, 0x07, 0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x08, 0x08, 0x09, 0x00, 0x09, 0x09, 0x00, 0x00, 0x00, 0x00,
        0x04, 0x05, 0x00, 0x05, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    ];

    /// <summary>Processes segments until the end of the data, an end segment or damage.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="data">The segments.</param>
    /// <returns><see cref="Jbig2Status.Success"/>, or <see cref="Jbig2Status.Failure"/> on damage or empty data.</returns>
    internal static Jbig2Status DecodeSequential(Jbig2DecodeState state, ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return Jbig2Status.Failure;
        }

        var reader = new Jbig2Reader(data, state.CancellationToken);
        while (reader.BytesLeft >= MinSegmentSize)
        {
            state.CancellationToken.ThrowIfCancellationRequested();
            if (Jbig2Segment.Parse(ref reader) is not { } segment)
            {
                return Jbig2Status.Failure;
            }

            var status = DecodeSegment(state, ref reader, segment);
            if (status != Jbig2Status.Success || !MoveToNextSegment(ref reader, segment))
            {
                segment.Dispose();
                return status == Jbig2Status.EndReached ? Jbig2Status.Success : Jbig2Status.Failure;
            }

            state.Segments.Add(segment);
        }

        return Jbig2Status.Success;
    }

    /// <summary>Moves the reader to the next segment header.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="segment">The segment just processed.</param>
    /// <returns><see langword="false"/> when the data length overflows.</returns>
    internal static bool MoveToNextSegment(ref Jbig2Reader reader, Jbig2Segment segment)
    {
        if (segment.DataLength == Jbig2Segment.UnknownLength)
        {
            reader.Skip(RowCountBytes);
            return true;
        }

        var next = (long)segment.DataOffset + segment.DataLength;
        if (next > uint.MaxValue)
        {
            return false;
        }

        reader.Offset = (int)Math.Min(next, reader.Data.Length);
        return true;
    }

    /// <summary>Finishes an arithmetic-coded segment: moves the reader to where the decoder stopped, aligned.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="position">The decoder's position.</param>
    internal static void FinishArithmetic(ref Jbig2Reader reader, int position)
    {
        reader.Offset = position;
        reader.AlignByte();
        reader.Skip(ArithmeticTrailer);
    }

    /// <summary>Skips the data of a segment that needs no processing.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="segment">The segment.</param>
    /// <returns><see cref="Jbig2Status.Success"/>.</returns>
    internal static Jbig2Status Skip(ref Jbig2Reader reader, Jbig2Segment segment)
    {
        reader.Skip(segment.DataLength);
        return Jbig2Status.Success;
    }

    /// <summary>Processes one segment's data.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <param name="segment">The segment.</param>
    /// <returns>The status.</returns>
    internal static Jbig2Status DecodeSegment(Jbig2DecodeState state, ref Jbig2Reader reader, Jbig2Segment segment)
    {
        var kind = (SegmentKind)SegmentKinds[(int)segment.Type & TypeMask];
        if (kind >= Jbig2Segments.SegmentKind.TextRegion)
        {
            return state.InPage && state.Page is not null ? DecodeRegion(state, ref reader, segment, kind, state.Page) : Jbig2Status.Failure;
        }

        return kind switch
        {
            Jbig2Segments.SegmentKind.SymbolDictionary => Jbig2Dictionaries.DecodeSymbolDictionary(state, ref reader, segment),
            Jbig2Segments.SegmentKind.PatternDictionary => Jbig2Dictionaries.DecodePatternDictionary(state, ref reader, segment),
            Jbig2Segments.SegmentKind.Table => Jbig2Dictionaries.DecodeTable(ref reader, segment),
            Jbig2Segments.SegmentKind.PageInformation => DecodePageInformation(state, ref reader),
            Jbig2Segments.SegmentKind.End => End(state, segment),
            _ => Skip(ref reader, segment),
        };
    }

    /// <summary>Processes a region segment.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="kind">The region kind.</param>
    /// <param name="page">The page.</param>
    /// <returns>The status.</returns>
    internal static Jbig2Status DecodeRegion(Jbig2DecodeState state, ref Jbig2Reader reader, Jbig2Segment segment, SegmentKind kind, Jbig2Bitmap page) => kind switch
    {
        Jbig2Segments.SegmentKind.TextRegion => Jbig2TextSegments.DecodeTextRegion(state, ref reader, segment, page),
        Jbig2Segments.SegmentKind.HalftoneRegion => Jbig2Regions.DecodeHalftoneRegion(state, ref reader, segment, page),
        Jbig2Segments.SegmentKind.GenericRegion => Jbig2Regions.DecodeGenericRegion(state, ref reader, segment, page),
        _ => Jbig2Regions.DecodeRefinementRegion(state, ref reader, segment, page),
    };

    /// <summary>Handles an end-of-page or end-of-file segment.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="segment">The segment.</param>
    /// <returns><see cref="Jbig2Status.EndReached"/>.</returns>
    internal static Jbig2Status End(Jbig2DecodeState state, Jbig2Segment segment)
    {
        if (segment.Type == Jbig2SegmentType.EndOfPage)
        {
            state.InPage = false;
        }

        return Jbig2Status.EndReached;
    }

    /// <summary>Processes a page information segment: the page is filled with its default pixel.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="reader">The reader.</param>
    /// <returns>The status.</returns>
    internal static Jbig2Status DecodePageInformation(Jbig2DecodeState state, ref Jbig2Reader reader)
    {
        if (!reader.TryReadUInt32(out _) || !reader.TryReadUInt32(out _) || reader.BytesLeft < PageInfoTail)
        {
            return Jbig2Status.Failure;
        }

        var flags = reader.Data[reader.Offset + PageFlagsOffset];
        reader.Skip(PageInfoTail);

        // The page has the PDF image's size, as in PDFium; the page information's own size is not used.
        state.Page?.Fill((flags & DefaultPixelFlag) != 0);
        state.InPage = true;
        state.PageSeen = true;
        return Jbig2Status.Success;
    }

    /// <summary>Finds a segment by number, in the global segments first.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="number">The segment number.</param>
    /// <returns>The segment, or <see langword="null"/>.</returns>
    internal static Jbig2Segment? Find(Jbig2DecodeState state, uint number)
    {
        if ((state.Globals is null ? null : Find(state.Globals, number)) is { } global)
        {
            return global;
        }

        foreach (var segment in state.Segments)
        {
            if (segment.Number == number)
            {
                return segment;
            }
        }

        return null;
    }

    /// <summary>Determines whether every segment a segment refers to has been processed.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="segment">The segment.</param>
    /// <returns><see langword="true"/> when all are found.</returns>
    internal static bool AllReferredFound(Jbig2DecodeState state, Jbig2Segment segment)
    {
        foreach (var number in segment.Referred)
        {
            if (Find(state, number) is null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets the nth Huffman table segment a segment refers to.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="index">The table's position among the referred table segments.</param>
    /// <returns>The table, or <see langword="null"/>.</returns>
    internal static Jbig2HuffmanTable? FindReferredTable(Jbig2DecodeState state, Jbig2Segment segment, int index)
    {
        var count = 0;
        foreach (var number in segment.Referred)
        {
            if (Find(state, number) is not { Type: Jbig2SegmentType.Tables } table)
            {
                continue;
            }

            if (count == index)
            {
                return table.Table;
            }

            count++;
        }

        return null;
    }

    /// <summary>Composites a region onto the page, or keeps it when the segment is intermediate.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="info">The region information.</param>
    /// <param name="region">The region, which this method now owns.</param>
    /// <param name="intermediate">Whether the segment type is the intermediate one.</param>
    /// <param name="page">The page.</param>
    /// <returns><see cref="Jbig2Status.Success"/>, or <see cref="Jbig2Status.Failure"/> when the budget is spent.</returns>
    internal static Jbig2Status Finish(Jbig2DecodeState state, Jbig2Segment segment, in Jbig2RegionInfo info, Jbig2Bitmap region, bool intermediate, Jbig2Bitmap page)
    {
        if (intermediate)
        {
            segment.Bitmap = region;
            return Jbig2Status.Success;
        }

        var composed = Jbig2Composer.Compose(page, region.View, info.X, info.Y, info.Operator);
        region.Dispose();
        return state.Workspace.TryCharge(composed) ? Jbig2Status.Success : Jbig2Status.Failure;
    }
}
