// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdf.Codecs.Mp4;

/// <summary>Expands the sample table boxes of one track into a list of samples with their places in the file and their times.</summary>
internal static class Mp4SampleTables
{
    /// <summary>The bytes of the entry count that follows the version and flags of a table box.</summary>
    private const int CountSize = 4;

    /// <summary>The bytes of an entry of the time to sample box and of the composition offset box.</summary>
    private const int PairSize = 8;

    /// <summary>The most samples one track may have, which keeps a damaged table from using all the memory.</summary>
    private const int MaxSamples = 50_000_000;

    /// <summary>Adds the samples of a track.</summary>
    /// <param name="data">The bytes of the movie box.</param>
    /// <param name="table">The sample table box.</param>
    /// <param name="track">The track's number.</param>
    /// <param name="timing">The track's time scale and shift.</param>
    /// <param name="samples">Receives the samples.</param>
    /// <returns><see langword="true"/> when the table was read.</returns>
    internal static bool Add(ReadOnlySpan<byte> data, Mp4Box table, int track, Mp4Timing timing, Mp4SampleList samples)
    {
        if (!FindBoxes(data, table, out var boxes))
        {
            return false;
        }

        var sizeBox = boxes.Sizes;
        var sizeHeader = sizeBox.Start + Mp4Boxes.FullBoxSize;
        if (sizeBox.End - sizeHeader < PairSize)
        {
            return false;
        }

        var fixedSize = BinaryPrimitives.ReadInt32BigEndian(data[sizeHeader..]);
        var count = BinaryPrimitives.ReadInt32BigEndian(data[(sizeHeader + CountSize)..]);
        var sizesAt = sizeHeader + PairSize;
        if (count is < 0 or > MaxSamples || (fixedSize == 0 && sizeBox.End - sizesAt < (long)count * CountSize))
        {
            return false;
        }

        var cursor = new Mp4SampleCursor(data, boxes.Runs, boxes.Offsets, boxes.IsWide, boxes.Times);
        var composition = FindComposition(data, table);
        var keys = FindSyncSamples(data, table);
        var keyIndex = 0;
        var decode = 0L;
        for (var sample = 0; sample < count; sample++)
        {
            var size = fixedSize != 0 ? fixedSize : BinaryPrimitives.ReadInt32BigEndian(data[(sizesAt + (sample * CountSize))..]);
            if (!cursor.Next(size, out var offset, out var delta))
            {
                break;
            }

            var isKey = IsKey(data, keys, sample + 1, ref keyIndex);
            var offsetTicks = composition.Next();
            var seconds = timing.Seconds(decode);
            samples.Add(offset, size, track, isKey, timing.Seconds(decode + offsetTicks), seconds, timing.Length(delta));
            decode += delta;
        }

        return true;
    }

    /// <summary>Finds the boxes a track's samples are read from.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="table">The sample table box.</param>
    /// <param name="boxes">Receives the boxes.</param>
    /// <returns><see langword="true"/> when the table has all of them.</returns>
    private static bool FindBoxes(ReadOnlySpan<byte> data, Mp4Box table, out Mp4TableBoxes boxes)
    {
        boxes = default;
        if (!Mp4Boxes.TryFind(data, table.Start, table.End, Mp4Boxes.Stsz, out var sizes)
            || !Mp4Boxes.TryFind(data, table.Start, table.End, Mp4Boxes.Stsc, out var runs)
            || !FindOffsets(data, table, out var offsets, out var wide)
            || !Mp4Boxes.TryFind(data, table.Start, table.End, Mp4Boxes.Stts, out var times))
        {
            return false;
        }

        boxes = new(sizes, runs, offsets, wide, times);
        return true;
    }

    /// <summary>Finds the chunk offset box, which is the 32 bit or the 64 bit kind.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="table">The sample table box.</param>
    /// <param name="box">Receives the box.</param>
    /// <param name="wide">Receives whether the offsets are 64 bits.</param>
    /// <returns><see langword="true"/> when found.</returns>
    private static bool FindOffsets(ReadOnlySpan<byte> data, Mp4Box table, out Mp4Box box, out bool wide)
    {
        wide = false;
        if (Mp4Boxes.TryFind(data, table.Start, table.End, Mp4Boxes.Stco, out box))
        {
            return true;
        }

        wide = true;
        return Mp4Boxes.TryFind(data, table.Start, table.End, Mp4Boxes.Co64, out box);
    }

    /// <summary>Finds the composition offset box and makes a reader of it.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="table">The sample table box.</param>
    /// <returns>The reader, which gives 0 for every sample when the box is missing.</returns>
    private static Mp4Compositions FindComposition(ReadOnlySpan<byte> data, Mp4Box table) =>
        Mp4Boxes.TryFind(data, table.Start, table.End, Mp4Boxes.Ctts, out var box) ? new(data, box) : default;

    /// <summary>Finds the sync sample box.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="table">The sample table box.</param>
    /// <returns>The box, or an empty box when the track has none, which means every sample is a key.</returns>
    private static Mp4Box FindSyncSamples(ReadOnlySpan<byte> data, Mp4Box table) =>
        Mp4Boxes.TryFind(data, table.Start, table.End, Mp4Boxes.Stss, out var box) ? box : default;

    /// <summary>Tells whether a sample is a key sample.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="keys">The sync sample box, or an empty box.</param>
    /// <param name="number">The sample's number, from 1.</param>
    /// <param name="index">The index of the next sync entry to look at, which only moves forward.</param>
    /// <returns><see langword="true"/> for a key sample.</returns>
    private static bool IsKey(ReadOnlySpan<byte> data, Mp4Box keys, int number, ref int index)
    {
        if (keys.End == 0)
        {
            return true;
        }

        var entries = (keys.End - keys.Start - Mp4Boxes.FullBoxSize - CountSize) / CountSize;
        var first = keys.Start + Mp4Boxes.FullBoxSize + CountSize;
        while (index < entries && BinaryPrimitives.ReadInt32BigEndian(data[(first + (index * CountSize))..]) < number)
        {
            index++;
        }

        return index < entries && BinaryPrimitives.ReadInt32BigEndian(data[(first + (index * CountSize))..]) == number;
    }
}
