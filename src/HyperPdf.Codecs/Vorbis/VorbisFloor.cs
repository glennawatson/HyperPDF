// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Vorbis;

/// <summary>
/// A type 1 floor: a piecewise linear curve over the spectrum, described by a few points whose heights are coded relative to a
/// prediction from their neighbours. The curve is the loudness envelope the residue is multiplied by.
/// </summary>
[DebuggerDisplay("VorbisFloor: {XList.Length} points")]
internal sealed class VorbisFloor
{
    /// <summary>The bits of the partition count.</summary>
    private const int PartitionBits = 5;

    /// <summary>The bits of a partition's class.</summary>
    private const int ClassBits = 4;

    /// <summary>The bits of a class's dimension count.</summary>
    private const int DimensionBits = 3;

    /// <summary>The bits of a class's subclass count.</summary>
    private const int SubclassBits = 2;

    /// <summary>The bits of a codebook number.</summary>
    private const int BookBits = 8;

    /// <summary>The bits of the multiplier.</summary>
    private const int MultiplierBits = 2;

    /// <summary>The bits of the number of bits of a point's position.</summary>
    private const int RangeBitCountBits = 4;

    /// <summary>The points that come before the points of the partitions: the two ends.</summary>
    private const int FirstValues = 2;

    /// <summary>Gets the class of each partition.</summary>
    internal required int[] PartitionClasses { get; init; }

    /// <summary>Gets the number of points in each class.</summary>
    internal required int[] ClassDimensions { get; init; }

    /// <summary>Gets the bits that choose a subclass in each class.</summary>
    internal required int[] ClassSubclasses { get; init; }

    /// <summary>Gets the codebook that codes the subclass of each class.</summary>
    internal required int[] ClassMasterbooks { get; init; }

    /// <summary>Gets the codebook of each subclass of each class, or -1.</summary>
    internal required int[][] SubclassBooks { get; init; }

    /// <summary>Gets the factor heights are multiplied by.</summary>
    internal required int Multiplier { get; init; }

    /// <summary>Gets the positions of the points, in the order they are coded.</summary>
    internal required int[] XList { get; init; }

    /// <summary>Gets the indexes of the points in order of position.</summary>
    internal required int[] SortedOrder { get; init; }

    /// <summary>Gets the point to the left that the prediction starts from, for each point.</summary>
    internal required int[] LowNeighbors { get; init; }

    /// <summary>Gets the point to the right that the prediction ends at, for each point.</summary>
    internal required int[] HighNeighbors { get; init; }

    /// <summary>Gets the range heights can take.</summary>
    internal int Range => Ranges[Multiplier - 1];

    /// <summary>Gets the ranges the heights can take, for each multiplier.</summary>
    private static ReadOnlySpan<int> Ranges => [0x100, 0x80, 0x54, 0x40];

    /// <summary>Reads a floor from the setup header.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="bookCount">The number of codebooks, which codebook numbers must be below.</param>
    /// <returns>The floor.</returns>
    /// <exception cref="InvalidDataException">The floor is damaged.</exception>
    internal static VorbisFloor Read(ref LsbBitReader reader, int bookCount)
    {
        var partitions = (int)reader.Read(PartitionBits);
        var classes = new int[partitions];
        var highest = -1;
        for (var partition = 0; partition < partitions; partition++)
        {
            classes[partition] = (int)reader.Read(ClassBits);
            highest = Math.Max(highest, classes[partition]);
        }

        var dimensions = new int[highest + 1];
        var subclasses = new int[highest + 1];
        var masterbooks = new int[highest + 1];
        var books = new int[highest + 1][];
        for (var index = 0; index <= highest; index++)
        {
            dimensions[index] = (int)reader.Read(DimensionBits) + 1;
            subclasses[index] = (int)reader.Read(SubclassBits);
            masterbooks[index] = subclasses[index] == 0 ? -1 : (int)reader.Read(BookBits);
            books[index] = new int[1 << subclasses[index]];
            for (var sub = 0; sub < books[index].Length; sub++)
            {
                books[index][sub] = (int)reader.Read(BookBits) - 1;
            }
        }

        var multiplier = (int)reader.Read(MultiplierBits) + 1;
        var rangeBits = (int)reader.Read(RangeBitCountBits);
        var positions = ReadPositions(ref reader, classes, dimensions, rangeBits);
        Validate(books, masterbooks, bookCount);
        var floor = new VorbisFloor
        {
            PartitionClasses = classes,
            ClassDimensions = dimensions,
            ClassSubclasses = subclasses,
            ClassMasterbooks = masterbooks,
            SubclassBooks = books,
            Multiplier = multiplier,
            XList = positions,
            SortedOrder = Sort(positions),
            LowNeighbors = new int[positions.Length],
            HighNeighbors = new int[positions.Length],
        };
        floor.FindNeighbors();
        return floor;
    }

