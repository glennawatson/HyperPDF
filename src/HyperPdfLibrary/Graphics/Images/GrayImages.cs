// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>
/// Decodes one-component images whose colours are all opaque grays to one byte per pixel. Greyscale scans and bilevel
/// pages then cost a quarter of the memory of BGRA while retaining the same opaque pixel values.
/// </summary>
internal static class GrayImages
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The offset of the green byte in a BGRA pixel.</summary>
    private const int Green = 1;

    /// <summary>The offset of the red byte in a BGRA pixel.</summary>
    private const int Red = 2;

    /// <summary>The offset of the alpha byte in a BGRA pixel.</summary>
    private const int Alpha = 3;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The number of one-bit patterns in a byte, and of 8-bit sample values.</summary>
    private const int Patterns = 256;

    /// <summary>The opaque alpha value.</summary>
    private const byte Opaque = 255;

    /// <summary>The gray table of a space whose samples are their own gray: the bytes 0 to 255.</summary>
    private static readonly byte[] IdentityTable = CreateIdentityTable();

    /// <summary>Decodes complete rows of a one-component image to gray, when its colour space allows it.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="space">The image's colour space.</param>
    /// <param name="unpacker">Unpacks the samples.</param>
    /// <param name="data">The samples, rows padded to whole bytes.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The gray image, or <see langword="null"/> when the colour space gives anything but opaque gray.</returns>
    internal static PdfImageData? TryDecode(in ImageHeader header, PdfColorSpace space, in SampleUnpacker unpacker, ReadOnlySpan<byte> data, ScratchPools scratchPools)
    {
        if (space.Components != 1 || unpacker.HasColorKey || CreateTable(space, out var identity) is not { } table)
        {
            return null;
        }

        var pixels = PixelMemory.Allocate((long)header.Width * header.Height, out var pinned);
        if (header.BitsPerComponent == 1)
        {
            DecodeBilevel(header, unpacker, table, data, pixels);
        }
        else
        {
            DecodeSamples(header, unpacker, table, identity, data, pixels, scratchPools);
        }

        return new(header.Width, header.Height, pixels, false, header.Interpolate, PdfImageCodec.None) { IsGray = true, IsPinned = pinned };
    }

    /// <summary>Decodes rows that a codec produces one at a time to gray, when the colour space allows it, so no buffer holds every sample as well.</summary>
    /// <typeparam name="TRows">The row source.</typeparam>
    /// <param name="header">The image header.</param>
    /// <param name="space">The image's colour space.</param>
    /// <param name="unpacker">Unpacks the samples.</param>
    /// <param name="rows">The rows, asked for in order. Nothing is asked for when the result is <see langword="null"/>.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The gray image, or <see langword="null"/> when the colour space gives anything but opaque gray, or the samples are one bit.</returns>
    internal static PdfImageData? TryDecodeRows<TRows>(in ImageHeader header, PdfColorSpace space, in SampleUnpacker unpacker, TRows rows, ScratchPools scratchPools)
        where TRows : IImageRows, allows ref struct
    {
        if (header.BitsPerComponent == 1 || space.Components != 1 || unpacker.HasColorKey || CreateTable(space, out var identity) is not { } table)
        {
            return null;
        }

        var pixels = PixelMemory.Allocate((long)header.Width * header.Height, out var pinned);
        DecodeRowSamples(header, unpacker, table, identity, rows, pixels, scratchPools);
        return new(header.Width, header.Height, pixels, false, header.Interpolate, PdfImageCodec.None) { IsGray = true, IsPinned = pinned };
    }

    /// <summary>Makes the table that maps every byte to itself.</summary>
    /// <returns>The table.</returns>
    private static byte[] CreateIdentityTable()
    {
        var table = new byte[Patterns];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = (byte)i;
        }

        return table;
    }

    /// <summary>Builds the table that maps an unpacked sample to its gray byte.</summary>
    /// <param name="space">The one-component colour space.</param>
    /// <param name="identity">Receives whether every sample maps to itself.</param>
    /// <returns>The table, or <see langword="null"/> when some sample is not an opaque gray.</returns>
    private static byte[]? CreateTable(PdfColorSpace space, out bool identity)
    {
        Span<byte> ramp = stackalloc byte[Patterns];
        Span<byte> bgra = stackalloc byte[Patterns * BgraBytes];
        for (var i = 0; i < Patterns; i++)
        {
            ramp[i] = (byte)i;
        }

        space.ConvertRow(ramp, bgra, Patterns);
        Span<byte> grays = stackalloc byte[Patterns];
        identity = true;
        for (var i = 0; i < Patterns; i++)
        {
            var pixel = bgra.Slice(i * BgraBytes, BgraBytes);
            if (pixel[Alpha] != Opaque || pixel[0] != pixel[Green] || pixel[0] != pixel[Red])
            {
                return null;
            }

            grays[i] = pixel[0];
            identity &= pixel[0] == i;
        }

        // The table is only read, so every image whose samples are their own gray shares one.
        return identity ? IdentityTable : grays.ToArray();
    }

    /// <summary>Builds, for every source byte of 1-bit samples, the eight gray pixels it holds, most significant bit first.</summary>
    /// <param name="zero">The gray of a 0 bit.</param>
    /// <param name="one">The gray of a 1 bit.</param>
    /// <param name="expand">Receives the pixels of each byte as a little-endian word.</param>
    private static void BuildExpansion(byte zero, byte one, Span<ulong> expand)
    {
        for (var pattern = 0; pattern < Patterns; pattern++)
        {
            ulong word = 0;
            for (var bit = 0; bit < ByteBits; bit++)
            {
                var gray = ((pattern >> (ByteBits - 1 - bit)) & 1) == 0 ? zero : one;
                word |= (ulong)gray << (bit * ByteBits);
            }

            expand[pattern] = word;
        }
    }

    /// <summary>Expands 1-bit rows eight pixels per source byte.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="unpacker">Maps the two sample values through /Decode.</param>
    /// <param name="table">The gray of each unpacked sample.</param>
    /// <param name="data">The packed rows.</param>
    /// <param name="pixels">Receives the gray pixels.</param>
    private static void DecodeBilevel(in ImageHeader header, in SampleUnpacker unpacker, byte[] table, ReadOnlySpan<byte> data, byte[] pixels)
    {
        Span<ulong> expand = stackalloc ulong[Patterns];
        BuildExpansion(table[unpacker.Map(0, 0)], table[unpacker.Map(0, 1)], expand);
        var width = header.Width;
        var whole = width / ByteBits;
        var rest = width - (whole * ByteBits);
        Span<byte> tail = stackalloc byte[ByteBits];
        for (var y = 0; y < header.Height; y++)
        {
            var row = data.Slice(y * header.RowBytes, header.RowBytes);
            var target = pixels.AsSpan(y * width, width);
            for (var i = 0; i < whole; i++)
            {
                BinaryPrimitives.WriteUInt64LittleEndian(target.Slice(i * ByteBits, ByteBits), expand[row[i]]);
            }

            if (rest == 0)
            {
                continue;
            }

            BinaryPrimitives.WriteUInt64LittleEndian(tail, expand[row[whole]]);
            tail[..rest].CopyTo(target[(whole * ByteBits)..]);
        }
    }

    /// <summary>Decodes rows from a row source through the unpacker and the gray table.</summary>
    /// <typeparam name="TRows">The row source.</typeparam>
    /// <param name="header">The image header.</param>
    /// <param name="unpacker">Unpacks the samples.</param>
    /// <param name="table">The gray of each unpacked sample.</param>
    /// <param name="identity">Whether the table maps every sample to itself.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="pixels">Receives the gray pixels.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    private static void DecodeRowSamples<TRows>(in ImageHeader header, in SampleUnpacker unpacker, byte[] table, bool identity, TRows rows, byte[] pixels, ScratchPools scratchPools)
        where TRows : IImageRows, allows ref struct
    {
        var width = header.Width;
        var pool = scratchPools.Get<byte>();
        var samples = pool.Rent(width);
        var rowBuffer = rows.UsesBuffer ? pool.Rent(header.RowBytes) : null;
        try
        {
            for (var y = 0; y < header.Height; y++)
            {
                var row = rows.GetRow(y, rowBuffer is null ? [] : rowBuffer.AsSpan(0, header.RowBytes));
                if (!unpacker.IsDirect)
                {
                    unpacker.Unpack(row, samples, [], width);
                    row = samples;
                }

                var target = pixels.AsSpan(y * width, width);
                if (identity)
                {
                    row[..width].CopyTo(target);
                    continue;
                }

                for (var x = 0; x < width; x++)
                {
                    target[x] = table[row[x]];
                }
            }
        }
        finally
        {
            pool.Return(samples);
            if (rowBuffer is not null)
            {
                pool.Return(rowBuffer);
            }
        }
    }

    /// <summary>Decodes rows of any depth through the unpacker and the gray table.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="unpacker">Unpacks the samples.</param>
    /// <param name="table">The gray of each unpacked sample.</param>
    /// <param name="identity">Whether the table maps every sample to itself.</param>
    /// <param name="data">The packed rows.</param>
    /// <param name="pixels">Receives the gray pixels.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    private static void DecodeSamples(in ImageHeader header, in SampleUnpacker unpacker, byte[] table, bool identity, ReadOnlySpan<byte> data, byte[] pixels, ScratchPools scratchPools)
    {
        var width = header.Width;
        var pool = scratchPools.Get<byte>();
        var samples = pool.Rent(width);
        try
        {
            for (var y = 0; y < header.Height; y++)
            {
                var row = data.Slice(y * header.RowBytes, header.RowBytes);
                if (!unpacker.IsDirect)
                {
                    unpacker.Unpack(row, samples, [], width);
                    row = samples;
                }

                var target = pixels.AsSpan(y * width, width);
                if (identity)
                {
                    row[..width].CopyTo(target);
                    continue;
                }

                for (var x = 0; x < width; x++)
                {
                    target[x] = table[row[x]];
                }
            }
        }
        finally
        {
            pool.Return(samples);
        }
    }
}
