// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdf.Codecs.Mp4;

/// <summary>Reads movie fragment boxes: the samples that fragmented files keep in runs after the movie box.</summary>
internal static class Mp4Fragments
{
    /// <summary>The flag of a track fragment header that says the base data offset is given.</summary>
    private const int BaseOffsetFlag = 0x1;

    /// <summary>The flag of a track fragment header that says the sample description index is given.</summary>
    private const int DescriptionFlag = 0x2;

    /// <summary>The flag of a track fragment header that says the default sample duration is given.</summary>
    private const int DurationFlag = 0x8;

    /// <summary>The flag of a track fragment header that says the default sample size is given.</summary>
    private const int SizeFlag = 0x10;

    /// <summary>The flag of a track fragment header that says the default sample flags are given.</summary>
    private const int FlagsFlag = 0x20;

    /// <summary>The flag of a track run that says the data offset is given.</summary>
    private const int DataOffsetFlag = 0x1;

    /// <summary>The flag of a track run that says the first sample's flags are given.</summary>
    private const int FirstFlagsFlag = 0x4;

    /// <summary>The flag of a track run that says each sample has a duration.</summary>
    private const int SampleDurationFlag = 0x100;

    /// <summary>The flag of a track run that says each sample has a size.</summary>
    private const int SampleSizeFlag = 0x200;

    /// <summary>The flag of a track run that says each sample has flags.</summary>
    private const int SampleFlagsFlag = 0x400;

    /// <summary>The flag of a track run that says each sample has a composition offset.</summary>
    private const int CompositionFlag = 0x800;

    /// <summary>The bit of a sample's flags that says it is not a sync sample.</summary>
    private const int NonSyncFlag = 0x10000;

    /// <summary>The bytes of the version and flags of a full box, read as one number.</summary>
    private const int VersionFlagsSize = 4;

    /// <summary>The mask of the flags in the version and flags number.</summary>
    private const int FlagsMask = 0xFFFFFF;

    /// <summary>Adds the samples of a movie fragment.</summary>
    /// <param name="data">The content of the movie fragment box.</param>
    /// <param name="moofOffset">The file offset of the movie fragment box, which sample offsets count from.</param>
    /// <param name="tracks">The tracks of the movie.</param>
    /// <param name="nextDecode">The decode time in ticks at which each track's next sample starts; updated.</param>
    /// <param name="samples">Receives the samples.</param>
    internal static void Add(ReadOnlySpan<byte> data, long moofOffset, List<Mp4TrackInfo> tracks, long[] nextDecode, Mp4SampleList samples)
    {
        var position = 0;
        while (Mp4Boxes.Next(data, ref position, data.Length, out var box))
        {
            if (box.Type == Mp4Boxes.Traf)
            {
                AddTrack(data, box, moofOffset, tracks, nextDecode, samples);
            }
        }
    }

    /// <summary>Adds the samples of one track fragment.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="traf">The track fragment box.</param>
    /// <param name="moofOffset">The file offset of the movie fragment box.</param>
    /// <param name="tracks">The tracks of the movie.</param>
    /// <param name="nextDecode">The next decode time of each track.</param>
    /// <param name="samples">Receives the samples.</param>
    private static void AddTrack(ReadOnlySpan<byte> data, Mp4Box traf, long moofOffset, List<Mp4TrackInfo> tracks, long[] nextDecode, Mp4SampleList samples)
    {
        if (!Mp4Boxes.TryFind(data, traf.Start, traf.End, Mp4Boxes.Tfhd, out var tfhd) || !ReadHeader(data, tfhd, moofOffset, tracks, out var header))
        {
            return;
        }

        var index = header.TrackIndex;
        if (Mp4Boxes.TryFind(data, traf.Start, traf.End, Mp4Boxes.Tfdt, out var tfdt) && tfdt.End - tfdt.Start >= VersionFlagsSize + sizeof(int))
        {
            nextDecode[index] = data[tfdt.Start] == 1
                ? (long)BinaryPrimitives.ReadUInt64BigEndian(data[(tfdt.Start + VersionFlagsSize)..])
                : BinaryPrimitives.ReadUInt32BigEndian(data[(tfdt.Start + VersionFlagsSize)..]);
        }

        var offset = header.BaseOffset;
        var position = traf.Start;
        while (Mp4Boxes.Next(data, ref position, traf.End, out var box))
        {
            if (box.Type != Mp4Boxes.Trun)
            {
                continue;
            }

            var defaults = new Mp4RunDefaults(tracks[index].Timing, index, header.BaseOffset, header.Duration, header.Size, header.Flags);
            offset = AddRun(data, box, in defaults, offset, nextDecode, samples);
        }
    }

