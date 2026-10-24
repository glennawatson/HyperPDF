// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Vorbis;

/// <summary>
/// A residue: the fine detail of the spectrum, coded in partitions that are each given a class, and each class a codebook for each of
/// up to eight passes. Each pass adds to the values the earlier passes left, so detail is refined a pass at a time.
/// </summary>
[DebuggerDisplay("VorbisResidue: type {Type}, {Begin} to {End}")]
internal sealed class VorbisResidue
{
    /// <summary>The residue type that codes all the channels of a submap as one interleaved vector.</summary>
    internal const int InterleavedAcrossChannels = 2;

    /// <summary>The bits of a position in the spectrum.</summary>
    private const int PositionBits = 24;

    /// <summary>The bits of the number of classifications.</summary>
    private const int ClassificationBits = 6;

    /// <summary>The bits of a codebook number.</summary>
    private const int BookBits = 8;

    /// <summary>The passes a residue has.</summary>
    private const int Passes = 8;

    /// <summary>The bits of the low part of the cascade.</summary>
    private const int LowBits = 3;

    /// <summary>The bits of the high part of the cascade.</summary>
    private const int HighBits = 5;

    /// <summary>The residue type that codes each channel's values in turn, interleaved across the book's dimensions.</summary>
    private const int InterleavedType = 0;

    /// <summary>Gets the type: 0, 1 or 2.</summary>
    internal required int Type { get; init; }

    /// <summary>Gets the first spectrum line the residue covers.</summary>
    internal required int Begin { get; init; }

    /// <summary>Gets the line after the last the residue covers.</summary>
    internal required int End { get; init; }

    /// <summary>Gets the lines in a partition.</summary>
    internal required int PartitionSize { get; init; }

    /// <summary>Gets the number of classifications.</summary>
    internal required int Classifications { get; init; }

    /// <summary>Gets the codebook that codes the classifications.</summary>
    internal required int ClassBook { get; init; }

    /// <summary>Gets the codebook of each pass of each classification, or -1.</summary>
    internal required int[][] Books { get; init; }

    /// <summary>Reads a residue from the setup header.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="type">The residue type, already read.</param>
    /// <param name="bookCount">The number of codebooks.</param>
    /// <returns>The residue.</returns>
    /// <exception cref="InvalidDataException">The residue is damaged.</exception>
    internal static VorbisResidue Read(ref LsbBitReader reader, int type, int bookCount)
    {
        var begin = (int)reader.Read(PositionBits);
        var end = (int)reader.Read(PositionBits);
        var partitionSize = (int)reader.Read(PositionBits) + 1;
        var classifications = (int)reader.Read(ClassificationBits) + 1;
        var classBook = (int)reader.Read(BookBits);
        var cascades = new int[classifications];
        for (var index = 0; index < classifications; index++)
        {
            var low = (int)reader.Read(LowBits);
            var high = reader.ReadBit() ? (int)reader.Read(HighBits) : 0;
            cascades[index] = (high << LowBits) | low;
        }

        var books = new int[classifications][];
        for (var index = 0; index < classifications; index++)
        {
            books[index] = new int[Passes];
            for (var pass = 0; pass < Passes; pass++)
            {
                books[index][pass] = (cascades[index] & (1 << pass)) != 0 ? (int)reader.Read(BookBits) : -1;
            }
        }

        if (classBook >= bookCount || Array.Exists(books, list => Array.Exists(list, book => book >= bookCount)))
        {
            throw new InvalidDataException("A Vorbis residue names a codebook that does not exist.");
        }

        return new() { Type = type, Begin = begin, End = end, PartitionSize = partitionSize, Classifications = classifications, ClassBook = classBook, Books = books };
    }

    /// <summary>Reads the residue of some channels and adds it to their vectors.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="books">The codebooks of the stream.</param>
    /// <param name="vectors">The vectors to add to, one for each channel; all of the same length.</param>
    /// <param name="skip">Which channels carry no residue in this packet.</param>
    /// <param name="size">The number of values in each vector that the residue covers.</param>
    /// <param name="classes">Working room for the class of each partition of each channel.</param>
    internal void Decode(ref LsbBitReader reader, VorbisCodebook[] books, scoped Span<float[]> vectors, scoped ReadOnlySpan<bool> skip, int size, int[] classes)
    {
        var begin = Math.Min(Begin, size);
        var partitions = (Math.Min(End, size) - begin) / PartitionSize;
        for (var pass = 0; pass < Passes && partitions > 0; pass++)
        {
            if (!RunPass(ref reader, books, vectors, skip, classes, new(0, pass, begin, partitions)))
            {
                return;
            }
        }
    }

