// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdf.Codecs.Mp4;

/// <summary>Reads the movie box: the tracks, their timing, and the samples their sample tables hold.</summary>
internal static class Mp4Movie
{
    /// <summary>The bytes of two numbers of four bytes.</summary>
    private const int TwoInts = sizeof(int) << 1;

    /// <summary>The bytes of the version and flags and the two times that start a version 0 header.</summary>
    private const int ShortTimes = 12;

    /// <summary>The bytes of the version and flags and the two times that start a version 1 header.</summary>
    private const int LongTimes = 20;

    /// <summary>The bytes of the track id and a reserved field in a track header.</summary>
    private const int TrackIdSize = 8;

    /// <summary>The movie time scale that stands in when a header does not give one.</summary>
    private const double DefaultScale = 1000;

    /// <summary>The media time value of an edit that shows nothing.</summary>
    private const long EmptyEdit = -1;

    /// <summary>The bytes of an edit list entry of version 0.</summary>
    private const int ShortEdit = 12;

    /// <summary>The bytes of an edit list entry of version 1.</summary>
    private const int LongEdit = 20;

    /// <summary>The numbers of a track extends box after its version and flags: the id and four defaults.</summary>
    private const int TrexFields = 5;

    /// <summary>Reads the movie box.</summary>
    /// <param name="data">The content of the movie box.</param>
    /// <param name="samples">Receives the samples of the tracks' sample tables.</param>
    /// <param name="tracks">Receives the tracks.</param>
    /// <returns>The length of the movie in seconds, or 0 when the header does not say.</returns>
    internal static double Read(ReadOnlySpan<byte> data, Mp4SampleList samples, List<Mp4TrackInfo> tracks)
    {
        var movieScale = DefaultScale;
        var movieSeconds = 0.0;
        if (Mp4Boxes.TryFind(data, 0, data.Length, Mp4Boxes.Mvhd, out var header))
        {
            var (scale, duration) = ReadHeader(data, header);
            movieScale = scale > 0 ? scale : DefaultScale;
            movieSeconds = duration / movieScale;
        }

        var position = 0;
        while (Mp4Boxes.Next(data, ref position, data.Length, out var box))
        {
            if (box.Type == Mp4Boxes.Trak)
            {
                ReadTrack(data, box, movieScale, samples, tracks);
            }
        }

        ReadDefaults(data, tracks);
        return movieSeconds;
    }

    /// <summary>Reads the time scale and duration from a movie or media header.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="box">The header box.</param>
    /// <returns>The time scale and the duration in ticks.</returns>
    private static Mp4Header ReadHeader(ReadOnlySpan<byte> data, Mp4Box box)
    {
        var start = box.Start;
        var wide = box.End > start && data[start] != 0;
        var times = wide ? LongTimes : ShortTimes;
        if (box.End - start < times + sizeof(int) + (wide ? sizeof(long) : sizeof(int)))
        {
            return new(0, 0);
        }

        var scale = BinaryPrimitives.ReadUInt32BigEndian(data[(start + times)..]);
        var at = start + times + sizeof(int);
        var duration = wide ? (long)BinaryPrimitives.ReadUInt64BigEndian(data[at..]) : BinaryPrimitives.ReadUInt32BigEndian(data[at..]);
        return new(scale, duration);
    }

    /// <summary>Finds the boxes that describe a track.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="track">The track box.</param>
    /// <param name="boxes">Receives the boxes.</param>
    /// <returns><see langword="true"/> when the track has all of them.</returns>
    private static bool TryFindBoxes(ReadOnlySpan<byte> data, Mp4Box track, out Mp4TrackBoxes boxes)
    {
        boxes = default;
        if (!Mp4Boxes.TryFind(data, track.Start, track.End, Mp4Boxes.Tkhd, out var header)
            || !Mp4Boxes.TryFind(data, track.Start, track.End, Mp4Boxes.Mdia, out var media)
            || !Mp4Boxes.TryFind(data, media.Start, media.End, Mp4Boxes.Mdhd, out var mediaHeader)
            || !Mp4Boxes.TryFind(data, media.Start, media.End, Mp4Boxes.Hdlr, out var handler)
            || !Mp4Boxes.TryFind(data, media.Start, media.End, Mp4Boxes.Minf, out var information)
            || !Mp4Boxes.TryFind(data, information.Start, information.End, Mp4Boxes.Stbl, out var table)
            || !Mp4Boxes.TryFind(data, table.Start, table.End, Mp4Boxes.Stsd, out var description))
        {
            return false;
        }

        boxes = new(header, mediaHeader, handler, table, description);
        return true;
    }

    /// <summary>Reads one track box and adds its track and samples.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="track">The track box.</param>
    /// <param name="movieScale">The time scale of the movie.</param>
    /// <param name="samples">Receives the samples.</param>
    /// <param name="tracks">Receives the track.</param>
    private static void ReadTrack(ReadOnlySpan<byte> data, Mp4Box track, double movieScale, Mp4SampleList samples, List<Mp4TrackInfo> tracks)
    {
        if (!TryFindBoxes(data, track, out var boxes))
        {
            return;
        }

        var mediaScale = ReadHeader(data, boxes.MediaHeader).Scale;
        var handler = BinaryPrimitives.ReadUInt32BigEndian(data[(boxes.Handler.Start + Mp4Boxes.FullBoxSize + sizeof(int))..]);
        var edit = ReadEdits(data, track, movieScale, mediaScale);
        var skip = handler == Mp4Handlers.Sound ? (int)Math.Clamp(edit.MediaStart, 0, int.MaxValue) : 0;
        var index = tracks.Count;
        var media = Mp4SampleEntries.Describe(data, boxes.Description, handler, index, skip);

        // The MP3 decoder finds the sound to drop at the start from the packet times, so its times stay on the media's own timeline.
        var timing = new Mp4Timing(mediaScale > 0 ? mediaScale : DefaultScale, media.Codec == MediaCodec.Mp3 ? 0 : edit.Shift);
        tracks.Add(new(media, ReadTrackId(data, boxes.Header), timing, 0, 0, 0));
        if (media.Codec != MediaCodec.Unknown)
        {
            _ = Mp4SampleTables.Add(data, boxes.Table, index, timing, samples);
        }
    }

