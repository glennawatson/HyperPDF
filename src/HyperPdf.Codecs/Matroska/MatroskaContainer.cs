// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using HyperPdf.Codecs.Containers;
using HyperPdf.Codecs.Mp3;

namespace HyperPdf.Codecs.Matroska;

/// <summary>
/// A Matroska or WebM file. The header, tracks and the position of every cluster are read when the file opens; packets are then read
/// by walking the clusters, splitting laced blocks into frames as they are met.
/// </summary>
[DebuggerDisplay("MatroskaContainer: {Tracks.Count} tracks, {Duration}")]
internal sealed class MatroskaContainer : MediaContainer
{
    /// <summary>The bytes of an element header at most: a four byte ID and an eight byte size.</summary>
    private const int MaxHeader = 12;

    /// <summary>The bytes at the start of a block read to find its header and the lengths of its laced frames.</summary>
    private const int BlockHead = 4_096;

    /// <summary>The most bytes of a small top level element, such as the tracks, that are read.</summary>
    private const int MaxSmallElement = 16 << 20;

    /// <summary>The seconds before a seek target that sound decoding starts at, so a codec that needs earlier sound has it.</summary>
    private const double PreRoll = 0.1;

    /// <summary>The file.</summary>
    private readonly Stream _stream;

    /// <summary>A window over the file.</summary>
    private readonly FileWindow _window;

    /// <summary>The tracks, with the numbers blocks name them by.</summary>
    private readonly List<MatroskaTrack> _entries;

    /// <summary>The tracks as the library describes them.</summary>
    private readonly MediaTrack[] _tracks;

    /// <summary>The seconds added to the times of each track's frames.</summary>
    private readonly double[] _timeShift;

    /// <summary>For each track, the last place found while seeking where decoding can start.</summary>
    private readonly MatroskaStart[] _best;

    /// <summary>For each track, whether seeking has met a frame after its target.</summary>
    private readonly bool[] _passed;

    /// <summary>The seconds in a timestamp unit.</summary>
    private readonly double _scale;

    /// <summary>The clusters found when the file opened.</summary>
    private readonly List<MatroskaCluster> _clusters;

    /// <summary>The file offset where the clusters and other segment children start.</summary>
    private readonly long _segmentStart;

    /// <summary>The file offset after the segment.</summary>
    private readonly long _segmentEnd;

    /// <summary>The frames of the block being given out.</summary>
    private readonly List<MatroskaFrame> _pending = [];

    /// <summary>The file offset of the next element to read.</summary>
    private long _position;

    /// <summary>The start time of the cluster being read, in seconds.</summary>
    private double _clusterSeconds;

    /// <summary>The file offset of the element that gave the pending frames.</summary>
    private long _blockPosition;

    /// <summary>The index of the next pending frame to give out.</summary>
    private int _nextFrame;

    /// <summary>Initializes a new instance of the <see cref="MatroskaContainer"/> class.</summary>
    /// <param name="stream">The file.</param>
    /// <param name="window">A window over the file.</param>
    /// <param name="layout">What was read when the file opened.</param>
    private MatroskaContainer(Stream stream, FileWindow window, MatroskaLayout layout)
    {
        _stream = stream;
        _window = window;
        _entries = layout.Tracks;
        _scale = layout.Scale;
        _clusters = layout.Clusters;
        _segmentStart = layout.SegmentStart;
        _segmentEnd = layout.SegmentEnd;
        _position = _segmentStart;
        _tracks = new MediaTrack[_entries.Count];
        _timeShift = new double[_entries.Count];
        _best = new MatroskaStart[_entries.Count];
        _passed = new bool[_entries.Count];
        for (var index = 0; index < _tracks.Length; index++)
        {
            _tracks[index] = _entries[index].Track;
        }

        for (var index = 0; index < _tracks.Length; index++)
        {
            ApplyDelay(index);
        }

        Duration = TimeSpan.FromSeconds(layout.Seconds);
    }

    /// <inheritdoc/>
    public override IReadOnlyList<MediaTrack> Tracks => _tracks;