    /// <summary>Reads the heights of a floor from a packet and turns them into the curve.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="books">The codebooks of the stream.</param>
    /// <param name="count">The number of spectrum lines the curve covers.</param>
    /// <param name="curve">Receives the curve as heights from 0 to 255, one for each spectrum line.</param>
    /// <param name="scratch">Working room for the heights of the points.</param>
    /// <returns><see langword="false"/> when the channel has no floor in this packet.</returns>
    internal bool Decode(ref LsbBitReader reader, VorbisCodebook[] books, int count, Span<int> curve, VorbisFloorScratch scratch)
    {
        if (!reader.ReadBit())
        {
            return false;
        }

        var range = Range;
        var heights = scratch.Heights;
        heights[0] = (int)reader.Read(VorbisCodebook.Ilog(range - 1));
        heights[1] = (int)reader.Read(VorbisCodebook.Ilog(range - 1));
        if (!ReadPartitions(ref reader, books, heights))
        {
            return false;
        }

        ResolveHeights(range, heights, scratch);
        DrawCurve(scratch, curve[..count]);
        return true;
    }

    /// <summary>Reads the position of every point after the first two.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="classes">The class of each partition.</param>
    /// <param name="dimensions">The points in each class.</param>
    /// <param name="rangeBits">The bits of a position.</param>
    /// <returns>The positions, starting with 0 and the end of the range.</returns>
    private static int[] ReadPositions(ref LsbBitReader reader, int[] classes, int[] dimensions, int rangeBits)
    {
        var positions = new List<int> { 0, 1 << rangeBits };
        foreach (var partitionClass in classes)
        {
            for (var point = 0; point < dimensions[partitionClass]; point++)
            {
                positions.Add((int)reader.Read(rangeBits));
            }
        }

        return [.. positions];
    }

    /// <summary>Checks that every codebook a floor names exists.</summary>
    /// <param name="books">The codebook of each subclass of each class.</param>
    /// <param name="masterbooks">The codebook that codes the subclass of each class.</param>
    /// <param name="bookCount">The number of codebooks.</param>
    /// <exception cref="InvalidDataException">A codebook number is too high.</exception>
    private static void Validate(int[][] books, int[] masterbooks, int bookCount)
    {
        var bad = Array.Exists(masterbooks, book => book >= bookCount) || Array.Exists(books, list => Array.Exists(list, book => book >= bookCount));
        if (bad)
        {
            throw new InvalidDataException("A Vorbis floor names a codebook that does not exist.");
        }
    }

    /// <summary>Sorts the points by position.</summary>
    /// <param name="positions">The positions.</param>
    /// <returns>The indexes of the points in order of position.</returns>
    private static int[] Sort(int[] positions)
    {
        var order = new int[positions.Length];
        for (var index = 0; index < order.Length; index++)
        {
            order[index] = index;
        }

        Array.Sort(order, (left, right) => positions[left].CompareTo(positions[right]));
        return order;
    }

