// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>A CFF INDEX: a counted array of variable-length objects, read in place.</summary>
/// <param name="Count">The number of objects.</param>
/// <param name="OffsetSize">The size of each offset, one to four bytes.</param>
/// <param name="OffsetsStart">The offset of the offset array.</param>
/// <param name="DataBase">The offset that object offsets are relative to: one byte before the object data.</param>
[DebuggerDisplay("CffIndex: {Count} objects")]
internal readonly record struct CffIndex(int Count, int OffsetSize, int OffsetsStart, int DataBase)
{
    /// <summary>The offset of the offset size after the count.</summary>
    private const int OffsetSizeOffset = 2;

    /// <summary>The size of the count and offset size.</summary>
    private const int HeaderSize = 3;

    /// <summary>The largest offset size.</summary>
    private const int MaxOffsetSize = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Reads an INDEX.</summary>
    /// <param name="data">The CFF data.</param>
    /// <param name="position">The offset of the INDEX.</param>
    /// <param name="end">The offset just after the INDEX, or the data length when it is damaged.</param>
    /// <returns>The INDEX; empty when damaged.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    internal static CffIndex Read(ReadOnlySpan<byte> data, int position, out int end)
    {
        if (position < 0 || position > data.Length - FontBytes.U16Size)
        {
            throw new PdfException(PdfError.Format, "The CFF INDEX header exceeds its data.");
        }

        var count = FontBytes.U16(data, position);
        if (count == 0)
        {
            end = position + FontBytes.U16Size;
            return default;
        }

        var offsetSize = FontBytes.U8(data, position + OffsetSizeOffset);
        var offsetsStart = (long)position + HeaderSize;
        var dataBase = offsetsStart + ((count + 1) * offsetSize) - 1;
        if (offsetSize is < 1 or > MaxOffsetSize || dataBase >= data.Length)
        {
            throw new PdfException(PdfError.Format, "The CFF INDEX offset array exceeds its data.");
        }

        var index = new CffIndex(count, offsetSize, (int)offsetsStart, (int)dataBase);
        var last = index.ReadOffset(data, count);
        if (index.ReadOffset(data, 0) != 1 || last < 1 || dataBase + last > data.Length)
        {
            throw new PdfException(PdfError.Format, "The CFF INDEX object range exceeds its data.");
        }

        end = (int)(dataBase + last);
        return index;
    }

    /// <summary>Gets an object.</summary>
    /// <param name="data">The CFF data.</param>
    /// <param name="index">The object's index.</param>
    /// <returns>The object's bytes, or empty when out of range or damaged.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    internal ReadOnlySpan<byte> Get(ReadOnlySpan<byte> data, int index)
    {
        if ((uint)index >= (uint)Count)
        {
            return [];
        }

        var start = (long)DataBase + ReadOffset(data, index);
        var end = (long)DataBase + ReadOffset(data, index + 1);
        if (start < DataBase + 1L || start > end || end > data.Length)
        {
            throw new PdfException(PdfError.Format, "The CFF INDEX object offsets are invalid.");
        }

        return data[(int)start..(int)end];
    }

    /// <summary>Reads one entry of the offset array.</summary>
    /// <param name="data">The CFF data.</param>
    /// <param name="index">The entry.</param>
    /// <returns>The offset.</returns>
    private uint ReadOffset(ReadOnlySpan<byte> data, int index)
    {
        var position = OffsetsStart + (index * OffsetSize);
        uint value = 0;
        for (var i = 0; i < OffsetSize; i++)
        {
            value = (value << ByteBits) | (uint)FontBytes.U8(data, position + i);
        }

        return value;
    }
}
