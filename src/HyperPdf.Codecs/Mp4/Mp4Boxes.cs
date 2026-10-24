// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.Mp4;

/// <summary>Finds and describes the boxes of an ISO base media file held in memory, and names the box types this reader knows.</summary>
internal static class Mp4Boxes
{
    /// <summary>The bytes of a box header: its size and its type.</summary>
    internal const int HeaderSize = 8;

    /// <summary>The bytes of a box header that has a 64 bit size.</summary>
    internal const int LargeHeaderSize = 16;

    /// <summary>The bytes of the version and flags that start a full box.</summary>
    internal const int FullBoxSize = 4;

    /// <summary>The size value that says the real size follows in 64 bits.</summary>
    private const uint LargeSize = 1;

    /// <summary>Gets the type of the movie box.</summary>
    internal static uint Moov { get; } = Type("moov"u8);

    /// <summary>Gets the type of the movie fragment box.</summary>
    internal static uint Moof { get; } = Type("moof"u8);

    /// <summary>Gets the type of the media data box.</summary>
    internal static uint Mdat { get; } = Type("mdat"u8);

    /// <summary>Gets the type of the movie header box.</summary>
    internal static uint Mvhd { get; } = Type("mvhd"u8);

    /// <summary>Gets the type of the track box.</summary>
    internal static uint Trak { get; } = Type("trak"u8);

    /// <summary>Gets the type of the track header box.</summary>
    internal static uint Tkhd { get; } = Type("tkhd"u8);

    /// <summary>Gets the type of the media box.</summary>
    internal static uint Mdia { get; } = Type("mdia"u8);

    /// <summary>Gets the type of the media header box.</summary>
    internal static uint Mdhd { get; } = Type("mdhd"u8);

    /// <summary>Gets the type of the handler box.</summary>
    internal static uint Hdlr { get; } = Type("hdlr"u8);

    /// <summary>Gets the type of the media information box.</summary>
    internal static uint Minf { get; } = Type("minf"u8);

    /// <summary>Gets the type of the sample table box.</summary>
    internal static uint Stbl { get; } = Type("stbl"u8);

    /// <summary>Gets the type of the sample description box.</summary>
    internal static uint Stsd { get; } = Type("stsd"u8);

    /// <summary>Gets the type of the decoding time to sample box.</summary>
    internal static uint Stts { get; } = Type("stts"u8);

    /// <summary>Gets the type of the composition offset box.</summary>
    internal static uint Ctts { get; } = Type("ctts"u8);

    /// <summary>Gets the type of the sample to chunk box.</summary>
    internal static uint Stsc { get; } = Type("stsc"u8);

    /// <summary>Gets the type of the sample size box.</summary>
    internal static uint Stsz { get; } = Type("stsz"u8);

    /// <summary>Gets the type of the chunk offset box.</summary>
    internal static uint Stco { get; } = Type("stco"u8);

    /// <summary>Gets the type of the 64 bit chunk offset box.</summary>
    internal static uint Co64 { get; } = Type("co64"u8);

    /// <summary>Gets the type of the sync sample box.</summary>
    internal static uint Stss { get; } = Type("stss"u8);

    /// <summary>Gets the type of the edit box.</summary>
    internal static uint Edts { get; } = Type("edts"u8);

    /// <summary>Gets the type of the edit list box.</summary>
    internal static uint Elst { get; } = Type("elst"u8);

    /// <summary>Gets the type of the movie extends box.</summary>
    internal static uint Mvex { get; } = Type("mvex"u8);

    /// <summary>Gets the type of the track extends box.</summary>
    internal static uint Trex { get; } = Type("trex"u8);

    /// <summary>Gets the type of the track fragment box.</summary>
    internal static uint Traf { get; } = Type("traf"u8);

    /// <summary>Gets the type of the track fragment header box.</summary>
    internal static uint Tfhd { get; } = Type("tfhd"u8);

    /// <summary>Gets the type of the track fragment decode time box.</summary>
    internal static uint Tfdt { get; } = Type("tfdt"u8);

    /// <summary>Gets the type of the track run box.</summary>
    internal static uint Trun { get; } = Type("trun"u8);

    /// <summary>Gets the type of the file type box.</summary>
    internal static uint Ftyp { get; } = Type("ftyp"u8);

    /// <summary>Makes the number that stands for a four character box type.</summary>
    /// <param name="name">The four characters.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Type(ReadOnlySpan<byte> name) => BinaryPrimitives.ReadUInt32BigEndian(name);

    /// <summary>Reads the box at a position and moves past it.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="position">The position of the box, which moves to the box after it.</param>
    /// <param name="limit">The end of the bytes that may hold boxes.</param>
    /// <param name="box">Receives the box.</param>
    /// <returns><see langword="false"/> when no whole box starts there.</returns>
    internal static bool Next(ReadOnlySpan<byte> data, ref int position, int limit, out Mp4Box box)
    {
        box = default;
        if (limit - position < HeaderSize)
        {
            return false;
        }

        var size = BinaryPrimitives.ReadUInt32BigEndian(data[position..]);
        var type = BinaryPrimitives.ReadUInt32BigEndian(data[(position + sizeof(uint))..]);
        long total = size;
        var header = HeaderSize;
        if (size == LargeSize)
        {
            if (limit - position < LargeHeaderSize)
            {
                return false;
            }

            total = (long)BinaryPrimitives.ReadUInt64BigEndian(data[(position + HeaderSize)..]);
            header = LargeHeaderSize;
        }
        else if (size == 0)
        {
            total = limit - position;
        }

        if (total < header || position + total > limit)
        {
            return false;
        }

        box = new(type, position + header, position + (int)total);
        position += (int)total;
        return true;
    }

    /// <summary>Finds the first box of a type among the boxes in a range.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="start">The start of the boxes.</param>
    /// <param name="end">The end of the boxes.</param>
    /// <param name="type">The type.</param>
    /// <param name="box">Receives the box.</param>
    /// <returns><see langword="true"/> when found.</returns>
    internal static bool TryFind(ReadOnlySpan<byte> data, int start, int end, uint type, out Mp4Box box)
    {
        var position = start;
        while (Next(data, ref position, end, out box))
        {
            if (box.Type == type)
            {
                return true;
            }
        }

        box = default;
        return false;
    }
}