    /// <summary>Reads a track fragment header.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="box">The header box.</param>
    /// <param name="moofOffset">The file offset of the movie fragment box.</param>
    /// <param name="tracks">The tracks of the movie.</param>
    /// <param name="header">Receives the header.</param>
    /// <returns><see langword="true"/> when the header names a track this library reads.</returns>
    private static bool ReadHeader(ReadOnlySpan<byte> data, Mp4Box box, long moofOffset, List<Mp4TrackInfo> tracks, out Mp4FragmentHeader header)
    {
        header = default;
        if (box.End - box.Start < VersionFlagsSize + sizeof(int))
        {
            return false;
        }

        var flags = BinaryPrimitives.ReadInt32BigEndian(data[box.Start..]) & FlagsMask;
        var id = BinaryPrimitives.ReadInt32BigEndian(data[(box.Start + VersionFlagsSize)..]);
        var index = IndexOfTrack(tracks, id);
        if (index < 0 || tracks[index].Track.Codec == MediaCodec.Unknown)
        {
            return false;
        }

        var position = box.Start + VersionFlagsSize + sizeof(int);
        var baseOffset = moofOffset;
        if ((flags & BaseOffsetFlag) != 0)
        {
            baseOffset = (long)BinaryPrimitives.ReadUInt64BigEndian(data[position..]);
            position += sizeof(long);
        }

        position += (flags & DescriptionFlag) != 0 ? sizeof(int) : 0;
        var duration = ReadOptional(data, flags, DurationFlag, ref position, tracks[index].DefaultDuration);
        var size = ReadOptional(data, flags, SizeFlag, ref position, tracks[index].DefaultSize);
        var sampleFlags = ReadOptional(data, flags, FlagsFlag, ref position, tracks[index].DefaultFlags);
        header = new(index, baseOffset, duration, size, sampleFlags);
        return true;
    }

    /// <summary>Finds a track by the id fragments name it by.</summary>
    /// <param name="tracks">The tracks.</param>
    /// <param name="id">The id.</param>
    /// <returns>The track's index, or -1.</returns>
    private static int IndexOfTrack(List<Mp4TrackInfo> tracks, int id)
    {
        for (var index = 0; index < tracks.Count; index++)
        {
            if (tracks[index].TrackId == id)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Reads a number that is there only when its flag is set.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="flags">The flags of the box.</param>
    /// <param name="flag">The flag that says the number is there.</param>
    /// <param name="position">The position of the number, which moves past it if it is there.</param>
    /// <param name="fallback">The value when the number is not there.</param>
    /// <returns>The number.</returns>
    private static int ReadOptional(ReadOnlySpan<byte> data, int flags, int flag, ref int position, int fallback)
    {
        if ((flags & flag) == 0)
        {
            return fallback;
        }

        var value = BinaryPrimitives.ReadInt32BigEndian(data[position..]);
        position += sizeof(int);
        return value;
    }

    /// <summary>Adds the samples of one track run.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="box">The track run box.</param>
    /// <param name="defaults">The track and the values for what the run does not say.</param>
    /// <param name="offset">The file offset the run's data starts at if the run does not say.</param>
    /// <param name="nextDecode">The next decode time of each track.</param>
    /// <param name="samples">Receives the samples.</param>
    /// <returns>The file offset after the run's data.</returns>
    private static long AddRun(ReadOnlySpan<byte> data, Mp4Box box, in Mp4RunDefaults defaults, long offset, long[] nextDecode, Mp4SampleList samples)
    {
        var flags = BinaryPrimitives.ReadInt32BigEndian(data[box.Start..]) & FlagsMask;
        var count = BinaryPrimitives.ReadInt32BigEndian(data[(box.Start + VersionFlagsSize)..]);
        var position = box.Start + VersionFlagsSize + sizeof(int);
        if ((flags & DataOffsetFlag) != 0)
        {
            offset = defaults.Base + BinaryPrimitives.ReadInt32BigEndian(data[position..]);
            position += sizeof(int);
        }

        var firstFlags = ReadOptional(data, flags, FirstFlagsFlag, ref position, defaults.Flags);
        var timing = defaults.Timing;
        for (var sample = 0; sample < count && position <= box.End; sample++)
        {
            var duration = ReadOptional(data, flags, SampleDurationFlag, ref position, defaults.Duration);
            var size = ReadOptional(data, flags, SampleSizeFlag, ref position, defaults.Size);
            var sampleFlags = ReadOptional(data, flags, SampleFlagsFlag, ref position, sample == 0 && (flags & FirstFlagsFlag) != 0 ? firstFlags : defaults.Flags);
            var composition = ReadOptional(data, flags, CompositionFlag, ref position, 0);
            var decode = nextDecode[defaults.Track];
            samples.Add(offset, size, defaults.Track, (sampleFlags & NonSyncFlag) == 0, timing.Seconds(decode + composition), timing.Seconds(decode), timing.Length(duration));
            offset += size;
            nextDecode[defaults.Track] = decode + duration;
        }

        return offset;
    }
}