    /// <summary>Reads the track id from a track header.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="box">The track header box.</param>
    /// <returns>The id.</returns>
    private static int ReadTrackId(ReadOnlySpan<byte> data, Mp4Box box)
    {
        var skip = data[box.Start] == 0 ? ShortTimes : LongTimes;
        return box.End - box.Start >= skip + TrackIdSize ? BinaryPrimitives.ReadInt32BigEndian(data[(box.Start + skip)..]) : 0;
    }

    /// <summary>Reads the edits of a track: how long it starts late and where in the media it starts.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="track">The track box.</param>
    /// <param name="movieScale">The time scale of the movie.</param>
    /// <param name="mediaScale">The time scale of the track's media.</param>
    /// <returns>The start of the media in ticks and the shift in seconds that puts the start of the media at the start of the track.</returns>
    private static Mp4Edit ReadEdits(ReadOnlySpan<byte> data, Mp4Box track, double movieScale, double mediaScale)
    {
        if (!Mp4Boxes.TryFind(data, track.Start, track.End, Mp4Boxes.Edts, out var edts)
            || !Mp4Boxes.TryFind(data, edts.Start, edts.End, Mp4Boxes.Elst, out var elst)
            || elst.End - elst.Start < Mp4Boxes.FullBoxSize + sizeof(int))
        {
            return new(0, 0);
        }

        var wide = data[elst.Start] == 1;
        var step = wide ? LongEdit : ShortEdit;
        var count = BinaryPrimitives.ReadInt32BigEndian(data[(elst.Start + Mp4Boxes.FullBoxSize)..]);
        var empty = 0.0;
        var position = elst.Start + Mp4Boxes.FullBoxSize + sizeof(int);
        for (var entry = 0; entry < count && position + step <= elst.End; entry++, position += step)
        {
            var (segment, mediaTime) = ReadEdit(data, position, wide);
            if (mediaTime != EmptyEdit)
            {
                return new(mediaTime, empty - (mediaTime / (mediaScale > 0 ? mediaScale : DefaultScale)));
            }

            empty += segment / movieScale;
        }

        return new(0, empty);
    }

    /// <summary>Reads one entry of an edit list.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="position">The position of the entry.</param>
    /// <param name="wide">Whether the entry has 64 bit numbers.</param>
    /// <returns>The length of the edit in movie ticks and its start in media ticks.</returns>
    private static Mp4EditEntry ReadEdit(ReadOnlySpan<byte> data, int position, bool wide) =>
        wide
            ? new((long)BinaryPrimitives.ReadUInt64BigEndian(data[position..]), BinaryPrimitives.ReadInt64BigEndian(data[(position + sizeof(long))..]))
            : new(BinaryPrimitives.ReadUInt32BigEndian(data[position..]), BinaryPrimitives.ReadInt32BigEndian(data[(position + sizeof(int))..]));

    /// <summary>Reads the default sample values the movie extends box gives each track.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="tracks">The tracks, whose defaults are filled.</param>
    private static void ReadDefaults(ReadOnlySpan<byte> data, List<Mp4TrackInfo> tracks)
    {
        if (!Mp4Boxes.TryFind(data, 0, data.Length, Mp4Boxes.Mvex, out var extends))
        {
            return;
        }

        var position = extends.Start;
        while (Mp4Boxes.Next(data, ref position, extends.End, out var box))
        {
            if (box.Type == Mp4Boxes.Trex && box.End - box.Start >= Mp4Boxes.FullBoxSize + (TrexFields * sizeof(int)))
            {
                ApplyDefaults(data, box, tracks);
            }
        }
    }

    /// <summary>Applies one track extends box to its track.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="box">The track extends box.</param>
    /// <param name="tracks">The tracks.</param>
    private static void ApplyDefaults(ReadOnlySpan<byte> data, Mp4Box box, List<Mp4TrackInfo> tracks)
    {
        var id = BinaryPrimitives.ReadInt32BigEndian(data[(box.Start + Mp4Boxes.FullBoxSize)..]);
        var at = box.Start + Mp4Boxes.FullBoxSize + TwoInts;
        for (var index = 0; index < tracks.Count; index++)
        {
            if (tracks[index].TrackId == id)
            {
                tracks[index] = tracks[index] with
                {
                    DefaultDuration = BinaryPrimitives.ReadInt32BigEndian(data[at..]),
                    DefaultSize = BinaryPrimitives.ReadInt32BigEndian(data[(at + sizeof(int))..]),
                    DefaultFlags = BinaryPrimitives.ReadInt32BigEndian(data[(at + TwoInts)..]),
                };
            }
        }
    }
}