    /// <summary>Reads one pass over all the partitions.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="books">The codebooks of the stream.</param>
    /// <param name="vectors">The vectors to add to.</param>
    /// <param name="skip">Which channels carry no residue.</param>
    /// <param name="classes">The classes of the partitions.</param>
    /// <param name="start">Which pass, and where the residue starts; the partition is ignored.</param>
    /// <returns><see langword="false"/> when the packet runs out.</returns>
    private bool RunPass(ref LsbBitReader reader, VorbisCodebook[] books, scoped Span<float[]> vectors, scoped ReadOnlySpan<bool> skip, int[] classes, PartitionAt start)
    {
        var classBook = books[ClassBook];
        var partition = 0;
        while (partition < start.Partitions)
        {
            if (start.Pass == 0 && !ReadClasses(ref reader, classBook, vectors.Length, skip, classes, partition, start.Partitions))
            {
                return false;
            }

            for (var inGroup = 0; inGroup < classBook.Dimensions && partition < start.Partitions; inGroup++)
            {
                if (!DecodePartition(ref reader, books, vectors, skip, classes, start with { Partition = partition }))
                {
                    return false;
                }

                partition++;
            }
        }

        return true;
    }

    /// <summary>Reads the classes of the next group of partitions for every channel.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="classBook">The codebook that codes classes.</param>
    /// <param name="channels">The number of channels.</param>
    /// <param name="skip">Which channels carry no residue.</param>
    /// <param name="classes">Receives the classes.</param>
    /// <param name="partition">The first partition of the group.</param>
    /// <param name="partitions">The number of partitions.</param>
    /// <returns><see langword="false"/> when the packet runs out.</returns>
    private bool ReadClasses(ref LsbBitReader reader, VorbisCodebook classBook, int channels, scoped ReadOnlySpan<bool> skip, int[] classes, int partition, int partitions)
    {
        var perCodeword = classBook.Dimensions;
        for (var channel = 0; channel < channels; channel++)
        {
            if (skip[channel])
            {
                continue;
            }

            var code = classBook.Decode(ref reader);
            if (code < 0)
            {
                return false;
            }

            for (var index = perCodeword - 1; index >= 0; index--)
            {
                var at = (channel * partitions) + partition + index;
                if (partition + index < partitions)
                {
                    classes[at] = code % Classifications;
                }

                code /= Classifications;
            }
        }

        return true;
    }

    /// <summary>Reads one partition of one pass for every channel.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="books">The codebooks of the stream.</param>
    /// <param name="vectors">The vectors to add to.</param>
    /// <param name="skip">Which channels carry no residue.</param>
    /// <param name="classes">The classes of the partitions.</param>
    /// <param name="at">Which partition and pass.</param>
    /// <returns><see langword="false"/> when the packet runs out.</returns>
    private bool DecodePartition(ref LsbBitReader reader, VorbisCodebook[] books, scoped Span<float[]> vectors, scoped ReadOnlySpan<bool> skip, int[] classes, PartitionAt at)
    {
        for (var channel = 0; channel < vectors.Length; channel++)
        {
            if (skip[channel])
            {
                continue;
            }

            var book = Books[classes[(channel * at.Partitions) + at.Partition]][at.Pass];
            if (book < 0)
            {
                continue;
            }

            var target = vectors[channel].AsSpan(at.Begin + (at.Partition * PartitionSize), PartitionSize);
            if (!ReadValues(ref reader, books[book], target))
            {
                return false;
            }
        }

        return !reader.IsOverrun;
    }

    /// <summary>Reads the vectors of one partition from a codebook and adds them to the target.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="book">The codebook.</param>
    /// <param name="target">The values of the partition.</param>
    /// <returns><see langword="false"/> when the packet holds a code the book does not have.</returns>
    private bool ReadValues(ref LsbBitReader reader, VorbisCodebook book, Span<float> target)
    {
        var dimensions = book.Dimensions;
        if (!book.HasVectors || dimensions == 0)
        {
            return false;
        }

        var steps = target.Length / dimensions;
        for (var step = 0; step < steps; step++)
        {
            var entry = book.Decode(ref reader);
            if (entry < 0)
            {
                return false;
            }

            var vector = book.Vector(entry);
            for (var part = 0; part < dimensions; part++)
            {
                var index = Type == InterleavedType ? step + (part * steps) : (step * dimensions) + part;
                target[index] += vector[part];
            }
        }

        return true;
    }

    /// <summary>Which partition of which pass is being read.</summary>
    /// <param name="Partition">The partition.</param>
    /// <param name="Pass">The pass.</param>
    /// <param name="Begin">The first line the residue covers.</param>
    /// <param name="Partitions">The number of partitions.</param>
    private readonly record struct PartitionAt(int Partition, int Pass, int Begin, int Partitions);
}
