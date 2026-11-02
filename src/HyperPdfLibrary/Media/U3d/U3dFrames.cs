// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Splits a region of a U3D file into blocks (ECMA-363 9.2): a type, two sizes, then the data and meta data sections, each padded to four bytes.</summary>
internal static class U3dFrames
{
    /// <summary>The position of the data size in a block's frame.</summary>
    private const int DataSizeOffset = 4;

    /// <summary>The position of the meta data size in a block's frame.</summary>
    private const int MetaSizeOffset = 8;

    /// <summary>The bytes of a block's type, data size and meta data size.</summary>
    private const int FrameLength = 12;

    /// <summary>The alignment of blocks and their sections.</summary>
    private const int Alignment = 4;

    /// <summary>Finds the next block in a region.</summary>
    /// <param name="region">The bytes from the start of the first block to the end of the region.</param>
    /// <param name="at">The position of the block, moved past it.</param>
    /// <returns>The block, or <see langword="null"/> when the region ends or the block is cut short.</returns>
    internal static U3dBlock? Next(ReadOnlyMemory<byte> region, ref int at)
    {
        var rest = region[at..];
        if (rest.Length < FrameLength)
        {
            return null;
        }

        var frame = rest.Span;
        var type = BinaryPrimitives.ReadUInt32LittleEndian(frame);
        var dataSize = BinaryPrimitives.ReadUInt32LittleEndian(frame[DataSizeOffset..]);
        var metaSize = BinaryPrimitives.ReadUInt32LittleEndian(frame[MetaSizeOffset..]);
        var padded = Padded(dataSize);
        var total = FrameLength + padded + Padded(metaSize);

        // The last block of a file may leave out its padding, so it is only cut short when its sections do not fit unpadded.
        if (dataSize > rest.Length - FrameLength || (total > rest.Length && FrameLength + (long)dataSize + metaSize > rest.Length))
        {
            return null;
        }

        var metaStart = FrameLength + (int)Math.Min(padded, rest.Length - FrameLength);
        var metaLength = (int)Math.Min(metaSize, rest.Length - metaStart);
        at += (int)Math.Min(total, rest.Length);
        return new(type, rest.Slice(FrameLength, (int)dataSize), rest.Slice(metaStart, metaLength));
    }

    /// <summary>Gets the length of a section after padding it to the alignment.</summary>
    /// <param name="length">The section's length.</param>
    /// <returns>The padded length.</returns>
    private static long Padded(uint length) => (((long)length + Alignment) - 1) / Alignment * Alignment;
}
