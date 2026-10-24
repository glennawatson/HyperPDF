// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdf.Codecs.Mp4;

/// <summary>Lists the top level boxes of a file by reading only their headers, so media data is stepped over, not read.</summary>
internal static class Mp4TopLevel
{
    /// <summary>The most top level boxes listed, which keeps a damaged file from filling the memory.</summary>
    private const int MaxBoxes = 1_000_000;

    /// <summary>Lists the top level boxes from the stream's position.</summary>
    /// <param name="stream">The file; it must be able to seek.</param>
    /// <returns>The boxes, or an empty list when the first bytes are not the start of a box.</returns>
    internal static List<Mp4TopBox> List(Stream stream)
    {
        var boxes = new List<Mp4TopBox>();
        var length = stream.Length;
        var position = stream.Position;
        Span<byte> header = stackalloc byte[Mp4Boxes.LargeHeaderSize];
        while (position + Mp4Boxes.HeaderSize <= length && boxes.Count < MaxBoxes)
        {
            stream.Position = position;
            var read = stream.ReadAtLeast(header, Mp4Boxes.HeaderSize, false);
            if (read < Mp4Boxes.HeaderSize)
            {
                break;
            }

            var size = (long)BinaryPrimitives.ReadUInt32BigEndian(header);
            var type = BinaryPrimitives.ReadUInt32BigEndian(header[sizeof(uint)..]);
            if (size == 1 && read >= Mp4Boxes.LargeHeaderSize)
            {
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(header[Mp4Boxes.HeaderSize..]);
            }
            else if (size == 0)
            {
                size = length - position;
            }

            if (size < Mp4Boxes.HeaderSize || position + size > length)
            {
                size = Math.Min(size, length - position);
                boxes.Add(new(type, position, Math.Max(size, Mp4Boxes.HeaderSize), false));
                break;
            }

            boxes.Add(new(type, position, size, true));
            position += size;
        }

        return boxes;
    }
}
