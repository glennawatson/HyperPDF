// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;

namespace HyperPdf.Codecs.Mp4;

/// <summary>
/// An ISO base media file (MP4, M4A, MOV): sound, pictures and text in tracks. The movie box is read when the file opens and turned
/// into one list of samples in the order the file stores them, so playing the file is a single pass over the media data.
/// </summary>
[DebuggerDisplay("Mp4Container: {Tracks.Count} tracks, {Duration}")]
internal sealed class Mp4Container : MediaContainer
{
    /// <summary>The most bytes of a movie box that are read.</summary>
    private const int MaxMovieBytes = 256 << 20;

    /// <summary>The most bytes of a movie fragment box that are read.</summary>
    private const int MaxFragmentBytes = 64 << 20;

    /// <summary>The seconds of sound before a seek target that decoding starts at, so filters that need earlier sound have it.</summary>
    private const double SoundPreRoll = 0.1;

    /// <summary>The bytes of the length that starts a timed text sample.</summary>
    private const int TextLengthSize = sizeof(ushort);

    /// <summary>The file.</summary>
    private readonly Stream _stream;

    /// <summary>The samples, in the order the file stores them.</summary>
    private readonly Mp4SampleList _samples;

    /// <summary>For each track, the indexes in the sample list of its samples.</summary>
    private readonly int[][] _trackSamples;

    /// <summary>A place to read a sample that must be changed before it is handed on.</summary>
    private byte[] _scratch = [];

    /// <summary>The index of the next sample to read.</summary>
    private int _next;

    /// <summary>Initializes a new instance of the <see cref="Mp4Container"/> class.</summary>
    /// <param name="stream">The file.</param>
    /// <param name="samples">The samples.</param>
    /// <param name="tracks">The tracks.</param>
    /// <param name="seconds">The length of the movie in seconds, or 0.</param>
    private Mp4Container(Stream stream, Mp4SampleList samples, MediaTrack[] tracks, double seconds)
    {
        _stream = stream;
        _samples = samples;
        Tracks = tracks;
        _trackSamples = BucketByTrack(samples, tracks.Length);
        Duration = TimeSpan.FromSeconds(seconds > 0 ? seconds : EndOf(samples));
    }

    /// <inheritdoc/>
    public override IReadOnlyList<MediaTrack> Tracks { get; }

    /// <inheritdoc/>
    public override TimeSpan Duration { get; }

    /// <inheritdoc/>
    public override bool ReadPacket(MediaPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (_next >= _samples.Count)
        {
            return false;
        }

        var index = _next;
        _next++;
        var track = _samples.Track(index);
        _stream.Position = _samples.Offset(index);
        var time = Math.Max(0, _samples.Time(index));
        if (Tracks[track].Codec == MediaCodec.Text)
        {
            return ReadText(packet, index, time);
        }

        var size = _samples.Size(index);
        var data = packet.Begin(track, size, time, _samples.Duration(index), _samples.IsKey(index));
        return _stream.ReadAtLeast(data, size, false) == size;
    }

    /// <inheritdoc/>
    public override bool Seek(TimeSpan position)
    {
        var target = position.TotalSeconds;
        var first = _samples.Count;
        for (var track = 0; track < _trackSamples.Length; track++)
        {
            if (_trackSamples[track].Length > 0)
            {
                first = Math.Min(first, StartFor(track, target));
            }
        }

        _next = first == _samples.Count ? 0 : first;
        return true;
    }

