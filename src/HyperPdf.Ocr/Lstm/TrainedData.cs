// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;

namespace HyperPdf.Ocr.Lstm;

/// <summary>A language data file: a table of offsets followed by the parts it points to, each running to the next one's start.</summary>
[DebuggerDisplay("TrainedData: {_data.Length} bytes, {_offsets.Length} parts")]
public sealed class TrainedData
{
    /// <summary>The size of the part count at the start of the file.</summary>
    private const int CountSize = sizeof(int);

    /// <summary>The most parts a file may claim, which guards against reading a file of another kind.</summary>
    private const int MaxComponents = 64;

    /// <summary>The offset the table stores for a part the file does not have.</summary>
    private const long Absent = -1;

    /// <summary>The bytes of the whole file.</summary>
    private readonly ReadOnlyMemory<byte> _data;

    /// <summary>The offset of each part, or <see cref="Absent"/>.</summary>
    private readonly long[] _offsets;

    /// <summary>Initializes a new instance of the <see cref="TrainedData"/> class.</summary>
    /// <param name="data">The bytes of the file.</param>
    /// <param name="offsets">The offset of each part.</param>
    private TrainedData(ReadOnlyMemory<byte> data, long[] offsets)
    {
        _data = data;
        _offsets = offsets;
    }

    /// <summary>Reads the table of a language data file.</summary>
    /// <param name="data">The bytes of the file.</param>
    /// <returns>The file.</returns>
    /// <exception cref="InvalidDataException">The bytes are not a language data file.</exception>
    public static TrainedData Open(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;
        if (span.Length < CountSize)
        {
            throw new InvalidDataException("The language data is too short.");
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(span);
        if (count is <= 0 or > MaxComponents || span.Length < CountSize + ((long)count * sizeof(long)))
        {
            throw new InvalidDataException("The language data has no valid table of parts.");
        }

        var offsets = new long[count];
        for (var i = 0; i < count; i++)
        {
            var offset = BinaryPrimitives.ReadInt64LittleEndian(span[(CountSize + (i * sizeof(long)))..]);
            if (offset != Absent && (offset < 0 || offset > span.Length))
            {
                throw new InvalidDataException("The language data points outside the file.");
            }

            offsets[i] = offset;
        }

        return new(data, offsets);
    }

    /// <summary>Determines whether the file holds a part.</summary>
    /// <param name="component">The part.</param>
    /// <returns><see langword="true"/> when it is present.</returns>
    public bool Has(TrainedDataComponent component) => (int)component < _offsets.Length && _offsets[(int)component] != Absent;

    /// <summary>Gets the bytes of a part.</summary>
    /// <param name="component">The part.</param>
    /// <returns>The bytes, without copying.</returns>
    /// <exception cref="InvalidDataException">The file does not have the part, or its parts are out of order.</exception>
    public ReadOnlyMemory<byte> Get(TrainedDataComponent component)
    {
        if (!Has(component))
        {
            throw new InvalidDataException($"The language data has no {component} part.");
        }

        var start = _offsets[(int)component];
        var end = EndOf(component);
        if (end < start)
        {
            throw new InvalidDataException("The language data parts are out of order.");
        }

        return _data.Slice((int)start, (int)(end - start));
    }

    /// <summary>Finds where a part ends: at the start of the next part the file has, or at the end of the file.</summary>
    /// <param name="component">The part.</param>
    /// <returns>The offset of the end.</returns>
    private long EndOf(TrainedDataComponent component)
    {
        for (var next = (int)component + 1; next < _offsets.Length; next++)
        {
            if (_offsets[next] != Absent)
            {
                return _offsets[next];
            }
        }

        return _data.Length;
    }
}