    /// <summary>Finds the neighbours each point's height is predicted from.</summary>
    private void FindNeighbors()
    {
        for (var point = FirstValues; point < XList.Length; point++)
        {
            var low = 0;
            var high = 1;
            for (var other = 0; other < point; other++)
            {
                if (XList[other] < XList[point] && XList[other] > XList[low])
                {
                    low = other;
                }

                if (XList[other] > XList[point] && XList[other] < XList[high])
                {
                    high = other;
                }
            }

            LowNeighbors[point] = low;
            HighNeighbors[point] = high;
        }
    }

    /// <summary>Reads the partitions' heights.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="books">The codebooks of the stream.</param>
    /// <param name="heights">Receives the heights of the points.</param>
    /// <returns><see langword="false"/> when the packet runs out or names an entry the book does not have.</returns>
    private bool ReadPartitions(ref LsbBitReader reader, VorbisCodebook[] books, Span<int> heights)
    {
        var offset = FirstValues;
        foreach (var partitionClass in PartitionClasses)
        {
            var dimension = ClassDimensions[partitionClass];
            var subclassBits = ClassSubclasses[partitionClass];
            var selector = subclassBits == 0 ? 0 : books[ClassMasterbooks[partitionClass]].Decode(ref reader);
            if (selector < 0)
            {
                return false;
            }

            for (var point = 0; point < dimension; point++)
            {
                var book = SubclassBooks[partitionClass][selector & ((1 << subclassBits) - 1)];
                selector >>= subclassBits;
                heights[offset + point] = book < 0 ? 0 : books[book].Decode(ref reader);
                if (heights[offset + point] < 0)
                {
                    return false;
                }
            }

            offset += dimension;
        }

        return !reader.IsOverrun;
    }

    /// <summary>Turns the coded heights into final heights by adding each to the prediction from its neighbours.</summary>
    /// <param name="range">The range of heights.</param>
    /// <param name="heights">The coded heights.</param>
    /// <param name="scratch">Receives the final heights and which points are drawn.</param>
    private void ResolveHeights(int range, Span<int> heights, VorbisFloorScratch scratch)
    {
        var final = scratch.Final;
        var used = scratch.Used;
        final[0] = heights[0];
        final[1] = heights[1];
        used[0] = true;
        used[1] = true;
        for (var point = FirstValues; point < XList.Length; point++)
        {
            var low = LowNeighbors[point];
            var high = HighNeighbors[point];
            var predicted = VorbisFloorMath.Predict(XList[low], final[low], XList[high], final[high], XList[point]);
            var value = heights[point];
            var highRoom = range - predicted;
            var lowRoom = predicted;
            var room = (highRoom < lowRoom ? highRoom : lowRoom) << 1;
            if (value == 0)
            {
                used[point] = false;
                final[point] = predicted;
                continue;
            }

            used[low] = true;
            used[high] = true;
            used[point] = true;
            final[point] = value >= room ? VorbisFloorMath.OutsideRoom(value, predicted, highRoom, lowRoom) : VorbisFloorMath.InsideRoom(value, predicted);
        }
    }

    /// <summary>Draws the curve through the used points, in order of position.</summary>
    /// <param name="scratch">The final heights and which points are used.</param>
    /// <param name="curve">Receives the heights, one for each line.</param>
    private void DrawCurve(VorbisFloorScratch scratch, Span<int> curve)
    {
        var lastX = 0;
        var lastY = scratch.Final[SortedOrder[0]] * Multiplier;
        var nextX = 0;
        var nextY = lastY;
        for (var sorted = 1; sorted < SortedOrder.Length; sorted++)
        {
            var point = SortedOrder[sorted];
            if (!scratch.Used[point])
            {
                continue;
            }

            nextY = scratch.Final[point] * Multiplier;
            nextX = XList[point];
            VorbisFloorMath.DrawLine(lastX, lastY, nextX, nextY, curve);
            lastX = nextX;
            lastY = nextY;
        }

        for (var line = nextX; line < curve.Length; line++)
        {
            curve[line] = nextY;
        }
    }
}
