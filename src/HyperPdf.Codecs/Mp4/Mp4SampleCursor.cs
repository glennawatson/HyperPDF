// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdf.Codecs.Mp4;

/// <summary>Walks the chunk and time tables of a track one sample at a time, giving each sample's file offset and duration.</summary>
internal ref struct Mp4SampleCursor
{
    /// <summary>The bytes of the version, flags and entry count that start a table box.</summary>
    private const int TableHeader = 8;

    /// <summary>The bytes of an entry of the sample to chunk box.</summary>
    private const int RunSize = 12;

    /// <summary>The bytes of an entry of the time to sample box.</summary>
    private const int TimeSize = 8;

    /// <summary>The bytes of a 32 bit chunk offset.</summary>
    private const int NarrowSize = 4;

    /// <summary>The bytes of a 64 bit chunk offset.</summary>
    private const int WideSize = 8;

    /// <summary>The bytes of the movie box.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The index of the first sample to chunk entry.</summary>
    private readonly int _runs;

    /// <summary>The number of sample to chunk entries.</summary>
    private readonly int _runCount;

    /// <summary>The index of the first chunk offset.</summary>
    private readonly int _offsets;

    /// <summary>The number of chunk offsets.</summary>
    private readonly int _chunkCount;

    /// <summary>Whether the chunk offsets are 64 bits.</summary>
    private readonly bool _wide;

    /// <summary>The index of the first time to sample entry.</summary>
    private readonly int _times;

    /// <summary>The number of time to sample entries.</summary>
    private readonly int _timeCount;

    /// <summary>The chunk the cursor is in, from 1, or 0 before the first.</summary>
    private int _chunk;

    /// <summary>The sample to chunk entry in use.</summary>
    private int _run;

    /// <summary>The samples left in the chunk.</summary>
    private int _left;

    /// <summary>The file offset of the next sample.</summary>
    private long _position;

    /// <summary>The time to sample entry in use, counted from 1.</summary>
    private int _timeEntry;

    /// <summary>The samples left in the time entry.</summary>
    private int _timeLeft;

    /// <summary>The duration of the samples of the time entry.</summary>
    private int _delta;

    /// <summary>Initializes a new instance of the <see cref="Mp4SampleCursor"/> struct.</summary>
    /// <param name="data">The bytes of the movie box.</param>
    /// <param name="runBox">The sample to chunk box.</param>
    /// <param name="offsetBox">The chunk offset box.</param>
    /// <param name="wide">Whether the chunk offsets are 64 bits.</param>
    /// <param name="timeBox">The time to sample box.</param>
    internal Mp4SampleCursor(ReadOnlySpan<byte> data, Mp4Box runBox, Mp4Box offsetBox, bool wide, Mp4Box timeBox)
    {
        _data = data;
        _runs = runBox.Start + TableHeader;
        _runCount = CountOf(data, runBox, RunSize);
        _offsets = offsetBox.Start + TableHeader;
        _wide = wide;
        _chunkCount = CountOf(data, offsetBox, wide ? WideSize : NarrowSize);
        _times = timeBox.Start + TableHeader;
        _timeCount = CountOf(data, timeBox, TimeSize);
    }

    /// <summary>Moves to the next sample.</summary>
    /// <param name="size">The sample's size, which moves the offset of the one after it.</param>
    /// <param name="offset">Receives the sample's file offset.</param>
    /// <param name="delta">Receives the sample's duration in ticks.</param>
    /// <returns><see langword="false"/> when the tables hold no more samples.</returns>
    internal bool Next(int size, out long offset, out int delta)
    {
        offset = 0;
        delta = _delta;
        while (_left == 0)
        {
            if (_chunk >= _chunkCount)
            {
                return false;
            }

            StartChunk();
        }

        offset = _position;
        _position += size;
        _left--;
        while (_timeLeft == 0 && _timeEntry < _timeCount)
        {
            var entry = _times + (_timeEntry * TimeSize);
            _timeLeft = BinaryPrimitives.ReadInt32BigEndian(_data[entry..]);
            _delta = BinaryPrimitives.ReadInt32BigEndian(_data[(entry + sizeof(int))..]);
            _timeEntry++;
        }

        delta = _delta;
        _timeLeft = Math.Max(0, _timeLeft - 1);
        return true;
    }

    /// <summary>Counts the entries of a table box that fit in the box.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="box">The box.</param>
    /// <param name="entrySize">The bytes of an entry.</param>
    /// <returns>The number of entries the box says it has, or fewer if it is cut short.</returns>
    private static int CountOf(ReadOnlySpan<byte> data, Mp4Box box, int entrySize)
    {
        if (box.End - box.Start < TableHeader)
        {
            return 0;
        }

        var declared = BinaryPrimitives.ReadInt32BigEndian(data[(box.Start + Mp4Boxes.FullBoxSize)..]);
        return Math.Clamp(declared, 0, (box.End - box.Start - TableHeader) / entrySize);
    }

    /// <summary>Moves to the next chunk.</summary>
    private void StartChunk()
    {
        _chunk++;
        while (_run + 1 < _runCount && BinaryPrimitives.ReadInt32BigEndian(_data[(_runs + ((_run + 1) * RunSize))..]) <= _chunk)
        {
            _run++;
        }

        _left = _runCount == 0 ? 0 : BinaryPrimitives.ReadInt32BigEndian(_data[(_runs + (_run * RunSize) + sizeof(int))..]);
        var at = _offsets + ((_chunk - 1) * (_wide ? WideSize : NarrowSize));
        _position = _wide ? (long)BinaryPrimitives.ReadUInt64BigEndian(_data[at..]) : BinaryPrimitives.ReadUInt32BigEndian(_data[at..]);
    }
}