    /// <summary>Opens a stream as an ISO base media file.</summary>
    /// <param name="stream">The file; it is left where it was when it is not one this reader plays.</param>
    /// <returns>The container, which owns the stream, or <see langword="null"/>.</returns>
    internal static Mp4Container? TryOpen(Stream stream)
    {
        var start = stream.Position;
        var opened = Open(stream);
        if (opened is null)
        {
            stream.Position = start;
        }

        return opened;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream.Dispose();
        }
    }

    /// <summary>Opens a stream, which is left wherever it ends up when the file is not one this reader plays.</summary>
    /// <param name="stream">The file.</param>
    /// <returns>The container, or <see langword="null"/>.</returns>
    private static Mp4Container? Open(Stream stream)
    {
        var boxes = Mp4TopLevel.List(stream);
        if (boxes.Count == 0 || !IsMovieStart(boxes[0].Type))
        {
            return null;
        }

        try
        {
            return Build(stream, boxes);
        }
        catch (Exception exception) when (exception is ArgumentException or IndexOutOfRangeException or InvalidDataException or EndOfStreamException)
        {
            return null;
        }
    }

    /// <summary>Reads the movie and fragments of a file.</summary>
    /// <param name="stream">The file.</param>
    /// <param name="boxes">The top level boxes.</param>
    /// <returns>The container, or <see langword="null"/> when the file has no movie or no samples this reader plays.</returns>
    private static Mp4Container? Build(Stream stream, List<Mp4TopBox> boxes)
    {
        var movie = boxes.Find(static box => box.Type == Mp4Boxes.Moov && box.IsComplete);
        if (movie.Size == 0)
        {
            return null;
        }

        var samples = new Mp4SampleList();
        var infos = new List<Mp4TrackInfo>();
        var seconds = ReadBox(stream, movie, MaxMovieBytes, samples, infos, null);
        if (infos.Count == 0)
        {
            return null;
        }

        var nextDecode = new long[infos.Count];
        foreach (var box in boxes)
        {
            if (box.Type == Mp4Boxes.Moof && box.IsComplete)
            {
                _ = ReadBox(stream, box, MaxFragmentBytes, samples, infos, nextDecode);
            }
        }

        if (samples.Count == 0)
        {
            return null;
        }

        samples.SortByOffset();
        var tracks = new MediaTrack[infos.Count];
        for (var index = 0; index < tracks.Length; index++)
        {
            tracks[index] = infos[index].Track;
        }

        return new(stream, samples, tracks, seconds);
    }

    /// <summary>Reads a movie or movie fragment box and adds what it holds.</summary>
    /// <param name="stream">The file.</param>
    /// <param name="box">The box.</param>
    /// <param name="limit">The most bytes of the box that are read.</param>
    /// <param name="samples">Receives the samples.</param>
    /// <param name="infos">Receives the tracks of a movie box, or holds the tracks for a fragment.</param>
    /// <param name="nextDecode">For a fragment, the next decode time of each track; <see langword="null"/> for a movie box.</param>
    /// <returns>The length of the movie in seconds for a movie box, otherwise 0.</returns>
    /// <exception cref="InvalidDataException">The box is too big or is not a box.</exception>
    private static double ReadBox(Stream stream, in Mp4TopBox box, int limit, Mp4SampleList samples, List<Mp4TrackInfo> infos, long[]? nextDecode)
    {
        if (box.Size > limit)
        {
            throw new InvalidDataException("A box of the file is too big.");
        }

        var bytes = new byte[(int)box.Size];
        stream.Position = box.Offset;
        stream.ReadExactly(bytes);
        var position = 0;
        if (!Mp4Boxes.Next(bytes, ref position, bytes.Length, out var inner))
        {
            throw new InvalidDataException("A box of the file is damaged.");
        }

        var content = bytes.AsSpan(inner.Start, inner.End - inner.Start);
        if (nextDecode is null)
        {
            return Mp4Movie.Read(content, samples, infos);
        }

        Mp4Fragments.Add(content, box.Offset, infos, nextDecode, samples);
        return 0;
    }

    /// <summary>Tells whether a box type can start an ISO base media file.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for a type that starts such files.</returns>
    private static bool IsMovieStart(uint type) =>
        type == Mp4Boxes.Ftyp
        || type == Mp4Boxes.Moov
        || type == Mp4Boxes.Mdat
        || type == Mp4Boxes.Moof
        || type == Mp4Boxes.Type("free"u8)
        || type == Mp4Boxes.Type("skip"u8)
        || type == Mp4Boxes.Type("wide"u8)
        || type == Mp4Boxes.Type("styp"u8);

    /// <summary>Lists the indexes of each track's samples.</summary>
    /// <param name="samples">The samples.</param>
    /// <param name="trackCount">The number of tracks.</param>
    /// <returns>For each track, the indexes in the order of the file.</returns>
    private static int[][] BucketByTrack(Mp4SampleList samples, int trackCount)
    {
        var counts = new int[trackCount];
        for (var index = 0; index < samples.Count; index++)
        {
            counts[samples.Track(index)]++;
        }

        var result = new int[trackCount][];
        for (var track = 0; track < trackCount; track++)
        {
            result[track] = new int[counts[track]];
            counts[track] = 0;
        }

        for (var index = 0; index < samples.Count; index++)
        {
            var track = samples.Track(index);
            result[track][counts[track]] = index;
            counts[track]++;
        }

        return result;
    }

    /// <summary>Finds when the last sample ends.</summary>
    /// <param name="samples">The samples.</param>
    /// <returns>The time in seconds.</returns>
    private static double EndOf(Mp4SampleList samples)
    {
        var end = 0.0;
        for (var index = 0; index < samples.Count; index++)
        {
            end = Math.Max(end, samples.Time(index) + samples.Duration(index));
        }

        return end;
    }

    /// <summary>Finds the sample from which a track is decoded to reach a time.</summary>
    /// <param name="track">The track.</param>
    /// <param name="target">The time in seconds.</param>
    /// <returns>The index in the sample list.</returns>
    private int StartFor(int track, double target)
    {
        var list = _trackSamples[track];
        var isSound = Tracks[track].Kind == MediaTrackKind.Audio;
        var wanted = isSound ? target - SoundPreRoll : target;
        var low = 0;
        var high = list.Length;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            if (_samples.DecodeTime(list[middle]) <= wanted)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        var at = Math.Max(0, low - 1);
        while (at > 0 && !_samples.IsKey(list[at]))
        {
            at--;
        }

        return list[at];
    }

    /// <summary>Reads a timed text sample, whose text follows a length.</summary>
    /// <param name="packet">Receives the text.</param>
    /// <param name="index">The sample.</param>
    /// <param name="time">When the sample is shown.</param>
    /// <returns><see langword="true"/> when the sample was read.</returns>
    private bool ReadText(MediaPacket packet, int index, double time)
    {
        var size = _samples.Size(index);
        if (_scratch.Length < size)
        {
            _scratch = new byte[size];
        }

        if (_stream.ReadAtLeast(_scratch.AsSpan(0, size), size, false) < size)
        {
            return false;
        }

        var length = size >= TextLengthSize ? Math.Min(BinaryPrimitives.ReadUInt16BigEndian(_scratch), size - TextLengthSize) : 0;
        var target = packet.Begin(_samples.Track(index), length, time, _samples.Duration(index), true);
        if (length > 0)
        {
            _scratch.AsSpan(TextLengthSize, length).CopyTo(target);
        }

        return true;
    }
}
