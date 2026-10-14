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

    /// <summary>What became of a segment that was processed.</summary>
    private enum Outcome
    {
        /// <summary>The segment decoded and is kept for later segments to refer to.</summary>
        Kept = 0,

        /// <summary>The segment failed, was dropped, and processing goes on with the next one.</summary>
        Skipped = 1,

        /// <summary>The segment failed and the next segment cannot be found.</summary>
        Stopped = 2,
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

        var reader = new Jbig2Reader(data, state.Workspace.ScratchPools, state.CancellationToken);
        var damaged = false;
        while (reader.BytesLeft >= MinSegmentSize)
        {
            state.CancellationToken.ThrowIfCancellationRequested();
            if (Jbig2Segment.Parse(ref reader) is not { } segment)
            {
                return Jbig2Status.Failure;
            }

            var status = DecodeSegment(state, ref reader, segment);
            if (status == Jbig2Status.EndReached)
            {
                segment.Dispose();
                return damaged ? Jbig2Status.Failure : Jbig2Status.Success;
            }

            var outcome = Settle(state, ref reader, segment, status);
            if (outcome == Outcome.Stopped)
            {
                return Jbig2Status.Failure;
            }

            damaged |= outcome == Outcome.Skipped;
        }

        return damaged ? Jbig2Status.Failure : Jbig2Status.Success;
    }

    /// <summary>Moves the reader to the next segment header.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="segment">The segment just processed, whose data length is known.</param>
    /// <returns><see langword="false"/> when the data length overflows.</returns>
    internal static bool MoveToNextSegment(ref Jbig2Reader reader, Jbig2Segment segment)
    {
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
            return state.Page is null ? Jbig2Status.Failure : DecodePageRegion(state, ref reader, segment, kind, state.Page);
        }

        return kind switch
        {
            Jbig2Segments.SegmentKind.SymbolDictionary => Jbig2Dictionaries.DecodeSymbolDictionary(state, ref reader, segment),
            Jbig2Segments.SegmentKind.PatternDictionary => Jbig2Dictionaries.DecodePatternDictionary(state, ref reader, segment),
            Jbig2Segments.SegmentKind.Table => Jbig2Dictionaries.DecodeTable(ref reader, segment),
            Jbig2Segments.SegmentKind.PageInformation => DecodePageInformation(state, ref reader),
            Jbig2Segments.SegmentKind.End => Jbig2Status.EndReached,
            _ => Skip(ref reader, segment),
        };
    }

    /// <summary>
    /// Processes a region segment on the page. A stream without a page information segment is read as a white page, so
    /// its regions still draw; the page then counts as seen once a region succeeds.
    /// </summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="kind">The region kind.</param>
    /// <param name="page">The page.</param>
    /// <returns>The status.</returns>
    internal static Jbig2Status DecodePageRegion(Jbig2DecodeState state, ref Jbig2Reader reader, Jbig2Segment segment, SegmentKind kind, Jbig2Bitmap page)
    {
        var status = DecodeRegion(state, ref reader, segment, kind, page);
        state.PageSeen |= status == Jbig2Status.Success;
        return status;
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

        // The page has the PDF image's size (ISO 32000-1 section 7.4.7 takes it from the image dictionary); the page information's own size is not used.
        state.Page?.Fill((flags & DefaultPixelFlag) != 0);
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

    /// <summary>
    /// Keeps a decoded segment and moves to the next header. A segment of known length that fails is skipped, so the
    /// regions after it still draw (lenient reading). A segment of unknown length leaves the reader after its own end
    /// marker and row count when it succeeds; when it fails there is no way to find the next segment.
    /// </summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="reader">The reader.</param>
    /// <param name="segment">The segment just processed.</param>
    /// <param name="status">What processing it returned.</param>
    /// <returns>What happened to the segment.</returns>
    private static Outcome Settle(Jbig2DecodeState state, ref Jbig2Reader reader, Jbig2Segment segment, Jbig2Status status)
    {
        var canContinue = segment.DataLength == Jbig2Segment.UnknownLength ? status == Jbig2Status.Success : MoveToNextSegment(ref reader, segment);
        if (status == Jbig2Status.Success && canContinue)
        {
            state.Segments.Add(segment);
            return Outcome.Kept;
        }

        segment.Dispose();
        return canContinue ? Outcome.Skipped : Outcome.Stopped;
    }
}
