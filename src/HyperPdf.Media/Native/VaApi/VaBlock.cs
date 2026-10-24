// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Media.Native.VaApi;

/// <summary>
/// The bytes of one VA-API parameter buffer, written field by field at the offsets the C structure puts them at. Writing at offsets keeps the
/// layout in plain sight, with no padding or bit-field rules to get wrong, and lets one block be cleared and reused for every picture.
/// </summary>
[DebuggerDisplay("VaBlock: {Bytes.Length} bytes")]
internal sealed class VaBlock
{
    /// <summary>The offset in a <c>VAPictureH264</c> of the frame index.</summary>
    private const int FrameIndexAt = 4;

    /// <summary>The offset in a <c>VAPictureH264</c> of the flags.</summary>
    private const int FlagsAt = 8;

    /// <summary>The offset in a <c>VAPictureH264</c> of the top field order count.</summary>
    private const int TopOrderCountAt = 12;

    /// <summary>The offset in a <c>VAPictureH264</c> of the bottom field order count.</summary>
    private const int BottomOrderCountAt = 16;

    /// <summary>Initializes a new instance of the <see cref="VaBlock"/> class.</summary>
    /// <param name="size">The size of the structure in bytes.</param>
    internal VaBlock(int size) => Bytes = new byte[size];

    /// <summary>Gets the bytes of the structure.</summary>
    internal byte[] Bytes { get; }

    /// <summary>Sets every byte to zero.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Clear() => Array.Clear(Bytes);

    /// <summary>Writes a byte.</summary>
    /// <param name="offset">Where the field is.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Set(int offset, byte value) => Bytes[offset] = value;

    /// <summary>Writes a signed byte.</summary>
    /// <param name="offset">Where the field is.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Set(int offset, sbyte value) => Bytes[offset] = (byte)value;

    /// <summary>Writes a 16 bit unsigned value in the machine's byte order.</summary>
    /// <param name="offset">Where the field is.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Set(int offset, ushort value) => MemoryMarshal.Write(Bytes.AsSpan(offset), in value);

    /// <summary>Writes a 16 bit signed value in the machine's byte order.</summary>
    /// <param name="offset">Where the field is.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Set(int offset, short value) => MemoryMarshal.Write(Bytes.AsSpan(offset), in value);

    /// <summary>Writes a 32 bit unsigned value in the machine's byte order.</summary>
    /// <param name="offset">Where the field is.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Set(int offset, uint value) => MemoryMarshal.Write(Bytes.AsSpan(offset), in value);

    /// <summary>Writes a 32 bit signed value in the machine's byte order.</summary>
    /// <param name="offset">Where the field is.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Set(int offset, int value) => MemoryMarshal.Write(Bytes.AsSpan(offset), in value);

    /// <summary>Writes a <c>VAPictureH264</c>.</summary>
    /// <param name="offset">Where the structure is.</param>
    /// <param name="entry">The picture.</param>
    internal void Set(int offset, in PictureEntry entry)
    {
        Set(offset, entry.Surface);
        Set(offset + FrameIndexAt, entry.FrameIndex);
        Set(offset + FlagsAt, entry.Flags);
        Set(offset + TopOrderCountAt, entry.TopOrderCount);
        Set(offset + BottomOrderCountAt, entry.BottomOrderCount);
    }

    /// <summary>Writes a <c>VAPictureHEVC</c>.</summary>
    /// <param name="offset">Where the structure is.</param>
    /// <param name="surface">The surface the picture is in, or <see cref="PictureEntry.NoSurface"/>.</param>
    /// <param name="orderCount">The picture order count.</param>
    /// <param name="flags">The flags.</param>
    internal void SetHevcPicture(int offset, uint surface, int orderCount, uint flags)
    {
        Set(offset, surface);
        Set(offset + FrameIndexAt, orderCount);
        Set(offset + FlagsAt, flags);
    }
}