    /// <inheritdoc/>
    public override TimeSpan Duration { get; }

    /// <inheritdoc/>
    public override bool ReadPacket(MediaPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        while (_nextFrame >= _pending.Count)
        {
            _pending.Clear();
            _nextFrame = 0;
            if (!ReadNextBlock())
            {
                return false;
            }
        }

        var frame = _pending[_nextFrame];
        _nextFrame++;
        var data = packet.Begin(frame.Track, frame.Length, Math.Max(0, frame.Time + _timeShift[frame.Track]), frame.Duration, frame.IsKey);
        _stream.Position = frame.Offset;
        return _stream.ReadAtLeast(data, frame.Length, false) == frame.Length;
    }

    /// <inheritdoc/>
    public override bool Seek(TimeSpan position)
    {
        if (_clusters.Count == 0)
        {
            return false;
        }

        var target = position.TotalSeconds;
        var chosen = 0;
        for (var index = 0; index < _clusters.Count; index++)
        {
            if (_clusters[index].Seconds <= target)
            {
                chosen = index;
            }
        }

        Restart(_clusters[chosen].Offset);
        var start = FindStart(target);
        Restart(start.Position);
        _clusterSeconds = start.ClusterSeconds;
        return true;
    }

    /// <summary>Opens a stream as a Matroska or WebM file.</summary>
    /// <param name="stream">The file; it is left where it was when it is not one.</param>
    /// <returns>The container, which owns the stream, or <see langword="null"/>.</returns>
    internal static MatroskaContainer? TryOpen(Stream stream)
    {
        var start = stream.Position;
        var window = new FileWindow(stream);
        MatroskaContainer? opened = null;
        try
        {
            var layout = ReadLayout(window, start);
            opened = layout is null ? null : new MatroskaContainer(stream, window, layout);
        }
        catch (Exception exception) when (exception is ArgumentException or IndexOutOfRangeException or InvalidDataException)
        {
            opened = null;
        }

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

    /// <summary>Reads the header, the segment information, the tracks and the cluster positions.</summary>
    /// <param name="window">The file.</param>
    /// <param name="start">Where the file starts.</param>
    /// <returns>What was read, or <see langword="null"/> when the file is not Matroska or has no track this reader lists.</returns>
    private static MatroskaLayout? ReadLayout(FileWindow window, long start)
    {
        if (!TryReadHeader(window, start, out var after) || !TryReadElement(window, after, out var segment) || segment.Id != MatroskaIds.Segment)
        {
            return null;
        }

        var segmentEnd = segment.End == Ebml.UnknownSize ? window.Length : segment.End;
        var builder = new MatroskaLayoutBuilder(segment.PayloadStart, segmentEnd);
        var position = segment.PayloadStart;
        while (position < segmentEnd && TryReadElement(window, position, out var element))
        {
            builder.Add(window, element);
            if (element.End == Ebml.UnknownSize)
            {
                break;
            }

            position = element.End;
        }

        return builder.Build();
    }

    /// <summary>Reads the EBML header and checks the document type.</summary>
    /// <param name="window">The file.</param>
    /// <param name="start">Where the file starts.</param>
    /// <param name="after">Receives the offset after the header.</param>
    /// <returns><see langword="true"/> for a Matroska or WebM header.</returns>
    private static bool TryReadHeader(FileWindow window, long start, out long after)
    {
        after = 0;
        if (!TryReadElement(window, start, out var header) || header.Id != MatroskaIds.Header || header.End == Ebml.UnknownSize)
        {
            return false;
        }

        var bytes = window.Get(header.PayloadStart, (int)Math.Min(header.End - header.PayloadStart, MaxSmallElement));
        var content = bytes[..(int)Math.Min(bytes.Length, header.End - header.PayloadStart)];
        var position = 0;
        while (Ebml.Next(content, ref position, content.Length, out var element))
        {
            if (element.Id != MatroskaIds.DocType)
            {
                continue;
            }

            var type = Ebml.ReadString(content[element.Start..element.End]);
            after = header.End;
            return type is "matroska" or "webm";
        }

        return false;
    }

    /// <summary>Reads the header of the element at an offset.</summary>
    /// <param name="window">The file.</param>
    /// <param name="offset">The file offset.</param>
    /// <param name="element">Receives the element.</param>
    /// <returns><see langword="false"/> when no element starts there.</returns>
    private static bool TryReadElement(FileWindow window, long offset, out MatroskaHeader element)
    {
        element = default;
        var bytes = window.Get(offset, MaxHeader);
        var id = Ebml.ReadId(bytes, out var idLength);
        if (id == 0)
        {
            return false;
        }

        var size = Ebml.ReadNumber(bytes[idLength..], out var sizeLength);
        if (size == Ebml.InvalidNumber)
        {
            return false;
        }

        var payload = offset + idLength + sizeLength;
        element = new(id, offset, payload, size == Ebml.UnknownSize ? Ebml.UnknownSize : payload + size);
        return true;
    }

    /// <summary>Starts reading again at an offset.</summary>
    /// <param name="offset">The file offset of an element.</param>
    private void Restart(long offset)
    {
        _pending.Clear();
        _nextFrame = 0;
        _position = offset;
    }

    /// <summary>Reads on from the cluster the position is in and finds where decoding of every track can start to reach a time.</summary>
    /// <param name="target">The time in seconds.</param>
    /// <returns>The earliest place a track needs to start from, with the start time of the cluster it is in.</returns>
    private MatroskaStart FindStart(double target)
    {
        var best = _best;
        var passed = _passed;
        Array.Fill(best, new(-1, 0));
        Array.Clear(passed);
        var first = new MatroskaStart(_position, _clusterSeconds);
        while (ReadNextBlock())
        {
            foreach (var frame in _pending)
            {
                NoteFrame(frame, target, best, passed);
            }

            _pending.Clear();
            if (AllPassed(passed))
            {
                break;
            }
        }

        var chosen = first;
        foreach (var candidate in best)
        {
            chosen = candidate.Position >= 0 && candidate.Position < chosen.Position ? candidate : chosen;
        }

        return chosen.Position == first.Position ? first : chosen;
    }

    /// <summary>Notes a frame met while finding where to start.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="target">The seek target in seconds.</param>
    /// <param name="best">For each track, the last place decoding could start at or before the target.</param>
    /// <param name="passed">For each track, whether a frame after the target was met.</param>
    private void NoteFrame(in MatroskaFrame frame, double target, MatroskaStart[] best, bool[] passed)
    {
        var track = _tracks[frame.Track];
        if (track.Kind == MediaTrackKind.Caption || track.Codec == MediaCodec.Unknown)
        {
            passed[frame.Track] = true;
            return;
        }

        var wanted = track.Kind == MediaTrackKind.Audio ? target - PreRoll : target;
        if (frame.Time > wanted)
        {
            passed[frame.Track] = true;
            return;
        }

        if (track.Kind != MediaTrackKind.Video || frame.IsKey)
        {
            best[frame.Track] = new(_blockPosition, _clusterSeconds);
        }
    }

    /// <summary>Tells whether every track has had a frame after the seek target.</summary>
    /// <param name="passed">For each track, whether a frame after the target was met.</param>
    /// <returns><see langword="true"/> when every track has.</returns>
    private bool AllPassed(bool[] passed)
    {
        for (var index = 0; index < passed.Length; index++)
        {
            if (!passed[index] && _tracks[index].Kind != MediaTrackKind.Caption && _tracks[index].Codec != MediaCodec.Unknown)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Sets up a sound track for the delay its codec needs before the first sound, which the track says in nanoseconds.</summary>
    /// <param name="index">The track.</param>
    private void ApplyDelay(int index)
    {
        var track = _tracks[index];
        var delay = _entries[index].DelaySeconds;
        if (delay <= 0 || track.Kind != MediaTrackKind.Audio)
        {
            return;
        }

        var skip = (int)Math.Round(delay * track.SampleRate);
        if (track.Codec == MediaCodec.Aac)
        {
            var configuration = track.Configuration.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(configuration, skip);
            _tracks[index] = track with { Configuration = configuration };
            _timeShift[index] = -delay;
        }
        else if (track.Codec == MediaCodec.Mp3)
        {
            // The MP3 decoder finds the sound to drop from the packet times, which therefore stay as the file has them.
            _tracks[index] = track with { Configuration = Mp3StreamTag.FromStartSkip(skip).ToConfiguration() };
        }
        else
        {
            _timeShift[index] = -delay;
        }
    }

    /// <summary>Reads the element at the position and acts on it.</summary>
    /// <returns><see langword="true"/> when frames were found; <see langword="false"/> at the end of the segment.</returns>
    private bool ReadNextBlock()
    {
        while (_position < _segmentEnd)
        {
            if (!TryReadElement(_window, _position, out var element) || !HandleElement(element))
            {
                return false;
            }

            if (_pending.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Acts on one element and moves the position past it, or into it when it holds the blocks.</summary>
    /// <param name="element">The element.</param>
    /// <returns><see langword="false"/> when the element cannot be passed.</returns>
    private bool HandleElement(in MatroskaHeader element)
    {
        switch (element.Id)
        {
            case MatroskaIds.Cluster:
            {
                _position = element.PayloadStart;
                return true;
            }

            case MatroskaIds.ClusterTimestamp:
            {
                _clusterSeconds = ReadUnsigned(element.PayloadStart, element.End) * _scale;
                break;
            }

            case MatroskaIds.SimpleBlock:
            {
                _blockPosition = element.Offset;
                SplitBlock(element.PayloadStart, element.End - element.PayloadStart, new(_clusterSeconds, _scale, 0, false), true);
                break;
            }

            case MatroskaIds.BlockGroup:
            {
                _blockPosition = element.Offset;
                ReadGroup(element);
                break;
            }
        }

        if (element.End == Ebml.UnknownSize)
        {
            return false;
        }

        _position = element.End;
        return true;
    }

    /// <summary>Reads an unsigned number element's value.</summary>
    /// <param name="start">The file offset of the content.</param>
    /// <param name="end">The file offset after the content.</param>
    /// <returns>The number.</returns>
    private double ReadUnsigned(long start, long end)
    {
        var length = (int)(end - start);
        return Ebml.ReadUnsigned(_window.Get(start, length)[..length]);
    }

    /// <summary>Reads a block group: its block, how long it lasts and whether it is a key block.</summary>
    /// <param name="group">The block group element.</param>
    private void ReadGroup(in MatroskaHeader group)
    {
        var position = group.PayloadStart;
        long blockOffset = -1;
        long blockLength = 0;
        var duration = 0.0;
        var isKey = true;
        while (position < group.End && TryReadElement(_window, position, out var child) && child.End != Ebml.UnknownSize)
        {
            switch (child.Id)
            {
                case MatroskaIds.Block:
                {
                    blockOffset = child.PayloadStart;
                    blockLength = child.End - child.PayloadStart;
                    break;
                }

                case MatroskaIds.BlockDuration:
                {
                    duration = ReadUnsigned(child.PayloadStart, child.End) * _scale;
                    break;
                }

                case MatroskaIds.ReferenceBlock:
                {
                    isKey = false;
                    break;
                }
            }

            position = child.End;
        }

        if (blockOffset >= 0)
        {
            SplitBlock(blockOffset, blockLength, new(_clusterSeconds, _scale, duration, isKey), false);
        }
    }

    /// <summary>Splits a block into frames and adds them to the pending list.</summary>
    /// <param name="offset">The file offset of the block's content.</param>
    /// <param name="length">The length of the content.</param>
    /// <param name="timing">What the surroundings say about the block.</param>
    /// <param name="isSimple">Whether the block is a simple block.</param>
    private void SplitBlock(long offset, long length, in MatroskaBlockTiming timing, bool isSimple)
    {
        var head = _window.Get(offset, (int)Math.Min(length, BlockHead));
        _ = MatroskaBlocks.Split(head[..(int)Math.Min(head.Length, length)], offset, length, in timing, _entries, _pending, isSimple);
    }
}
