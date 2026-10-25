// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>Builds JBIG2 segment streams by hand for tests of damaged and hostile data.</summary>
[DebuggerDisplay("Jbig2StreamBuilder: {_bytes.Count} bytes")]
internal sealed class Jbig2StreamBuilder
{
    /// <summary>The page information segment type.</summary>
    internal const int PageInformation = 48;

    /// <summary>The immediate generic region segment type.</summary>
    internal const int ImmediateGeneric = 38;

    /// <summary>The symbol dictionary segment type.</summary>
    internal const int SymbolDictionary = 0;

    /// <summary>The immediate text region segment type.</summary>
    internal const int ImmediateText = 6;

    /// <summary>The pattern dictionary segment type.</summary>
    internal const int PatternDictionary = 16;

    /// <summary>The immediate halftone region segment type.</summary>
    internal const int ImmediateHalftone = 22;

    /// <summary>The table segment type.</summary>
    internal const int Table = 53;

    /// <summary>The bytes of a 32-bit field.</summary>
    private const int IntBytes = 4;

    /// <summary>The bytes of a 16-bit field.</summary>
    private const int ShortBytes = 2;

    /// <summary>The shift of the referred-to segment count in its byte.</summary>
    private const int CountShift = 5;

    /// <summary>The stream so far.</summary>
    private readonly List<byte> _bytes = [];

    /// <summary>The next segment number.</summary>
    private int _next;

    /// <summary>Builds region segment information.</summary>
    /// <param name="width">The region width.</param>
    /// <param name="height">The region height.</param>
    /// <returns>The 17 bytes, at the origin with the OR operator.</returns>
    internal static List<byte> RegionInfo(int width, int height)
    {
        var data = new List<byte>();
        AddInt(data, width);
        AddInt(data, height);
        AddInt(data, 0);
        AddInt(data, 0);
        data.Add(0);
        return data;
    }

    /// <summary>Appends a big-endian 32-bit integer.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="value">The value.</param>
    internal static void AddInt(List<byte> bytes, int value)
    {
        Span<byte> buffer = stackalloc byte[IntBytes];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        bytes.AddRange(buffer);
    }

    /// <summary>Appends a big-endian 16-bit integer.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="value">The value.</param>
    internal static void AddShort(List<byte> bytes, int value)
    {
        Span<byte> buffer = stackalloc byte[ShortBytes];
        BinaryPrimitives.WriteInt16BigEndian(buffer, (short)value);
        bytes.AddRange(buffer);
    }

    /// <summary>Adds a page information segment.</summary>
    /// <param name="width">The page width.</param>
    /// <param name="height">The page height.</param>
    /// <returns>This builder.</returns>
    internal Jbig2StreamBuilder Page(int width, int height)
    {
        var data = new List<byte>();
        AddInt(data, width);
        AddInt(data, height);
        AddInt(data, 0);
        AddInt(data, 0);
        data.Add(0);
        AddShort(data, 0);
        return Segment(PageInformation, [], [.. data]);
    }

    /// <summary>Adds a segment.</summary>
    /// <param name="type">The segment type.</param>
    /// <param name="referred">The referred-to segment numbers, at most four.</param>
    /// <param name="data">The segment data.</param>
    /// <returns>This builder.</returns>
    internal Jbig2StreamBuilder Segment(int type, int[] referred, byte[] data)
    {
        AddInt(_bytes, _next);
        _next++;
        _bytes.Add((byte)type);
        _bytes.Add((byte)(referred.Length << CountShift));
        foreach (var number in referred)
        {
            _bytes.Add((byte)number);
        }

        _bytes.Add(1);
        AddInt(_bytes, data.Length);
        _bytes.AddRange(data);
        return this;
    }

    /// <summary>Gets the stream.</summary>
    /// <returns>The bytes.</returns>
    internal byte[] ToArray() => [.. _bytes];
}
