// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdf.Codecs.Mp4;

/// <summary>Reads the composition offset box one sample at a time: how much later than it is decoded each sample is shown.</summary>
internal ref struct Mp4Compositions
{
    /// <summary>The bytes of the version, flags and entry count that start the box.</summary>
    private const int TableHeader = 8;

    /// <summary>The bytes of an entry: a count and an offset.</summary>
    private const int EntrySize = 8;

    /// <summary>The bytes of the movie box.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The index of the first entry.</summary>
    private readonly int _first;

    /// <summary>The number of entries.</summary>
    private readonly int _entries;

    /// <summary>The entry in use.</summary>
    private int _entry;

    /// <summary>The samples left in the entry.</summary>
    private int _left;

    /// <summary>The offset of the samples of the entry.</summary>
    private int _offset;

    /// <summary>Initializes a new instance of the <see cref="Mp4Compositions"/> struct.</summary>
    /// <param name="data">The bytes of the movie box.</param>
    /// <param name="box">The composition offset box.</param>
    internal Mp4Compositions(ReadOnlySpan<byte> data, Mp4Box box)
    {
        _data = data;
        _first = box.Start + TableHeader;
        var declared = box.End - box.Start < TableHeader ? 0 : BinaryPrimitives.ReadInt32BigEndian(data[(box.Start + Mp4Boxes.FullBoxSize)..]);
        _entries = Math.Clamp(declared, 0, Math.Max(0, (box.End - box.Start - TableHeader) / EntrySize));
    }

    /// <summary>Gets the offset of the next sample.</summary>
    /// <returns>The offset in ticks, or 0 when the box has no more entries.</returns>
    internal int Next()
    {
        while (_left == 0 && _entry < _entries)
        {
            var at = _first + (_entry * EntrySize);
            _left = BinaryPrimitives.ReadInt32BigEndian(_data[at..]);
            _offset = BinaryPrimitives.ReadInt32BigEndian(_data[(at + sizeof(int))..]);
            _entry++;
        }

        if (_left == 0)
        {
            return 0;
        }

        _left--;
        return _offset;
    }
}
