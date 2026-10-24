// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdf.Codecs.Matroska;

/// <summary>Splits a block into its frames, working out where each frame's bytes are and when it plays.</summary>
internal static class MatroskaBlocks
{
    /// <summary>The bits of the flags byte that say how the block is laced.</summary>
    private const int LacingMask = 0x06;

    /// <summary>The shift of the lacing bits.</summary>
    private const int LacingShift = 1;

    /// <summary>The lacing of Xiph, with lengths written as runs of 255.</summary>
    private const int XiphLacing = 1;

    /// <summary>The lacing of equal sized frames.</summary>
    private const int FixedLacing = 2;

    /// <summary>The lacing of EBML, with each length after the first written as a change.</summary>
    private const int EbmlLacing = 3;

    /// <summary>The flag of a block that says it is a key block.</summary>
    private const int KeyFlag = 0x80;

    /// <summary>The byte value that says a length in Xiph lacing continues in the next byte.</summary>
    private const int XiphContinues = 255;

    /// <summary>The bits of each byte of a variable length number that carry its value.</summary>
    private const int LengthValueBits = 7;

    /// <summary>Splits a block into frames.</summary>
    /// <param name="head">The first bytes of the block, enough to hold its header and the lengths of laced frames.</param>
    /// <param name="offset">The file offset of the block's first byte.</param>
    /// <param name="length">The length of the block in bytes.</param>
    /// <param name="timing">What the surroundings say about the block.</param>
    /// <param name="tracks">The container's tracks.</param>
    /// <param name="frames">Receives the frames.</param>
    /// <param name="isSimple">Whether the block is a simple block, whose own flags say whether it is a key block.</param>
    /// <returns><see langword="false"/> when the block is damaged.</returns>
    internal static bool Split(ReadOnlySpan<byte> head, long offset, long length, in MatroskaBlockTiming timing, List<MatroskaTrack> tracks, List<MatroskaFrame> frames, bool isSimple)
    {
        var number = Ebml.ReadNumber(head, out var numberLength);
        if (number < 0 || head.Length < numberLength + sizeof(short) + 1)
        {
            return false;
        }

        var track = IndexOf(tracks, (int)number);
        var relative = BinaryPrimitives.ReadInt16BigEndian(head[numberLength..]);
        var flags = head[numberLength + sizeof(short)];
        var headerLength = numberLength + sizeof(short) + 1;
        var start = timing.ClusterSeconds + (relative * timing.Scale);
        var isKey = isSimple ? (flags & KeyFlag) != 0 : timing.IsKey;
        var lacing = (flags & LacingMask) >> LacingShift;
        if (track < 0)
        {
            return true;
        }

        if (lacing == 0)
        {
            var single = new MatroskaFrame(offset + headerLength, (int)(length - headerLength), track, start, timing.Duration, isKey);
            frames.Add(single);
            return true;
        }

        var shape = new MatroskaLaceShape(track, start, isKey, tracks[track].FrameSeconds, timing.Duration);
        return SplitLaced(head[headerLength..], offset + headerLength, length - headerLength, lacing, in shape, frames);
    }

    /// <summary>Finds a track by the number blocks name it by.</summary>
    /// <param name="tracks">The tracks.</param>
    /// <param name="number">The number.</param>
    /// <returns>The track's index, or -1 when the file has no such track in its list.</returns>
    private static int IndexOf(List<MatroskaTrack> tracks, int number)
    {
        for (var index = 0; index < tracks.Count; index++)
        {
            if (tracks[index].Number == number)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Splits the frames of a laced block.</summary>
    /// <param name="data">The bytes after the block header, from the frame count on.</param>
    /// <param name="offset">The file offset of those bytes.</param>
    /// <param name="length">The number of those bytes in the file.</param>
    /// <param name="lacing">The kind of lacing.</param>
    /// <param name="shape">The track, start, key flag and frame lengths of the block.</param>
    /// <param name="frames">Receives the frames.</param>
    /// <returns><see langword="false"/> when the lacing is damaged.</returns>
    private static bool SplitLaced(ReadOnlySpan<byte> data, long offset, long length, int lacing, in MatroskaLaceShape shape, List<MatroskaFrame> frames)
    {
        if (data.IsEmpty)
        {
            return false;
        }

        var count = data[0] + 1;
        var sizes = new int[count];
        var used = ReadSizes(data, lacing, length, sizes);
        if (used < 0)
        {
            return false;
        }

        var duration = shape.FrameSeconds > 0 ? shape.FrameSeconds : shape.BlockDuration / count;
        var position = offset + used;
        for (var frame = 0; frame < count; frame++)
        {
            frames.Add(new(position, sizes[frame], shape.Track, shape.Start + (frame * duration), duration, shape.IsKey));
            position += sizes[frame];
        }

        return true;
    }

    /// <summary>Reads the lengths of the frames of a laced block, working out the last from the length of the block.</summary>
    /// <param name="data">The bytes after the block header, from the frame count on.</param>
    /// <param name="lacing">The kind of lacing.</param>
    /// <param name="length">The number of bytes after the block header in the file.</param>
    /// <param name="sizes">Receives the lengths.</param>
    /// <returns>The bytes the frame count and lengths take, or -1 when they are damaged.</returns>
    private static int ReadSizes(ReadOnlySpan<byte> data, int lacing, long length, Span<int> sizes)
    {
        var position = 1;
        var count = sizes.Length;
        var ok = lacing switch
        {
            XiphLacing => ReadXiph(data, ref position, sizes[..(count - 1)]),
            EbmlLacing => ReadEbml(data, ref position, sizes[..(count - 1)]),
            FixedLacing => true,
            _ => false,
        };
        if (!ok)
        {
            return -1;
        }

        var payload = length - position;
        if (lacing == FixedLacing)
        {
            sizes.Fill((int)(payload / count));
            return position;
        }

        var known = 0L;
        foreach (var size in sizes[..(count - 1)])
        {
            known += size;
        }

        sizes[count - 1] = (int)(payload - known);
        return sizes[count - 1] < 0 ? -1 : position;
    }

    /// <summary>Reads Xiph laced lengths.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="position">The position of the first length, which moves past the lengths.</param>
    /// <param name="sizes">Receives the lengths.</param>
    /// <returns><see langword="false"/> when the bytes run out.</returns>
    private static bool ReadXiph(ReadOnlySpan<byte> data, ref int position, Span<int> sizes)
    {
        for (var index = 0; index < sizes.Length; index++)
        {
            var size = 0;
            byte next;
            do
            {
                if (position >= data.Length)
                {
                    return false;
                }

                next = data[position];
                position++;
                size += next;
            }
            while (next == XiphContinues);

            sizes[index] = size;
        }

        return true;
    }

    /// <summary>Reads EBML laced lengths: the first as a number, the rest as signed changes from the one before.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="position">The position of the first length, which moves past the lengths.</param>
    /// <param name="sizes">Receives the lengths.</param>
    /// <returns><see langword="false"/> when the bytes run out or a length is negative.</returns>
    private static bool ReadEbml(ReadOnlySpan<byte> data, ref int position, Span<int> sizes)
    {
        var previous = 0L;
        for (var index = 0; index < sizes.Length; index++)
        {
            var value = Ebml.ReadNumber(data[position..], out var used);
            if (value < 0)
            {
                return false;
            }

            position += used;
            previous = index == 0 ? value : previous + (value - ((1L << ((used * LengthValueBits) - 1)) - 1));
            if (previous is < 0 or > int.MaxValue)
            {
                return false;
            }

            sizes[index] = (int)previous;
        }

        return true;
    }
}
